using UnityEngine;
using TMPro;
using VContainer;
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
        [SerializeField] private AchievementScreenUI achievementScreenUI;
        [SerializeField] private DailyBonusScreenUI dailyBonusScreenUI;
        [SerializeField] private BottomTabUI[] tabs;
        [SerializeField] private GameObject playScreenPanel;

        private SaveManager saveManager;
        private DailyBonusManager bonusManager;

        [Inject]
        public void Construct(SaveManager saveManager, DailyBonusManager bonusManager)
        {
            this.saveManager = saveManager;
            this.bonusManager = bonusManager;
            
            if (this.saveManager != null)
            {
                this.saveManager.OnDataChanged += UpdateCoinsDisplay;
                UpdateCoinsDisplay();
            }

            if (this.bonusManager != null)
            {
                this.bonusManager.OnBonusStateChanged += UpdateBadges;
            }
        }

        private void Start()
        {
            UpdateBadges();
            
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
            // Placeholder logic for badges
            if (achievementsBadge != null) achievementsBadge.SetActive(false);
            
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
            Debug.Log("[MainMenuUI] Start Game clicked!");
            SceneManager.LoadScene("Gameplay");
        }

        public void OnPlayClicked()
        {
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
            Debug.Log("[MainMenuUI] Abilities clicked (Placeholder)");
            CloseAllPanels();
            SelectTab(1);
        }

        public void OnAchievementsClicked()
        {
            Debug.Log("[MainMenuUI] Achievements clicked");
            CloseAllPanels();
            SelectTab(3);
            if (achievementScreenUI != null)
            {
                achievementScreenUI.gameObject.SetActive(true);
            }
        }

        public void OnBonusClicked()
        {
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
            Debug.Log("[MainMenuUI] Settings clicked (Placeholder)");
        }
    }
}
