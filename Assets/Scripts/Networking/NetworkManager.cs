using UnityEngine;
using System;
using System.Collections.Generic;
using Pangaea.Core;

namespace Pangaea.Networking
{
    /// <summary>
    /// Network manager for PANGAEA - handles connections, player sync, and server communication.
    /// Uses Mirror networking (authoritative server model).
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Network Settings")]
        [SerializeField] private string serverAddress = "localhost";
        [SerializeField] private int serverPort = 7777;
        [SerializeField] private int maxPlayers = 1000;

        [Header("Sync Settings")]
        [SerializeField] private float positionSyncRate = 0.05f; // 20 updates/sec
        [SerializeField] private float stateSyncRate = 0.5f;     // 2 updates/sec

        [Header("Prefabs")]
        [SerializeField] private GameObject playerPrefab;

        // Connection state
        private ConnectionState connectionState = ConnectionState.Disconnected;
        private uint localPlayerId;
        private GeoLocation playerHomeLocation;

        // Network players
        private Dictionary<uint, NetworkPlayer> networkPlayers = new Dictionary<uint, NetworkPlayer>();

        // Message queue
        private Queue<NetworkMessage> incomingMessages = new Queue<NetworkMessage>();
        private Queue<NetworkMessage> outgoingMessages = new Queue<NetworkMessage>();

        // Events
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<uint> OnPlayerConnected;
        public event Action<uint> OnPlayerDisconnected;

