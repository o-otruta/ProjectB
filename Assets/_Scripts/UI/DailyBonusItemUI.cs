using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Core;
using ProjectB.Data.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// One day cell of the Daily Bonus (Sprint 13.2): collected (check + dimmed), today (accent glow + pulse),
    /// future (darkened). Coin amounts use the coin icon.
    /// </summary>
    public class DailyBonusItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private GameObject claimedOverlay;
        [SerializeField] private GameObject currentDayHighlight;

        [Header("Sprint 13")]
        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private PulseUI todayPulse;

        public bool IsClaimed { get; private set; }
        public bool IsToday { get; private set; }

        public void Setup(int dayIndex, DailyBonusReward reward, int coinsAmount, bool isClaimed, bool isCurrentDay)
        {
            UiTheme theme = UiTheme.Current;
            IsClaimed = isClaimed;
            IsToday = isCurrentDay;

            if (dayText != null) dayText.text = Loc.Format("ui.daily.day", dayIndex);

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
                    amountText.text = $"<sprite name=\"coin\"> {UiFormat.Grouped(coinsAmount)}";
                    amountText.gameObject.SetActive(true);
                }
                else
                {
                    amountText.gameObject.SetActive(false);
                }
            }

            if (claimedOverlay != null) claimedOverlay.SetActive(isClaimed);
            if (currentDayHighlight != null) currentDayHighlight.SetActive(isCurrentDay);
            if (todayPulse != null) todayPulse.enabled = isCurrentDay;

            if (background != null)
            {
                background.color = isCurrentDay ? theme.surfaceRaised : (isClaimed ? theme.surfaceSunken : theme.surface);
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isClaimed ? 0.55f : (isCurrentDay ? 1f : 0.8f);
            }
            if (dayText != null) dayText.color = isCurrentDay ? theme.gold : theme.textSecondary;
        }
    }
}
