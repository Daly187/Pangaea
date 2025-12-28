using UnityEngine;
using Pangaea.Player;

namespace Pangaea.AI
{
    /// <summary>
    /// Handles headshot detection for zombies.
    /// Attach to a separate head collider on the zombie.
    /// Headshots are instant kills (Walking Dead style).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HeadshotHitbox : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float headshotMultiplier = 100f; // Effectively instant kill

        private ZombieAI zombieAI;
        private ZombieStats zombieStats;

        private void Awake()
        {
            // Get references from parent (zombie root)
            zombieAI = GetComponentInParent<ZombieAI>();
            zombieStats = GetComponentInParent<ZombieStats>();

            // Ensure this collider is a trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        /// <summary>
        /// Called by weapon/projectile when hitting this collider.
        /// </summary>
        public void OnHeadshot(float baseDamage, PlayerController attacker)
        {
            if (zombieAI == null || !zombieAI.IsAlive) return;

            Debug.Log("[HeadshotHitbox] HEADSHOT!");
            zombieAI.TakeHeadshot(baseDamage * headshotMultiplier, attacker);
        }
    }

    /// <summary>
    /// Body hitbox for standard damage.
    /// Attach to the main body collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BodyHitbox : MonoBehaviour
    {
        [Header("Damage Modifiers")]
        [SerializeField] private float damageMultiplier = 1f;

        private ZombieAI zombieAI;
        private ZombieStats zombieStats;

        private void Awake()
        {
            zombieAI = GetComponentInParent<ZombieAI>();
            zombieStats = GetComponentInParent<ZombieStats>();
        }

        /// <summary>
        /// Called by weapon/projectile when hitting this collider.
        /// </summary>
        public void OnHit(float damage, PlayerController attacker)
        {
            if (zombieAI == null || !zombieAI.IsAlive) return;

            zombieAI.TakeDamage(damage * damageMultiplier, attacker);
        }
    }

    /// <summary>
    /// Helper component to detect what was hit by a weapon.
    /// Attach to weapons/projectiles.
    /// </summary>
    public class DamageDealer : MonoBehaviour
    {
        [SerializeField] private float baseDamage = 25f;
        [SerializeField] private LayerMask hitLayers;
        [SerializeField] private float hitRange = 2f;

        private PlayerController owner;

        public void Initialize(PlayerController player, float damage)
        {
            owner = player;
            baseDamage = damage;
        }

        /// <summary>
        /// Perform a melee attack check.
        /// </summary>
        public void PerformMeleeAttack(Vector3 origin, Vector3 direction)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, hitRange, hitLayers))
            {
                ProcessHit(hit.collider, hit.point);
            }
        }

        /// <summary>
        /// Process a hit on a collider.
        /// </summary>
        public void ProcessHit(Collider hitCollider, Vector3 hitPoint)
        {
            // Check for headshot first
            HeadshotHitbox headshot = hitCollider.GetComponent<HeadshotHitbox>();
            if (headshot != null)
            {
                headshot.OnHeadshot(baseDamage, owner);
                return;
            }

            // Check for body hit
            BodyHitbox bodyHit = hitCollider.GetComponent<BodyHitbox>();
            if (bodyHit != null)
            {
                bodyHit.OnHit(baseDamage, owner);
                return;
            }

            // Direct ZombieAI hit (fallback)
            ZombieAI zombie = hitCollider.GetComponent<ZombieAI>();
            if (zombie == null)
            {
                zombie = hitCollider.GetComponentInParent<ZombieAI>();
            }

            if (zombie != null && zombie.IsAlive)
            {
                zombie.TakeDamage(baseDamage, owner);
            }
        }
    }

    /// <summary>
    /// For projectile-based weapons (arrows, thrown weapons).
    /// Attach to the projectile prefab.
    /// </summary>
    public class DamageProjectile : MonoBehaviour
    {
        [SerializeField] private float baseDamage = 50f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private bool destroyOnHit = true;

        private PlayerController owner;
        private bool hasHit = false;

        public void Initialize(PlayerController player, float damage)
        {
            owner = player;
            baseDamage = damage;
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;
            if (other.isTrigger) return; // Skip other triggers

            // Don't hit owner
            PlayerController hitPlayer = other.GetComponent<PlayerController>();
            if (hitPlayer != null && hitPlayer == owner) return;

            // Check for headshot
            HeadshotHitbox headshot = other.GetComponent<HeadshotHitbox>();
            if (headshot != null)
            {
                hasHit = true;
                headshot.OnHeadshot(baseDamage, owner);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
                return;
            }

            // Check for body hit
            BodyHitbox bodyHit = other.GetComponent<BodyHitbox>();
            if (bodyHit != null)
            {
                hasHit = true;
                bodyHit.OnHit(baseDamage, owner);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
                return;
            }

            // Direct zombie hit
            ZombieAI zombie = other.GetComponent<ZombieAI>();
            if (zombie == null)
            {
                zombie = other.GetComponentInParent<ZombieAI>();
            }

            if (zombie != null && zombie.IsAlive)
            {
                hasHit = true;
                zombie.TakeDamage(baseDamage, owner);

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
