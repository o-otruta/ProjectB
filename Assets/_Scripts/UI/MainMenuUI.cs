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

        [Header("Sprint 12.2: FTUE upgrades hint")]
        [SerializeField] private GameObject ftueUpgradesGlow;
        [SerializeField] private GameObject ftueUpgradesHintRoot;
        [SerializeField] private TMP_Text ftueUpgradesHintText;

        private SaveManager saveManager;
        private DailyBonusManager bonusManager;
        private AchievementManager achievementManager;
        private AudioManager audioManager;
        private MetaUpgradeManager metaUpgradeManager;
        private LocalizationService localization;
        private int previewCullingMask = -1;
        private bool previewActive = true;
        private int selectedTab = -1;
        private bool isFtueUpgradesHintActive;

        public bool IsHeroPreviewActive => previewActive;
        public int SelectedTab => selectedTab;
        public bool IsFtueUpgradesHintActive => isFtueUpgradesHintActive;

        [Inject]
        public void Construct(
            SaveManager saveManager,
            DailyBonusManager bonusManager,
            AchievementManager achievementManager = null,
            AudioManager audioManager = null,
            MetaUpgradeManager metaUpgradeManager = null,
            LocalizationService localization = null)
        {
            this.saveManager = saveManager;
            this.bonusManager = bonusManager;
            this.achievementManager = achievementManager;
            this.audioManager = audioManager;
            this.metaUpgradeManager = metaUpgradeManager;
            this.localization = localization;

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

            if (this.metaUpgradeManager != null)
            {
                this.metaUpgradeManager.OnUpgradePurchased += HandleMetaUpgradePurchased;
            }

            if (this.localization != null)
            {
                this.localization.LanguageChanged += RefreshFtueTexts;
            }

            RefreshFtueTexts();
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
            if (metaUpgradeManager != null)
            {
                metaUpgradeManager.OnUpgradePurchased -= HandleMetaUpgradePurchased;
            }
            if (localization != null)
            {
                localization.LanguageChanged -= RefreshFtueTexts;
            }
        }

        private void HandleAchievementUnlocked(AchievementData _)
        {
            UpdateBadges();
        }

        private void HandleMetaUpgradePurchased(string _)
        {
            if (saveManager != null && !saveManager.IsFtueUpgradesHintDismissed)
            {
                saveManager.MarkFtueUpgradesHintDismissed(saveImmediately: true);
            }
            UpdateBadges();
        }

        private void RefreshFtueTexts()
        {
            if (ftueUpgradesHintText != null)
            {
                ftueUpgradesHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_UPGRADES_HINT, "Upgrade your hero!")
                    : "Upgrade your hero!";
            }
        }

        private void UpdateCoinsDisplay()
        {
            if (saveManager == null || saveManager.Data == null) return;

            if (coinsLabel != null) coinsLabel.SetValue(saveManager.Data.coins, animate: true);
            else if (coinsText != null) coinsText.text = saveManager.Data.coins.ToString();

            if (shardsLabel != null) shardsLabel.SetValue(saveManager.Data.bossShards, animate: true);

            UpdateBadges();
        }

        public bool ShouldShowFtueUpgradesHint()
        {
            return saveManager != null
                && saveManager.Data != null
                && saveManager.Data.totalRuns >= 1
                && !saveManager.IsFtueUpgradesHintDismissed
                && metaUpgradeManager != null
                && metaUpgradeManager.CanAffordAnyUpgrade();
        }

        private void UpdateBadges()
        {
            int newAchievements = achievementManager != null ? achievementManager.unlockedThisRun.Count : 0;
            bool bonusAvailable = bonusManager != null && bonusManager.IsRewardAvailable();
            isFtueUpgradesHintActive = ShouldShowFtueUpgradesHint();

            RefreshFtueHintVisuals();

            if (tabBar != null)
            {
                tabBar.SetBadgeDot(TAB_UPGRADES, isFtueUpgradesHintActive);
                tabBar.SetBadge(TAB_ACHIEVEMENTS, newAchievements);
                tabBar.SetBadgeDot(TAB_BONUS, bonusAvailable);
                return;
            }

            if (achievementsBadge != null) achievementsBadge.SetActive(newAchievements > 0);
            if (bonusBadge != null) bonusBadge.SetActive(bonusAvailable);
        }

        private void RefreshFtueHintVisuals()
        {
            if (ftueUpgradesGlow != null && ftueUpgradesGlow.activeSelf != isFtueUpgradesHintActive)
            {
                ftueUpgradesGlow.SetActive(isFtueUpgradesHintActive);
            }
            if (ftueUpgradesHintRoot != null)
            {
                bool showPopup = isFtueUpgradesHintActive && selectedTab != TAB_UPGRADES;
                if (ftueUpgradesHintRoot.activeSelf != showPopup)
                {
                    ftueUpgradesHintRoot.SetActive(showPopup);
                }
            }
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
            RefreshFtueHintVisuals();
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
            if (isFtueUpgradesHintActive || ShouldShowFtueUpgradesHint())
            {
                saveManager?.MarkFtueUpgradesHintDismissed(saveImmediately: true);
                UpdateBadges();
            }
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
