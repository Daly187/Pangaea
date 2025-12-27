using UnityEngine;

namespace Pangaea.Inventory
{
    /// <summary>
    /// Weapon item - swords, spears, bows, etc.
    /// No guns per design doc.
    /// </summary>
    [CreateAssetMenu(fileName = "New Weapon", menuName = "Pangaea/Items/Weapon")]
    public class WeaponItem : Item
    {
        [Header("Weapon Stats")]
        public WeaponType weaponType;
        public float baseDamage = 10f;
        public float attackSpeed = 1f; // Attacks per second
        public float range = 2f; // Melee range or projectile range

        [Header("Stamina")]
        public float staminaCost = 10f;

        [Header("Combat")]
        public float criticalChance = 0.05f;
        public float criticalMultiplier = 1.5f;
        public float knockback = 1f;

        [Header("Durability")]
        public int maxDurability = 100;
        public bool usesDurability = true;

        [Header("Requirements")]
        public int requiredStrength = 0;
        public int requiredAgility = 0;
        public int requiredLevel = 1;

        [Header("Visuals")]
        public GameObject weaponPrefab;
        public AnimatorOverrideController animatorOverride;

        private void OnEnable()
        {
            itemType = ItemType.Weapon;
            isStackable = false;
            maxStackSize = 1;
        }

        public float CalculateDamage(Player.PlayerStats stats)
        {
            float damage = baseDamage;

            // Strength bonus for melee
            if (weaponType != WeaponType.Bow && weaponType != WeaponType.Thrown)
            {
                damage += stats.Attributes.Strength * 2f;
            }
            // Agility bonus for ranged
            else
            {
                damage += stats.Attributes.Agility * 1.5f;
            }

            // Random variance (10%)
            damage *= Random.Range(0.9f, 1.1f);

            return damage;
        }

        public bool CanEquip(Player.PlayerStats stats)
        {
            return stats.Attributes.Strength >= requiredStrength &&
                   stats.Attributes.Agility >= requiredAgility &&
                   stats.Level >= requiredLevel;
        }

        public override string GetTooltip()
        {
            string tooltip = base.GetTooltip();
            tooltip += $"\n\nDamage: {baseDamage}";
            tooltip += $"\nAttack Speed: {attackSpeed}/s";
            tooltip += $"\nRange: {range}m";

            if (requiredStrength > 0)
                tooltip += $"\nRequires: {requiredStrength} STR";
            if (requiredAgility > 0)
                tooltip += $"\nRequires: {requiredAgility} AGI";
            if (requiredLevel > 1)
                tooltip += $"\nRequires: Level {requiredLevel}";

            return tooltip;
        }
    }

    public enum WeaponType
    {
        Sword,
        Knife,
        Spear,
        Axe,
        Club,
        Bow,
        Thrown // Rocks, javelins
    }
}
