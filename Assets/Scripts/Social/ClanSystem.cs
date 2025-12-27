using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Pangaea.Player;

namespace Pangaea.Social
{
    /// <summary>
    /// Clan and alliance system.
    /// Clan cap: 20 players. Alliances allow multiple clans to cooperate.
    /// </summary>
    public class ClanSystem : MonoBehaviour
    {
        public static ClanSystem Instance { get; private set; }

        [Header("Clan Settings")]
        [SerializeField] private int maxClanSize = 20;
        [SerializeField] private int maxAlliances = 3;
        [SerializeField] private int clanCreationCost = 500; // Gold

        // Data
        private Dictionary<uint, Clan> clans = new Dictionary<uint, Clan>();
        private Dictionary<uint, Alliance> alliances = new Dictionary<uint, Alliance>();
        private Dictionary<uint, uint> playerToClan = new Dictionary<uint, uint>(); // playerId -> clanId

        private uint nextClanId = 1;
        private uint nextAllianceId = 1;

        // Events
        public System.Action<Clan> OnClanCreated;
        public System.Action<uint, uint> OnPlayerJoinedClan; // playerId, clanId
        public System.Action<uint, uint> OnPlayerLeftClan;
        public System.Action<uint, uint> OnAllianceFormed; // clan1Id, clan2Id

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #region Clan Management

        public Clan CreateClan(uint founderId, string clanName, string clanTag)
        {
            // Check if player already in clan
            if (playerToClan.ContainsKey(founderId))
            {
                Debug.Log("[Clan] Player already in a clan");
                return null;
            }

            // Validate name/tag
            if (string.IsNullOrEmpty(clanName) || clanName.Length > 24)
            {
                Debug.Log("[Clan] Invalid clan name");
                return null;
            }

            if (string.IsNullOrEmpty(clanTag) || clanTag.Length > 4)
            {
                Debug.Log("[Clan] Invalid clan tag");
                return null;
            }

            // Check for duplicate names
            if (clans.Values.Any(c => c.Name.Equals(clanName, System.StringComparison.OrdinalIgnoreCase)))
            {
                Debug.Log("[Clan] Clan name already exists");
                return null;
            }

            // Create clan
            Clan clan = new Clan
            {
                ClanId = nextClanId++,
                Name = clanName,
                Tag = clanTag.ToUpper(),
                LeaderId = founderId,
                CreatedTime = Time.time,
                Members = new List<ClanMember>
                {
                    new ClanMember
                    {
                        PlayerId = founderId,
                        Rank = ClanRank.Leader,
                        JoinedTime = Time.time
                    }
                },
                Alliances = new List<uint>()
            };

            clans[clan.ClanId] = clan;
            playerToClan[founderId] = clan.ClanId;

            OnClanCreated?.Invoke(clan);
            Debug.Log($"[Clan] Created: [{clan.Tag}] {clan.Name} by {founderId}");

            return clan;
        }

        public bool JoinClan(uint playerId, uint clanId)
        {
            if (playerToClan.ContainsKey(playerId))
            {
                Debug.Log("[Clan] Player already in a clan");
                return false;
            }

            if (!clans.TryGetValue(clanId, out Clan clan))
            {
                Debug.Log("[Clan] Clan not found");
                return false;
            }

            if (clan.Members.Count >= maxClanSize)
            {
                Debug.Log("[Clan] Clan is full");
                return false;
            }

            clan.Members.Add(new ClanMember
            {
                PlayerId = playerId,
                Rank = ClanRank.Member,
                JoinedTime = Time.time
            });

            playerToClan[playerId] = clanId;
            OnPlayerJoinedClan?.Invoke(playerId, clanId);

            Debug.Log($"[Clan] {playerId} joined [{clan.Tag}] {clan.Name}");
            return true;
        }

