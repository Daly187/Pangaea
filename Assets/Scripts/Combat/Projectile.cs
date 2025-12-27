using UnityEngine;
using Pangaea.Player;
using Pangaea.Inventory;

namespace Pangaea.Combat
{
    /// <summary>
    /// Projectile for ranged attacks - arrows, thrown weapons.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 30f;
        [SerializeField] private float maxDistance = 50f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private bool canCrit = true;
        [SerializeField] private float critChance = 0.1f;
        [SerializeField] private float critMultiplier = 1.5f;

        [Header("Physics")]
        [SerializeField] private bool useGravity = true;
        [SerializeField] private float gravityMultiplier = 0.5f;

        [Header("Effects")]
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private AudioClip hitSound;

        // State
        private Vector3 startPosition;
        private Vector3 velocity;
        private PlayerController owner;
        private bool hasHit = false;

        public void Initialize(PlayerController shooter, float baseDamage, Vector3 direction, WeaponItem weapon = null)
        {
            owner = shooter;
            damage = baseDamage;
            velocity = direction.normalized * speed;
            startPosition = transform.position;

            if (weapon != null)
            {
                critChance = weapon.criticalChance;
                critMultiplier = weapon.criticalMultiplier;
            }

            // Set rotation to face direction
            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void Update()
        {
            if (hasHit) return;

            // Apply gravity
            if (useGravity)
            {
                velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
            }

            // Move
            Vector3 movement = velocity * Time.deltaTime;
            transform.position += movement;
            transform.rotation = Quaternion.LookRotation(velocity.normalized);

            // Check max distance
            float distance = Vector3.Distance(startPosition, transform.position);
            if (distance > maxDistance)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;
            if (other.gameObject == owner.gameObject) return;

            hasHit = true;

            // Check for player hit
            PlayerController targetPlayer = other.GetComponent<PlayerController>();
            if (targetPlayer != null && targetPlayer.CanBeAttacked())
            {
                float finalDamage = damage;
                bool isCrit = false;

                // Critical hit
                if (canCrit && Random.value < critChance)
                {
                    finalDamage *= critMultiplier;
                    isCrit = true;
                }

                // Apply defense
                float defense = targetPlayer.Inventory?.Equipment?.GetTotalDefense() ?? 0f;
                finalDamage = Mathf.Max(1f, finalDamage - defense);

                targetPlayer.TakeDamage(finalDamage, owner);

                Debug.Log($"[Projectile] Hit {targetPlayer.PlayerId} for {finalDamage} damage (Crit: {isCrit})");
            }

            // Check for other damageable objects
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, owner);
            }

            // Spawn hit effect
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // Play hit sound
            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, transform.position);
            }

            Destroy(gameObject);
        }
    }
}
