using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;
using Pangaea.Core;

namespace Pangaea.Social
{
    /// <summary>
    /// Proximity voice chat system - always-on, distance-based volume.
    /// Creates emergent social gameplay like Rust/DayZ.
    /// </summary>
    public class ProximityVoiceChat : MonoBehaviour
    {
        public static ProximityVoiceChat Instance { get; private set; }

        [Header("Voice Settings")]
        [SerializeField] private float maxVoiceDistance = 50f;
        [SerializeField] private float minVoiceDistance = 2f; // Full volume within this range
        [SerializeField] private float volumeFalloffPower = 2f; // Quadratic falloff
        [SerializeField] private bool voiceEnabled = true;

        [Header("Microphone")]
        [SerializeField] private string microphoneDevice;
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private float voiceActivationThreshold = 0.01f;
        [SerializeField] private bool pushToTalk = false;
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        [Header("Audio Processing")]
        [SerializeField] private bool noiseGate = true;
        [SerializeField] private bool compressor = true;

        // State
        private AudioClip microphoneClip;
        private bool isRecording = false;
        private bool isTalking = false;
        private int lastSamplePosition = 0;

        // Voice data buffer
        private float[] sampleBuffer;
        private const int BUFFER_SIZE = 1024;

        // Nearby players
        private Dictionary<uint, VoiceReceiver> voiceReceivers = new Dictionary<uint, VoiceReceiver>();

        // Events
        public System.Action<bool> OnTalkingStateChanged;
        public System.Action<uint, float[]> OnVoiceDataReceived;

        public bool IsVoiceEnabled => voiceEnabled;
        public bool IsTalking => isTalking;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            sampleBuffer = new float[BUFFER_SIZE];
        }

        private void Start()
        {
            InitializeMicrophone();
        }

        private void Update()
        {
            if (!voiceEnabled) return;

            // Check push-to-talk or voice activation
            bool shouldRecord = pushToTalk ? Input.GetKey(pushToTalkKey) : true;

            if (shouldRecord && !isRecording)
            {
                StartRecording();
            }
            else if (!shouldRecord && isRecording)
            {
                StopRecording();
            }

            if (isRecording)
            {
                ProcessMicrophoneInput();
            }

            UpdateNearbyPlayers();
        }

