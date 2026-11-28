using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        public const int TAB_CONSTELLATIONS = 1;
        public const int TAB_PLAY = 2;
        public const int TAB_ABILITIES = 3;
        public const int TAB_ACHIEVEMENTS = 4;

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
        [SerializeField] private Button dailyBonusButton;
        [SerializeField] private BadgeUI dailyBonusBadge;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private GameObject heroPreviewRoot;

        [Header("FTUE pointer (glow + hint bubble, moved between targets by MenuFtueFlow)")]
        [SerializeField] private GameObject ftueUpgradesGlow;
        [SerializeField] private GameObject ftueUpgradesHintRoot;
        [SerializeField] private TMP_Text ftueUpgradesHintText;

        private SaveManager saveManager;
        private DailyBonusManager bonusManager;
        private AchievementManager achievementManager;
        private AudioManager audioManager;
        private MetaUpgradeManager metaUpgradeManager;
        private LocalizationService localization;
        private GameInputService gameInput;
        private InputModeService inputModeService;
        private TalentTreeManager talentTreeManager;
        private HeroSelectionUI heroSelectionUI;
        private Action<InputMode> cachedInputModeHandler;
        private Action cachedSettingsClosedHandler;
        private int previewCullingMask = -1;
        private bool previewActive = true;
        private int selectedTab = -1;
        private FtueSpotlight ftueSpotlight;
        private MenuFtueStep ftueStep;

        public bool IsHeroPreviewActive => previewActive;
        public int SelectedTab => selectedTab;
        public MenuFtueStep FtueStep => ftueStep;
        public FtueSpotlight FtueSpotlight => ftueSpotlight;

        [Inject]
        public void Construct(
            SaveManager saveManager,
            DailyBonusManager bonusManager,
            AchievementManager achievementManager = null,
            AudioManager audioManager = null,
            MetaUpgradeManager metaUpgradeManager = null,
            LocalizationService localization = null,
            GameInputService gameInput = null,
            InputModeService inputModeService = null,
            TalentTreeManager talentTreeManager = null)
        {
            UnsubscribeInputMode();
            if (this.talentTreeManager != null)
            {
                this.talentTreeManager.OnTreeChanged -= UpdateBadges;
            }
            this.saveManager = saveManager;
            this.bonusManager = bonusManager;
            this.achievementManager = achievementManager;
            this.audioManager = audioManager;
            this.metaUpgradeManager = metaUpgradeManager;
            this.localization = localization;
            this.gameInput = gameInput;
            this.inputModeService = inputModeService;
            this.talentTreeManager = talentTreeManager;

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

            if (this.talentTreeManager != null)
            {
                this.talentTreeManager.OnTreeChanged += UpdateBadges;
            }

            if (this.localization != null)
            {
                this.localization.LanguageChanged += RefreshFtueGuide;
            }

            SubscribeInputMode();
            UpdateBadges();
        }

        private void Awake()
        {
            if (previewCamera != null) previewCullingMask = previewCamera.cullingMask;
            if (playScreenPanel != null) heroSelectionUI = playScreenPanel.GetComponentInChildren<HeroSelectionUI>(true);
            cachedSettingsClosedHandler ??= FocusCurrentTab;
            if (settingsPopupUI != null)
            {
                settingsPopupUI.OnClosed += cachedSettingsClosedHandler;
            }
            if (dailyBonusScreenUI != null)
            {
                dailyBonusScreenUI.OnClosed += cachedSettingsClosedHandler;
            }
        }

        private void Start()
        {
            UpdateBadges();
            audioManager?.PlayMusic(MusicType.Menu);

            // FTUE: right after the first run the menu opens on Upgrades with enough coins for the cheapest upgrade.
            if (BeginFtueMenuFlow())
            {
                OpenUpgradesTab(playSound: false);
                return;
            }

            // Set default tab on startup
            OpenPlayTab(playSound: false);
        }

        private void LateUpdate()
        {
            ftueSpotlight?.LateTick();
        }

        /// <summary>
        /// Starts the post-first-run menu onboarding: tops coins up to the cheapest upgrade once.
        /// Returns true when the menu should open on the Upgrades tab.
        /// </summary>
        public bool BeginFtueMenuFlow()
        {
            if (!MenuFtueFlow.IsFlowPending(saveManager) || saveManager.HasAnyMetaUpgrade()) return false;

            if (metaUpgradeManager != null)
            {
                int cheapest = metaUpgradeManager.GetCheapestAvailableUpgradeCost();
                if (cheapest > 0) saveManager.GrantFtueStarterCoins(cheapest, saveImmediately: true);
            }

            return MenuFtueFlow.ShouldBuyUpgrade(saveManager, metaUpgradeManager != null && metaUpgradeManager.CanAffordAnyUpgrade());
        }

        private void Update()
        {
            if (gameInput != null && gameInput.WasCancelPressedThisFrame())
            {
                HandleCancelInput();
            }
        }

        public void HandleCancelInput()
        {
            if (settingsPopupUI != null && settingsPopupUI.IsOpen)
            {
                settingsPopupUI.Hide();
                return;
            }

            if (dailyBonusScreenUI != null && dailyBonusScreenUI.IsOpen)
            {
                dailyBonusScreenUI.Hide();
                return;
            }

            if (achievementScreenUI != null && achievementScreenUI.IsRewardPopupOpen)
            {
                achievementScreenUI.HideRewardPopup();
                FocusCurrentTab();
                return;
            }

            if (constellationTreeUI != null && constellationTreeUI.IsOpen && constellationTreeUI.IsDetailOpen)
            {
                constellationTreeUI.CloseDetail();
                return;
            }

            if (selectedTab != TAB_PLAY)
            {
                OpenPlayTab(playSound: true);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeInputMode();
            if (settingsPopupUI != null && cachedSettingsClosedHandler != null)
            {
                settingsPopupUI.OnClosed -= cachedSettingsClosedHandler;
            }
            if (dailyBonusScreenUI != null && cachedSettingsClosedHandler != null)
            {
                dailyBonusScreenUI.OnClosed -= cachedSettingsClosedHandler;
            }
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
            if (talentTreeManager != null)
            {
                talentTreeManager.OnTreeChanged -= UpdateBadges;
            }
            if (localization != null)
            {
                localization.LanguageChanged -= RefreshFtueGuide;
            }
        }

        private void HandleAchievementUnlocked(AchievementData _)
        {
            UpdateBadges();
        }

        private void HandleMetaUpgradePurchased(string _)
        {
            UpdateBadges();
        }

        private void UpdateCoinsDisplay()
        {
            if (saveManager == null || saveManager.Data == null) return;

            if (coinsLabel != null) coinsLabel.SetValue(saveManager.Data.coins, animate: true);
            else if (coinsText != null) coinsText.text = saveManager.Data.coins.ToString();

            if (shardsLabel != null) shardsLabel.SetValue(saveManager.Data.bossShards, animate: true);

            UpdateBadges();
        }

        private void UpdateBadges()
        {
            int newAchievements = achievementManager != null ? achievementManager.unlockedThisRun.Count : 0;
            int affordableStars = talentTreeManager != null ? talentTreeManager.GetAffordableWithShardsCount() : 0;
            bool bonusAvailable = bonusManager != null && bonusManager.IsRewardAvailable();

            RefreshFtueGuide();

            if (dailyBonusBadge != null)
            {
                dailyBonusBadge.SetDot(bonusAvailable);
            }

            if (tabBar != null)
            {
                bool upgradesDot = ftueStep == MenuFtueStep.OpenUpgrades || ftueStep == MenuFtueStep.BuyUpgrade;
                tabBar.SetBadgeDot(TAB_UPGRADES, upgradesDot);
                tabBar.SetBadge(TAB_CONSTELLATIONS, affordableStars);
                tabBar.SetBadge(TAB_ACHIEVEMENTS, newAchievements);
                return;
            }

            if (achievementsBadge != null) achievementsBadge.SetActive(newAchievements > 0);
            if (dailyBonusBadge == null && bonusBadge != null) bonusBadge.SetActive(bonusAvailable);
        }

        /// <summary>Re-evaluates the onboarding step and moves the pointer (glow + bubble) to its target.</summary>
        public void RefreshFtueGuide()
        {
            bool canAfford = metaUpgradeManager != null && metaUpgradeManager.CanAffordAnyUpgrade();
            ftueStep = MenuFtueFlow.Evaluate(saveManager, selectedTab, canAfford);

            ftueSpotlight ??= new FtueSpotlight(ftueUpgradesGlow, ftueUpgradesHintRoot, ftueUpgradesHintText);

            switch (ftueStep)
            {
                case MenuFtueStep.OpenUpgrades:
                    ShowFtueOnTab(TAB_UPGRADES, LocalizationService.KEY_FTUE_UPGRADES_HINT, "Upgrade your hero!");
                    break;
                case MenuFtueStep.BuyUpgrade:
                    Button buy = metaUpgradeUI != null ? metaUpgradeUI.GetCheapestAffordableBuyButton() : null;
                    ftueSpotlight.Show(buy != null ? buy.transform as RectTransform : null,
                        FtueText(LocalizationService.KEY_FTUE_UPGRADE_BUY_HINT, "Buy your first upgrade"));
                    break;
                case MenuFtueStep.OpenConstellations:
                    ShowFtueOnTab(TAB_CONSTELLATIONS, LocalizationService.KEY_FTUE_CONSTELLATIONS_HINT, "Spend Boss Shards on Constellation stars");
                    break;
                case MenuFtueStep.OpenPlayTab:
                    ShowFtueOnTab(TAB_PLAY, LocalizationService.KEY_FTUE_PLAY_TAB_HINT, "Ready for the next run?");
                    break;
                case MenuFtueStep.PressStart:
                    // The big START button speaks for itself: pulse only, a bubble would cover the hero tiles.
                    Button start = heroSelectionUI != null ? heroSelectionUI.StartRunButton : null;
                    ftueSpotlight.Show(start != null ? start.transform as RectTransform : null, null);
                    break;
                default:
                    ftueSpotlight.Hide();
                    break;
            }
        }

        private void ShowFtueOnTab(int tabIndex, string key, string fallback)
        {
            BottomTabUI tab = tabBar != null ? tabBar.GetTab(tabIndex) : (tabs != null && tabIndex < tabs.Length ? tabs[tabIndex] : null);
            ftueSpotlight.Show(tab != null ? tab.transform as RectTransform : null, FtueText(key, fallback), isTab: true);
        }

        private string FtueText(string key, string fallback)
        {
            return localization != null ? localization.Get(key, fallback) : fallback;
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
            if (achievementScreenUI != null) achievementScreenUI.Hide();
            if (abilitiesScreenUI != null) abilitiesScreenUI.Hide();
            if (metaUpgradeUI != null) metaUpgradeUI.Hide();
            if (dailyBonusScreenUI != null && dailyBonusScreenUI.IsOpen) dailyBonusScreenUI.Hide();
            if (playScreenPanel != null) playScreenPanel.SetActive(false);
            if (constellationTreeUI != null) constellationTreeUI.Hide();
            SetHeroPreviewActive(false);
        }

        /// <summary>Opens the Radial Constellation Tree tab.</summary>
        public void OnConstellationClicked()
        {
            OpenConstellationTab(playSound: true);
        }

        private void OpenConstellationTab(bool playSound)
        {
            if (playSound) audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            if (MenuFtueFlow.IsConstellationsHintPending(saveManager))
            {
                saveManager.MarkFtueConstellationsHintDismissed(saveImmediately: true);
            }
            CloseAllPanels();
            SelectTab(TAB_CONSTELLATIONS);
            if (constellationTreeUI != null)
            {
                constellationTreeUI.SetMainMenu(this);
                constellationTreeUI.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] ConstellationTreeUI reference is missing!");
            }
            FocusCurrentTab();
        }

        /// <summary>Switches to the Constellations tab and focuses a specific star node.</summary>
        public void OpenConstellationNode(string nodeId)
        {
            OpenConstellationTab(playSound: false);
            if (constellationTreeUI != null && !string.IsNullOrEmpty(nodeId))
            {
                constellationTreeUI.FocusNode(nodeId, playSound: false);
            }
        }

        private void SelectTab(int index)
        {
            selectedTab = index;
            RefreshFtueGuide();
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
            if (MenuFtueFlow.IsFlowPending(saveManager))
            {
                saveManager.MarkFtueMenuFlowCompleted(saveImmediately: true);
            }
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
            FocusCurrentTab();
        }

        public void OnUpgradesClicked()
        {
            OpenUpgradesTab(playSound: true);
        }

        private void OpenUpgradesTab(bool playSound)
        {
            if (playSound) audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
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
            FocusCurrentTab();
        }

        public void OnAbilitiesClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            CloseAllPanels();
            SelectTab(TAB_ABILITIES);
            if (abilitiesScreenUI != null)
            {
                abilitiesScreenUI.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] AbilitiesScreenUI reference is missing!");
            }
            FocusCurrentTab();
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
                achievementScreenUI.Show();
            }
            FocusCurrentTab();
        }

        /// <summary>Opens the Achievements tab focused on <paramref name="achievementId"/> (Play tab goal card).</summary>
        public void OpenAchievement(string achievementId)
        {
            OnAchievementsClicked();
            if (achievementScreenUI != null && !string.IsNullOrEmpty(achievementId))
            {
                achievementScreenUI.FocusAchievement(achievementId);
            }
        }

        public void OnBonusClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            DevLog.Log("[MainMenuUI] Bonus clicked");
            if (dailyBonusScreenUI != null)
            {
                dailyBonusScreenUI.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] DailyBonusScreenUI reference is missing!");
            }
        }

        private void SubscribeInputMode()
        {
            if (inputModeService == null) return;
            cachedInputModeHandler ??= HandleInputModeChanged;
            inputModeService.InputModeChanged -= cachedInputModeHandler;
            inputModeService.InputModeChanged += cachedInputModeHandler;
        }

        private void UnsubscribeInputMode()
        {
            if (inputModeService != null && cachedInputModeHandler != null)
            {
                inputModeService.InputModeChanged -= cachedInputModeHandler;
            }
        }

        private void HandleInputModeChanged(InputMode mode)
        {
            if (mode == InputMode.Touch)
            {
                UiFocusHelper.ClearSelection();
            }
            else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                FocusCurrentTab();
            }
        }

        public void FocusCurrentTab()
        {
            if (settingsPopupUI != null && settingsPopupUI.IsOpen) return;
            if (dailyBonusScreenUI != null && dailyBonusScreenUI.IsOpen) return;

            Selectable target = null;
            switch (selectedTab)
            {
                case TAB_PLAY:
                    if (heroSelectionUI == null && playScreenPanel != null)
                    {
                        heroSelectionUI = playScreenPanel.GetComponentInChildren<HeroSelectionUI>(true);
                    }
                    target = heroSelectionUI != null ? heroSelectionUI.GetFirstSelectable() : null;
                    break;
                case TAB_UPGRADES:
                    target = metaUpgradeUI != null ? metaUpgradeUI.GetFirstSelectable() : null;
                    break;
                case TAB_CONSTELLATIONS:
                    target = constellationTreeUI != null ? constellationTreeUI.GetFirstSelectable() : null;
                    break;
                case TAB_ABILITIES:
                    target = abilitiesScreenUI != null ? abilitiesScreenUI.GetFirstSelectable() : null;
                    break;
                case TAB_ACHIEVEMENTS:
                    target = achievementScreenUI != null ? achievementScreenUI.GetFirstSelectable() : null;
                    break;
            }

            if (target == null && tabBar != null)
            {
                target = UiFocusHelper.FindFirstInteractable(tabBar.transform);
            }
            UiFocusHelper.SetFirstSelected(target, inputModeService);
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
