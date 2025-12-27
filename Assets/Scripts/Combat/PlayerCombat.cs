using UnityEngine;
using System.Collections;
using Pangaea.Player;
using Pangaea.Inventory;

namespace Pangaea.Combat
{
    /// <summary>
    /// Player combat system - melee and ranged attacks.
    /// No guns per design doc - swords, spears, bows only.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combat State")]
        [SerializeField] private bool isAttacking = false;
        [SerializeField] private bool canAttack = true;
        [SerializeField] private float attackCooldown = 0f;

        [Header("Targeting")]
        [SerializeField] private float autoTargetRange = 10f;
        [SerializeField] private float autoTargetAngle = 45f;
        [SerializeField] private LayerMask targetLayers;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        // Components
        private PlayerController player;
        private PlayerStats stats;
        private PlayerInventory inventory;
        private Animator animator;

        // Combat state
        private PlayerController currentTarget;
        private float lastAttackTime;
        private int comboCount = 0;
        private float comboResetTimer = 0f;
        private const float COMBO_WINDOW = 1.5f;
        private const int MAX_COMBO = 3;

        // Events
        public System.Action<float, bool> OnDamageDealt;
        public System.Action OnAttackStart;
        public System.Action OnAttackEnd;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            animator = GetComponent<Animator>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            UpdateCooldowns();
            UpdateCombo();
        }

        private void UpdateCooldowns()
        {
            if (attackCooldown > 0)
            {
                attackCooldown -= Time.deltaTime;
                if (attackCooldown <= 0)
                {
                    canAttack = true;
                }
            }
        }

        private void UpdateCombo()
        {
            if (comboResetTimer > 0)
            {
                comboResetTimer -= Time.deltaTime;
                if (comboResetTimer <= 0)
                {
                    comboCount = 0;
                }
            }
        }

        public void TryAttack()
        {
            if (!canAttack || isAttacking) return;
            if (!player.CanAttack()) return;

            WeaponItem weapon = inventory.Equipment.Weapon;

            // Check stamina
            float staminaCost = weapon?.staminaCost ?? 10f;
            if (!stats.UseStamina(staminaCost))
            {
                Debug.Log("[Combat] Not enough stamina");
                return;
            }

            // Find target
            currentTarget = FindBestTarget();

            // Start attack
            StartCoroutine(PerformAttack(weapon));
        }

        private IEnumerator PerformAttack(WeaponItem weapon)
        {
            isAttacking = true;
            canAttack = false;
            OnAttackStart?.Invoke();

            // Calculate attack timing
            float attackSpeed = weapon?.attackSpeed ?? 1f;
            float attackDuration = 1f / attackSpeed;

            // Apply agility bonus
            attackDuration /= (1f + stats.Attributes.Agility * 0.05f);

            // Trigger animation
            if (animator != null)
            {
                animator.SetTrigger($"Attack{comboCount}");
            }

            // Face target
            if (currentTarget != null)
            {
                Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // Wait for attack windup (first half of animation)
            yield return new WaitForSeconds(attackDuration * 0.4f);

            // Deal damage at hit frame
            if (weapon != null && (weapon.weaponType == WeaponType.Bow || weapon.weaponType == WeaponType.Thrown))
            {
                // Ranged attack
                FireProjectile(weapon);
            }
            else
            {
                // Melee attack
                PerformMeleeHit(weapon);
            }

            // Wait for recovery
            yield return new WaitForSeconds(attackDuration * 0.6f);

            isAttacking = false;
            OnAttackEnd?.Invoke();

            // Set cooldown
            attackCooldown = 0.1f; // Small buffer between attacks
            canAttack = false;

            // Update combo
            comboCount = (comboCount + 1) % MAX_COMBO;
            comboResetTimer = COMBO_WINDOW;

            lastAttackTime = Time.time;
        }

        private void PerformMeleeHit(WeaponItem weapon)
        {
            float range = weapon?.range ?? 2f;

            // Get all potential targets in range
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * (range * 0.5f), range, targetLayers);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                PlayerController targetPlayer = hit.GetComponent<PlayerController>();
                if (targetPlayer != null && targetPlayer.CanBeAttacked())
                {
                    DealDamage(targetPlayer, weapon);
                }

                // Also check for NPCs, destructibles, etc.
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && damageable != player as IDamageable)
                {
                    float damage = CalculateDamage(weapon);
                    damageable.TakeDamage(damage, player);
                }
            }

            // Emit combat sound
            player.EmitSound(30f);
        }

        private void FireProjectile(WeaponItem weapon)
        {
            // Create projectile
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
            Vector3 direction;

            if (currentTarget != null)
            {
                // Aim at target
                Vector3 targetPos = currentTarget.transform.position + Vector3.up;
                direction = (targetPos - spawnPos).normalized;
            }
            else
            {
                // Aim forward
                direction = transform.forward;
            }

            // Spawn projectile (would instantiate prefab)
            Debug.Log($"[Combat] Fired projectile from {spawnPos} in direction {direction}");

            // For now, simulate instant hit if target exists
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                if (distance <= weapon.range)
                {
                    DealDamage(currentTarget, weapon);
                }
            }

            player.EmitSound(20f);
        }

        private void DealDamage(PlayerController target, WeaponItem weapon)
        {
            float damage = CalculateDamage(weapon);
            bool isCritical = false;

            // Critical hit
            float critChance = weapon?.criticalChance ?? 0.05f;
            if (Random.value < critChance)
            {
                float critMult = weapon?.criticalMultiplier ?? 1.5f;
                damage *= critMult;
                isCritical = true;
            }

            // Combo bonus
            damage *= (1f + comboCount * 0.1f);

            // Apply defense
            float defense = target.Inventory?.Equipment?.GetTotalDefense() ?? 0f;
            damage = Mathf.Max(1f, damage - defense);

            // Deal damage
            target.TakeDamage(damage, player);

            // Knockback
            if (weapon != null)
            {
                Vector3 knockDir = (target.transform.position - transform.position).normalized;
                // Apply knockback force to target
            }

            // Trigger effects
            OnDamageDealt?.Invoke(damage, isCritical);

            // Camera shake
            CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
            cam?.Shake(isCritical ? 0.3f : 0.1f, 0.15f);

            Debug.Log($"[Combat] Dealt {damage:F1} damage to {target.PlayerId} (Crit: {isCritical})");
        }

        private float CalculateDamage(WeaponItem weapon)
        {
            float baseDamage = weapon?.baseDamage ?? 5f;

            // Strength bonus for melee
            if (weapon == null || (weapon.weaponType != WeaponType.Bow && weapon.weaponType != WeaponType.Thrown))
            {
                baseDamage += stats.Attributes.Strength * 2f;
            }
            else
            {
                // Agility bonus for ranged
                baseDamage += stats.Attributes.Agility * 1.5f;
            }

            // Random variance
            baseDamage *= Random.Range(0.9f, 1.1f);

            return baseDamage;
        }

        private PlayerController FindBestTarget()
        {
            var nearbyPlayers = Core.GameManager.Instance?.PlayerManager?.GetPlayersInRange(transform.position, autoTargetRange);
            if (nearbyPlayers == null || nearbyPlayers.Count == 0) return null;

            PlayerController best = null;
            float bestScore = -1f;

            foreach (var target in nearbyPlayers)
            {
                if (!target.CanBeAttacked()) continue;

                Vector3 toTarget = target.transform.position - transform.position;
                float distance = toTarget.magnitude;
                float angle = Vector3.Angle(transform.forward, toTarget);

                if (angle > autoTargetAngle) continue;

                // Score: closer and more centered = better
                float score = (1f - distance / autoTargetRange) + (1f - angle / autoTargetAngle);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            return best;
        }

        public void PerformFinisher(PlayerController target)
        {
            if (target == null) return;
            if (target.Stats.CurrentHealth > 20f) return; // Only on low health

            Debug.Log($"[Combat] Performing finisher on {target.PlayerId}");

            // Anime-style finishing move
            StartCoroutine(FinisherSequence(target));
        }

        private IEnumerator FinisherSequence(PlayerController target)
        {
            // Lock both players
            isAttacking = true;

            // Play finisher animation
            if (animator != null)
            {
                animator.SetTrigger("Finisher");
            }

            // Slow-mo effect
            Time.timeScale = 0.3f;

            yield return new WaitForSecondsRealtime(1.5f);

            Time.timeScale = 1f;

            // Kill target
            target.TakeDamage(999f, player);

            isAttacking = false;
        }

        // Block/Parry system (future enhancement)
        public void StartBlock()
        {
            // Blocking reduces incoming damage
        }

        public void EndBlock()
        {
            // End blocking
        }
    }

    public interface IDamageable
    {
        void TakeDamage(float damage, PlayerController attacker);
        bool IsAlive { get; }
    }
}