        private void InitializeMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[VoiceChat] No microphone detected");
                voiceEnabled = false;
                return;
            }

            microphoneDevice = Microphone.devices[0];
            Debug.Log($"[VoiceChat] Using microphone: {microphoneDevice}");
        }

        private void StartRecording()
        {
            if (string.IsNullOrEmpty(microphoneDevice)) return;

            microphoneClip = Microphone.Start(microphoneDevice, true, 1, sampleRate);
            isRecording = true;
            lastSamplePosition = 0;

            Debug.Log("[VoiceChat] Started recording");
        }

        private void StopRecording()
        {
            if (!isRecording) return;

            Microphone.End(microphoneDevice);
            isRecording = false;

            if (isTalking)
            {
                isTalking = false;
                OnTalkingStateChanged?.Invoke(false);
            }

            Debug.Log("[VoiceChat] Stopped recording");
        }

        private void ProcessMicrophoneInput()
        {
            int currentPosition = Microphone.GetPosition(microphoneDevice);
            if (currentPosition < lastSamplePosition)
            {
                // Wrapped around
                currentPosition += microphoneClip.samples;
            }

            int samplesToRead = currentPosition - lastSamplePosition;
            if (samplesToRead < BUFFER_SIZE) return;

            // Read samples
            microphoneClip.GetData(sampleBuffer, lastSamplePosition % microphoneClip.samples);
            lastSamplePosition = currentPosition;

            // Check voice activation
            float maxAmplitude = 0f;
            for (int i = 0; i < sampleBuffer.Length; i++)
            {
                float abs = Mathf.Abs(sampleBuffer[i]);
                if (abs > maxAmplitude) maxAmplitude = abs;
            }

            bool wasTalking = isTalking;
            isTalking = maxAmplitude > voiceActivationThreshold;

            if (isTalking != wasTalking)
            {
                OnTalkingStateChanged?.Invoke(isTalking);
            }

            if (isTalking)
            {
                // Process audio
                if (noiseGate)
                {
                    ApplyNoiseGate(sampleBuffer, voiceActivationThreshold * 0.5f);
                }

                // Send to nearby players
                SendVoiceData(sampleBuffer);
            }
        }

        private void ApplyNoiseGate(float[] samples, float threshold)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (Mathf.Abs(samples[i]) < threshold)
                {
                    samples[i] = 0f;
                }
            }
        }

        private void SendVoiceData(float[] samples)
        {
            // Compress audio data for network
            byte[] compressed = CompressAudio(samples);

            // Send via network
            Networking.NetworkManager.Instance?.SendMessage(new Networking.NetworkMessage
            {
                Type = Networking.MessageType.VoiceData,
                PlayerId = Networking.NetworkManager.Instance.LocalPlayerId,
                Data = compressed
            });
        }

        private byte[] CompressAudio(float[] samples)
        {
            // Simple compression: convert to 8-bit
            // Real implementation would use Opus or similar codec
            byte[] compressed = new byte[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                compressed[i] = (byte)((samples[i] + 1f) * 127.5f);
            }
            return compressed;
        }

        private float[] DecompressAudio(byte[] compressed)
        {
            float[] samples = new float[compressed.Length];
            for (int i = 0; i < compressed.Length; i++)
            {
                samples[i] = (compressed[i] / 127.5f) - 1f;
            }
            return samples;
        }

        public void ReceiveVoiceData(uint senderId, byte[] compressedData)
        {
            if (!voiceEnabled) return;

            // Get or create receiver
            if (!voiceReceivers.TryGetValue(senderId, out VoiceReceiver receiver))
            {
                receiver = CreateVoiceReceiver(senderId);
                if (receiver == null) return;
            }

            // Decompress and play
            float[] samples = DecompressAudio(compressedData);
            receiver.QueueSamples(samples);
        }

        private void UpdateNearbyPlayers()
        {
            PlayerController localPlayer = GameManager.Instance?.PlayerManager?.LocalPlayer;
            if (localPlayer == null) return;

            Vector3 localPos = localPlayer.transform.position;

            // Update volume for all receivers based on distance
            foreach (var kvp in voiceReceivers)
            {
                PlayerController sender = GameManager.Instance.PlayerManager.GetPlayer(kvp.Key);
                if (sender == null)
                {
                    kvp.Value.SetVolume(0f);
                    continue;
                }

                float distance = Vector3.Distance(localPos, sender.transform.position);
                float volume = CalculateVolume(distance);
                kvp.Value.SetVolume(volume);
            }
        }

        private float CalculateVolume(float distance)
        {
            if (distance <= minVoiceDistance) return 1f;
            if (distance >= maxVoiceDistance) return 0f;

            // Quadratic falloff
            float t = (distance - minVoiceDistance) / (maxVoiceDistance - minVoiceDistance);
            return 1f - Mathf.Pow(t, volumeFalloffPower);
        }

        private VoiceReceiver CreateVoiceReceiver(uint playerId)
        {
            PlayerController player = GameManager.Instance?.PlayerManager?.GetPlayer(playerId);
            if (player == null) return null;

            GameObject receiverObj = new GameObject($"VoiceReceiver_{playerId}");
            receiverObj.transform.SetParent(player.transform);
            receiverObj.transform.localPosition = Vector3.zero;

            VoiceReceiver receiver = receiverObj.AddComponent<VoiceReceiver>();
            receiver.Initialize(sampleRate);

            voiceReceivers[playerId] = receiver;
            return receiver;
        }

        public void SetVoiceEnabled(bool enabled)
        {
            voiceEnabled = enabled;
            if (!enabled && isRecording)
            {
                StopRecording();
            }
        }

        public void SetPushToTalk(bool enabled)
        {
            pushToTalk = enabled;
        }

        public void SetMicrophoneDevice(string device)
        {
            if (isRecording)
            {
                StopRecording();
            }
            microphoneDevice = device;
        }

        private void OnDestroy()
        {
            if (isRecording)
            {
                StopRecording();
            }
        }
    }

    /// <summary>
    /// Receives and plays voice audio for a specific player.
    /// </summary>
    public class VoiceReceiver : MonoBehaviour
    {
        private AudioSource audioSource;
        private Queue<float> sampleQueue = new Queue<float>();
        private int sampleRate;
        private AudioClip streamClip;
        private int writePosition = 0;

        public void Initialize(int rate)
        {
            sampleRate = rate;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D audio (volume controlled manually)
            audioSource.loop = true;
            audioSource.playOnAwake = false;

            // Create streaming audio clip (2 second buffer)
            streamClip = AudioClip.Create("VoiceStream", sampleRate * 2, 1, sampleRate, true, OnAudioRead);
            audioSource.clip = streamClip;
        }

        public void QueueSamples(float[] samples)
        {
            foreach (float sample in samples)
            {
                sampleQueue.Enqueue(sample);
            }

            if (!audioSource.isPlaying && sampleQueue.Count > sampleRate / 4)
            {
                audioSource.Play();
            }
        }

        private void OnAudioRead(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (sampleQueue.Count > 0)
                {
                    data[i] = sampleQueue.Dequeue();
                }
                else
                {
                    data[i] = 0f;
                }
            }
        }

        public void SetVolume(float volume)
        {
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
        }
    }
}
