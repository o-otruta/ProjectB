using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;

namespace ProjectB.UI
{
    public class AchievementRewardPopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Image rewardIconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }

        public void ShowReward(AchievementData achievement)
        {
            if (achievement == null) return;
            
            if (titleText != null) titleText.text = "Reward Unlocked!";
            
            if (descriptionText != null)
            {
                switch (achievement.rewardType)
                {
                    case AchievementRewardType.GiveCoins:
                        descriptionText.text = $"+{achievement.rewardValue} Coins";
                        break;
                    case AchievementRewardType.UnlockAbility:
                        // In future, you might want to show the ability name instead of a hardcoded string
                        descriptionText.text = "New Ability Unlocked!";
                        break;
                    case AchievementRewardType.UnlockHero:
                        descriptionText.text = "New Hero Unlocked!";
                        break;
                    default:
                        descriptionText.text = "Reward Unlocked!";
                        break;
                }
            }

            if (rewardIconImage != null)
            {
                // In future, you could get a specific reward icon, for now just use the achievement icon
                rewardIconImage.sprite = achievement.icon;
            }

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }
    }
}
