using UnityEngine;
using UnityEngine.AI;
using Pangaea.Player;
using Pangaea.Combat;

namespace Pangaea.AI
{
    /// <summary>
    /// Walking Dead style zombie AI.
    /// - Detects players by sight and sound
    /// - Shambles toward targets, slightly faster than walking speed
    /// - Can be killed by headshots (instant) or body damage
    /// - Attracts other zombies when aggroed
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(ZombieStats))]
    [RequireComponent(typeof(ZombieSenses))]
    public class ZombieAI : MonoBehaviour, IDamageable
    {
        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 1f;
        [SerializeField] private float chaseSpeed = 6f; // Slightly faster than player walk (5)
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float wanderInterval = 5f;

        [Header("Combat")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float lungeDistance = 2f;

        [Header("Behavior")]
        [SerializeField] private float aggroRange = 30f;
        [SerializeField] private float deaggroTime = 10f;
        [SerializeField] private float alertOthersRadius = 15f;
        [SerializeField] private bool canAlertOthers = true;

        [Header("Audio")]
        [SerializeField] private AudioClip[] idleSounds;
        [SerializeField] private AudioClip[] aggroSounds;
        [SerializeField] private AudioClip[] attackSounds;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private float idleSoundInterval = 5f;

        // Components
        private NavMeshAgent agent;
        private ZombieStats stats;
        private ZombieSenses senses;
        private Animator animator;
        private AudioSource audioSource;

        // State
        private ZombieState currentState = ZombieState.Idle;
        private Transform currentTarget;
        private Vector3 wanderDestination;
        private float lastWanderTime;
        private float lastAttackTime;
        private float lastTargetSeenTime;
        private float lastIdleSoundTime;
        private bool isDead = false;

        // Animation hashes
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDeath = Animator.StringToHash("Death");
        private static readonly int AnimHit = Animator.StringToHash("Hit");

        public bool IsAlive => !isDead;
        public ZombieState State => currentState;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            stats = GetComponent<ZombieStats>();
            senses = GetComponent<ZombieSenses>();
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // 3D sound
                audioSource.maxDistance = 20f;
            }
        }

        private void Start()
        {
            agent.speed = wanderSpeed;
            agent.stoppingDistance = attackRange * 0.8f;

            // Subscribe to senses events
            senses.OnPlayerDetected += OnPlayerDetected;
            senses.OnSoundHeard += OnSoundHeard;

            SetState(ZombieState.Idle);
        }

        private void Update()
        {
            if (isDead) return;

            switch (currentState)
            {
                case ZombieState.Idle:
                    UpdateIdle();
                    break;
                case ZombieState.Wandering:
                    UpdateWandering();
                    break;
                case ZombieState.Investigating:
                    UpdateInvestigating();
                    break;
                case ZombieState.Chasing:
                    UpdateChasing();
                    break;
                case ZombieState.Attacking:
                    UpdateAttacking();
                    break;
            }

            UpdateAnimation();
            UpdateIdleSounds();
        }

        #region State Updates

        private void UpdateIdle()
        {
            // Occasionally start wandering
            if (Time.time - lastWanderTime > wanderInterval)
            {
                SetState(ZombieState.Wandering);
            }
        }

        private void UpdateWandering()
        {
            if (!agent.hasPath || agent.remainingDistance < 0.5f)
            {
                // Pick new wander destination
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += transform.position;

                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    wanderDestination = hit.position;
                    agent.SetDestination(wanderDestination);
                }

                lastWanderTime = Time.time;

                // Sometimes return to idle
                if (Random.value < 0.3f)
                {
                    SetState(ZombieState.Idle);
                }
            }
        }

        private void UpdateInvestigating()
        {
            if (!agent.hasPath || agent.remainingDistance < 1f)
            {
                // Reached investigation point, look around
                SetState(ZombieState.Idle);
            }

            // Check if we can see target now
            if (currentTarget != null && senses.CanSeeTarget(currentTarget))
            {
                SetState(ZombieState.Chasing);
            }
        }

        private void UpdateChasing()
        {
            if (currentTarget == null)
            {
                SetState(ZombieState.Idle);
                return;
            }

            // Check if target is still valid
            PlayerController player = currentTarget.GetComponent<PlayerController>();
            if (player != null && player.Stats.CurrentHealth <= 0)
            {
                currentTarget = null;
                SetState(ZombieState.Idle);
                return;
            }

            // Update destination
            agent.SetDestination(currentTarget.position);

            // Check if can see target
            if (senses.CanSeeTarget(currentTarget))
            {
                lastTargetSeenTime = Time.time;
            }
            else
            {
                // Lost sight - deaggro after time
                if (Time.time - lastTargetSeenTime > deaggroTime)
                {
                    currentTarget = null;
                    SetState(ZombieState.Wandering);
                    return;
                }
            }

            // Check attack range
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
            if (distanceToTarget <= attackRange)
            {
                SetState(ZombieState.Attacking);
            }
        }

        private void UpdateAttacking()
        {
            if (currentTarget == null)
            {
                SetState(ZombieState.Idle);
                return;
            }

            // Face target
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
            }

            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

            // If target moved out of range, chase again
            if (distanceToTarget > attackRange * 1.5f)
            {
                SetState(ZombieState.Chasing);
                return;
            }

            // Attack on cooldown
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                PerformAttack();
            }
        }

        #endregion

        #region State Management

        private void SetState(ZombieState newState)
        {
            if (currentState == newState) return;

            // Exit current state
            switch (currentState)
            {
                case ZombieState.Chasing:
                    agent.speed = wanderSpeed;
                    break;
            }

            currentState = newState;

            // Enter new state
            switch (newState)
            {
                case ZombieState.Idle:
                    agent.ResetPath();
                    lastWanderTime = Time.time;
                    break;

                case ZombieState.Wandering:
                    agent.speed = wanderSpeed;
                    break;

                case ZombieState.Chasing:
                    agent.speed = chaseSpeed;
                    PlayAggroSound();
                    if (canAlertOthers)
                    {
                        AlertNearbyZombies();
                    }
                    break;

                case ZombieState.Attacking:
                    agent.ResetPath();
                    break;
            }

            Debug.Log($"[Zombie] State changed to: {newState}");
        }

        #endregion

        #region Detection Events

        private void OnPlayerDetected(Transform player)
        {
            if (isDead) return;
            if (currentState == ZombieState.Chasing || currentState == ZombieState.Attacking) return;

            currentTarget = player;
            lastTargetSeenTime = Time.time;
            SetState(ZombieState.Chasing);
        }

        private void OnSoundHeard(Vector3 soundPosition, float loudness)
        {
            if (isDead) return;
            if (currentState == ZombieState.Chasing || currentState == ZombieState.Attacking) return;

            // Investigate sound
            wanderDestination = soundPosition;
            agent.SetDestination(soundPosition);
            SetState(ZombieState.Investigating);
        }

        #endregion

        #region Combat

        private void PerformAttack()
        {
            lastAttackTime = Time.time;

            // Play animation
            if (animator != null)
            {
                animator.SetTrigger(AnimAttack);
            }

            // Play sound
            PlayAttackSound();

            // Deal damage after small delay (animation sync)
            Invoke(nameof(DealDamage), 0.3f);
        }

        private void DealDamage()
        {
            if (currentTarget == null) return;

            float distance = Vector3.Distance(transform.position, currentTarget.position);
            if (distance > attackRange * 1.2f) return;

            PlayerController player = currentTarget.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(attackDamage, null);
                Debug.Log($"[Zombie] Attacked player for {attackDamage} damage");
            }
        }

        public void TakeDamage(float damage, PlayerController attacker)
        {
            if (isDead) return;

            stats.TakeDamage(damage);

            // Play hit animation
            if (animator != null)
            {
                animator.SetTrigger(AnimHit);
            }

            // Aggro on attacker
            if (attacker != null && currentTarget == null)
            {
                currentTarget = attacker.transform;
                lastTargetSeenTime = Time.time;
                SetState(ZombieState.Chasing);
            }

            if (stats.CurrentHealth <= 0)
            {
                Die();
            }
        }

        public void TakeHeadshot(float damage, PlayerController attacker)
        {
            if (isDead) return;

            Debug.Log("[Zombie] HEADSHOT!");

            // Headshots are instant kill
            stats.TakeDamage(stats.CurrentHealth + 100);
            Die();
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            currentState = ZombieState.Dead;

            // Stop movement
            agent.enabled = false;

            // Play death animation
            if (animator != null)
            {
                animator.SetTrigger(AnimDeath);
            }

            // Play death sound
            if (deathSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(deathSound);
            }

            // Disable colliders
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Destroy after delay
            Destroy(gameObject, 5f);

            Debug.Log("[Zombie] Died");
        }

        #endregion

        #region Alerting Other Zombies

        private void AlertNearbyZombies()
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, alertOthersRadius);

            foreach (var col in nearby)
            {
                if (col.gameObject == gameObject) continue;

                ZombieAI otherZombie = col.GetComponent<ZombieAI>();
                if (otherZombie != null && otherZombie.IsAlive)
                {
                    otherZombie.AlertToTarget(currentTarget);
                }
            }
        }

        public void AlertToTarget(Transform target)
        {
            if (isDead) return;
            if (currentState == ZombieState.Chasing || currentState == ZombieState.Attacking) return;

            currentTarget = target;
            lastTargetSeenTime = Time.time;
            SetState(ZombieState.Chasing);
        }

        #endregion

        #region Animation & Audio

        private void UpdateAnimation()
        {
            if (animator == null) return;

            float speed = agent.velocity.magnitude;
            animator.SetFloat(AnimSpeed, speed);
        }

        private void UpdateIdleSounds()
        {
            if (currentState == ZombieState.Dead) return;

            if (Time.time - lastIdleSoundTime > idleSoundInterval)
            {
                lastIdleSoundTime = Time.time + Random.Range(-2f, 2f);

                if (idleSounds != null && idleSounds.Length > 0 && audioSource != null)
                {
                    AudioClip clip = idleSounds[Random.Range(0, idleSounds.Length)];
                    audioSource.PlayOneShot(clip, 0.5f);
                }
            }
        }

        private void PlayAggroSound()
        {
            if (aggroSounds != null && aggroSounds.Length > 0 && audioSource != null)
            {
                AudioClip clip = aggroSounds[Random.Range(0, aggroSounds.Length)];
                audioSource.PlayOneShot(clip);
            }
        }

        private void PlayAttackSound()
        {
            if (attackSounds != null && attackSounds.Length > 0 && audioSource != null)
            {
                AudioClip clip = attackSounds[Random.Range(0, attackSounds.Length)];
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (senses != null)
            {
                senses.OnPlayerDetected -= OnPlayerDetected;
                senses.OnSoundHeard -= OnSoundHeard;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Alert range
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, alertOthersRadius);
        }
    }

    public enum ZombieState
    {
        Idle,
        Wandering,
        Investigating,
        Chasing,
        Attacking,
        Dead
    }
}
