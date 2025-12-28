using UnityEngine;
using System;
using System.Collections.Generic;
using Pangaea.Player;
using Pangaea.Core;

namespace Pangaea.AI
{
    /// <summary>
    /// Walking Dead style zombie senses.
    /// - Sight: Limited FOV, blocked by obstacles
    /// - Sound: 360 degrees, louder = further detection
    /// - Zombies are attracted to noise (gunshots, running, combat)
    /// </summary>
    public class ZombieSenses : MonoBehaviour
    {
        [Header("Sight")]
        [SerializeField] private float sightRange = 20f;
        [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private LayerMask sightBlockingLayers;
        [SerializeField] private LayerMask playerLayer;

        [Header("Hearing")]
        [SerializeField] private float hearingRange = 30f;
        [SerializeField] private float hearingSensitivity = 1f;

        [Header("Detection")]
        [SerializeField] private float detectionInterval = 0.2f;
        [SerializeField] private bool drawDebugRays = false;

        // Events
        public event Action<Transform> OnPlayerDetected;
        public event Action<Vector3, float> OnSoundHeard; // position, loudness

        // State
        private float lastDetectionTime;
        private Transform detectedPlayer;
        private ZombieStats stats;

        // Sound event registration
        private static List<ZombieSenses> allZombieSenses = new List<ZombieSenses>();

        private void Awake()
        {
            stats = GetComponent<ZombieStats>();

            if (stats != null)
            {
                sightRange = stats.SightRange;
                hearingRange = stats.HearingRange;
                fieldOfView = stats.FieldOfView;
            }
        }

        private void OnEnable()
        {
            allZombieSenses.Add(this);
        }

        private void OnDisable()
        {
            allZombieSenses.Remove(this);
        }

        private void Update()
        {
            if (Time.time - lastDetectionTime >= detectionInterval)
            {
                lastDetectionTime = Time.time;
                CheckForPlayers();
            }
        }

        #region Sight Detection

        private void CheckForPlayers()
        {
            // Get all players in range
            Collider[] playersInRange = Physics.OverlapSphere(transform.position, sightRange, playerLayer);

            foreach (var playerCol in playersInRange)
            {
                PlayerController player = playerCol.GetComponent<PlayerController>();
                if (player == null || player.Stats.CurrentHealth <= 0) continue;

                if (CanSeeTarget(player.transform))
                {
                    detectedPlayer = player.transform;
                    OnPlayerDetected?.Invoke(player.transform);
                    return;
                }
            }
        }

        public bool CanSeeTarget(Transform target)
        {
            if (target == null) return false;

            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPosition = target.position + Vector3.up * 1f; // Target center mass

            // Check distance
            float distance = Vector3.Distance(eyePosition, targetPosition);
            if (distance > sightRange) return false;

            // Check field of view
            Vector3 directionToTarget = (targetPosition - eyePosition).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > fieldOfView * 0.5f) return false;

            // Check line of sight (raycast for obstacles)
            if (Physics.Raycast(eyePosition, directionToTarget, out RaycastHit hit, distance, sightBlockingLayers))
            {
                // Something is blocking the view
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                {
                    if (drawDebugRays)
                    {
                        Debug.DrawLine(eyePosition, hit.point, Color.red, detectionInterval);
                    }
                    return false;
                }
            }

            if (drawDebugRays)
            {
                Debug.DrawLine(eyePosition, targetPosition, Color.green, detectionInterval);
            }

            return true;
        }

        #endregion

        #region Sound Detection

        /// <summary>
        /// Called when a sound is made in the world.
        /// All zombies within range will hear it.
        /// </summary>
        public static void MakeSound(Vector3 position, float loudness, SoundType type)
        {
            foreach (var zombie in allZombieSenses)
            {
                if (zombie == null) continue;
                zombie.HearSound(position, loudness, type);
            }
        }

        private void HearSound(Vector3 position, float loudness, SoundType type)
        {
            float distance = Vector3.Distance(transform.position, position);
            float effectiveRange = hearingRange * loudness * hearingSensitivity;

            // Sound type modifiers
            float typeModifier = type switch
            {
                SoundType.Footstep => 0.3f,
                SoundType.Running => 0.6f,
                SoundType.Combat => 1.0f,
                SoundType.Gunshot => 2.0f,
                SoundType.Explosion => 3.0f,
                SoundType.Voice => 0.8f,
                SoundType.Vehicle => 2.5f,
                _ => 1.0f
            };

            effectiveRange *= typeModifier;

            if (distance <= effectiveRange)
            {
                // Heard the sound!
                float perceivedLoudness = 1f - (distance / effectiveRange);
                OnSoundHeard?.Invoke(position, perceivedLoudness);

                if (drawDebugRays)
                {
                    Debug.DrawLine(transform.position + Vector3.up, position, Color.yellow, 1f);
                }
            }
        }

        /// <summary>
        /// Register a sound at the zombie's current position (for zombie groans attracting others)
        /// </summary>
        public void EmitSound(float loudness)
        {
            MakeSound(transform.position, loudness, SoundType.Voice);
        }

        #endregion

        #region Detection Helpers

        public Transform GetNearestPlayer()
        {
            PlayerController localPlayer = GameManager.Instance?.PlayerManager?.LocalPlayer;
            if (localPlayer != null)
            {
                return localPlayer.transform;
            }

            // Fallback: find any player
            var players = FindObjectsOfType<PlayerController>();
            if (players.Length == 0) return null;

            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var player in players)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = player.transform;
                }
            }

            return nearest;
        }

        public bool IsPlayerInRange(float range)
        {
            Transform nearest = GetNearestPlayer();
            if (nearest == null) return false;

            return Vector3.Distance(transform.position, nearest.position) <= range;
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            // Sight range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, sightRange);

            // Field of view
            Vector3 leftBound = Quaternion.Euler(0, -fieldOfView * 0.5f, 0) * transform.forward;
            Vector3 rightBound = Quaternion.Euler(0, fieldOfView * 0.5f, 0) * transform.forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, leftBound * sightRange);
            Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, rightBound * sightRange);

            // Hearing range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, hearingRange);
        }
    }

    public enum SoundType
    {
        Footstep,   // Walking
        Running,    // Running/sprinting
        Combat,     // Melee attacks
        Gunshot,    // Ranged weapons (if any)
        Explosion,  // Explosives, meteors
        Voice,      // Player voice chat, zombie groans
        Vehicle,    // Cars, mounts
        Building,   // Construction sounds
        Other
    }
}
