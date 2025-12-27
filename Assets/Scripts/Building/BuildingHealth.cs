using UnityEngine;
using Pangaea.Player;
using Pangaea.Combat;

namespace Pangaea.Building
{
    /// <summary>
    /// Building health and damage system.
    /// Includes offline raid protection.
    /// </summary>
    public class BuildingHealth : MonoBehaviour, IDamageable
    {
        [Header("State")]
        [SerializeField] private float currentHealth;
        [SerializeField] private float maxHealth;
        [SerializeField] private bool isProtected;

        [Header("Protection")]
        [SerializeField] private float protectionDamageReduction = 0.9f; // 90% reduction when protected
        [SerializeField] private float decayRate = 0f; // Optional decay over time

        private PlacedBuilding buildingData;
        private uint ownerId;
        private uint clanId;

        // Damage state
        private enum DamageState { Pristine, Damaged, Critical }
        private DamageState damageState = DamageState.Pristine;

        public bool IsAlive => currentHealth > 0;
        public float HealthPercentage => currentHealth / maxHealth;

        public void Initialize(PlacedBuilding data)
        {
            buildingData = data;
            maxHealth = data.Piece.maxHealth;
            currentHealth = data.Health;
            ownerId = data.OwnerId;
            clanId = data.ClanId;

            UpdateDamageState();
        }

        private void Update()
        {
            // Check offline raid protection
            UpdateProtectionStatus();

            // Optional decay
            if (decayRate > 0)
            {
                currentHealth -= decayRate * Time.deltaTime;
                if (currentHealth <= 0)
                {
                    DestroyBuilding();
                }
            }
        }

        private void UpdateProtectionStatus()
        {
            // Check if any clan members are online
            // This would be server-side in production
            bool ownerOnline = IsPlayerOnline(ownerId);
            bool clanOnline = clanId > 0 && IsClanOnline(clanId);

            isProtected = !ownerOnline && !clanOnline;
            buildingData.IsProtected = isProtected;
        }

        private bool IsPlayerOnline(uint playerId)
        {
            // Check player manager
            var player = Core.GameManager.Instance?.PlayerManager?.GetPlayer(playerId);
            return player != null;
        }

        private bool IsClanOnline(uint clanId)
        {
            // Would check if any clan members are online
            // For MVP, just check owner
            return false;
        }

        public void TakeDamage(float damage, PlayerController attacker)
        {
            if (currentHealth <= 0) return;

            // Apply protection reduction
            if (isProtected)
            {
                damage *= (1f - protectionDamageReduction);
                Debug.Log($"[Building] Protected! Damage reduced to {damage:F1}");
            }

            // Check if attacker can damage (territory rules, etc.)
            if (!CanBeDamagedBy(attacker))
            {
                Debug.Log("[Building] Cannot damage this building");
                return;
            }

            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);

            // Update visual state
            UpdateDamageState();

            // Sync to network
            buildingData.Health = currentHealth;

            if (currentHealth <= 0)
            {
                DestroyBuilding();
            }
        }

        private bool CanBeDamagedBy(PlayerController attacker)
        {
            // Own buildings - always damageable (for demolition)
            if (attacker.PlayerId == ownerId) return true;

            // Clan buildings - clan members can damage
            // if (attacker.ClanId == clanId) return true;

            // PvP mode check
            if (!attacker.CanAttack()) return false;

            // Territory rules would go here
            return true;
        }

        private void UpdateDamageState()
        {
            DamageState newState;

            if (HealthPercentage > 0.6f)
                newState = DamageState.Pristine;
            else if (HealthPercentage > 0.25f)
                newState = DamageState.Damaged;
            else
                newState = DamageState.Critical;

            if (newState != damageState)
            {
                damageState = newState;
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            // Swap to damaged model if available
            if (damageState == DamageState.Damaged && buildingData.Piece.damagedPrefab != null)
            {
                // Would swap mesh/material here
            }
        }

        private void DestroyBuilding()
        {
            Debug.Log($"[Building] {buildingData.Piece.pieceName} destroyed!");

            // Spawn destruction effect
            if (buildingData.Piece.destroyedEffect != null)
            {
                Instantiate(buildingData.Piece.destroyedEffect, transform.position, Quaternion.identity);
            }

            // Drop some resources (salvage)
            DropSalvage();

            // Remove from building system
            BuildingSystem.Instance?.DestroyBuilding(buildingData.BuildingId);
        }

        private void DropSalvage()
        {
            // Drop 25-50% of resources used to build
            float salvagePercent = Random.Range(0.25f, 0.5f);

            foreach (var cost in buildingData.Piece.resourceCosts)
            {
                int salvageAmount = Mathf.FloorToInt(cost.quantity * salvagePercent);
                if (salvageAmount > 0)
                {
                    // Spawn dropped item at position
                    Debug.Log($"[Building] Dropped {salvageAmount}x {cost.item.itemName}");
                }
            }
        }

        public void Repair(PlayerController repairer)
        {
            if (currentHealth >= maxHealth) return;

            // Check if has repair materials
            float repairPercent = 0.25f; // Repair 25% at a time
            float repairAmount = maxHealth * repairPercent;

            // Calculate cost (50% of proportional build cost)
            foreach (var cost in buildingData.Piece.resourceCosts)
            {
                int repairCost = Mathf.CeilToInt(cost.quantity * repairPercent * buildingData.Piece.repairCostMultiplier);
                if (!repairer.Inventory.HasItem(cost.item, repairCost))
                {
                    Debug.Log($"[Building] Need {repairCost}x {cost.item.itemName} to repair");
                    return;
                }
            }

            // Consume resources and repair
            foreach (var cost in buildingData.Piece.resourceCosts)
            {
                int repairCost = Mathf.CeilToInt(cost.quantity * repairPercent * buildingData.Piece.repairCostMultiplier);
                repairer.Inventory.RemoveItem(cost.item, repairCost);
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + repairAmount);
            UpdateDamageState();

            Debug.Log($"[Building] Repaired to {HealthPercentage:P0}");
        }
    }
}
