using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;
using Pangaea.Inventory;
using Pangaea.AI;

namespace Pangaea.Survival
{
    /// <summary>
    /// World containers that can be looted by players.
    /// Examples: fridges, cabinets, cars, dumpsters, bushes.
    /// Makes noise when searched (attracts zombies).
    /// </summary>
    public class ScavengeableContainer : MonoBehaviour, IInteractable
    {
        [Header("Container Settings")]
        [SerializeField] private string containerName = "Container";
        [SerializeField] private LootTable lootTable;
        [SerializeField] private LootTableType fallbackType = LootTableType.Residential;

        [Header("Search Settings")]
        [SerializeField] private float searchTime = 3f;
        [SerializeField] private float searchNoiseRadius = 10f;
        [SerializeField] private SoundType searchSoundType = SoundType.Building;

        [Header("Respawn Settings")]
        [SerializeField] private bool canRespawn = true;
        [SerializeField] private float respawnTime = 300f; // 5 minutes
        [SerializeField] private bool respawnOnlyWhenFar = true;
        [SerializeField] private float respawnDistance = 50f;

        [Header("Visual")]
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openVisual;
        [SerializeField] private GameObject emptyVisual;

        [Header("Audio")]
        [SerializeField] private AudioClip searchSound;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip emptySound;

        // State
        private bool hasBeenLooted = false;
        private float lastLootedTime;
        private List<LootDrop> currentLoot = new List<LootDrop>();
        private bool isBeingSearched = false;
        private PlayerController currentSearcher;
        private float searchProgress = 0f;

        // Components
        private AudioSource audioSource;

        public string InteractionPrompt => hasBeenLooted ? $"{containerName} (Empty)" : $"Search {containerName}";
        public bool IsEmpty => hasBeenLooted;
        public float SearchProgress => searchProgress / searchTime;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }

            // Generate initial loot
            GenerateLoot();
            UpdateVisuals();
        }

        private void Update()
        {
            // Handle active search
            if (isBeingSearched && currentSearcher != null)
            {
                searchProgress += Time.deltaTime;

                // Emit search noise periodically
                if (Mathf.FloorToInt(searchProgress) != Mathf.FloorToInt(searchProgress - Time.deltaTime))
                {
                    ZombieSenses.MakeSound(transform.position, 0.5f, searchSoundType);
                }

                if (searchProgress >= searchTime)
                {
                    CompleteSearch();
                }
            }

            // Handle respawning
            if (canRespawn && hasBeenLooted)
            {
                if (Time.time - lastLootedTime >= respawnTime)
                {
                    TryRespawn();
                }
            }
        }

        private void GenerateLoot()
        {
            currentLoot.Clear();

            if (lootTable != null)
            {
                currentLoot = lootTable.GenerateLoot();
            }
            else
            {
                // Use fallback default loot based on type
                currentLoot = GenerateDefaultLoot(fallbackType);
            }

            hasBeenLooted = currentLoot.Count == 0;
        }

        private List<LootDrop> GenerateDefaultLoot(LootTableType type)
        {
            List<LootDrop> loot = new List<LootDrop>();

            // This would normally reference actual Item ScriptableObjects
            // For now, just log what would be generated
            Debug.Log($"[Scavengeable] Would generate {type} loot");

            return loot;
        }

        public void Interact(PlayerController player)
        {
            if (hasBeenLooted)
            {
                // Play empty sound
                if (emptySound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(emptySound);
                }
                return;
            }

            if (isBeingSearched)
            {
                if (currentSearcher == player)
                {
                    // Cancel search
                    CancelSearch();
                }
                return;
            }

            // Start searching
            StartSearch(player);
        }

        private void StartSearch(PlayerController player)
        {
            isBeingSearched = true;
            currentSearcher = player;
            searchProgress = 0f;

            // Play search sound
            if (searchSound != null && audioSource != null)
            {
                audioSource.clip = searchSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // Initial noise
            ZombieSenses.MakeSound(transform.position, 0.7f, searchSoundType);

            Debug.Log($"[Scavengeable] {player.PlayerId} started searching {containerName}");
        }

        private void CancelSearch()
        {
            isBeingSearched = false;
            currentSearcher = null;
            searchProgress = 0f;

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            Debug.Log($"[Scavengeable] Search cancelled");
        }

        private void CompleteSearch()
        {
            isBeingSearched = false;

            // Stop search sound
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            // Play open sound
            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            // Give loot to player
            if (currentSearcher != null && currentSearcher.Inventory != null)
            {
                foreach (var drop in currentLoot)
                {
                    if (drop.item != null)
                    {
                        bool added = currentSearcher.Inventory.AddItem(drop.item, drop.quantity);
                        if (added)
                        {
                            Debug.Log($"[Scavengeable] {currentSearcher.PlayerId} found {drop.quantity}x {drop.item.itemName}");
                        }
                        else
                        {
                            // Inventory full - drop on ground
                            Debug.Log($"[Scavengeable] Inventory full, dropping {drop.item.itemName}");
                            // Would spawn world item here
                        }
                    }
                }
            }

            // Mark as looted
            hasBeenLooted = true;
            lastLootedTime = Time.time;
            currentLoot.Clear();
            currentSearcher = null;

            UpdateVisuals();

            // Final noise from opening
            ZombieSenses.MakeSound(transform.position, 1f, searchSoundType);
        }

        private void TryRespawn()
        {
            if (!canRespawn) return;

            if (respawnOnlyWhenFar)
            {
                // Check if any player is nearby
                var localPlayer = Core.GameManager.Instance?.PlayerManager?.LocalPlayer;
                if (localPlayer != null)
                {
                    float distance = Vector3.Distance(transform.position, localPlayer.transform.position);
                    if (distance < respawnDistance)
                    {
                        return; // Player too close, wait longer
                    }
                }
            }

            // Respawn loot
            GenerateLoot();
            hasBeenLooted = false;
            UpdateVisuals();

            Debug.Log($"[Scavengeable] {containerName} respawned loot");
        }

        private void UpdateVisuals()
        {
            if (closedVisual != null) closedVisual.SetActive(!hasBeenLooted && !isBeingSearched);
            if (openVisual != null) openVisual.SetActive(isBeingSearched);
            if (emptyVisual != null) emptyVisual.SetActive(hasBeenLooted);
        }

        /// <summary>
        /// Force add specific items to this container.
        /// </summary>
        public void AddLoot(Item item, int quantity)
        {
            currentLoot.Add(new LootDrop { item = item, quantity = quantity });
            hasBeenLooted = false;
            UpdateVisuals();
        }

        /// <summary>
        /// Check if container has a specific item.
        /// </summary>
        public bool HasItem(Item item)
        {
            foreach (var drop in currentLoot)
            {
                if (drop.item == item) return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            // Search noise radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, searchNoiseRadius);

            // Respawn distance
            if (respawnOnlyWhenFar)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, respawnDistance);
            }
        }
    }
}
