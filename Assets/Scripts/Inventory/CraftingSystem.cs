using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;

namespace Pangaea.Inventory
{
    /// <summary>
    /// Crafting system - profession-locked high-tier items.
    /// No one can do everything.
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        public static CraftingSystem Instance { get; private set; }

        [Header("Recipes")]
        [SerializeField] private List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

        private Dictionary<string, CraftingRecipe> recipeById = new Dictionary<string, CraftingRecipe>();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Index recipes
            foreach (var recipe in allRecipes)
            {
                recipeById[recipe.recipeId] = recipe;
            }
        }

        public List<CraftingRecipe> GetAvailableRecipes(PlayerController player)
        {
            List<CraftingRecipe> available = new List<CraftingRecipe>();
            PlayerStats stats = player.Stats;
            PlayerInventory inventory = player.Inventory;

            foreach (var recipe in allRecipes)
            {
                if (CanCraft(recipe, player))
                {
                    available.Add(recipe);
                }
            }

            return available;
        }

        public bool CanCraft(CraftingRecipe recipe, PlayerController player)
        {
            PlayerStats stats = player.Stats;
            PlayerInventory inventory = player.Inventory;

            // Check profession requirement
            if (recipe.requiredProfession != Player.Profession.None &&
                stats.CurrentProfession != recipe.requiredProfession)
            {
                return false;
            }

            // Check crafting level
            if (stats.Attributes.Crafting < recipe.requiredCraftingLevel)
            {
                return false;
            }

            // Check if has all ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                if (!inventory.HasItem(ingredient.item, ingredient.quantity))
                {
                    return false;
                }
            }

            // Check for crafting station if required
            if (recipe.requiresStation != CraftingStation.None)
            {
                // Check if near appropriate station
                // For MVP, we'll skip this check
            }

            return true;
        }

        public bool Craft(CraftingRecipe recipe, PlayerController player)
        {
            if (!CanCraft(recipe, player))
            {
                Debug.Log($"[Crafting] Cannot craft {recipe.result.itemName}");
                return false;
            }

            PlayerInventory inventory = player.Inventory;
            PlayerStats stats = player.Stats;

            // Check if can hold result
            if (!inventory.CanAddItem(recipe.result, recipe.resultQuantity))
            {
                Debug.Log("[Crafting] Inventory full");
                return false;
            }

            // Consume ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                inventory.RemoveItem(ingredient.item, ingredient.quantity);
            }

            // Calculate quality based on crafting stat
            float quality = CalculateCraftQuality(stats.Attributes.Crafting, recipe.requiredCraftingLevel);

            // Create result
            inventory.AddItem(recipe.result, recipe.resultQuantity);

            // Grant XP
            stats.AddExperience(recipe.experienceGain);

            Debug.Log($"[Crafting] Crafted {recipe.resultQuantity}x {recipe.result.itemName} (Quality: {quality:P0})");
            return true;
        }

        private float CalculateCraftQuality(int craftingLevel, int requiredLevel)
        {
            // Quality ranges from 0.8 to 1.2 based on skill vs requirement
            int diff = craftingLevel - requiredLevel;
            float quality = 1.0f + (diff * 0.05f);
            quality += Random.Range(-0.1f, 0.1f);
            return Mathf.Clamp(quality, 0.8f, 1.2f);
        }

        public CraftingRecipe GetRecipe(string recipeId)
        {
            recipeById.TryGetValue(recipeId, out CraftingRecipe recipe);
            return recipe;
        }
    }

    [CreateAssetMenu(fileName = "New Recipe", menuName = "Pangaea/Crafting/Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        public string recipeId;
        public string recipeName;
        [TextArea]
        public string description;

        [Header("Requirements")]
        public Player.Profession requiredProfession = Player.Profession.None;
        public int requiredCraftingLevel = 1;
        public CraftingStation requiresStation = CraftingStation.None;

        [Header("Ingredients")]
        public CraftingIngredient[] ingredients;

        [Header("Result")]
        public Item result;
        public int resultQuantity = 1;

        [Header("XP")]
        public int experienceGain = 10;

        [Header("Time")]
        public float craftTime = 2f; // Seconds
    }

    [System.Serializable]
    public class CraftingIngredient
    {
        public Item item;
        public int quantity = 1;
    }

    public enum CraftingStation
    {
        None,           // Can craft anywhere
        Workbench,      // Basic crafting
        Forge,          // Blacksmith - weapons/armor
        AlchemyLab,     // Alchemist - potions
        Workshop,       // Engineer - vehicles/traps
        TanningRack,    // Hunter - leather/mounts
        Constructor     // Builder - base structures
    }
}
