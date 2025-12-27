using UnityEngine;

namespace Pangaea.Inventory
{
    /// <summary>
    /// Consumable items - food, potions, buffs.
    /// </summary>
    [CreateAssetMenu(fileName = "New Consumable", menuName = "Pangaea/Items/Consumable")]
    public class ConsumableItem : Item
    {
        [Header("Consumable Type")]
        public ConsumableType consumableType;

        [Header("Instant Effects")]
        public float healthRestore = 0f;
        public float staminaRestore = 0f;
        public float hungerRestore = 0f;

        [Header("Buff Effects")]
        public BuffEffect[] buffs;
        public float buffDuration = 0f;

        [Header("Usage")]
        public float useTime = 1f; // Seconds to consume
        public bool interruptable = true; // Can be canceled by combat

        [Header("Audio/Visual")]
        public AudioClip useSound;

        private void OnEnable()
        {
            itemType = ItemType.Consumable;
            isStackable = true;
        }

        public override void Use(Player.PlayerController player)
        {
            if (player == null || player.Stats == null) return;

            // Apply instant effects
            if (healthRestore > 0)
                player.Stats.Heal(healthRestore);

            if (hungerRestore > 0)
                player.Stats.Feed(hungerRestore);

            // Stamina restore is instant
            // (handled differently since it's not a stat method)

            // Apply buffs
            foreach (var buff in buffs)
            {
                ApplyBuff(player, buff, buffDuration);
            }

            Debug.Log($"[Consumable] {itemName} used - Health: +{healthRestore}, Hunger: +{hungerRestore}");
        }

        private void ApplyBuff(Player.PlayerController player, BuffEffect buff, float duration)
        {
            // Buff system would track active effects
            Debug.Log($"[Consumable] Applied buff {buff.buffType} x{buff.value} for {duration}s");
        }

        public override string GetTooltip()
        {
            string tooltip = base.GetTooltip();

            if (healthRestore > 0)
                tooltip += $"\n+{healthRestore} Health";
            if (staminaRestore > 0)
                tooltip += $"\n+{staminaRestore} Stamina";
            if (hungerRestore > 0)
                tooltip += $"\n+{hungerRestore} Hunger";

            foreach (var buff in buffs)
            {
                tooltip += $"\n{buff.GetDescription()}";
            }

            if (buffDuration > 0)
                tooltip += $"\nDuration: {buffDuration}s";

            return tooltip;
        }
    }

    public enum ConsumableType
    {
        Food,
        Potion,
        Medicine,
        Buff
    }

    [System.Serializable]
    public class BuffEffect
    {
        public BuffType buffType;
        public float value;
        public bool isPercentage;

        public string GetDescription()
        {
            string sign = value >= 0 ? "+" : "";
            string percent = isPercentage ? "%" : "";
            return $"{sign}{value}{percent} {buffType}";
        }
    }

    public enum BuffType
    {
        MaxHealth,
        MaxStamina,
        HealthRegen,
        StaminaRegen,
        MoveSpeed,
        AttackDamage,
        Defense,
        CritChance,
        HungerDecay, // Negative = slower hunger
        VisionRange
    }
}
