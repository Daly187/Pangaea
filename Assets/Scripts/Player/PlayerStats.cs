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
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float hungerDecayRate = 0.5f; // Per minute when idle

        [Header("Health Regen (CoD Style)")]
        [SerializeField] private float regenDelayAfterDamage = 5f; // Seconds before regen starts
        [SerializeField] private float fastRegenRate = 0.83f; // ~50 HP in 60 seconds (to 50%)
        [SerializeField] private float slowRegenRate = 0.014f; // ~50 HP in 60 minutes (50% to 100%)
        [SerializeField] private float fedRegenMultiplier = 3f; // Eating boosts regen 3x
        [SerializeField] private float wellFedThreshold = 70f; // Hunger above this = "well fed"

        [Header("Energy Drain (Running)")]
        [SerializeField] private float runningHungerMultiplier = 3f; // Running drains hunger 3x faster
        [SerializeField] private float combatHungerMultiplier = 2f; // Combat drains hunger 2x faster

        [Header("Attributes")]
        [SerializeField] private PlayerAttributes attributes;

        [Header("Profession")]
        [SerializeField] private Profession profession = Profession.None;
        [SerializeField] private bool professionLocked = false;

        [Header("Reputation")]
        [SerializeField] private int karma = 0; // -1000 to 1000
        [SerializeField] private int bountyGold = 0;

        // Combat state
        private float lastDamageTime = -100f;
        private float lastCombatTime = -100f;
        private const float COMBAT_COOLDOWN = 10f;

        // Activity state (set by PlayerController)
        private bool isRunning = false;
        private bool isInCombat = false;

        // Regen state
        private bool isRegenerating = false;

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
        public bool IsRegenerating => isRegenerating;
        public float HealthPercent => currentHealth / maxHealth;

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

            // Health regen (CoD style)
            UpdateHealthRegeneration();
        }

        /// <summary>
        /// CoD-style health regeneration:
        /// - Wait for delay after taking damage
        /// - Fast regen to 50% (~1 minute)
        /// - Slow regen from 50% to 100% (~1 hour)
        /// - Eating food multiplies regen rate
        /// </summary>
        private void UpdateHealthRegeneration()
        {
            // Don't regen if dead
            if (currentHealth <= 0) return;

            // Don't regen if at full health
            if (currentHealth >= maxHealth)
            {
                isRegenerating = false;
                return;
            }

            // Check if enough time has passed since taking damage
            float timeSinceDamage = Time.time - lastDamageTime;
            if (timeSinceDamage < regenDelayAfterDamage)
            {
                isRegenerating = false;
                return;
            }

            isRegenerating = true;

            // Calculate current health percentage
            float healthPercent = currentHealth / maxHealth;

            // Determine regen rate based on health percentage
            float baseRegenRate;
            if (healthPercent < 0.5f)
            {
                // Fast regen phase (0% to 50%)
                // ~50 HP in 60 seconds = 0.83 HP/sec
                baseRegenRate = fastRegenRate;
            }
            else
            {
                // Slow regen phase (50% to 100%)
                // ~50 HP in 60 minutes = 0.014 HP/sec
                baseRegenRate = slowRegenRate;
            }

            // Food bonus: well-fed players heal faster
            float foodMultiplier = 1f;
            if (currentHunger >= wellFedThreshold)
            {
                // Well fed - heal 3x faster
                foodMultiplier = fedRegenMultiplier;
            }
            else if (currentHunger >= 30f)
            {
                // Partially fed - heal 1.5x faster
                foodMultiplier = 1.5f;
            }
            else if (currentHunger <= 0f)
            {
                // Starving - no healing (taking damage instead)
                return;
            }

            // Endurance attribute bonus (+5% per point)
            float enduranceBonus = 1f + (attributes.Endurance * 0.05f);

            // Calculate final regen
            float regenAmount = baseRegenRate * foodMultiplier * enduranceBonus * Time.deltaTime;
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenAmount);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void UpdateHunger()
        {
            // Calculate hunger multiplier based on activity
            float hungerMultiplier = 1f;
            if (isRunning) hungerMultiplier = runningHungerMultiplier;
            else if (isInCombat) hungerMultiplier = combatHungerMultiplier;

            // Survival attribute reduces hunger decay
            float survivalBonus = 1f - (attributes.Survival * 0.05f); // 5% reduction per point
            survivalBonus = Mathf.Max(0.5f, survivalBonus); // Cap at 50% reduction

            // Hunger decays over time (faster when running/fighting)
            float hungerDrain = (hungerDecayRate / 60f) * hungerMultiplier * survivalBonus * Time.deltaTime;
            currentHunger -= hungerDrain;
            currentHunger = Mathf.Max(0f, currentHunger);

            // Starving causes damage
            if (currentHunger <= 0f)
            {
                TakeDamage(1f * Time.deltaTime); // 1 damage per second while starving
            }

            OnHungerChanged?.Invoke(currentHunger, maxHunger);
        }

        /// <summary>
        /// Set running state (called by PlayerController)
        /// </summary>
        public void SetRunning(bool running)
        {
            isRunning = running;
        }

        /// <summary>
        /// Set combat state
        /// </summary>
        public void SetInCombat(bool combat)
        {
            isInCombat = combat;
            if (combat) lastCombatTime = Time.time;
        }

        public void TakeDamage(float damage)
        {
            lastDamageTime = Time.time;
            lastCombatTime = Time.time;
            isRegenerating = false;

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