        // Properties
        public ConnectionState State => connectionState;
        public uint LocalPlayerId => localPlayerId;
        public bool IsConnected => connectionState == ConnectionState.Connected;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (connectionState == ConnectionState.Connected)
            {
                ProcessIncomingMessages();
                SendQueuedMessages();
            }
        }

        #region Connection

        public void Connect(string address, int port, GeoLocation homeLocation)
        {
            if (connectionState != ConnectionState.Disconnected)
            {
                Debug.LogWarning("[Network] Already connected or connecting");
                return;
            }

            serverAddress = address;
            serverPort = port;
            playerHomeLocation = homeLocation;

            SetConnectionState(ConnectionState.Connecting);
            Debug.Log($"[Network] Connecting to {address}:{port}...");

            // In real implementation, this would use Mirror's NetworkClient.Connect()
            // For now, simulate connection
            StartCoroutine(SimulateConnection());
        }

        private System.Collections.IEnumerator SimulateConnection()
        {
            yield return new WaitForSeconds(0.5f);

            // Simulate successful connection
            localPlayerId = (uint)UnityEngine.Random.Range(1, 999999);
            SetConnectionState(ConnectionState.Connected);

            // Request spawn
            SendSpawnRequest();
        }

        public void Disconnect()
        {
            if (connectionState == ConnectionState.Disconnected) return;

            Debug.Log("[Network] Disconnecting...");

            // Send disconnect message
            SendMessage(new NetworkMessage
            {
                Type = MessageType.Disconnect,
                PlayerId = localPlayerId
            });

            // Clean up
            foreach (var player in networkPlayers.Values)
            {
                if (player.PlayerObject != null)
                {
                    Destroy(player.PlayerObject);
                }
            }
            networkPlayers.Clear();

            SetConnectionState(ConnectionState.Disconnected);
        }

        private void SetConnectionState(ConnectionState newState)
        {
            if (connectionState == newState) return;

            connectionState = newState;
            OnConnectionStateChanged?.Invoke(newState);

            // Update game state
            if (newState == ConnectionState.Connected)
            {
                GameManager.Instance?.SetGameState(GameState.Playing);
            }
            else if (newState == ConnectionState.Disconnected)
            {
                GameManager.Instance?.SetGameState(GameState.Disconnected);
            }
        }

        #endregion

        #region Player Management

        private void SendSpawnRequest()
        {
            SendMessage(new NetworkMessage
            {
                Type = MessageType.SpawnRequest,
                PlayerId = localPlayerId,
                Position = GameManager.Instance.PlayerManager.CalculateSpawnPosition(playerHomeLocation),
                Data = SerializeGeoLocation(playerHomeLocation)
            });

            // In real impl, server would validate and respond
            // For now, spawn immediately
            SpawnLocalPlayer();
        }

        private void SpawnLocalPlayer()
        {
            Vector3 spawnPos = GameManager.Instance.PlayerManager.CalculateSpawnPosition(playerHomeLocation);

            GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            var controller = playerObj.GetComponent<Player.PlayerController>();
            controller.Initialize(localPlayerId, true, playerHomeLocation);

            networkPlayers[localPlayerId] = new NetworkPlayer
            {
                PlayerId = localPlayerId,
                PlayerObject = playerObj,
                IsLocal = true,
                HomeLocation = playerHomeLocation
            };

            Debug.Log($"[Network] Local player spawned at {spawnPos}");
        }

        private void SpawnRemotePlayer(uint playerId, Vector3 position)
        {
            if (networkPlayers.ContainsKey(playerId)) return;

            GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
            var controller = playerObj.GetComponent<Player.PlayerController>();
            controller.Initialize(playerId, false, new GeoLocation());

            networkPlayers[playerId] = new NetworkPlayer
            {
                PlayerId = playerId,
                PlayerObject = playerObj,
                IsLocal = false
            };

            OnPlayerConnected?.Invoke(playerId);
            Debug.Log($"[Network] Remote player {playerId} spawned at {position}");
        }

        private void DespawnPlayer(uint playerId)
        {
            if (networkPlayers.TryGetValue(playerId, out NetworkPlayer player))
            {
                if (player.PlayerObject != null)
                {
                    Destroy(player.PlayerObject);
                }
                networkPlayers.Remove(playerId);
                OnPlayerDisconnected?.Invoke(playerId);
            }
        }

        #endregion

        #region Message Handling

        public void SendMessage(NetworkMessage message)
        {
            message.Timestamp = Time.time;
            outgoingMessages.Enqueue(message);
        }

        private void SendQueuedMessages()
        {
            while (outgoingMessages.Count > 0)
            {
                NetworkMessage msg = outgoingMessages.Dequeue();
                // In real impl, serialize and send via transport
                // For offline testing, loopback some messages
                ProcessServerResponse(msg);
            }
        }

        private void ProcessIncomingMessages()
        {
            while (incomingMessages.Count > 0)
            {
                NetworkMessage msg = incomingMessages.Dequeue();
                HandleMessage(msg);
            }
        }

        private void HandleMessage(NetworkMessage message)
        {
            switch (message.Type)
            {
                case MessageType.PlayerJoined:
                    SpawnRemotePlayer(message.PlayerId, message.Position);
                    break;

                case MessageType.PlayerLeft:
                    DespawnPlayer(message.PlayerId);
                    break;

                case MessageType.PositionUpdate:
                    UpdatePlayerPosition(message.PlayerId, message.Position, message.Rotation);
                    break;

                case MessageType.StateUpdate:
                    UpdatePlayerState(message.PlayerId, message.Data);
                    break;

                case MessageType.Combat:
                    HandleCombatMessage(message);
                    break;

                case MessageType.Chat:
                    HandleChatMessage(message);
                    break;
            }
        }

        // Simulated server response for offline testing
        private void ProcessServerResponse(NetworkMessage msg)
        {
            // In real impl, server processes and broadcasts
            // For now, just echo position updates
        }

        #endregion

        #region Sync

        public void SendPositionUpdate(Vector3 position, Quaternion rotation)
        {
            SendMessage(new NetworkMessage
            {
                Type = MessageType.PositionUpdate,
                PlayerId = localPlayerId,
                Position = position,
                Rotation = rotation
            });
        }

        public void SendStateUpdate(PlayerStateData state)
        {
            SendMessage(new NetworkMessage
            {
                Type = MessageType.StateUpdate,
                PlayerId = localPlayerId,
                Data = SerializeState(state)
            });
        }

        public void SendCombatAction(CombatAction action, uint targetId, float damage)
        {
            SendMessage(new NetworkMessage
            {
                Type = MessageType.Combat,
                PlayerId = localPlayerId,
                TargetId = targetId,
                Data = SerializeCombat(action, damage)
            });
        }

        private void UpdatePlayerPosition(uint playerId, Vector3 position, Quaternion rotation)
        {
            if (!networkPlayers.TryGetValue(playerId, out NetworkPlayer player)) return;
            if (player.IsLocal) return;

            // Interpolate to new position
            var sync = player.PlayerObject?.GetComponent<NetworkPositionSync>();
            sync?.SetTargetPosition(position, rotation);
        }

        private void UpdatePlayerState(uint playerId, byte[] data)
        {
            // Deserialize and apply state
        }

        private void HandleCombatMessage(NetworkMessage msg)
        {
            // Process combat action from server
        }

        private void HandleChatMessage(NetworkMessage msg)
        {
            // Handle voice/text chat
        }

        #endregion

        #region Serialization

        private byte[] SerializeGeoLocation(GeoLocation loc)
        {
            // Simple serialization
            return System.BitConverter.GetBytes(loc.Latitude);
        }

        private byte[] SerializeState(PlayerStateData state)
        {
            return new byte[0]; // Placeholder
        }

        private byte[] SerializeCombat(CombatAction action, float damage)
        {
            return new byte[0]; // Placeholder
        }

        #endregion
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    public enum MessageType
    {
        // Connection
        Connect,
        Disconnect,
        SpawnRequest,
        SpawnResponse,

        // Players
        PlayerJoined,
        PlayerLeft,
        PositionUpdate,
        StateUpdate,

        // Combat
        Combat,
        Damage,
        Death,
        Respawn,

        // Social
        Chat,
        VoiceData,
        Emote,

        // World
        WorldEvent,
        LootSpawn,
        BuildingUpdate
    }

    public class NetworkMessage
    {
        public MessageType Type;
        public uint PlayerId;
        public uint TargetId;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte[] Data;
        public float Timestamp;
    }

    public class NetworkPlayer
    {
        public uint PlayerId;
        public GameObject PlayerObject;
        public bool IsLocal;
        public GeoLocation HomeLocation;
        public float LastUpdateTime;
    }

    public struct PlayerStateData
    {
        public float Health;
        public float Stamina;
        public float Hunger;
        public int Level;
        public Player.PvPMode PvPMode;
        public Player.ReputationTier Reputation;
    }

    public enum CombatAction
    {
        Attack,
        Block,
        Dodge,
        Finisher
    }
}
