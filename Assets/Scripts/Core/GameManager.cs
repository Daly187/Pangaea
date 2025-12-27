using UnityEngine;
using System;

namespace Pangaea.Core
{
    /// <summary>
    /// Central game manager - handles game state, initialization, and global systems.
    /// Singleton pattern for easy access across all systems.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private GameState currentState = GameState.Initializing;

        [Header("Configuration")]
        [SerializeField] private GameConfig gameConfig;

        // Core system references
        public PlayerManager PlayerManager { get; private set; }
        public WorldManager WorldManager { get; private set; }
        public NetworkManager NetworkManager { get; private set; }
        public UIManager UIManager { get; private set; }

        // Events
        public event Action<GameState> OnGameStateChanged;
        public event Action OnGameInitialized;

        public GameState CurrentState => currentState;
        public GameConfig Config => gameConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeGame();
        }

        private void InitializeGame()
        {
            Debug.Log("[PANGAEA] Initializing game systems...");

            // Load configuration
            if (gameConfig == null)
            {
                gameConfig = Resources.Load<GameConfig>("Config/GameConfig");
            }

            // Initialize core managers (order matters)
            InitializeManagers();

            SetGameState(GameState.MainMenu);
            OnGameInitialized?.Invoke();

            Debug.Log("[PANGAEA] Game initialized successfully.");
        }

        private void InitializeManagers()
        {
            // Find or create managers
            PlayerManager = FindOrCreateManager<PlayerManager>("PlayerManager");
            WorldManager = FindOrCreateManager<WorldManager>("WorldManager");
            UIManager = FindOrCreateManager<UIManager>("UIManager");

            // Network manager is special - created on connect
        }

        private T FindOrCreateManager<T>(string name) where T : MonoBehaviour
        {
            T manager = FindObjectOfType<T>();
            if (manager == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(transform);
                manager = go.AddComponent<T>();
            }
            return manager;
        }

        public void SetGameState(GameState newState)
        {
            if (currentState == newState) return;

            GameState previousState = currentState;
            currentState = newState;

            Debug.Log($"[PANGAEA] Game state changed: {previousState} -> {newState}");
            OnGameStateChanged?.Invoke(newState);
        }

        public void StartGame()
        {
            SetGameState(GameState.Connecting);
            // Network connection logic will transition to Playing state
        }

        public void PauseGame()
        {
            if (currentState == GameState.Playing)
            {
                SetGameState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                SetGameState(GameState.Playing);
                Time.timeScale = 1f;
            }
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            SetGameState(GameState.MainMenu);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && currentState == GameState.Playing)
            {
                // Mobile: handle background state
                Debug.Log("[PANGAEA] Application paused - handling background state");
            }
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[PANGAEA] Application quitting - saving state...");
            // Save player state before quit
        }
    }

    public enum GameState
    {
        Initializing,
        MainMenu,
        Connecting,
        Loading,
        Playing,
        Paused,
        Disconnected
    }
}
