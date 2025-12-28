using UnityEngine;
using Pangaea.Core;
using Pangaea.Combat;
using Pangaea.Survival;
using Pangaea.Inventory;
using Pangaea.AI;

namespace Pangaea.Player
{
    /// <summary>
    /// Main player controller - handles input, movement, and coordinates all player systems.
    /// Works for both local and networked players.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Network Identity")]
        [SerializeField] private uint playerId;
        [SerializeField] private bool isLocalPlayer = false;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = -20f;

        [Header("Stamina Costs")]
        [SerializeField] private float runStaminaCost = 10f; // Per second
        [SerializeField] private float jumpStaminaCost = 15f;

        [Header("Sound Settings")]
        [SerializeField] private float walkSoundRadius = 5f;
        [SerializeField] private float runSoundRadius = 15f;
        [SerializeField] private float combatSoundRadius = 30f;
        [SerializeField] private float footstepInterval = 0.5f;
        [SerializeField] private float runFootstepInterval = 0.3f;

        // Components
        private CharacterController characterController;
        private PlayerStats stats;
        private PlayerInventory inventory;
        private PlayerCombat combat;
        private PlayerInput playerInput;

        // State
        private Vector3 moveDirection;
        private Vector3 velocity;
        private bool isRunning;
        private bool isGrounded;

        // PvP State
        private PvPMode pvpMode = PvPMode.Engaged;
        private float pvpModeChangeTimer = 0f;
        private const float PVP_MODE_COOLDOWN = 30f;

        // Sound emission
        private float lastFootstepTime;

        // Properties
        public uint PlayerId => playerId;
        public bool IsLocalPlayer => isLocalPlayer;
        public PlayerStats Stats => stats;
        public PlayerInventory Inventory => inventory;
        public PlayerCombat Combat => combat;
        public PvPMode CurrentPvPMode => pvpMode;
        public bool IsRunning => isRunning;
        public float CurrentSoundRadius => isRunning ? runSoundRadius : (moveDirection.magnitude > 0.1f ? walkSoundRadius : 0f);

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            combat = GetComponent<PlayerCombat>();

