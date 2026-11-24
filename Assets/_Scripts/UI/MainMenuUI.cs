using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using UnityEngine.SceneManagement;

namespace ProjectB.UI
{
    /// <summary>
    /// Main menu shell. Sprint 13.2: shared header with coin and Boss Shard pills (count-up), bottom
    /// <see cref="TabBarUI"/> with numeric badges, opaque screens, and the 3D hero preview (camera culling +
    /// <see cref="MenuHeroRotator"/>) switched off on every tab except Play.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public const int TAB_UPGRADES = 0;
        public const int TAB_ABILITIES = 1;
        public const int TAB_PLAY = 2;
        public const int TAB_ACHIEVEMENTS = 3;
        public const int TAB_BONUS = 4;

        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private GameObject achievementsBadge;
        [SerializeField] private GameObject bonusBadge;
        [SerializeField] private MetaUpgradeUI metaUpgradeUI;
        [SerializeField] private AbilitiesScreenUI abilitiesScreenUI;
        [SerializeField] private AchievementScreenUI achievementScreenUI;
        [SerializeField] private DailyBonusScreenUI dailyBonusScreenUI;
        [SerializeField] private SettingsPopupUI settingsPopupUI;
        [SerializeField] private BottomTabUI[] tabs;
        [SerializeField] private GameObject playScreenPanel;
        [SerializeField] private ConstellationTreeUI constellationTreeUI;

        [Header("Sprint 13: header, tabs, preview")]
        [SerializeField] private CurrencyLabel coinsLabel;
        [SerializeField] private CurrencyLabel shardsLabel;
        [SerializeField] private TabBarUI tabBar;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private GameObject heroPreviewRoot;

        private SaveManager saveManager;
        private DailyBonusManager bonusManager;
        private AchievementManager achievementManager;
        private AudioManager audioManager;
        private int previewCullingMask = -1;
        private bool previewActive = true;
        private int selectedTab = -1;

        public bool IsHeroPreviewActive => previewActive;
        public int SelectedTab => selectedTab;

        [Inject]
        public void Construct(
            SaveManager saveManager,
            DailyBonusManager bonusManager,
            AchievementManager achievementManager = null,
            AudioManager audioManager = null)
        {
            this.saveManager = saveManager;
            this.bonusManager = bonusManager;
            this.achievementManager = achievementManager;
            this.audioManager = audioManager;

            if (this.saveManager != null)
            {
                this.saveManager.OnDataChanged += UpdateCoinsDisplay;
                UpdateCoinsDisplay();
            }

            if (this.bonusManager != null)
            {
                this.bonusManager.OnBonusStateChanged += UpdateBadges;
            }

            if (this.achievementManager != null)
            {
                this.achievementManager.OnAchievementUnlocked += HandleAchievementUnlocked;
            }
        }

        private void Awake()
        {
            if (previewCamera != null) previewCullingMask = previewCamera.cullingMask;
        }

        private void Start()
        {
            UpdateBadges();
            audioManager?.PlayMusic(MusicType.Menu);

            // Set default tab on startup
            OpenPlayTab(playSound: false);
        }

