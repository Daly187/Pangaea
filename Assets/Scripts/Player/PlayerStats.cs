using UnityEngine;
using System;

namespace Pangaea.Player
{
    /// <summary>
    /// Player stats, attributes, and progression system.
    /// Handles levels 1-10, attribute points, and profession.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Level & Experience")]
        [SerializeField] private int level = 1;
        [SerializeField] private int experience = 0;
        [SerializeField] private int attributePoints = 0;

        [Header("Vital Stats")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;
        [SerializeField] private float maxHunger = 100f;
        [SerializeField] private float currentHunger = 100f;

        [Header("Regeneration")]
        [SerializeField] private float healthRegenRate = 1f; // Per second when not in combat
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float hungerDecayRate = 0.5f; // Per minute

        [Header("Attributes")]
        [SerializeField] private PlayerAttributes attributes;

        [Header("Profession")]
        [SerializeField] private Profession profession = Profession.None;
        [SerializeField] private bool professionLocked = false;

        [Header("Reputation")]
        [SerializeField] private int karma = 0; // -1000 to 1000
        [SerializeField] private int bountyGold = 0;

        // Combat state
        private float lastCombatTime = -100f;
        private const float COMBAT_COOLDOWN = 10f;

        // Events
        public event Action<int> OnLevelChanged;
        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action<float, float> OnHungerChanged;
        public event Action<int> OnKarmaChanged;
        public event Action OnDeath;

        // Properties
        public int Level => level;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float CurrentHunger => currentHunger;
        public int Karma => karma;
        public int BountyGold => bountyGold;
        public Profession CurrentProfession => profession;
        public bool IsProfessionLocked => professionLocked;
        public PlayerAttributes Attributes => attributes;
        public ReputationTier ReputationTier => GetReputationTier();

        // XP required per level
        private static readonly int[] XP_REQUIREMENTS = { 0, 100, 250, 500, 1000, 2000, 4000, 7000, 11000, 16000 };

        private void Start()
        {
            // Initialize attributes if needed
            if (attributes == null)
            {
                attributes = new PlayerAttributes();
            }

            // Apply attribute bonuses to stats
            RecalculateStats();
        }

        private void Update()
        {
            UpdateRegeneration();
            UpdateHunger();
        }

        private void UpdateRegeneration()
        {
            // Stamina always regenerates
            if (currentStamina < maxStamina)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }

            // Health regenerates when out of combat and fed
            bool inCombat = Time.time - lastCombatTime < COMBAT_COOLDOWN;
            if (!inCombat && currentHunger > 20f && currentHealth < maxHealth)
            {
                float regenMultiplier = currentHunger > 50f ? 1f : 0.5f;
                currentHealth = Mathf.Min(maxHealth, currentHealth + healthRegenRate * regenMultiplier * Time.deltaTime);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }

        private void UpdateHunger()
        {
            // Hunger decays over time
            currentHunger -= (hungerDecayRate / 60f) * Time.deltaTime;
            currentHunger = Mathf.Max(0f, currentHunger);

            // Starving causes damage
            if (currentHunger <= 0f)
            {
                TakeDamage(1f * Time.deltaTime); // 1 damage per second while starving
            }

            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        public void TakeDamage(float damage)
        {
            lastCombatTime = Time.time;
            currentHealth -= damage;
            currentHealth = Mathf.Max(0f, currentHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void Feed(float amount)
        {
            currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        public bool UseStamina(float amount)
        {
            if (currentStamina < amount) return false;

            currentStamina -= amount;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true;
        }

        public void AddExperience(int xp)
        {
            if (level >= 10) return; // Max level

            experience += xp;

            // Check for level up
            while (level < 10 && experience >= XP_REQUIREMENTS[level])
            {
                experience -= XP_REQUIREMENTS[level];
                LevelUp();
            }
        }

        private void LevelUp()
        {
            level++;
            attributePoints += 3; // 3 points per level

            // Unlock new skills/abilities based on level
            Debug.Log($"[PlayerStats] Leveled up to {level}! +3 attribute points");

            OnLevelChanged?.Invoke(level);
            RecalculateStats();
        }

        public void ResetLevel()
        {
            // Death penalty: reset to level 1
            level = 1;
            experience = 0;
            // Note: Attribute points spent are NOT refunded
            // This is intentional per design doc

            OnLevelChanged?.Invoke(level);
            RecalculateStats();
        }

        public bool SpendAttributePoint(AttributeType attribute)
        {
            if (attributePoints <= 0) return false;

            switch (attribute)
            {
                case AttributeType.Strength:
                    attributes.Strength++;
                    break;
                case AttributeType.Agility:
                    attributes.Agility++;
                    break;
                case AttributeType.Endurance:
                    attributes.Endurance++;
                    break;
                case AttributeType.Perception:
                    attributes.Perception++;
                    break;
                case AttributeType.Crafting:
                    attributes.Crafting++;
                    break;
                case AttributeType.Survival:
                    attributes.Survival++;
                    break;
            }

            attributePoints--;
            RecalculateStats();
            return true;
        }

        private void RecalculateStats()
        {
            // Base stats + attribute bonuses
            maxHealth = 100f + (attributes.Endurance * 10f);
            maxStamina = 100f + (attributes.Agility * 5f) + (attributes.Endurance * 5f);

            // Clamp current values
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            currentStamina = Mathf.Min(currentStamina, maxStamina);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        public bool SetProfession(Profession newProfession)
        {
            if (professionLocked) return false;

            profession = newProfession;
            professionLocked = true;

            Debug.Log($"[PlayerStats] Profession locked: {profession}");
            return true;
        }

        public void OnPlayerKill(PlayerController victim)
        {
            // Killing affects karma
            int karmaChange = -50; // Base penalty

            // Killing lower level = more penalty
            int levelDiff = level - victim.Stats.Level;
            if (levelDiff > 2) karmaChange -= 25;

            // Killing someone with positive karma = penalty
            if (victim.Stats.Karma > 0) karmaChange -= 25;

            // Killing a bandit = less penalty or bonus
            if (victim.Stats.Karma < -100) karmaChange += 75;

            ModifyKarma(karmaChange);

            // Add bounty based on karma
            if (karma < 0)
            {
                bountyGold += 10 * Mathf.Abs(karmaChange);
            }
        }

        public void ModifyKarma(int change)
        {
            karma = Mathf.Clamp(karma + change, -1000, 1000);
            OnKarmaChanged?.Invoke(karma);
        }

        public void AddBounty(int gold)
        {
            bountyGold += gold;
        }

        public void ClearBounty()
        {
            bountyGold = 0;
        }

        public ReputationTier GetReputationTier()
        {
            if (karma < -500) return ReputationTier.Bandit;
            if (karma < -100) return ReputationTier.Outlaw;
            if (karma < 100) return ReputationTier.Neutral;
            if (karma < 500) return ReputationTier.Trusted;
            return ReputationTier.Guardian;
        }
    }

    [Serializable]
    public class PlayerAttributes
    {
        public int Strength = 1;      // Melee damage, carry weight
        public int Agility = 1;       // Movement speed, attack speed
        public int Endurance = 1;     // Health, stamina
        public int Perception = 1;    // Vision range, sound detection
        public int Crafting = 1;      // Craft quality, recipes
        public int Survival = 1;      // Hunger decay, resource gathering
    }

    public enum AttributeType
    {
        Strength,
        Agility,
        Endurance,
        Perception,
        Crafting,
        Survival
    }

    public enum Profession
    {
        None,
        Blacksmith,   // Weapons and armor
        Alchemist,    // Potions and buffs
        Engineer,     // Vehicles and traps
        Hunter,       // Tracking and taming
        Builder       // Bases and structures
    }

    public enum ReputationTier
    {
        Bandit,       // Red name, KOS by guards
        Outlaw,       // Orange name
        Neutral,      // White name
        Trusted,      // Light blue name
        Guardian      // Blue name, defender
    }
}
