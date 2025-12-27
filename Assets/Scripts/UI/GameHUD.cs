using UnityEngine;
using UnityEngine.UI;
using Pangaea.Player;
using Pangaea.Core;

namespace Pangaea.UI
{
    /// <summary>
    /// Main game HUD - displays health, stamina, hunger bars and player info.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] private Image healthBarFill;
        [SerializeField] private Text healthText;

        [Header("Stamina Bar")]
        [SerializeField] private Image staminaBarFill;
        [SerializeField] private Text staminaText;

        [Header("Hunger Bar")]
        [SerializeField] private Image hungerBarFill;
        [SerializeField] private Text hungerText;

        [Header("Player Info")]
        [SerializeField] private Text levelText;
        [SerializeField] private Text karmaText;
        [SerializeField] private Image karmaIndicator;

        [Header("PvP Mode")]
        [SerializeField] private Text pvpModeText;
        [SerializeField] private Image pvpModeBackground;

        [Header("Colors")]
        [SerializeField] private Color healthColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color healthLowColor = new Color(1f, 0f, 0f);
        [SerializeField] private Color staminaColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color hungerColor = new Color(0.8f, 0.6f, 0.2f);
        [SerializeField] private Color hungerLowColor = new Color(0.8f, 0.2f, 0.2f);

        [Header("Karma Colors")]
        [SerializeField] private Color banditColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color neutralColor = Color.white;
        [SerializeField] private Color guardianColor = new Color(0.2f, 0.5f, 1f);

        [Header("PvP Colors")]
        [SerializeField] private Color passiveModeColor = new Color(0.2f, 0.6f, 0.2f);
        [SerializeField] private Color engagedModeColor = new Color(0.8f, 0.2f, 0.2f);

        private PlayerController localPlayer;
        private PlayerStats playerStats;

        private void Start()
        {
            // Try to find local player
            FindLocalPlayer();
        }

        private void Update()
        {
            if (localPlayer == null || playerStats == null)
            {
                FindLocalPlayer();
                return;
            }

            UpdateHealthBar();
            UpdateStaminaBar();
            UpdateHungerBar();
            UpdatePlayerInfo();
            UpdatePvPMode();
        }

        private void FindLocalPlayer()
        {
            localPlayer = GameManager.Instance?.PlayerManager?.LocalPlayer;
            if (localPlayer != null)
            {
                playerStats = localPlayer.Stats;

                // Subscribe to events
                if (playerStats != null)
                {
                    playerStats.OnHealthChanged += OnHealthChanged;
                    playerStats.OnStaminaChanged += OnStaminaChanged;
                    playerStats.OnHungerChanged += OnHungerChanged;
                    playerStats.OnLevelChanged += OnLevelChanged;
                    playerStats.OnKarmaChanged += OnKarmaChanged;
                }
            }
        }

        private void UpdateHealthBar()
        {
            if (playerStats == null) return;

            float healthPercent = playerStats.CurrentHealth / playerStats.MaxHealth;

            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = healthPercent;
                healthBarFill.color = healthPercent < 0.25f ? healthLowColor : healthColor;
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(playerStats.CurrentHealth)}/{Mathf.CeilToInt(playerStats.MaxHealth)}";
            }
        }

        private void UpdateStaminaBar()
        {
            if (playerStats == null) return;

            float staminaPercent = playerStats.CurrentStamina / playerStats.MaxStamina;

            if (staminaBarFill != null)
            {
                staminaBarFill.fillAmount = staminaPercent;
                staminaBarFill.color = staminaColor;
            }

            if (staminaText != null)
            {
                staminaText.text = $"{Mathf.CeilToInt(playerStats.CurrentStamina)}/{Mathf.CeilToInt(playerStats.MaxStamina)}";
            }
        }

        private void UpdateHungerBar()
        {
            if (playerStats == null) return;

            float hungerPercent = playerStats.CurrentHunger / 100f; // Assuming max 100

            if (hungerBarFill != null)
            {
                hungerBarFill.fillAmount = hungerPercent;
                hungerBarFill.color = hungerPercent < 0.2f ? hungerLowColor : hungerColor;
            }

            if (hungerText != null)
            {
                hungerText.text = $"{Mathf.CeilToInt(playerStats.CurrentHunger)}%";
            }
        }

        private void UpdatePlayerInfo()
        {
            if (playerStats == null) return;

            if (levelText != null)
            {
                levelText.text = $"Lv.{playerStats.Level}";
            }

            if (karmaText != null)
            {
                karmaText.text = $"Karma: {playerStats.Karma}";
            }

            if (karmaIndicator != null)
            {
                karmaIndicator.color = GetKarmaColor(playerStats.Karma);
            }
        }

        private void UpdatePvPMode()
        {
            if (localPlayer == null) return;

            bool isPassive = localPlayer.CurrentPvPMode == PvPMode.Passive;

            if (pvpModeText != null)
            {
                pvpModeText.text = isPassive ? "PASSIVE" : "ENGAGED";
            }

            if (pvpModeBackground != null)
            {
                pvpModeBackground.color = isPassive ? passiveModeColor : engagedModeColor;
            }
        }

        private Color GetKarmaColor(int karma)
        {
            if (karma < -100) return banditColor;
            if (karma > 100) return guardianColor;
            return neutralColor;
        }

        // Event handlers
        private void OnHealthChanged(float current, float max)
        {
            UpdateHealthBar();
        }

        private void OnStaminaChanged(float current, float max)
        {
            UpdateStaminaBar();
        }

        private void OnHungerChanged(float current, float max)
        {
            UpdateHungerBar();
        }

        private void OnLevelChanged(int level)
        {
            UpdatePlayerInfo();
        }

        private void OnKarmaChanged(int karma)
        {
            UpdatePlayerInfo();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= OnHealthChanged;
                playerStats.OnStaminaChanged -= OnStaminaChanged;
                playerStats.OnHungerChanged -= OnHungerChanged;
                playerStats.OnLevelChanged -= OnLevelChanged;
                playerStats.OnKarmaChanged -= OnKarmaChanged;
            }
        }
    }
}
