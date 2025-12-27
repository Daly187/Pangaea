using UnityEngine;

namespace Pangaea.Core
{
    /// <summary>
    /// Global game configuration - balancing and settings.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Pangaea/Config/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Player Settings")]
        public float baseHealth = 100f;
        public float baseStamina = 100f;
        public float baseHunger = 100f;
        public float baseMoveSpeed = 5f;
        public float baseRunSpeed = 10f;

        [Header("Combat")]
        public float baseMeleeDamage = 10f;
        public float baseRangedDamage = 8f;
        public float baseCritChance = 0.05f;
        public float baseCritMultiplier = 1.5f;
        public float combatLogoutTimer = 10f;

        [Header("Survival")]
        public float hungerDecayRate = 0.5f; // Per minute
        public float starvationDamageRate = 1f; // Per second when starving
        public float healthRegenRate = 1f; // Per second when out of combat

        [Header("Progression")]
        public int maxLevel = 10;
        public int attributePointsPerLevel = 3;
        public int[] experiencePerLevel = { 0, 100, 250, 500, 1000, 2000, 4000, 7000, 11000, 16000 };

        [Header("PvP")]
        public float pvpModeCooldown = 30f;
        public int karmaKillPenalty = 50;
        public int karmaBanditThreshold = -100;
        public int karmaGuardianThreshold = 500;

        [Header("Economy")]
        public float baseResourceValue = 1f;
        public float tradeTax = 0.05f; // 5% tax on trades
        public int clanCreationCost = 500;

        [Header("World")]
        public float dayNightCycleMinutes = 24f;
        public float weatherChangeMinutes = 10f;
        public float worldEventIntervalMinutes = 10f;

        [Header("Building")]
        public float baseStructureHealth = 500f;
        public float offlineProtectionDamageReduction = 0.9f;
        public float buildingDecayRate = 0f; // Optional decay

        [Header("Social")]
        public int maxClanSize = 20;
        public int maxAlliances = 3;
        public float voiceChatMaxDistance = 50f;
        public float voiceChatFalloffPower = 2f;

        [Header("Mobile Optimization")]
        public int maxVisiblePlayers = 50;
        public float networkUpdateRate = 0.05f; // 20 updates/sec
        public int chunkLoadRadius = 5;
    }
}
