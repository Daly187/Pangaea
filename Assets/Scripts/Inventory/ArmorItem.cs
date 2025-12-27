using UnityEngine;

namespace Pangaea.Inventory
{
    /// <summary>
    /// Armor item - provides defense and can be cosmetically customized.
    /// </summary>
    [CreateAssetMenu(fileName = "New Armor", menuName = "Pangaea/Items/Armor")]
    public class ArmorItem : Item
    {
        [Header("Armor Stats")]
        public ArmorSlot slot;
        public float defense = 5f;
        public float damageReduction = 0f; // Percentage 0-1

        [Header("Resistances")]
        public float physicalResist = 0f;
        public float environmentResist = 0f; // Weather effects

        [Header("Movement")]
        public float movementPenalty = 0f; // Heavy armor slows you

        [Header("Durability")]
        public int maxDurability = 100;

        [Header("Requirements")]
        public int requiredEndurance = 0;
        public int requiredLevel = 1;

        [Header("Visuals")]
        public GameObject armorPrefab;
        public Color defaultColor = Color.white;
        public bool allowDyeing = true;

        private void OnEnable()
        {
            itemType = ItemType.Armor;
            isStackable = false;
            maxStackSize = 1;
        }

        public float CalculateDamageReduction(float incomingDamage)
        {
            // Flat defense
            float reduced = Mathf.Max(0, incomingDamage - defense);

            // Percentage reduction
            reduced *= (1f - damageReduction);

            return reduced;
        }

        public bool CanEquip(Player.PlayerStats stats)
        {
            return stats.Attributes.Endurance >= requiredEndurance &&
                   stats.Level >= requiredLevel;
        }

        public override string GetTooltip()
        {
            string tooltip = base.GetTooltip();
            tooltip += $"\n\nDefense: {defense}";

            if (damageReduction > 0)
                tooltip += $"\nDamage Reduction: {damageReduction * 100}%";
            if (movementPenalty > 0)
                tooltip += $"\nMovement: -{movementPenalty * 100}%";
            if (requiredEndurance > 0)
                tooltip += $"\nRequires: {requiredEndurance} END";
            if (requiredLevel > 1)
                tooltip += $"\nRequires: Level {requiredLevel}";

            return tooltip;
        }
    }

    public enum ArmorSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        Back // Cloaks, capes
    }
}
