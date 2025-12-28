using UnityEngine;
using System.Collections.Generic;
using Pangaea.Player;
using Pangaea.Survival;

namespace Pangaea.Building
{
    /// <summary>
    /// Greenhouse building for sustainable food production.
    /// Protects crops from weather and provides growth bonuses.
    /// Can be upgraded for better yields.
    /// </summary>
    public class Greenhouse : MonoBehaviour, IInteractable
    {
        [Header("Greenhouse Settings")]
        [SerializeField] private string greenhouseName = "Greenhouse";
        [SerializeField] private GreenhouseTier tier = GreenhouseTier.Basic;
        [SerializeField] private int maxPlots = 4;

        [Header("Growth Bonuses")]
        [SerializeField] private float growthSpeedBonus = 0.25f; // 25% faster growth
        [SerializeField] private float yieldBonus = 0.1f; // 10% more harvest
        [SerializeField] private float waterRetention = 1.5f; // Water lasts 50% longer

        [Header("Climate Control")]
        [SerializeField] private float interiorTemperature = 22f;
        [SerializeField] private bool hasHeating = false;
        [SerializeField] private bool hasCooling = false;

        [Header("Farm Plots")]
        [SerializeField] private List<FarmPlot> farmPlots = new List<FarmPlot>();
        [SerializeField] private Transform[] plotSpawnPoints;

        [Header("Upgrade Costs")]
        [SerializeField] private int upgradeWoodCost = 50;
        [SerializeField] private int upgradeMetalCost = 25;

        [Header("Visuals")]
        [SerializeField] private GameObject[] tierVisuals; // Different looks per tier

        // State
        private BuildingPiece buildingPiece;
        private uint ownerPlayerId;

        public string InteractionPrompt => $"{greenhouseName} (Tier {(int)tier + 1})";
        public GreenhouseTier Tier => tier;
        public int PlotCount => farmPlots.Count;
        public int MaxPlots => maxPlots;
        public float GrowthBonus => growthSpeedBonus;
        public float YieldBonus => yieldBonus;

        private void Awake()
        {
            buildingPiece = GetComponent<BuildingPiece>();

            // Initialize plots
            foreach (var plot in farmPlots)
            {
                if (plot != null)
                {
                    plot.SetIndoors(true, yieldBonus);
                }
            }

            UpdateTierVisuals();
            ApplyTierBonuses();
        }

        private void Start()
        {
            // If no plots assigned, try to find children
            if (farmPlots.Count == 0)
            {
                farmPlots.AddRange(GetComponentsInChildren<FarmPlot>());
                foreach (var plot in farmPlots)
                {
                    plot.SetIndoors(true, yieldBonus);
                }
            }
        }

        public void Interact(PlayerController player)
        {
            // Open greenhouse management UI
            Debug.Log($"[Greenhouse] Opening management for {greenhouseName}");

            // Would show:
            // - Current plots and their status
            // - Option to plant seeds
            // - Option to water all
            // - Upgrade option if materials available
        }

        /// <summary>
        /// Add a new farm plot to this greenhouse.
        /// </summary>
        public FarmPlot AddPlot(GameObject plotPrefab)
        {
            if (farmPlots.Count >= maxPlots)
            {
                Debug.Log("[Greenhouse] Maximum plots reached");
                return null;
            }

            // Find spawn point
            Transform spawnPoint = null;
            if (plotSpawnPoints != null && farmPlots.Count < plotSpawnPoints.Length)
            {
                spawnPoint = plotSpawnPoints[farmPlots.Count];
            }

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            GameObject plotObj = Instantiate(plotPrefab, spawnPos, spawnRot, transform);
            FarmPlot plot = plotObj.GetComponent<FarmPlot>();

            if (plot != null)
            {
                plot.SetIndoors(true, yieldBonus);
                farmPlots.Add(plot);
                Debug.Log($"[Greenhouse] Added plot, now has {farmPlots.Count}/{maxPlots}");
            }

            return plot;
        }