        private void OnDestroy()
        {
            if (saveManager != null)
            {
                saveManager.OnDataChanged -= UpdateCoinsDisplay;
            }
            if (bonusManager != null)
            {
                bonusManager.OnBonusStateChanged -= UpdateBadges;
            }
            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked -= HandleAchievementUnlocked;
            }
        }

        private void HandleAchievementUnlocked(AchievementData _)
        {
            UpdateBadges();
        }

        private void UpdateCoinsDisplay()
        {
            if (saveManager == null || saveManager.Data == null) return;

            if (coinsLabel != null) coinsLabel.SetValue(saveManager.Data.coins, animate: true);
            else if (coinsText != null) coinsText.text = saveManager.Data.coins.ToString();

            if (shardsLabel != null) shardsLabel.SetValue(saveManager.Data.bossShards, animate: true);
        }

        private void UpdateBadges()
        {
            int newAchievements = achievementManager != null ? achievementManager.unlockedThisRun.Count : 0;
            bool bonusAvailable = bonusManager != null && bonusManager.IsRewardAvailable();

            if (tabBar != null)
            {
                tabBar.SetBadge(TAB_ACHIEVEMENTS, newAchievements);
                tabBar.SetBadgeDot(TAB_BONUS, bonusAvailable);
                return;
            }

            if (achievementsBadge != null) achievementsBadge.SetActive(newAchievements > 0);
            if (bonusBadge != null) bonusBadge.SetActive(bonusAvailable);
        }

        /// <summary>
        /// Hero preview only renders on the Play tab: other screens are opaque, so the camera culls nothing and the
        /// rotating preview hero is deactivated (no GPU / CPU cost behind menus).
        /// </summary>
        public void SetHeroPreviewActive(bool active)
        {
            previewActive = active;
            if (previewCamera != null)
            {
                if (previewCullingMask == -1 && previewCamera.cullingMask != 0) previewCullingMask = previewCamera.cullingMask;
                previewCamera.cullingMask = active ? (previewCullingMask == -1 ? ~0 : previewCullingMask) : 0;
            }
            if (heroPreviewRoot != null && heroPreviewRoot.activeSelf != active)
            {
                heroPreviewRoot.SetActive(active);
            }
        }

        private void CloseAllPanels()
        {
            if (achievementScreenUI != null) achievementScreenUI.gameObject.SetActive(false);
            if (abilitiesScreenUI != null) abilitiesScreenUI.gameObject.SetActive(false);
            if (metaUpgradeUI != null) metaUpgradeUI.Hide();
            if (dailyBonusScreenUI != null) dailyBonusScreenUI.gameObject.SetActive(false);
            if (playScreenPanel != null) playScreenPanel.SetActive(false);
            if (constellationTreeUI != null) constellationTreeUI.Hide();
            SetHeroPreviewActive(false);
        }

        /// <summary>Opens the Radial Constellation Tree (from the Upgrades tab).</summary>
        public void OnConstellationClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            CloseAllPanels();
            SelectTab(TAB_UPGRADES);
            if (constellationTreeUI != null)
            {
                constellationTreeUI.SetMainMenu(this);
                constellationTreeUI.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] ConstellationTreeUI reference is missing!");
            }
        }

        private void SelectTab(int index)
        {
            selectedTab = index;
            if (tabBar != null)
            {
                tabBar.Select(index);
                return;
            }
            if (tabs == null) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].SetActiveState(i == index);
            }
        }

        public void StartGame()
        {
            audioManager?.PlaySfx(SfxType.UiConfirm, randomizePitch: false);
            DevLog.Log("[MainMenuUI] Start Game clicked!");
            SceneManager.LoadScene("Gameplay");
        }

        public void OnPlayClicked()
        {
            OpenPlayTab(playSound: true);
        }

        private void OpenPlayTab(bool playSound)
        {
            if (playSound) audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            DevLog.Log("[MainMenuUI] Play tab clicked");
            CloseAllPanels();
            SelectTab(TAB_PLAY);
            if (playScreenPanel != null)
            {
                playScreenPanel.SetActive(true);
            }
            SetHeroPreviewActive(true);
        }

        public void OnUpgradesClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            DevLog.Log("[MainMenuUI] Upgrades clicked");
            CloseAllPanels();
            SelectTab(TAB_UPGRADES);
            if (metaUpgradeUI != null)
            {
                metaUpgradeUI.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] MetaUpgradeUI reference is missing!");
            }
        }

        public void OnAbilitiesClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            CloseAllPanels();
            SelectTab(TAB_ABILITIES);
            if (abilitiesScreenUI != null)
            {
                abilitiesScreenUI.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] AbilitiesScreenUI reference is missing!");
            }
        }

        public void OnAchievementsClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            DevLog.Log("[MainMenuUI] Achievements clicked");
            CloseAllPanels();
            SelectTab(TAB_ACHIEVEMENTS);
            achievementManager?.ClearRunUnlocks();
            UpdateBadges();
            if (achievementScreenUI != null)
            {
                achievementScreenUI.gameObject.SetActive(true);
            }
        }

        public void OnBonusClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            DevLog.Log("[MainMenuUI] Bonus clicked");
            CloseAllPanels();
            SelectTab(TAB_BONUS);
            if (dailyBonusScreenUI != null)
            {
                dailyBonusScreenUI.gameObject.SetActive(true);
            }
        }

        public void OnSettingsClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            if (settingsPopupUI != null)
            {
                settingsPopupUI.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] SettingsPopupUI reference is missing!");
            }
        }
    }
}
