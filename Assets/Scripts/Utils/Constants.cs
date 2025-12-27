namespace Pangaea
{
    /// <summary>
    /// Global constants for PANGAEA.
    /// </summary>
    public static class Constants
    {
        // Version
        public const string GAME_VERSION = "0.1.0-alpha";
        public const int PROTOCOL_VERSION = 1;

        // Layers
        public const string LAYER_PLAYER = "Player";
        public const string LAYER_ENEMY = "Enemy";
        public const string LAYER_GROUND = "Ground";
        public const string LAYER_BUILDING = "Building";
        public const string LAYER_INTERACTABLE = "Interactable";
        public const string LAYER_PREVIEW = "Preview";

        // Tags
        public const string TAG_PLAYER = "Player";
        public const string TAG_ENEMY = "Enemy";
        public const string TAG_LOOT = "Loot";
        public const string TAG_BUILDING = "Building";

        // Scene Names
        public const string SCENE_BOOT = "Boot";
        public const string SCENE_MENU = "MainMenu";
        public const string SCENE_GAME = "GameWorld";
        public const string SCENE_LOADING = "Loading";

        // Resource Paths
        public const string PATH_CONFIG = "Config/GameConfig";
        public const string PATH_ITEMS = "Items/";
        public const string PATH_PREFABS = "Prefabs/";

        // Network
        public const int DEFAULT_PORT = 7777;
        public const int MAX_PLAYERS_PER_SHARD = 1000;
        public const float NETWORK_TICK_RATE = 20f; // 20 ticks per second

        // Gameplay
        public const int MAX_LEVEL = 10;
        public const int MAX_CLAN_SIZE = 20;
        public const int MAX_INVENTORY_SLOTS = 30;
        public const float MAX_CARRY_WEIGHT = 50f;

        // World
        public const float WORLD_SCALE = 100f; // 1 unit = 1 km
        public const float HOME_RADIUS_KM = 10f;
        public const float RESPAWN_RADIUS_KM = 5f;

        // Combat
        public const float MELEE_ATTACK_RANGE = 2f;
        public const float RANGED_MAX_RANGE = 50f;
        public const float COMBAT_LOG_TIME = 10f;

        // Audio
        public const float VOICE_MAX_DISTANCE = 50f;
        public const float WALK_SOUND_RADIUS = 5f;
        public const float RUN_SOUND_RADIUS = 15f;
        public const float COMBAT_SOUND_RADIUS = 30f;

        // UI
        public const float TOOLTIP_DELAY = 0.5f;
        public const float NOTIFICATION_DURATION = 3f;
        public const float DAMAGE_NUMBER_DURATION = 1f;
    }
}
