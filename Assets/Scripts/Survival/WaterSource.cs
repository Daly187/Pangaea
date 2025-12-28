using UnityEngine;
using Pangaea.Player;
using Pangaea.Inventory;

namespace Pangaea.Survival
{
    /// <summary>
    /// Water sources in the world (rivers, lakes, wells, rain collectors).
    /// Players can fill water containers here.
    /// </summary>
    public class WaterSource : MonoBehaviour, IInteractable
    {
        [Header("Source Settings")]
        [SerializeField] private string sourceName = "Water Source";
        [SerializeField] private WaterSourceType sourceType = WaterSourceType.River;
        [SerializeField] private bool isInfinite = true;
        [SerializeField] private float waterAmount = 100f;
        [SerializeField] private float maxWater = 100f;

        [Header("Refill (for collectors)")]
        [SerializeField] private float refillRate = 1f; // Per second during rain
        [SerializeField] private bool requiresRain = false;

        [Header("Water Quality")]
        [SerializeField] private float purity = 1f; // 1 = pure, 0 = contaminated
        [SerializeField] private bool requiresPurification = false;

        [Header("Audio")]
        [SerializeField] private AudioClip fillSound;

        private AudioSource audioSource;

        public string InteractionPrompt => $"Fill Container ({sourceName})";
        public float WaterAmount => waterAmount;
        public float Purity => purity;
        public bool IsEmpty => !isInfinite && waterAmount <= 0;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }
        }

        private void Update()
        {
            // Refill during rain (for rain collectors)
            if (requiresRain && !isInfinite)
            {
                bool isRaining = false; // Would get from weather system

                if (isRaining && waterAmount < maxWater)
                {
                    waterAmount = Mathf.Min(maxWater, waterAmount + refillRate * Time.deltaTime);
                }
            }
        }

        public void Interact(PlayerController player)
        {
            if (IsEmpty)
            {
                Debug.Log("[WaterSource] Source is empty");
                return;
            }

            // Try to fill player's water container
            if (TryFillPlayerContainer(player))
            {
                // Play sound
                if (fillSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(fillSound);
                }
            }
        }

        private bool TryFillPlayerContainer(PlayerController player)
        {
            if (player?.Inventory == null) return false;

            // Look for water container in inventory
            // For now, just add water item directly
            Debug.Log("[WaterSource] Would fill player's water container");

            // Consume water if not infinite
            if (!isInfinite)
            {
                float consumed = Mathf.Min(10f, waterAmount);
                waterAmount -= consumed;
            }

            return true;
        }

        /// <summary>
        /// Draw water from this source.
        /// </summary>
        public float DrawWater(float amount)
        {
            if (isInfinite) return amount;

            float drawn = Mathf.Min(amount, waterAmount);
            waterAmount -= drawn;
            return drawn;
        }

        /// <summary>
        /// Add water to this source (for player-filled containers).
        /// </summary>
        public void AddWater(float amount, float waterPurity = 1f)
        {
            if (isInfinite) return;

            waterAmount = Mathf.Min(maxWater, waterAmount + amount);

            // Average purity
            purity = (purity + waterPurity) / 2f;
        }
    }

    public enum WaterSourceType
    {
        River,          // Infinite, may be contaminated
        Lake,           // Infinite, cleaner
        Well,           // Infinite, clean (base building)
        RainCollector,  // Limited, refills during rain
        WaterTank,      // Limited, player-filled
        Puddle          // Limited, contaminated
    }

    /// <summary>
    /// Rain collector building for bases.
    /// Collects rainwater over time.
    /// </summary>
    public class RainCollector : MonoBehaviour, IInteractable
    {
        [Header("Collector Settings")]
        [SerializeField] private float maxCapacity = 50f;
        [SerializeField] private float currentWater = 0f;
        [SerializeField] private float collectionRate = 0.5f; // Per second during rain

        [Header("Visuals")]
        [SerializeField] private Transform waterLevelIndicator;
        [SerializeField] private float minIndicatorY = 0f;
        [SerializeField] private float maxIndicatorY = 1f;

        public string InteractionPrompt => $"Rain Collector ({Mathf.FloorToInt(currentWater)}/{maxCapacity})";
        public float CurrentWater => currentWater;
        public float Capacity => maxCapacity;
        public bool IsFull => currentWater >= maxCapacity;
        public bool IsEmpty => currentWater <= 0;

        private void Update()
        {
            // Check if raining (would come from weather system)
            bool isRaining = false; // Placeholder

            if (isRaining && currentWater < maxCapacity)
            {
                currentWater = Mathf.Min(maxCapacity, currentWater + collectionRate * Time.deltaTime);
                UpdateVisual();
            }
        }

        public void Interact(PlayerController player)
        {
            if (IsEmpty)
            {
                Debug.Log("[RainCollector] Collector is empty");
                return;
            }

            // Give water to player
            float taken = Mathf.Min(10f, currentWater);
            currentWater -= taken;

            Debug.Log($"[RainCollector] Player took {taken} water");
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (waterLevelIndicator == null) return;

            float fillPercent = currentWater / maxCapacity;
            float y = Mathf.Lerp(minIndicatorY, maxIndicatorY, fillPercent);

            Vector3 pos = waterLevelIndicator.localPosition;
            pos.y = y;
            waterLevelIndicator.localPosition = pos;
        }

        /// <summary>
        /// Manually add water.
        /// </summary>
        public void AddWater(float amount)
        {
            currentWater = Mathf.Min(maxCapacity, currentWater + amount);
            UpdateVisual();
        }
    }
}
