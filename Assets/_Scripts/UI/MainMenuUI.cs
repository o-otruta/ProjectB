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

        private SaveManager saveManager;

        [Inject]
        public void Construct(SaveManager saveManager)
        {
            this.saveManager = saveManager;
            
            if (this.saveManager != null)
            {
                this.saveManager.OnDataChanged += UpdateCoinsDisplay;
                UpdateCoinsDisplay();
            }
        }

        private void Start()
        {
            UpdateBadges();
        }

        private void OnDestroy()
        {
            if (saveManager != null)
            {
                saveManager.OnDataChanged -= UpdateCoinsDisplay;
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
            if (bonusBadge != null) bonusBadge.SetActive(false);
        }

        public void OnPlayClicked()
        {
            SceneManager.LoadScene("Gameplay");
        }

        public void OnUpgradesClicked()
        {
            Debug.Log("[MainMenuUI] Upgrades clicked (Placeholder)");
        }

        public void OnAbilitiesClicked()
        {
            Debug.Log("[MainMenuUI] Abilities clicked (Placeholder)");
        }

        public void OnAchievementsClicked()
        {
            Debug.Log("[MainMenuUI] Achievements clicked (Placeholder)");
        }

        public void OnBonusClicked()
        {
            Debug.Log("[MainMenuUI] Bonus clicked (Placeholder)");
        }

        public void OnSettingsClicked()
        {
            Debug.Log("[MainMenuUI] Settings clicked (Placeholder)");
        }
    }
}
