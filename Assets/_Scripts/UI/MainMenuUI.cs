using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using UnityEngine.SceneManagement;

namespace ProjectB.UI
{
    public class MainMenuUI : MonoBehaviour
    {
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

        private SaveManager saveManager;
        private DailyBonusManager bonusManager;
        private AchievementManager achievementManager;
        private AudioManager audioManager;

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

        private void Start()
        {
            UpdateBadges();
            audioManager?.PlayMusic(MusicType.Menu);
            
            // Set default tab on startup
            OnPlayClicked();
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

        private void HandleAchievementUnlocked(ProjectB.Data.AchievementData _)
        {
            UpdateBadges();
        }

        private void UpdateCoinsDisplay()
        {
            if (saveManager != null && coinsText != null)
            {
                coinsText.text = saveManager.Data.coins.ToString();
            }
        }

        private void UpdateBadges()
        {
            if (achievementsBadge != null)
            {
                bool hasNewAchievements = achievementManager != null && achievementManager.unlockedThisRun.Count > 0;
                achievementsBadge.SetActive(hasNewAchievements);
            }
            
            if (bonusBadge != null && bonusManager != null)
            {
                bonusBadge.SetActive(bonusManager.IsRewardAvailable());
            }
            else if (bonusBadge != null)
            {
                bonusBadge.SetActive(false);
            }
        }

        private void CloseAllPanels()
        {
            if (achievementScreenUI != null) achievementScreenUI.gameObject.SetActive(false);
            if (abilitiesScreenUI != null) abilitiesScreenUI.gameObject.SetActive(false);
            if (metaUpgradeUI != null) metaUpgradeUI.Hide();
            if (dailyBonusScreenUI != null) dailyBonusScreenUI.gameObject.SetActive(false);
            if (playScreenPanel != null) playScreenPanel.SetActive(false);
        }

        private void SelectTab(int index)
        {
            if (tabs == null) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].SetActiveState(i == index);
            }
        }

        public void StartGame()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            Debug.Log("[MainMenuUI] Start Game clicked!");
            SceneManager.LoadScene("Gameplay");
        }

        public void OnPlayClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            Debug.Log("[MainMenuUI] Play tab clicked");
            CloseAllPanels();
            SelectTab(2);
            if (playScreenPanel != null)
            {
                playScreenPanel.SetActive(true);
            }
        }

        public void OnUpgradesClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            Debug.Log("[MainMenuUI] Upgrades clicked");
            CloseAllPanels();
            SelectTab(0);
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
            SelectTab(1);
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
            Debug.Log("[MainMenuUI] Achievements clicked");
            CloseAllPanels();
            SelectTab(3);
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
            Debug.Log("[MainMenuUI] Bonus clicked");
            CloseAllPanels();
            SelectTab(4);
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