            if (isLocalPlayer)
            {
                playerInput = gameObject.AddComponent<PlayerInput>();
            }
        }

        private void Start()
        {
            if (isLocalPlayer)
            {
                // Register with player manager
                GameManager.Instance?.PlayerManager?.RegisterPlayer(playerId, this, true);

                // Set up camera to follow this player
                SetupCamera();
            }
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            UpdateInput();
            UpdateMovement();
            UpdatePvPModeTimer();
            UpdateFootstepSounds();
        }

        private void UpdateInput()
        {
            if (playerInput == null) return;

            // Get movement input
            Vector2 input = playerInput.GetMovementInput();
            moveDirection = new Vector3(input.x, 0f, input.y).normalized;

            // Check running
            isRunning = playerInput.IsRunning() && stats != null && stats.CurrentStamina > 0 && moveDirection.magnitude > 0.1f;

            // Handle other inputs
            if (playerInput.IsAttacking())
            {
                combat?.TryAttack();
            }

            if (playerInput.IsInteracting())
            {
                TryInteract();
            }

            if (playerInput.OpenedInventory())
            {
                GameManager.Instance?.UIManager?.PushScreen(UIScreen.Inventory);
            }
        }

        private void UpdateMovement()
        {
            if (characterController == null) return;

            isGrounded = characterController.isGrounded;

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            // Calculate speed
            float currentSpeed = isRunning ? runSpeed : walkSpeed;

            // Apply stamina cost for running and notify stats
            if (stats != null)
            {
                stats.SetRunning(isRunning);
                if (isRunning)
                {
                    stats.UseStamina(runStaminaCost * Time.deltaTime);
                }
            }

            // Convert to camera-relative movement (isometric)
            Vector3 move = ConvertToIsometric(moveDirection) * currentSpeed;

            // Move character
            characterController.Move(move * Time.deltaTime);

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Rotate towards movement direction
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(ConvertToIsometric(moveDirection));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private Vector3 ConvertToIsometric(Vector3 input)
        {
            // Convert input to isometric direction
            // 45 degree rotation for isometric view
            Quaternion isoRotation = Quaternion.Euler(0, 45, 0);
            return isoRotation * input;
        }

        private void SetupCamera()
        {
            // Find or create isometric camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Add follow component
                CameraFollow follow = cam.GetComponent<CameraFollow>();
                if (follow == null)
                {
                    follow = cam.gameObject.AddComponent<CameraFollow>();
                }
                follow.SetTarget(transform);
            }
        }

        private void TryInteract()
        {
            // Find nearest interactable
            Collider[] nearby = Physics.OverlapSphere(transform.position, 2f);
            IInteractable closestInteractable = null;
            float closestDistance = float.MaxValue;

            foreach (var col in nearby)
            {
                IInteractable interactable = col.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestInteractable = interactable;
                    }
                }
            }

            closestInteractable?.Interact(this);
        }

        public void TogglePvPMode()
        {
            if (pvpModeChangeTimer > 0)
            {
                GameManager.Instance?.UIManager?.ShowNotification($"PvP mode change on cooldown: {pvpModeChangeTimer:F0}s");
                return;
            }

            pvpMode = pvpMode == PvPMode.Passive ? PvPMode.Engaged : PvPMode.Passive;
            pvpModeChangeTimer = PVP_MODE_COOLDOWN;

            string modeText = pvpMode == PvPMode.Passive ? "PASSIVE" : "ENGAGED";
            GameManager.Instance?.UIManager?.ShowNotification($"PvP Mode: {modeText}");
        }

        private void UpdatePvPModeTimer()
        {
            if (pvpModeChangeTimer > 0)
            {
                pvpModeChangeTimer -= Time.deltaTime;
            }
        }

        private void UpdateFootstepSounds()
        {
            // Only emit sounds when moving
            if (moveDirection.magnitude < 0.1f) return;

            float interval = isRunning ? runFootstepInterval : footstepInterval;

            if (Time.time - lastFootstepTime >= interval)
            {
                lastFootstepTime = Time.time;

                // Emit sound for zombie detection
                SoundType soundType = isRunning ? SoundType.Running : SoundType.Footstep;
                float loudness = isRunning ? 1.0f : 0.5f;

                ZombieSenses.MakeSound(transform.position, loudness, soundType);
            }
        }

        public bool CanBeAttacked()
        {
            return pvpMode == PvPMode.Engaged;
        }

        public bool CanAttack()
        {
            return pvpMode == PvPMode.Engaged;
        }

        public void TakeDamage(float damage, PlayerController attacker)
        {
            if (pvpMode == PvPMode.Passive) return;

            stats.TakeDamage(damage);

            // Grant combat sounds for nearby detection
            EmitSound(combatSoundRadius);

            // Alert zombies to combat
            ZombieSenses.MakeSound(transform.position, 1f, SoundType.Combat);

            if (stats.CurrentHealth <= 0)
            {
                Die(attacker);
            }
        }

        public void EmitSound(float radius)
        {
            // Notify nearby players of sound
            var nearbyPlayers = GameManager.Instance?.PlayerManager?.GetPlayersInRange(transform.position, radius);
            if (nearbyPlayers == null) return;

            foreach (var player in nearbyPlayers)
            {
                // Player can "hear" this player
                // Used for stealth detection and proximity voice
            }
        }

        /// <summary>
        /// Emit a sound that zombies can hear.
        /// Called during combat, building, etc.
        /// </summary>
        public void EmitZombieSound(SoundType type, float loudness = 1f)
        {
            ZombieSenses.MakeSound(transform.position, loudness, type);
        }

        private void Die(PlayerController killer)
        {
            Debug.Log($"[Player] {playerId} was killed by {killer?.PlayerId}");

            // Drop inventory
            inventory.DropAllItems(transform.position);

            // Reset level (per design doc)
            stats.ResetLevel();

            // Update karma/reputation
            if (killer != null)
            {
                killer.Stats.OnPlayerKill(this);
            }

            // Show death screen
            GameManager.Instance?.UIManager?.ShowScreen(UIScreen.Death);

            // Respawn will be handled by spawn system
        }

        public void Initialize(uint id, bool isLocal, GeoLocation homeLocation)
        {
            playerId = id;
            isLocalPlayer = isLocal;

            // Set spawn position based on home location
            transform.position = GameManager.Instance.PlayerManager.CalculateSpawnPosition(homeLocation);
        }

        private void OnDestroy()
        {
            GameManager.Instance?.PlayerManager?.UnregisterPlayer(playerId);
        }
    }

    public enum PvPMode
    {
        Passive,
        Engaged
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        void Interact(PlayerController player);
    }
}
