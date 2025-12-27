using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Pangaea.Player;
using Pangaea.Core;

namespace Pangaea.Social
{
    /// <summary>
    /// Bounty system - player-driven justice.
    /// Players place gold bounties on killers.
    /// </summary>
    public class BountySystem : MonoBehaviour
    {
        public static BountySystem Instance { get; private set; }

        [Header("Bounty Settings")]
        [SerializeField] private int minimumBounty = 10;
        [SerializeField] private int maximumBounty = 10000;
        [SerializeField] private float bountyMapRevealRadius = 100f; // Approximate location
        [SerializeField] private float bountyUpdateInterval = 60f; // Position update frequency

        [Header("Auto-Bounty")]
        [SerializeField] private bool autoBountyEnabled = true;
        [SerializeField] private int autoBountyPerKill = 25;
        [SerializeField] private float autoBountyKarmaThreshold = -100f;

        // Active bounties
        private Dictionary<uint, Bounty> activeBounties = new Dictionary<uint, Bounty>();

        // Events
        public System.Action<Bounty> OnBountyPlaced;
        public System.Action<Bounty> OnBountyUpdated;
        public System.Action<uint, uint> OnBountyClaimed; // targetId, claimerId

        private float lastUpdateTime;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Periodic position updates for bounty targets
            if (Time.time - lastUpdateTime > bountyUpdateInterval)
            {
                UpdateBountyPositions();
                lastUpdateTime = Time.time;
            }
        }

        public bool PlaceBounty(uint targetId, uint placerId, int amount, string reason = "")
        {
            if (amount < minimumBounty || amount > maximumBounty)
            {
                Debug.Log($"[Bounty] Invalid amount: {amount}");
                return false;
            }

            // Check if placer has funds
            PlayerController placer = GameManager.Instance?.PlayerManager?.GetPlayer(placerId);
            if (placer == null) return false;

            // Would check player's gold here
            // if (!placer.Inventory.HasCurrency(amount)) return false;

            // Add to existing bounty or create new
            if (activeBounties.TryGetValue(targetId, out Bounty existingBounty))
            {
                existingBounty.TotalAmount += amount;
                existingBounty.Contributors.Add(new BountyContribution
                {
                    ContributorId = placerId,
                    Amount = amount,
                    Reason = reason,
                    Timestamp = Time.time
                });

                OnBountyUpdated?.Invoke(existingBounty);
                Debug.Log($"[Bounty] Increased bounty on {targetId} to {existingBounty.TotalAmount}g");
            }
            else
            {
                Bounty newBounty = new Bounty
                {
                    TargetId = targetId,
                    TotalAmount = amount,
                    CreatedTime = Time.time,
                    Contributors = new List<BountyContribution>
                    {
                        new BountyContribution
                        {
                            ContributorId = placerId,
                            Amount = amount,
                            Reason = reason,
                            Timestamp = Time.time
                        }
                    }
                };

                activeBounties[targetId] = newBounty;
                OnBountyPlaced?.Invoke(newBounty);
                Debug.Log($"[Bounty] New bounty placed on {targetId}: {amount}g");
            }

            // Consume gold from placer
            // placer.Inventory.RemoveCurrency(amount);

            return true;
        }

        public void OnPlayerKilled(uint killerId, uint victimId)
        {
            // Check if killer had a bounty
            if (activeBounties.TryGetValue(killerId, out Bounty bounty))
            {
                ClaimBounty(killerId, victimId);
            }

            // Auto-bounty for negative karma players
            if (autoBountyEnabled)
            {
                PlayerController killer = GameManager.Instance?.PlayerManager?.GetPlayer(killerId);
                if (killer != null && killer.Stats.Karma <= autoBountyKarmaThreshold)
                {
                    // Add automatic bounty
                    killer.Stats.AddBounty(autoBountyPerKill);

                    if (!activeBounties.ContainsKey(killerId))
                    {
                        activeBounties[killerId] = new Bounty
                        {
                            TargetId = killerId,
                            TotalAmount = autoBountyPerKill,
                            CreatedTime = Time.time,
                            IsAutoBounty = true,
                            Contributors = new List<BountyContribution>()
                        };
                    }
                    else
                    {
                        activeBounties[killerId].TotalAmount += autoBountyPerKill;
                    }
                }
            }
        }

        private void ClaimBounty(uint targetId, uint claimerId)
        {
            if (!activeBounties.TryGetValue(targetId, out Bounty bounty)) return;

            PlayerController claimer = GameManager.Instance?.PlayerManager?.GetPlayer(claimerId);
            if (claimer == null) return;

            // Award bounty gold
            // claimer.Inventory.AddCurrency(bounty.TotalAmount);

            // Karma boost for claiming bounty
            claimer.Stats.ModifyKarma(25);

            // Clear target's bounty
            PlayerController target = GameManager.Instance?.PlayerManager?.GetPlayer(targetId);
            if (target != null)
            {
                target.Stats.ClearBounty();
            }

            Debug.Log($"[Bounty] {claimerId} claimed {bounty.TotalAmount}g bounty on {targetId}");

            OnBountyClaimed?.Invoke(targetId, claimerId);
            activeBounties.Remove(targetId);
        }

        private void UpdateBountyPositions()
        {
            foreach (var bounty in activeBounties.Values)
            {
                PlayerController target = GameManager.Instance?.PlayerManager?.GetPlayer(bounty.TargetId);
                if (target != null)
                {
                    // Update approximate location (with some randomization for fairness)
                    Vector3 actualPos = target.transform.position;
                    Vector2 randomOffset = Random.insideUnitCircle * bountyMapRevealRadius * 0.5f;
                    bounty.LastKnownPosition = actualPos + new Vector3(randomOffset.x, 0, randomOffset.y);
                    bounty.LastPositionUpdate = Time.time;
                }
            }
        }

        public Bounty GetBounty(uint targetId)
        {
            activeBounties.TryGetValue(targetId, out Bounty bounty);
            return bounty;
        }

        public List<Bounty> GetAllBounties()
        {
            return activeBounties.Values.ToList();
        }

        public List<Bounty> GetTopBounties(int count)
        {
            return activeBounties.Values
                .OrderByDescending(b => b.TotalAmount)
                .Take(count)
                .ToList();
        }

        public bool HasBounty(uint playerId)
        {
            return activeBounties.ContainsKey(playerId);
        }

        public int GetTotalBounty(uint playerId)
        {
            if (activeBounties.TryGetValue(playerId, out Bounty bounty))
            {
                return bounty.TotalAmount;
            }
            return 0;
        }
    }

    [System.Serializable]
    public class Bounty
    {
        public uint TargetId;
        public int TotalAmount;
        public float CreatedTime;
        public bool IsAutoBounty;
        public List<BountyContribution> Contributors;

        // Location tracking
        public Vector3 LastKnownPosition;
        public float LastPositionUpdate;
    }

    [System.Serializable]
    public class BountyContribution
    {
        public uint ContributorId;
        public int Amount;
        public string Reason;
        public float Timestamp;
    }
}
