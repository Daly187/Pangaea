using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;

namespace Pangaea.Core
{
    /// <summary>
    /// Manages all players in the game world - local and remote.
    /// Handles player spawning, tracking, and lookup.
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float playerUpdateRate = 0.1f; // 10 updates per second

        // Player tracking
        private Dictionary<uint, PlayerController> players = new Dictionary<uint, PlayerController>();
        private PlayerController localPlayer;

        // Properties
        public PlayerController LocalPlayer => localPlayer;
        public int PlayerCount => players.Count;
        public IReadOnlyDictionary<uint, PlayerController> AllPlayers => players;

        public void RegisterPlayer(uint playerId, PlayerController player, bool isLocal)
        {
            if (players.ContainsKey(playerId))
            {
                Debug.LogWarning($"[PlayerManager] Player {playerId} already registered.");
                return;
            }

            players[playerId] = player;

            if (isLocal)
            {
                localPlayer = player;
                Debug.Log($"[PlayerManager] Local player registered: {playerId}");
            }
            else
            {
                Debug.Log($"[PlayerManager] Remote player registered: {playerId}");
            }
        }

        public void UnregisterPlayer(uint playerId)
        {
            if (players.TryGetValue(playerId, out PlayerController player))
            {
                if (player == localPlayer)
                {
                    localPlayer = null;
                }
                players.Remove(playerId);
                Debug.Log($"[PlayerManager] Player unregistered: {playerId}");
            }
        }

        public PlayerController GetPlayer(uint playerId)
        {
            players.TryGetValue(playerId, out PlayerController player);
            return player;
        }

        public PlayerController GetNearestPlayer(Vector3 position, float maxDistance = float.MaxValue, bool excludeLocal = true)
        {
            PlayerController nearest = null;
            float nearestDistance = maxDistance;

            foreach (var kvp in players)
            {
                if (excludeLocal && kvp.Value == localPlayer) continue;

                float distance = Vector3.Distance(position, kvp.Value.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = kvp.Value;
                }
            }

            return nearest;
        }

        public List<PlayerController> GetPlayersInRange(Vector3 position, float range, bool excludeLocal = true)
        {
            List<PlayerController> inRange = new List<PlayerController>();

            foreach (var kvp in players)
            {
                if (excludeLocal && kvp.Value == localPlayer) continue;

                float distance = Vector3.Distance(position, kvp.Value.transform.position);
                if (distance <= range)
                {
                    inRange.Add(kvp.Value);
                }
            }

            return inRange;
        }

        public Vector3 CalculateSpawnPosition(GeoLocation homeLocation)
        {
            // Convert geo location to world position
            // For MVP, we'll use a simplified conversion
            // Real implementation will map lat/long to world coordinates

            float worldX = (float)(homeLocation.Longitude + 180) * 100f; // Scale factor
            float worldZ = (float)(homeLocation.Latitude + 90) * 100f;

            // Add some randomization within spawn radius
            Vector2 randomOffset = Random.insideUnitCircle * homeLocation.SpawnRadiusKm;
            worldX += randomOffset.x;
            worldZ += randomOffset.y;

            return new Vector3(worldX, 0f, worldZ);
        }
    }

    [System.Serializable]
    public struct GeoLocation
    {
        public double Latitude;
        public double Longitude;
        public float SpawnRadiusKm;

        public GeoLocation(double lat, double lon, float radiusKm = 10f)
        {
            Latitude = lat;
            Longitude = lon;
            SpawnRadiusKm = radiusKm;
        }
    }
}
