using UnityEngine;
using Pangaea.Player;
using Pangaea.Inventory;
using Pangaea.Building;

namespace Pangaea.Survival
{
    /// <summary>
    /// A farm plot where crops can be planted and grown.
    /// Can be placed outdoors or inside a greenhouse.
    /// </summary>
    public class FarmPlot : MonoBehaviour, IInteractable
    {
        [Header("Plot Settings")]
        [SerializeField] private bool isIndoors = false;
        [SerializeField] private float qualityBonus = 0f; // From greenhouse tier

        [Header("Current Crop")]
        [SerializeField] private CropInstance currentCrop;

        [Header("Visuals")]
        [SerializeField] private Transform cropSpawnPoint;
        [SerializeField] private GameObject emptyPlotVisual;
        [SerializeField] private GameObject wateredSoilVisual;
        [SerializeField] private GameObject drySoilVisual;

        [Header("Audio")]
        [SerializeField] private AudioClip plantSound;
        [SerializeField] private AudioClip waterSound;
        [SerializeField] private AudioClip harvestSound;

        // State
        private GameObject currentCropVisual;
        private int lastVisualStage = -1;
        private AudioSource audioSource;

        // Ownership
        private uint ownerPlayerId;
        private BuildingPiece parentBuilding;

        public string InteractionPrompt
        {
            get
            {
                if (currentCrop == null || currentCrop.cropData == null)
                    return "Plant Seed";
                if (currentCrop.isFullyGrown)
                    return $"Harvest {currentCrop.cropData.cropName}";
                if (!currentCrop.isWatered && currentCrop.cropData.requiresWater)
                    return $"Water {currentCrop.cropData.cropName}";
                return $"{currentCrop.cropData.cropName} ({Mathf.FloorToInt(currentCrop.growthProgress * 100)}%)";
            }
        }

        public bool HasCrop => currentCrop != null && currentCrop.cropData != null;
        public bool IsReadyToHarvest => currentCrop?.isFullyGrown ?? false;
        public float GrowthProgress => currentCrop?.growthProgress ?? 0f;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }

