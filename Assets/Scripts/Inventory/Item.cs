using UnityEngine;

namespace Pangaea.Inventory
{
    /// <summary>
    /// Base item data - defined as ScriptableObject for easy editing.
    /// All items in the game derive from this.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Pangaea/Items/Item")]
    public class Item : ScriptableObject
    {
        [Header("Basic Info")]
        public string itemId;
        public string itemName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Type & Category")]
        public ItemType itemType = ItemType.Misc;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Stacking")]
        public bool isStackable = true;
        public int maxStackSize = 99;

        [Header("Weight & Value")]
        public float weight = 1f;
        public int baseValue = 10; // Gold value

        [Header("Flags")]
        public bool isDroppable = true;
        public bool isTradeable = true;
        public bool isSoulbound = false; // Cosmetics are soulbound

        [Header("Crafting")]
        public bool isCraftable = false;
        public Player.Profession requiredProfession = Player.Profession.None;
        public int craftingLevel = 0;

        public virtual void Use(Player.PlayerController player)
        {
            Debug.Log($"[Item] Using {itemName}");
        }

        public virtual string GetTooltip()
        {
            string tooltip = $"<b>{itemName}</b>\n";
            tooltip += $"<color=#{GetRarityColor()}>{rarity}</color>\n";
            tooltip += $"{description}\n";
            tooltip += $"Weight: {weight} | Value: {baseValue}g";
            return tooltip;
        }

        private string GetRarityColor()
        {
            return rarity switch
            {
                ItemRarity.Common => "FFFFFF",
                ItemRarity.Uncommon => "00FF00",
                ItemRarity.Rare => "0088FF",
                ItemRarity.Epic => "AA00FF",
                ItemRarity.Legendary => "FF8800",
                _ => "FFFFFF"
            };
        }
    }

    public enum ItemType
    {
        Misc,
        Weapon,
        Armor,
        Consumable,
        Material,
        Tool,
        Cosmetic,
        Blueprint,
        Currency
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

}
