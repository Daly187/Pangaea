using UnityEngine;
using System;
using Pangaea.Core;

namespace Pangaea.World
{
    /// <summary>
    /// Geo-based spawning system.
    /// First spawn: within 10km of player's real location.
    /// Respawns: always return to home radius.
    /// Home location cannot be changed.
    /// </summary>
    public class GeoSpawnSystem : MonoBehaviour
    {
        public static GeoSpawnSystem Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private float homeRadiusKm = 10f;
        [SerializeField] private float respawnRadiusKm = 5f; // Smaller radius for respawns
        [SerializeField] private float worldScale = 100f; // 1 world unit = 1 km

        [Header("Location Detection")]
        [SerializeField] private bool useRealLocation = true;
        [SerializeField] private float locationTimeout = 30f;

        // Cached location
        private bool hasLocation = false;
        private GeoLocation detectedLocation;
        private float locationRequestTime;

        // Events
        public event Action<GeoLocation> OnLocationDetected;
        public event Action<string> OnLocationError;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (useRealLocation)
            {
                RequestLocation();
            }
        }

        public void RequestLocation()
        {
            locationRequestTime = Time.time;

            #if UNITY_IOS || UNITY_ANDROID
            StartCoroutine(GetMobileLocation());
            #else
            // Desktop: use IP-based geolocation or default
            StartCoroutine(GetIPBasedLocation());
            #endif
        }

        private System.Collections.IEnumerator GetMobileLocation()
        {
            // Check permission
            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[GeoSpawn] Location services disabled by user");
                OnLocationError?.Invoke("Location services disabled. Please enable in settings.");
                UseDefaultLocation();
                yield break;
            }

            // Start location service
            Input.location.Start(10f, 10f); // 10m accuracy, 10m update distance

            // Wait for initialization
            int maxWait = (int)locationTimeout;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            if (maxWait <= 0)
            {
                Debug.LogWarning("[GeoSpawn] Location request timed out");
                OnLocationError?.Invoke("Location request timed out");
                UseDefaultLocation();
                yield break;
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                Debug.LogWarning("[GeoSpawn] Unable to determine location");
                OnLocationError?.Invoke("Unable to determine location");
                UseDefaultLocation();
                yield break;
            }

            // Success!
            LocationInfo info = Input.location.lastData;
            detectedLocation = new GeoLocation(info.latitude, info.longitude, homeRadiusKm);
            hasLocation = true;

            Debug.Log($"[GeoSpawn] Location detected: {detectedLocation.Latitude}, {detectedLocation.Longitude}");
            OnLocationDetected?.Invoke(detectedLocation);

            // Stop service to save battery
            Input.location.Stop();
        }

        private System.Collections.IEnumerator GetIPBasedLocation()
        {
            // Use a free IP geolocation API
            // For demo, just use default location
            yield return new WaitForSeconds(0.5f);

            UseDefaultLocation();
        }

        private void UseDefaultLocation()
        {
            // Default: somewhere interesting (varies by player for demo)
            // In production, this would be determined by IP or require manual selection

            // Random starting locations for demo
            float lat = UnityEngine.Random.Range(-60f, 70f);
            float lon = UnityEngine.Random.Range(-180f, 180f);

            detectedLocation = new GeoLocation(lat, lon, homeRadiusKm);
            hasLocation = true;

            Debug.Log($"[GeoSpawn] Using default location: {detectedLocation.Latitude}, {detectedLocation.Longitude}");
            OnLocationDetected?.Invoke(detectedLocation);
        }

        public Vector3 GetSpawnPosition(GeoLocation homeLocation)
        {
            // Convert geo coordinates to world position
            float worldX = GeoToWorldX(homeLocation.Longitude);
            float worldZ = GeoToWorldZ(homeLocation.Latitude);

            // Add random offset within spawn radius
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * homeLocation.SpawnRadiusKm * worldScale;
            worldX += randomOffset.x;
            worldZ += randomOffset.y;

            // Find ground height at position
            float worldY = GetGroundHeight(worldX, worldZ);

            return new Vector3(worldX, worldY, worldZ);
        }

        public Vector3 GetRespawnPosition(GeoLocation homeLocation)
        {
            // Respawn within smaller radius
            float worldX = GeoToWorldX(homeLocation.Longitude);
            float worldZ = GeoToWorldZ(homeLocation.Latitude);

            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * respawnRadiusKm * worldScale;
            worldX += randomOffset.x;
            worldZ += randomOffset.y;

            float worldY = GetGroundHeight(worldX, worldZ);

            return new Vector3(worldX, worldY, worldZ);
        }

        private float GeoToWorldX(double longitude)
        {
            // Map -180 to 180 longitude to world X
            // Pangaea is scaled down, so we use a multiplier
            return (float)((longitude + 180.0) * worldScale);
        }

        private float GeoToWorldZ(double latitude)
        {
            // Map -90 to 90 latitude to world Z
            return (float)((latitude + 90.0) * worldScale);
        }

        public GeoLocation WorldToGeo(Vector3 worldPosition)
        {
            double longitude = (worldPosition.x / worldScale) - 180.0;
            double latitude = (worldPosition.z / worldScale) - 90.0;
            return new GeoLocation(latitude, longitude);
        }

        private float GetGroundHeight(float x, float z)
        {
            // Raycast down to find ground
            RaycastHit hit;
            Vector3 rayStart = new Vector3(x, 1000f, z);

            if (Physics.Raycast(rayStart, Vector3.down, out hit, 2000f, LayerMask.GetMask("Ground", "Terrain")))
            {
                return hit.point.y + 0.5f;
            }

            return 0f; // Default ground level
        }

        public bool IsLocationReady()
        {
            return hasLocation;
        }

        public GeoLocation GetDetectedLocation()
        {
            return detectedLocation;
        }

        public float GetDistanceBetween(GeoLocation loc1, GeoLocation loc2)
        {
            // Haversine formula for distance between two coordinates
            double R = 6371; // Earth radius in km

            double lat1Rad = loc1.Latitude * Mathf.Deg2Rad;
            double lat2Rad = loc2.Latitude * Mathf.Deg2Rad;
            double deltaLat = (loc2.Latitude - loc1.Latitude) * Mathf.Deg2Rad;
            double deltaLon = (loc2.Longitude - loc1.Longitude) * Mathf.Deg2Rad;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return (float)(R * c);
        }

        public string GetRegionName(GeoLocation location)
        {
            // Approximate region names based on coordinates
            // In production, would use actual geo data

            double lat = location.Latitude;
            double lon = location.Longitude;

            // Asia
            if (lat >= 10 && lat <= 55 && lon >= 60 && lon <= 150)
            {
                if (lon > 100) return "Eastern Asia";
                if (lat > 35) return "Central Asia";
                return "South Asia";
            }

            // Europe
            if (lat >= 35 && lat <= 70 && lon >= -10 && lon <= 50)
            {
                if (lat > 55) return "Northern Europe";
                if (lon < 10) return "Western Europe";
                return "Eastern Europe";
            }

            // Americas
            if (lon >= -170 && lon <= -30)
            {
                if (lat > 15) return "North America";
                return "South America";
            }

            // Africa
            if (lat >= -35 && lat <= 35 && lon >= -20 && lon <= 50)
            {
                return "Africa";
            }

            // Oceania
            if (lat >= -50 && lat <= 0 && lon >= 100 && lon <= 180)
            {
                return "Oceania";
            }

            return "Unknown Region";
        }
    }
}
