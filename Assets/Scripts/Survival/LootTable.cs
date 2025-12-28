using UnityEngine;
using System.Collections.Generic;
using Pangaea.Inventory;

namespace Pangaea.Survival
{
    /// <summary>
    /// Configurable loot table for scavengeable containers.
    /// Supports weighted random drops with min/max quantities.
    /// </summary>
    [CreateAssetMenu(fileName = "New Loot Table", menuName = "Pangaea/Survival/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Header("Loot Settings")]
        [SerializeField] private string tableName;
        [SerializeField] private LootEntry[] entries;

        [Header("Drop Settings")]
        [SerializeField] private int minDrops = 1;
        [SerializeField] private int maxDrops = 3;
        [SerializeField] private bool allowDuplicates = true;

        public string TableName => tableName;

        /// <summary>
        /// Generate random loot from this table.
        /// </summary>
        public List<LootDrop> GenerateLoot()
        {
            List<LootDrop> drops = new List<LootDrop>();
            int dropCount = Random.Range(minDrops, maxDrops + 1);

            // Calculate total weight
            float totalWeight = 0f;
            foreach (var entry in entries)
            {
                totalWeight += entry.weight;
            }

            if (totalWeight <= 0 || entries.Length == 0) return drops;

            // Track used entries if no duplicates
            HashSet<int> usedIndices = new HashSet<int>();

            for (int i = 0; i < dropCount; i++)
            {
                // Roll for item
                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;

                for (int j = 0; j < entries.Length; j++)
                {
                    if (!allowDuplicates && usedIndices.Contains(j)) continue;

                    cumulative += entries[j].weight;
                    if (roll <= cumulative)
                    {
                        LootEntry entry = entries[j];

                        // Check drop chance
                        if (Random.value <= entry.dropChance)
                        {
                            int quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                            drops.Add(new LootDrop
                            {
                                item = entry.item,
                                quantity = quantity
                            });

                            if (!allowDuplicates)
                            {
                                usedIndices.Add(j);
                            }
                        }
                        break;
                    }
                }
            }

            return drops;
        }

        /// <summary>
        /// Get a specific item by chance (for guaranteed drops)
        /// </summary>
        public LootDrop? TryGetGuaranteedDrop(Item item)
        {
            foreach (var entry in entries)
            {
                if (entry.item == item && entry.isGuaranteed)
                {
                    return new LootDrop
                    {
                        item = entry.item,
                        quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1)
                    };
                }
            }
            return null;
        }
    }

    [System.Serializable]
    public class LootEntry
    {
        public Item item;
        public float weight = 1f;
        [Range(0f, 1f)] public float dropChance = 1f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
        public bool isGuaranteed = false;
    }

    [System.Serializable]
    public struct LootDrop
    {
        public Item item;
        public int quantity;
    }

    /// <summary>
    /// Predefined loot table types for world containers.
    /// </summary>
    public enum LootTableType
    {
        Residential,    // Homes - food, basic supplies
        Commercial,     // Stores - varied goods
        Industrial,     // Factories - materials, tools
        Medical,        // Hospitals - medicine, bandages
        Military,       // Bases - weapons, armor (rare)
        Vehicle,        // Cars - fuel, parts
        Trash,          // Dumpsters - low quality random
        Nature          // Bushes, trees - berries, wood
    }
}