        public bool LeaveClan(uint playerId)
        {
            if (!playerToClan.TryGetValue(playerId, out uint clanId))
            {
                return false;
            }

            if (!clans.TryGetValue(clanId, out Clan clan))
            {
                return false;
            }

            ClanMember member = clan.Members.Find(m => m.PlayerId == playerId);
            if (member == null) return false;

            // Leader cannot leave unless transferring or disbanding
            if (member.Rank == ClanRank.Leader)
            {
                if (clan.Members.Count > 1)
                {
                    Debug.Log("[Clan] Leader must transfer leadership or disband");
                    return false;
                }
                else
                {
                    // Last member - disband clan
                    DisbandClan(clanId);
                    return true;
                }
            }

            clan.Members.Remove(member);
            playerToClan.Remove(playerId);

            OnPlayerLeftClan?.Invoke(playerId, clanId);
            Debug.Log($"[Clan] {playerId} left [{clan.Tag}] {clan.Name}");

            return true;
        }

        public bool KickMember(uint kickerId, uint targetId)
        {
            if (!playerToClan.TryGetValue(kickerId, out uint clanId))
                return false;

            if (!clans.TryGetValue(clanId, out Clan clan))
                return false;

            ClanMember kicker = clan.Members.Find(m => m.PlayerId == kickerId);
            ClanMember target = clan.Members.Find(m => m.PlayerId == targetId);

            if (kicker == null || target == null) return false;

            // Can only kick lower ranks
            if (kicker.Rank <= target.Rank) return false;

            clan.Members.Remove(target);
            playerToClan.Remove(targetId);

            OnPlayerLeftClan?.Invoke(targetId, clanId);
            return true;
        }

        public bool PromoteMember(uint promoterId, uint targetId)
        {
            if (!playerToClan.TryGetValue(promoterId, out uint clanId))
                return false;

            if (!clans.TryGetValue(clanId, out Clan clan))
                return false;

            ClanMember promoter = clan.Members.Find(m => m.PlayerId == promoterId);
            ClanMember target = clan.Members.Find(m => m.PlayerId == targetId);

            if (promoter == null || target == null) return false;
            if (promoter.Rank != ClanRank.Leader) return false;
            if (target.Rank >= ClanRank.Officer) return false;

            target.Rank = ClanRank.Officer;
            return true;
        }

        public bool TransferLeadership(uint currentLeaderId, uint newLeaderId)
        {
            if (!playerToClan.TryGetValue(currentLeaderId, out uint clanId))
                return false;

            if (!clans.TryGetValue(clanId, out Clan clan))
                return false;

            ClanMember currentLeader = clan.Members.Find(m => m.PlayerId == currentLeaderId);
            ClanMember newLeader = clan.Members.Find(m => m.PlayerId == newLeaderId);

            if (currentLeader?.Rank != ClanRank.Leader) return false;
            if (newLeader == null) return false;

            currentLeader.Rank = ClanRank.Officer;
            newLeader.Rank = ClanRank.Leader;
            clan.LeaderId = newLeaderId;

            return true;
        }

        public void DisbandClan(uint clanId)
        {
            if (!clans.TryGetValue(clanId, out Clan clan))
                return;

            // Remove all members
            foreach (var member in clan.Members)
            {
                playerToClan.Remove(member.PlayerId);
                OnPlayerLeftClan?.Invoke(member.PlayerId, clanId);
            }

            // Remove from alliances
            foreach (uint allianceId in clan.Alliances)
            {
                if (alliances.TryGetValue(allianceId, out Alliance alliance))
                {
                    alliance.MemberClans.Remove(clanId);
                }
            }

            clans.Remove(clanId);
            Debug.Log($"[Clan] Disbanded: [{clan.Tag}] {clan.Name}");
        }

        #endregion

        #region Alliances

