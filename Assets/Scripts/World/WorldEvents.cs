using UnityEngine;
using System.Collections.Generic;
using Pangaea.Core;

namespace Pangaea.World
{
    /// <summary>
    /// Dynamic world events - meteors, supply drops, resource surges.
    /// Forces PvP convergence and creates emergent gameplay.
    /// </summary>
    public class WorldEvents : MonoBehaviour
    {
        public static WorldEvents Instance { get; private set; }

        [Header("Event Timing")]
        [SerializeField] private float minEventInterval = 300f; // 5 minutes
        [SerializeField] private float maxEventInterval = 900f; // 15 minutes
        [SerializeField] private int maxConcurrentEvents = 3;

        [Header("Event Prefabs")]
        [SerializeField] private GameObject meteorPrefab;
        [SerializeField] private GameObject supplyCratePrefab;
        [SerializeField] private GameObject resourceNodePrefab;

        [Header("Event Settings")]
        [SerializeField] private float eventWarningTime = 60f; // 1 minute warning
        [SerializeField] private float meteorLandingRadius = 50f;
        [SerializeField] private float supplyDropDuration = 600f; // 10 minutes

        // Active events
        private List<WorldEvent> activeEvents = new List<WorldEvent>();
        private float nextEventTime;
        private uint nextEventId = 1;

        // Events
        public System.Action<WorldEvent> OnEventAnnounced;
        public System.Action<WorldEvent> OnEventStarted;
        public System.Action<WorldEvent> OnEventEnded;

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
            ScheduleNextEvent();
        }

        private void Update()
        {
            // Check for new events
            if (Time.time >= nextEventTime && activeEvents.Count < maxConcurrentEvents)
            {
                TriggerRandomEvent();
                ScheduleNextEvent();
            }

            // Update active events
            for (int i = activeEvents.Count - 1; i >= 0; i--)
            {
                UpdateEvent(activeEvents[i]);
            }
        }

        private void ScheduleNextEvent()
        {
            nextEventTime = Time.time + Random.Range(minEventInterval, maxEventInterval);
        }

        private void TriggerRandomEvent()
        {
            WorldEventType type = (WorldEventType)Random.Range(0, System.Enum.GetValues(typeof(WorldEventType)).Length);
            TriggerEvent(type);
        }

        public void TriggerEvent(WorldEventType type, Vector3? position = null)
        {
            Vector3 eventPosition = position ?? GetRandomEventPosition();

            WorldEvent worldEvent = new WorldEvent
            {
                EventId = nextEventId++,
                Type = type,
                Position = eventPosition,
                AnnouncedTime = Time.time,
                StartTime = Time.time + eventWarningTime,
                State = EventState.Announced
            };

            switch (type)
            {
                case WorldEventType.MeteorStrike:
                    worldEvent.Duration = 300f;
                    worldEvent.Radius = meteorLandingRadius;
                    worldEvent.Rewards = GenerateMeteorRewards();
                    break;

                case WorldEventType.SupplyDrop:
                    worldEvent.Duration = supplyDropDuration;
                    worldEvent.Radius = 20f;
                    worldEvent.Rewards = GenerateSupplyRewards();
                    break;

                case WorldEventType.ResourceSurge:
                    worldEvent.Duration = 600f;
                    worldEvent.Radius = 100f;
                    break;

                case WorldEventType.BossSpawn:
                    worldEvent.Duration = 1200f;
                    worldEvent.Radius = 50f;
                    worldEvent.Rewards = GenerateBossRewards();
                    break;
            }

            activeEvents.Add(worldEvent);
            OnEventAnnounced?.Invoke(worldEvent);

            // Notify all players
            AnnounceEvent(worldEvent);

            Debug.Log($"[WorldEvents] {type} announced at {eventPosition}");
        }

