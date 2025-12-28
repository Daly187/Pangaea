using UnityEngine;
using Pangaea.Inventory;

namespace Pangaea.Survival
{
    /// <summary>
    /// Defines a crop type that can be grown in farm plots/greenhouses.
    /// </summary>
    [CreateAssetMenu(fileName = "New Crop", menuName = "Pangaea/Survival/Crop")]
    public class CropData : ScriptableObject
    {
        [Header("Basic Info")]
        public string cropName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("Seed")]
        public Item seedItem;
        public int seedCost = 1; // Seeds consumed per planting

        [Header("Harvest")]
        public Item harvestItem;
        public int minYield = 1;
        public int maxYield = 3;
        public float bonusSeedChance = 0.3f; // Chance to get seed back

        [Header("Growth")]
        public float growthTime = 300f; // Seconds to fully grow (5 min default)
        public int growthStages = 4; // Visual stages
        public bool requiresWater = true;
        public float waterInterval = 60f; // How often it needs water

        [Header("Environment")]
        public bool indoorOnly = false; // Must be in greenhouse
        public float temperatureMin = 10f;
        public float temperatureMax = 35f;
        public Season[] validSeasons; // Which seasons it can grow

        [Header("Visuals")]
        public GameObject[] stagePrefabs; // Prefabs for each growth stage

        /// <summary>
        /// Get the growth stage (0-indexed) for a given progress.
        /// </summary>
        public int GetGrowthStage(float progress)
        {
            if (progress >= 1f) return growthStages - 1;
            return Mathf.FloorToInt(progress * growthStages);
        }

        /// <summary>
        /// Calculate yield with bonuses.
        /// </summary>
        public int CalculateYield(float qualityBonus = 0f)
        {
            int baseYield = Random.Range(minYield, maxYield + 1);
            int bonus = Mathf.FloorToInt(baseYield * qualityBonus);
            return baseYield + bonus;
        }

        /// <summary>
        /// Check if seed is returned on harvest.
        /// </summary>
        public bool RollForBonusSeed()
        {
            return Random.value <= bonusSeedChance;
        }
    }

    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    /// <summary>
    /// Crop growth state tracking.
    /// </summary>
    [System.Serializable]
    public class CropInstance
    {
        public CropData cropData;
        public float plantedTime;
        public float lastWateredTime;
        public bool isWatered;
        public bool isFullyGrown;
        public float growthProgress;

        public CropInstance(CropData data)
        {
            cropData = data;
            plantedTime = Time.time;
            lastWateredTime = Time.time;
            isWatered = true;
            isFullyGrown = false;
            growthProgress = 0f;
        }

        /// <summary>
        /// Update growth progress.
        /// </summary>
        public void UpdateGrowth(float deltaTime, bool isIndoors, float temperature)
        {
            if (isFullyGrown) return;
            if (cropData == null) return;

            // Check if needs water
            if (cropData.requiresWater)
            {
                float timeSinceWater = Time.time - lastWateredTime;
                isWatered = timeSinceWater < cropData.waterInterval;

                if (!isWatered)
                {
                    // Growth halts without water
                    return;
                }
            }

            // Check temperature (indoor plants ignore this)
            if (!isIndoors && cropData.indoorOnly)
            {
                // Can't grow outdoors
                return;
            }

            if (!isIndoors)
            {
                if (temperature < cropData.temperatureMin || temperature > cropData.temperatureMax)
                {
                    // Too hot or cold, growth slows
                    deltaTime *= 0.25f;
                }
            }

            // Progress growth
            growthProgress += deltaTime / cropData.growthTime;
            growthProgress = Mathf.Clamp01(growthProgress);

            if (growthProgress >= 1f)
            {
                isFullyGrown = true;
            }
        }

        /// <summary>
        /// Water this crop.
        /// </summary>
        public void Water()
        {
            lastWateredTime = Time.time;
            isWatered = true;
        }

        /// <summary>
        /// Get current visual stage index.
        /// </summary>
        public int GetCurrentStage()
        {
            return cropData?.GetGrowthStage(growthProgress) ?? 0;
        }
    }
}
