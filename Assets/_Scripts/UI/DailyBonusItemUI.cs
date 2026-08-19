using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data.Meta;

namespace ProjectB.UI
{
    public class DailyBonusItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private GameObject claimedOverlay;
        [SerializeField] private GameObject currentDayHighlight;

        public void Setup(int dayIndex, DailyBonusReward reward, int coinsAmount, bool isClaimed, bool isCurrentDay)
        {
            if (dayText != null) dayText.text = $"День {dayIndex}";
            
            if (iconImage != null && reward.icon != null)
            {
                iconImage.sprite = reward.icon;
                iconImage.gameObject.SetActive(true);
            }
            else if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }

            if (amountText != null)
            {
                if (reward.rewardType == DailyBonusRewardType.Coins)
                {
                    amountText.text = coinsAmount.ToString();
                    amountText.gameObject.SetActive(true);
                }
                else
                {
                    amountText.gameObject.SetActive(false);
                }
            }

            if (claimedOverlay != null) claimedOverlay.SetActive(isClaimed);
            if (currentDayHighlight != null) currentDayHighlight.SetActive(isCurrentDay);
        }
    }
}