        public bool FormAlliance(uint clanId1, uint clanId2)
        {
            if (!clans.TryGetValue(clanId1, out Clan clan1)) return false;
            if (!clans.TryGetValue(clanId2, out Clan clan2)) return false;

            if (clan1.Alliances.Count >= maxAlliances || clan2.Alliances.Count >= maxAlliances)
            {
                Debug.Log("[Clan] Max alliances reached");
                return false;
            }

            // Check if already allied
            if (AreAllied(clanId1, clanId2))
            {
                Debug.Log("[Clan] Already allied");
                return false;
            }

            Alliance alliance = new Alliance
            {
                AllianceId = nextAllianceId++,
                MemberClans = new List<uint> { clanId1, clanId2 },
                FormedTime = Time.time
            };

            alliances[alliance.AllianceId] = alliance;
            clan1.Alliances.Add(alliance.AllianceId);
            clan2.Alliances.Add(alliance.AllianceId);

            OnAllianceFormed?.Invoke(clanId1, clanId2);
            Debug.Log($"[Clan] Alliance formed: [{clan1.Tag}] + [{clan2.Tag}]");

            return true;
        }

        public bool BreakAlliance(uint clanId1, uint clanId2)
        {
            uint? allianceId = FindAllianceBetween(clanId1, clanId2);
            if (!allianceId.HasValue) return false;

            if (!alliances.TryGetValue(allianceId.Value, out Alliance alliance))
                return false;

            // Remove from clans
            if (clans.TryGetValue(clanId1, out Clan clan1))
                clan1.Alliances.Remove(allianceId.Value);

            if (clans.TryGetValue(clanId2, out Clan clan2))
                clan2.Alliances.Remove(allianceId.Value);

            alliances.Remove(allianceId.Value);
            return true;
        }

        public bool AreAllied(uint clanId1, uint clanId2)
        {
            return FindAllianceBetween(clanId1, clanId2).HasValue;
        }

        private uint? FindAllianceBetween(uint clanId1, uint clanId2)
        {
            foreach (var alliance in alliances.Values)
            {
                if (alliance.MemberClans.Contains(clanId1) && alliance.MemberClans.Contains(clanId2))
                {
                    return alliance.AllianceId;
                }
            }
            return null;
        }

        #endregion

        #region Queries

        public Clan GetClan(uint clanId)
        {
            clans.TryGetValue(clanId, out Clan clan);
            return clan;
        }

        public Clan GetPlayerClan(uint playerId)
        {
            if (playerToClan.TryGetValue(playerId, out uint clanId))
            {
                return GetClan(clanId);
            }
            return null;
        }

        public bool AreInSameClan(uint playerId1, uint playerId2)
        {
            if (!playerToClan.TryGetValue(playerId1, out uint clan1)) return false;
            if (!playerToClan.TryGetValue(playerId2, out uint clan2)) return false;
            return clan1 == clan2;
        }

        public bool AreFriendly(uint playerId1, uint playerId2)
        {
            // Same clan?
            if (AreInSameClan(playerId1, playerId2)) return true;

            // Allied clans?
            Clan clan1 = GetPlayerClan(playerId1);
            Clan clan2 = GetPlayerClan(playerId2);

            if (clan1 != null && clan2 != null)
            {
                return AreAllied(clan1.ClanId, clan2.ClanId);
            }

            return false;
        }

        public List<uint> GetOnlineClanMembers(uint clanId)
        {
            List<uint> online = new List<uint>();

            if (!clans.TryGetValue(clanId, out Clan clan)) return online;

            foreach (var member in clan.Members)
            {
                if (Core.GameManager.Instance?.PlayerManager?.GetPlayer(member.PlayerId) != null)
                {
                    online.Add(member.PlayerId);
                }
            }

            return online;
        }

        #endregion
    }

    [System.Serializable]
    public class Clan
    {
        public uint ClanId;
        public string Name;
        public string Tag;
        public uint LeaderId;
        public float CreatedTime;
        public List<ClanMember> Members;
        public List<uint> Alliances;

        // Customization
        public Color BannerColor1 = Color.blue;
        public Color BannerColor2 = Color.white;
        public string Description = "";
    }

    [System.Serializable]
    public class ClanMember
    {
        public uint PlayerId;
        public ClanRank Rank;
        public float JoinedTime;
        public int ContributionPoints;
    }

    public enum ClanRank
    {
        Member = 0,
        Officer = 1,
        Leader = 2
    }

    [System.Serializable]
    public class Alliance
    {
        public uint AllianceId;
        public List<uint> MemberClans;
        public float FormedTime;
    }
}