        private void UpdateEvent(WorldEvent worldEvent)
        {
            switch (worldEvent.State)
            {
                case EventState.Announced:
                    if (Time.time >= worldEvent.StartTime)
                    {
                        StartEvent(worldEvent);
                    }
                    break;

                case EventState.Active:
                    if (Time.time >= worldEvent.StartTime + worldEvent.Duration)
                    {
                        EndEvent(worldEvent);
                    }
                    break;
            }
        }

        private void StartEvent(WorldEvent worldEvent)
        {
            worldEvent.State = EventState.Active;

            // Spawn event objects
            switch (worldEvent.Type)
            {
                case WorldEventType.MeteorStrike:
                    SpawnMeteor(worldEvent);
                    break;

                case WorldEventType.SupplyDrop:
                    SpawnSupplyDrop(worldEvent);
                    break;

                case WorldEventType.ResourceSurge:
                    SpawnResourceNodes(worldEvent);
                    break;

                case WorldEventType.BossSpawn:
                    SpawnBoss(worldEvent);
                    break;
            }

            OnEventStarted?.Invoke(worldEvent);
            Debug.Log($"[WorldEvents] {worldEvent.Type} started!");
        }

        private void EndEvent(WorldEvent worldEvent)
        {
            worldEvent.State = EventState.Ended;

            // Clean up event objects
            if (worldEvent.SpawnedObject != null)
            {
                Destroy(worldEvent.SpawnedObject);
            }

            activeEvents.Remove(worldEvent);
            OnEventEnded?.Invoke(worldEvent);

            Debug.Log($"[WorldEvents] {worldEvent.Type} ended");
        }

        private void SpawnMeteor(WorldEvent worldEvent)
        {
            // Spawn high in sky, animate falling
            Vector3 spawnPos = worldEvent.Position + Vector3.up * 500f;

            if (meteorPrefab != null)
            {
                GameObject meteor = Instantiate(meteorPrefab, spawnPos, Quaternion.identity);
                worldEvent.SpawnedObject = meteor;

                // Meteor would animate falling and create crater with loot
                StartCoroutine(AnimateMeteorFall(meteor, worldEvent.Position));
            }
        }

        private System.Collections.IEnumerator AnimateMeteorFall(GameObject meteor, Vector3 targetPos)
        {
            Vector3 startPos = meteor.transform.position;
            float duration = 5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                meteor.transform.position = Vector3.Lerp(startPos, targetPos, t * t); // Accelerating
                yield return null;
            }

            // Impact!
            meteor.transform.position = targetPos;

