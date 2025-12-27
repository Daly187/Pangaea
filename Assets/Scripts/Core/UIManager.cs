using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace Pangaea.Core
{
    /// <summary>
    /// Central UI management - handles all UI panels, HUD, and user input for menus.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Canvases")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Canvas overlayCanvas;

        [Header("Screen References")]
        private Dictionary<UIScreen, GameObject> screens = new Dictionary<UIScreen, GameObject>();
        private Stack<UIScreen> screenStack = new Stack<UIScreen>();

        private UIScreen currentScreen = UIScreen.None;

        // Events
        public event Action<UIScreen> OnScreenChanged;
        public event Action<string, float> OnNotification;

        public UIScreen CurrentScreen => currentScreen;

        private void Start()
        {
            InitializeUI();

            // Subscribe to game state changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void InitializeUI()
        {
            // Create main canvas if not assigned
            if (mainCanvas == null)
            {
                GameObject canvasObj = new GameObject("MainCanvas");
                mainCanvas = canvasObj.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                canvasObj.transform.SetParent(transform);
            }

            // Create HUD canvas
            if (hudCanvas == null)
            {
                GameObject hudObj = new GameObject("HUDCanvas");
                hudCanvas = hudObj.AddComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas.sortingOrder = 1;
                hudObj.AddComponent<CanvasScaler>();
                hudObj.AddComponent<GraphicRaycaster>();
                hudObj.transform.SetParent(transform);
            }

            // Create overlay canvas for notifications
            if (overlayCanvas == null)
            {
                GameObject overlayObj = new GameObject("OverlayCanvas");
                overlayCanvas = overlayObj.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 100;
                overlayObj.AddComponent<CanvasScaler>();
                overlayObj.AddComponent<GraphicRaycaster>();
                overlayObj.transform.SetParent(transform);
            }
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:
                    ShowScreen(UIScreen.MainMenu);
                    break;
                case GameState.Playing:
                    ShowScreen(UIScreen.HUD);
                    break;
                case GameState.Paused:
                    ShowScreen(UIScreen.PauseMenu);
                    break;
                case GameState.Loading:
                    ShowScreen(UIScreen.Loading);
                    break;
            }
        }

        public void ShowScreen(UIScreen screen)
        {
            if (currentScreen == screen) return;

            // Hide current screen
            if (currentScreen != UIScreen.None && screens.TryGetValue(currentScreen, out GameObject currentObj))
            {
                currentObj.SetActive(false);
            }

            // Show new screen
            if (screens.TryGetValue(screen, out GameObject newObj))
            {
                newObj.SetActive(true);
            }

            UIScreen previousScreen = currentScreen;
            currentScreen = screen;

            OnScreenChanged?.Invoke(screen);
            Debug.Log($"[UIManager] Screen changed: {previousScreen} -> {screen}");
        }

        public void PushScreen(UIScreen screen)
        {
            screenStack.Push(currentScreen);
            ShowScreen(screen);
        }

        public void PopScreen()
        {
            if (screenStack.Count > 0)
            {
                UIScreen previousScreen = screenStack.Pop();
                ShowScreen(previousScreen);
            }
        }

        public void RegisterScreen(UIScreen screenType, GameObject screenObject)
        {
            screens[screenType] = screenObject;
            screenObject.SetActive(false);
        }

        public void ShowNotification(string message, float duration = 3f)
        {
            OnNotification?.Invoke(message, duration);
            Debug.Log($"[UIManager] Notification: {message}");
        }

        public void ShowDamageNumber(Vector3 worldPosition, int damage, bool isCritical = false)
        {
            // Convert world position to screen position
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);

            // Create floating damage text (would instantiate prefab)
            Debug.Log($"[UIManager] Damage number: {damage} at {screenPos}");
        }

        public void UpdateHealthBar(float current, float max)
        {
            // Update HUD health bar
            float percentage = current / max;
            // HUD update logic here
        }

        public void UpdateStaminaBar(float current, float max)
        {
            float percentage = current / max;
            // HUD update logic here
        }

        public void UpdateHungerBar(float current, float max)
        {
            float percentage = current / max;
            // HUD update logic here
        }

        public void ShowInteractionPrompt(string action, string target)
        {
            // Show "Press E to [action] [target]" style prompt
        }

        public void HideInteractionPrompt()
        {
            // Hide interaction prompt
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }
    }

    public enum UIScreen
    {
        None,
        MainMenu,
        Loading,
        HUD,
        PauseMenu,
        Inventory,
        Crafting,
        Map,
        Social,
        Clan,
        Settings,
        CharacterCreation,
        Death,
        Trading
    }
}
