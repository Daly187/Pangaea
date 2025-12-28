using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Pangaea.Core;

namespace Pangaea.AI
{
    /// <summary>
    /// Spawns zombies based on population density zones.
    /// More zombies in urban ruins, fewer in wilderness.
    /// Uses the same density map as the world manager.
    /// </summary>
    public class ZombieSpawner : MonoBehaviour
    {
        public static ZombieSpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private GameObject[] zombiePrefabs;
        [SerializeField] private int maxZombiesInWorld = 100;
        [SerializeField] private float spawnRadius = 50f;
        [SerializeField] private float despawnRadius = 80f;
        [SerializeField] private float minSpawnDistance = 20f; // Don't spawn too close to player

        [Header("Density Multipliers")]
        [SerializeField] private float urbanDensity = 1.5f;     // Cities have more zombies
        [SerializeField] private float suburbanDensity = 1.0f;
        [SerializeField] private float wildernessDensity = 0.3f;
        [SerializeField] private float deepWildernessDensity = 0.1f;

        [Header("Spawn Timing")]
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private float checkInterval = 5f;

        [Header("Zombie Type Weights")]
        [SerializeField] private float walkerWeight = 60f;
        [SerializeField] private float shamblerWeight = 15f;
        [SerializeField] private float runnerWeight = 10f;
        [SerializeField] private float crawlerWeight = 10f;
        [SerializeField] private float bruteWeight = 4f;
        [SerializeField] private float screamerWeight = 1f;

        // Active zombies
        private List<ZombieAI> activeZombies = new List<ZombieAI>();
        private float lastSpawnTime;
        private float lastCheckTime;
        private Transform playerTransform;

        // Object pooling (optional optimization)
        private Queue<GameObject> zombiePool = new Queue<GameObject>();

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
            // Get player reference
            if (playerTransform == null)
            {
                var localPlayer = GameManager.Instance?.PlayerManager?.LocalPlayer;
                if (localPlayer != null)
                {
                    playerTransform = localPlayer.transform;
                }
                return;
            }

            // Spawn check
            if (Time.time - lastSpawnTime >= spawnInterval)
            {
                lastSpawnTime = Time.time;
                TrySpawnZombie();
            }

            // Despawn check
            if (Time.time - lastCheckTime >= checkInterval)
            {
                lastCheckTime = Time.time;
                CheckDespawnZombies();
                CleanupDeadZombies();
            }
        }

        private void TrySpawnZombie()
        {
            if (activeZombies.Count >= maxZombiesInWorld) return;

            // Get spawn position
            Vector3? spawnPos = GetValidSpawnPosition();
            if (!spawnPos.HasValue) return;

            // Get density at this location
            float density = GetDensityAtPosition(spawnPos.Value);

            // Random chance based on density
            if (Random.value > density) return;

            // Select zombie type
            ZombieType type = SelectZombieType();

            // Spawn
            SpawnZombie(spawnPos.Value, type);
        }

        private Vector3? GetValidSpawnPosition()
        {
            // Try several times to find valid position
            for (int i = 0; i < 10; i++)
            {
                // Random position in spawn radius
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                Vector3 randomPos = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

                // Check minimum distance
                float distToPlayer = Vector3.Distance(randomPos, playerTransform.position);
                if (distToPlayer < minSpawnDistance) continue;

                // Check if on NavMesh
                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    // Check if visible to player (don't spawn in view)
                    Vector3 dirToPlayer = (playerTransform.position - hit.position).normalized;
                    if (Physics.Raycast(hit.position + Vector3.up, dirToPlayer, distToPlayer))
                    {
                        // Blocked by something, good spawn point
                        return hit.position;
                    }

                    // Check if behind player
                    float angle = Vector3.Angle(playerTransform.forward, hit.position - playerTransform.position);
                    if (angle > 90f)
                    {
                        return hit.position;
                    }
                }
            }

            return null;
        }

        private float GetDensityAtPosition(Vector3 position)
        {
            Region region = GameManager.Instance?.WorldManager?.GetRegionAt(position) ?? Region.Wilderness;

            return region switch
            {
                Region.UrbanRuins => urbanDensity,
                Region.Suburban => suburbanDensity,
                Region.Wilderness => wildernessDensity,
                Region.DeepWilderness => deepWildernessDensity,
                _ => wildernessDensity
            };
        }

        private ZombieType SelectZombieType()
        {
            float totalWeight = walkerWeight + shamblerWeight + runnerWeight +
                               crawlerWeight + bruteWeight + screamerWeight;
            float random = Random.Range(0, totalWeight);

            if (random < walkerWeight) return ZombieType.Walker;
            random -= walkerWeight;

            if (random < shamblerWeight) return ZombieType.Shambler;
            random -= shamblerWeight;

            if (random < runnerWeight) return ZombieType.Runner;
            random -= runnerWeight;

            if (random < crawlerWeight) return ZombieType.Crawler;
            random -= crawlerWeight;

            if (random < bruteWeight) return ZombieType.Brute;

            return ZombieType.Screamer;
        }

        private void SpawnZombie(Vector3 position, ZombieType type)
        {
            if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            {
                Debug.LogWarning("[ZombieSpawner] No zombie prefabs assigned");
                return;
            }

            // Select prefab (could have different prefabs per type)
            int prefabIndex = Mathf.Min((int)type, zombiePrefabs.Length - 1);
            GameObject prefab = zombiePrefabs[prefabIndex];

            if (prefab == null)
            {
                prefab = zombiePrefabs[0];
            }

            // Spawn
            GameObject zombieObj = Instantiate(prefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0));

            ZombieAI zombie = zombieObj.GetComponent<ZombieAI>();
            if (zombie != null)
            {
                activeZombies.Add(zombie);

                // Set type
                ZombieStats stats = zombieObj.GetComponent<ZombieStats>();
                if (stats != null)
                {
                    stats.SetZombieType(type);
                }
            }

            Debug.Log($"[ZombieSpawner] Spawned {type} at {position}");
        }

        private void CheckDespawnZombies()
        {
            for (int i = activeZombies.Count - 1; i >= 0; i--)
            {
                if (activeZombies[i] == null)
                {
                    activeZombies.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(activeZombies[i].transform.position, playerTransform.position);

                if (distance > despawnRadius)
                {
                    // Despawn
                    Destroy(activeZombies[i].gameObject);
                    activeZombies.RemoveAt(i);
                }
            }
        }

        private void CleanupDeadZombies()
        {
            activeZombies.RemoveAll(z => z == null || !z.IsAlive);
        }

        public int GetActiveZombieCount()
        {
            return activeZombies.Count;
        }

        public List<ZombieAI> GetZombiesInRange(Vector3 position, float range)
        {
            List<ZombieAI> result = new List<ZombieAI>();

            foreach (var zombie in activeZombies)
            {
                if (zombie == null || !zombie.IsAlive) continue;

                if (Vector3.Distance(position, zombie.transform.position) <= range)
                {
                    result.Add(zombie);
                }
            }

            return result;
        }

        /// <summary>
        /// Force spawn a horde at a location (for world events, etc.)
        /// </summary>
        public void SpawnHorde(Vector3 center, int count, float radius = 10f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 pos = center + new Vector3(offset.x, 0, offset.y);

                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    ZombieType type = SelectZombieType();
                    SpawnZombie(hit.position, type);
                }
            }

            Debug.Log($"[ZombieSpawner] Spawned horde of {count} at {center}");
        }
    }
}