            parentBuilding = GetComponentInParent<BuildingPiece>();
        }

        private void Update()
        {
            if (currentCrop != null && currentCrop.cropData != null)
            {
                // Get current temperature (would come from world manager)
                float temperature = 20f; // Default moderate temp

                // Update crop growth
                currentCrop.UpdateGrowth(Time.deltaTime, isIndoors, temperature);

                // Update visuals if stage changed
                int currentStage = currentCrop.GetCurrentStage();
                if (currentStage != lastVisualStage)
                {
                    UpdateCropVisual(currentStage);
                    lastVisualStage = currentStage;
                }

                // Update soil visual
                UpdateSoilVisual();
            }
        }

        public void Interact(PlayerController player)
        {
            if (currentCrop == null || currentCrop.cropData == null)
            {
                // Try to plant - check if player has seeds
                TryPlantFromInventory(player);
            }
            else if (currentCrop.isFullyGrown)
            {
                // Harvest
                Harvest(player);
            }
            else if (!currentCrop.isWatered && currentCrop.cropData.requiresWater)
            {
                // Water the crop
                Water(player);
            }
        }

        private void TryPlantFromInventory(PlayerController player)
        {
            if (player?.Inventory == null) return;

            // Look for any seed items in player's inventory
            // For now, this would need to open a seed selection UI
            // or automatically plant the first available seed

            Debug.Log("[FarmPlot] Would open seed selection UI");
        }

        /// <summary>
        /// Plant a specific crop in this plot.
        /// </summary>
        public bool Plant(CropData crop, PlayerController player)
        {
            if (crop == null) return false;
            if (currentCrop != null && currentCrop.cropData != null) return false;

            // Check if player has seeds
            if (player?.Inventory != null && crop.seedItem != null)
            {
                if (!player.Inventory.HasItem(crop.seedItem, crop.seedCost))
                {
                    Debug.Log("[FarmPlot] Not enough seeds");
                    return false;
                }

                // Consume seeds
                player.Inventory.RemoveItem(crop.seedItem, crop.seedCost);
            }

            // Check if crop can grow here
            if (crop.indoorOnly && !isIndoors)
            {
                Debug.Log("[FarmPlot] This crop requires a greenhouse");
                return false;
            }

            // Plant the crop
            currentCrop = new CropInstance(crop);
            lastVisualStage = -1;

            // Play sound
            if (plantSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(plantSound);
            }

            UpdateCropVisual(0);
            UpdateSoilVisual();

            Debug.Log($"[FarmPlot] Planted {crop.cropName}");
            return true;
        }

        /// <summary>
        /// Water the current crop.
        /// </summary>
        public void Water(PlayerController player)
        {
            if (currentCrop == null) return;

            currentCrop.Water();

            // Play sound
            if (waterSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(waterSound);
            }

            UpdateSoilVisual();

            Debug.Log("[FarmPlot] Crop watered");
        }

        /// <summary>
        /// Harvest the fully grown crop.
        /// </summary>
        public void Harvest(PlayerController player)
        {
            if (currentCrop == null || !currentCrop.isFullyGrown) return;

            CropData cropData = currentCrop.cropData;

            // Calculate yield
            int yield = cropData.CalculateYield(qualityBonus);

            // Give harvest to player
            if (player?.Inventory != null && cropData.harvestItem != null)
            {
                bool added = player.Inventory.AddItem(cropData.harvestItem, yield);
                if (added)
                {
                    Debug.Log($"[FarmPlot] Harvested {yield}x {cropData.harvestItem.itemName}");
                }
                else
                {
                    Debug.Log("[FarmPlot] Inventory full!");
                    return; // Don't clear crop if couldn't add to inventory
                }

                // Chance to get seed back
                if (cropData.RollForBonusSeed() && cropData.seedItem != null)
                {
                    player.Inventory.AddItem(cropData.seedItem, 1);
                    Debug.Log("[FarmPlot] Got bonus seed!");
                }
            }

            // Play sound
            if (harvestSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(harvestSound);
            }

            // Clear the plot
            ClearPlot();
        }

        /// <summary>
        /// Remove crop from plot (death, manual removal, etc.)
        /// </summary>
        public void ClearPlot()
        {
            currentCrop = null;
            lastVisualStage = -1;

            if (currentCropVisual != null)
            {
                Destroy(currentCropVisual);
                currentCropVisual = null;
            }

            UpdateSoilVisual();
        }

        private void UpdateCropVisual(int stage)
        {
            // Remove old visual
            if (currentCropVisual != null)
            {
                Destroy(currentCropVisual);
            }

            if (currentCrop?.cropData?.stagePrefabs == null) return;
            if (stage < 0 || stage >= currentCrop.cropData.stagePrefabs.Length) return;

            GameObject prefab = currentCrop.cropData.stagePrefabs[stage];
            if (prefab == null) return;

            Vector3 spawnPos = cropSpawnPoint != null ? cropSpawnPoint.position : transform.position;
            currentCropVisual = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
        }

        private void UpdateSoilVisual()
        {
            bool hasCrop = currentCrop != null && currentCrop.cropData != null;
            bool isWatered = currentCrop?.isWatered ?? false;

            if (emptyPlotVisual != null)
                emptyPlotVisual.SetActive(!hasCrop);

            if (wateredSoilVisual != null)
                wateredSoilVisual.SetActive(hasCrop && isWatered);

            if (drySoilVisual != null)
                drySoilVisual.SetActive(hasCrop && !isWatered);
        }

        /// <summary>
        /// Set whether this plot is indoors (in a greenhouse).
        /// </summary>
        public void SetIndoors(bool indoor, float bonus = 0f)
        {
            isIndoors = indoor;
            qualityBonus = bonus;
        }
    }
}
