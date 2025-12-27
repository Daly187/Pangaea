using UnityEngine;
using System.Collections.Generic;

namespace Pangaea.Core
{
    /// <summary>
    /// Manages the game world - regions, chunks, spawning, and world state.
    /// Handles the Pangaea supercontinent with population density zones.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        [Header("World Configuration")]
        [SerializeField] private float worldScale = 1000f; // 1 unit = 1km
        [SerializeField] private int chunkSize = 100; // 100km chunks
        [SerializeField] private float viewDistance = 5f; // Chunks to load around player

        [Header("Population Density")]
        [SerializeField] private PopulationDensityMap densityMap;

        // Chunk management
        private Dictionary<Vector2Int, WorldChunk> loadedChunks = new Dictionary<Vector2Int, WorldChunk>();
        private Vector2Int currentPlayerChunk;

        // World state
        private float worldTime = 0f;
        private WeatherState currentWeather = WeatherState.Clear;
        private float weatherTimer = 0f;

        public float WorldTime => worldTime;
        public WeatherState CurrentWeather => currentWeather;

        private void Update()
        {
            UpdateWorldTime();
            UpdateWeather();
            UpdateChunks();
        }

        private void UpdateWorldTime()
        {
            // 24 minute day/night cycle (1 real minute = 1 game hour)
            worldTime += Time.deltaTime / 60f;
            if (worldTime >= 24f) worldTime -= 24f;
        }

        private void UpdateWeather()
        {
            weatherTimer -= Time.deltaTime;
            if (weatherTimer <= 0f)
            {
                // Random weather change
                currentWeather = (WeatherState)Random.Range(0, System.Enum.GetValues(typeof(WeatherState)).Length);
                weatherTimer = Random.Range(300f, 900f); // 5-15 minutes
            }
        }

        private void UpdateChunks()
        {
            if (GameManager.Instance?.PlayerManager?.LocalPlayer == null) return;

            Vector3 playerPos = GameManager.Instance.PlayerManager.LocalPlayer.transform.position;
            Vector2Int newChunk = GetChunkCoord(playerPos);

            if (newChunk != currentPlayerChunk)
            {
                currentPlayerChunk = newChunk;
                LoadChunksAroundPlayer();
            }
        }

        private Vector2Int GetChunkCoord(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / chunkSize),
                Mathf.FloorToInt(worldPosition.z / chunkSize)
            );
        }

        private void LoadChunksAroundPlayer()
        {
            HashSet<Vector2Int> chunksToKeep = new HashSet<Vector2Int>();
            int viewDist = Mathf.CeilToInt(viewDistance);

            // Determine which chunks should be loaded
            for (int x = -viewDist; x <= viewDist; x++)
            {
                for (int z = -viewDist; z <= viewDist; z++)
                {
                    Vector2Int chunkCoord = currentPlayerChunk + new Vector2Int(x, z);
                    chunksToKeep.Add(chunkCoord);

                    if (!loadedChunks.ContainsKey(chunkCoord))
                    {
                        LoadChunk(chunkCoord);
                    }
                }
            }

            // Unload distant chunks
            List<Vector2Int> chunksToUnload = new List<Vector2Int>();
            foreach (var kvp in loadedChunks)
            {
                if (!chunksToKeep.Contains(kvp.Key))
                {
                    chunksToUnload.Add(kvp.Key);
                }
            }

            foreach (var coord in chunksToUnload)
            {
                UnloadChunk(coord);
            }
        }

        private void LoadChunk(Vector2Int coord)
        {
            WorldChunk chunk = new WorldChunk(coord, chunkSize);

            // Get population density for this region
            float density = GetPopulationDensity(coord);
            chunk.PopulationDensity = density;

            // Generate content based on density
            chunk.GenerateContent(density);

            loadedChunks[coord] = chunk;
            Debug.Log($"[WorldManager] Loaded chunk {coord} with density {density:F2}");
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (loadedChunks.TryGetValue(coord, out WorldChunk chunk))
            {
                chunk.Cleanup();
                loadedChunks.Remove(coord);
            }
        }

        public float GetPopulationDensity(Vector2Int chunkCoord)
        {
            // Convert chunk coord to approximate lat/long
            // This is simplified - real implementation would use actual geo data
            float worldX = chunkCoord.x * chunkSize;
            float worldZ = chunkCoord.y * chunkSize;

            // Estimate lat/long from world position
            float lon = (worldX / 100f) - 180f;
            float lat = (worldZ / 100f) - 90f;

            return GetPopulationDensityAtGeo(lat, lon);
        }

        public float GetPopulationDensityAtGeo(float latitude, float longitude)
        {
            // Simplified population density based on real-world regions
            // High density: India, China, SE Asia, major cities
            // Medium: Europe, Americas
            // Low: Deserts, wilderness

            // China/India region
            if (latitude >= 20 && latitude <= 45 && longitude >= 70 && longitude <= 140)
            {
                return Random.Range(0.7f, 1.0f);
            }

            // Europe
            if (latitude >= 35 && latitude <= 60 && longitude >= -10 && longitude <= 40)
            {
                return Random.Range(0.5f, 0.8f);
            }

            // North America (populated regions)
            if (latitude >= 25 && latitude <= 50 && longitude >= -130 && longitude <= -60)
            {
                return Random.Range(0.4f, 0.7f);
            }

            // Default: wilderness
            return Random.Range(0.1f, 0.3f);
        }

        public Region GetRegionAt(Vector3 worldPosition)
        {
            float density = GetPopulationDensity(GetChunkCoord(worldPosition));

            if (density > 0.7f) return Region.UrbanRuins;
            if (density > 0.4f) return Region.Suburban;
            if (density > 0.2f) return Region.Wilderness;
            return Region.DeepWilderness;
        }
    }

    public class WorldChunk
    {
        public Vector2Int Coord { get; private set; }
        public int Size { get; private set; }
        public float PopulationDensity { get; set; }
        public List<GameObject> SpawnedObjects { get; private set; } = new List<GameObject>();

        public WorldChunk(Vector2Int coord, int size)
        {
            Coord = coord;
            Size = size;
        }

        public void GenerateContent(float density)
        {
            // Generate NPCs, loot, structures based on density
            int npcCount = Mathf.RoundToInt(density * 10);
            int lootSpots = Mathf.RoundToInt(density * 20);

            // Actual spawning would happen here
            // For now, just track counts
        }

        public void Cleanup()
        {
            foreach (var obj in SpawnedObjects)
            {
                if (obj != null) Object.Destroy(obj);
            }
            SpawnedObjects.Clear();
        }
    }

    public enum WeatherState
    {
        Clear,
        Cloudy,
        Rain,
        AcidRain,
        Fog,
        Sandstorm
    }

    public enum Region
    {
        UrbanRuins,
        Suburban,
        Wilderness,
        DeepWilderness,
        Desert,
        Mountains,
        Jungle
    }

    [System.Serializable]
    public class PopulationDensityMap
    {
        // This would be loaded from actual geo data
        // For MVP, using simplified region-based density
    }
}