            // Create explosion effect
            // Spawn crater with loot
            Debug.Log("[WorldEvents] Meteor impact!");
        }

        private void SpawnSupplyDrop(WorldEvent worldEvent)
        {
            // Spawn crate that falls from sky
            Vector3 spawnPos = worldEvent.Position + Vector3.up * 200f;

            if (supplyCratePrefab != null)
            {
                GameObject crate = Instantiate(supplyCratePrefab, spawnPos, Quaternion.identity);
                worldEvent.SpawnedObject = crate;

                // Add parachute animation
                StartCoroutine(AnimateSupplyDrop(crate, worldEvent.Position));
            }
        }

        private System.Collections.IEnumerator AnimateSupplyDrop(GameObject crate, Vector3 targetPos)
        {
            Vector3 startPos = crate.transform.position;
            float fallSpeed = 20f;
            float distance = startPos.y - targetPos.y;
            float duration = distance / fallSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                crate.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            crate.transform.position = targetPos;
            Debug.Log("[WorldEvents] Supply crate landed!");
        }

        private void SpawnResourceNodes(WorldEvent worldEvent)
        {
            // Spawn multiple resource nodes in area
            int nodeCount = Random.Range(5, 15);

            for (int i = 0; i < nodeCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * worldEvent.Radius;
                Vector3 pos = worldEvent.Position + new Vector3(offset.x, 0, offset.y);

                // Find ground
                if (Physics.Raycast(pos + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f))
                {
                    pos = hit.point;
                }

                if (resourceNodePrefab != null)
                {
                    Instantiate(resourceNodePrefab, pos, Quaternion.identity);
                }
            }
        }

        private void SpawnBoss(WorldEvent worldEvent)
        {
            // Spawn world boss
            Debug.Log($"[WorldEvents] World boss spawned at {worldEvent.Position}");
            // Would instantiate boss prefab
        }

        private void AnnounceEvent(WorldEvent worldEvent)
        {
            string message = worldEvent.Type switch
            {
                WorldEventType.MeteorStrike => $"METEOR INCOMING! Impact in {eventWarningTime}s - {GetRegionName(worldEvent.Position)}",
                WorldEventType.SupplyDrop => $"SUPPLY DROP INBOUND - {GetRegionName(worldEvent.Position)}",
                WorldEventType.ResourceSurge => $"RESOURCE SURGE DETECTED - {GetRegionName(worldEvent.Position)}",
                WorldEventType.BossSpawn => $"DANGER! WORLD BOSS EMERGING - {GetRegionName(worldEvent.Position)}",
                _ => "Unknown Event"
            };

            GameManager.Instance?.UIManager?.ShowNotification(message, 10f);
        }

        private Vector3 GetRandomEventPosition()
        {
            // Get position near players for relevance
            var localPlayer = GameManager.Instance?.PlayerManager?.LocalPlayer;
            if (localPlayer != null)
            {
                Vector2 offset = Random.insideUnitCircle * 500f; // Within 500 units
                return localPlayer.transform.position + new Vector3(offset.x, 0, offset.y);
            }

            // Fallback: random position
            return new Vector3(Random.Range(-1000f, 1000f), 0, Random.Range(-1000f, 1000f));
        }

        private string GetRegionName(Vector3 position)
        {
            GeoLocation geo = GeoSpawnSystem.Instance?.WorldToGeo(position) ?? new GeoLocation();
            return GeoSpawnSystem.Instance?.GetRegionName(geo) ?? "Unknown";
        }

        private List<EventReward> GenerateMeteorRewards()
        {
            return new List<EventReward>
            {
                new EventReward { ItemId = "rare_ore", Quantity = Random.Range(10, 30) },
                new EventReward { ItemId = "meteor_fragment", Quantity = Random.Range(3, 8) }
            };
        }

        private List<EventReward> GenerateSupplyRewards()
        {
            return new List<EventReward>
            {
                new EventReward { ItemId = "military_gear", Quantity = Random.Range(1, 3) },
                new EventReward { ItemId = "medicine", Quantity = Random.Range(5, 15) },
                new EventReward { ItemId = "ammunition", Quantity = Random.Range(20, 50) }
            };
        }

        private List<EventReward> GenerateBossRewards()
        {
            return new List<EventReward>
            {
                new EventReward { ItemId = "legendary_item", Quantity = 1 },
                new EventReward { ItemId = "boss_trophy", Quantity = 1 }
            };
        }

        public List<WorldEvent> GetActiveEvents()
        {
            return new List<WorldEvent>(activeEvents);
        }

        public WorldEvent GetNearestEvent(Vector3 position)
        {
            WorldEvent nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var evt in activeEvents)
            {
                float dist = Vector3.Distance(position, evt.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = evt;
                }
            }

            return nearest;
        }
    }

    public enum WorldEventType
    {
        MeteorStrike,
        SupplyDrop,
        ResourceSurge,
        BossSpawn
    }

    public enum EventState
    {
        Announced,
        Active,
        Ended
    }

    [System.Serializable]
    public class WorldEvent
    {
        public uint EventId;
        public WorldEventType Type;
        public Vector3 Position;
        public float AnnouncedTime;
        public float StartTime;
        public float Duration;
        public float Radius;
        public EventState State;
        public GameObject SpawnedObject;
        public List<EventReward> Rewards;
    }

    [System.Serializable]
    public class EventReward
    {
        public string ItemId;
        public int Quantity;
    }
}