        /// <summary>
        /// Water all plots in the greenhouse.
        /// </summary>
        public void WaterAll(PlayerController player)
        {
            int watered = 0;
            foreach (var plot in farmPlots)
            {
                if (plot != null && plot.HasCrop)
                {
                    plot.Water(player);
                    watered++;
                }
            }

            Debug.Log($"[Greenhouse] Watered {watered} plots");
        }

        /// <summary>
        /// Harvest all ready crops.
        /// </summary>
        public void HarvestAll(PlayerController player)
        {
            int harvested = 0;
            foreach (var plot in farmPlots)
            {
                if (plot != null && plot.IsReadyToHarvest)
                {
                    plot.Harvest(player);
                    harvested++;
                }
            }

            Debug.Log($"[Greenhouse] Harvested {harvested} crops");
        }

        /// <summary>
        /// Upgrade the greenhouse to the next tier.
        /// </summary>
        public bool Upgrade(PlayerController player)
        {
            if (tier >= GreenhouseTier.Advanced)
            {
                Debug.Log("[Greenhouse] Already at max tier");
                return false;
            }

            // Check if player has materials (would check inventory)
            // For now, just upgrade
            tier++;
            ApplyTierBonuses();
            UpdateTierVisuals();

            Debug.Log($"[Greenhouse] Upgraded to tier {(int)tier + 1}");
            return true;
        }

        private void ApplyTierBonuses()
        {
            switch (tier)
            {
                case GreenhouseTier.Basic:
                    maxPlots = 4;
                    growthSpeedBonus = 0.25f;
                    yieldBonus = 0.1f;
                    waterRetention = 1.5f;
                    hasHeating = false;
                    hasCooling = false;
                    break;

                case GreenhouseTier.Improved:
                    maxPlots = 6;
                    growthSpeedBonus = 0.5f;
                    yieldBonus = 0.2f;
                    waterRetention = 2f;
                    hasHeating = true;
                    hasCooling = false;
                    break;

                case GreenhouseTier.Advanced:
                    maxPlots = 9;
                    growthSpeedBonus = 0.75f;
                    yieldBonus = 0.35f;
                    waterRetention = 3f;
                    hasHeating = true;
                    hasCooling = true;
                    break;
            }

            // Update all plots with new bonus
            foreach (var plot in farmPlots)
            {
                if (plot != null)
                {
                    plot.SetIndoors(true, yieldBonus);
                }
            }
        }

        private void UpdateTierVisuals()
        {
            if (tierVisuals == null) return;

            for (int i = 0; i < tierVisuals.Length; i++)
            {
                if (tierVisuals[i] != null)
                {
                    tierVisuals[i].SetActive(i == (int)tier);
                }
            }
        }

        /// <summary>
        /// Get status of all plots.
        /// </summary>
        public GreenhouseStatus GetStatus()
        {
            GreenhouseStatus status = new GreenhouseStatus();
            status.tier = tier;
            status.plotsUsed = 0;
            status.plotsReady = 0;
            status.plotsNeedWater = 0;

            foreach (var plot in farmPlots)
            {
                if (plot == null) continue;

                if (plot.HasCrop)
                {
                    status.plotsUsed++;

                    if (plot.IsReadyToHarvest)
                        status.plotsReady++;
                }
            }

            status.plotsAvailable = maxPlots - status.plotsUsed;
            return status;
        }

        private void OnDrawGizmosSelected()
        {
            // Show plot positions
            Gizmos.color = Color.green;
            if (plotSpawnPoints != null)
            {
                foreach (var point in plotSpawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireCube(point.position, Vector3.one * 0.5f);
                    }
                }
            }
        }
    }

    public enum GreenhouseTier
    {
        Basic,      // Wood frame, basic glass
        Improved,   // Metal frame, heating
        Advanced    // Full climate control, max efficiency
    }

    [System.Serializable]
    public struct GreenhouseStatus
    {
        public GreenhouseTier tier;
        public int plotsUsed;
        public int plotsAvailable;
        public int plotsReady;
        public int plotsNeedWater;
    }
}
