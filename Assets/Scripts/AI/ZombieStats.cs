using UnityEngine;
using System;

namespace Pangaea.AI
{
    /// <summary>
    /// Zombie stats and configuration.
    /// Different zombie types can have different stats.
    /// </summary>
    public class ZombieStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("Combat")]
        [SerializeField] private float baseDamage = 15f;
        [SerializeField] private float attackSpeed = 1f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 1f;
        [SerializeField] private float chaseSpeed = 6f; // Faster than player walk (5), slower than run (10)

        [Header("Detection")]
        [SerializeField] private float sightRange = 20f;
        [SerializeField] private float hearingRange = 30f;
        [SerializeField] private float fieldOfView = 120f;

        [Header("Zombie Type")]
        [SerializeField] private ZombieType zombieType = ZombieType.Walker;

        // Events
        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;

        // Properties
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float BaseDamage => baseDamage;
        public float AttackSpeed => attackSpeed;
        public float WalkSpeed => walkSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float SightRange => sightRange;
        public float HearingRange => hearingRange;
        public float FieldOfView => fieldOfView;
        public ZombieType Type => zombieType;
        public bool IsAlive => currentHealth > 0;

        private void Awake()
        {
            currentHealth = maxHealth;
            ApplyZombieTypeModifiers();
        }

        private void ApplyZombieTypeModifiers()
        {
            switch (zombieType)
            {
                case ZombieType.Walker:
                    // Default stats
                    break;

                case ZombieType.Shambler:
                    // Slow but tough
                    walkSpeed *= 0.7f;
                    chaseSpeed *= 0.7f;
                    maxHealth *= 1.5f;
                    currentHealth = maxHealth;
                    break;

                case ZombieType.Runner:
                    // Fast but fragile
                    walkSpeed *= 1.3f;
                    chaseSpeed *= 1.4f; // Can almost keep up with running player
                    maxHealth *= 0.7f;
                    currentHealth = maxHealth;
                    break;

                case ZombieType.Crawler:
                    // Low, hard to hit, ambush
                    walkSpeed *= 0.5f;
                    chaseSpeed *= 0.8f;
                    sightRange *= 0.5f;
                    hearingRange *= 1.5f; // Better hearing
                    break;

                case ZombieType.Brute:
                    // Tank zombie
                    walkSpeed *= 0.6f;
                    chaseSpeed *= 0.7f;
                    maxHealth *= 3f;
                    currentHealth = maxHealth;
                    baseDamage *= 2f;
                    break;

                case ZombieType.Screamer:
                    // Alerts others, weak itself
                    maxHealth *= 0.5f;
                    currentHealth = maxHealth;
                    hearingRange *= 2f;
                    break;
            }
        }

        public void TakeDamage(float damage)
        {
            if (currentHealth <= 0) return;

            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);

            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (currentHealth <= 0) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetZombieType(ZombieType type)
        {
            zombieType = type;
            ApplyZombieTypeModifiers();
        }
    }

    public enum ZombieType
    {
        Walker,     // Standard zombie - Walking Dead style
        Shambler,   // Slow but tough
        Runner,     // Fast but fragile (28 Days Later style)
        Crawler,    // Crawls on ground, ambush predator
        Brute,      // Tank zombie, lots of health and damage
        Screamer    // Alerts all nearby zombies when it sees player
    }
}
