using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;

namespace ProjectB.UI
{
    /// <summary>Popup with the unlocked reward of a completed achievement (Sprint 13: real reward icon, pop-in).</summary>
    public class AchievementRewardPopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Image rewardIconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform window;

        public bool IsOpen => popupPanel != null && popupPanel.activeSelf;
        public event System.Action OnClosed;

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
            ShowReward(achievement, null);
        }

        public void ShowReward(AchievementData achievement, AbilityCatalog abilityCatalog)
        {
            if (achievement == null) return;

            if (titleText != null) titleText.text = Loc.Get("ui.achievements.reward_unlocked", "Reward Unlocked!");

            if (descriptionText != null)
            {
                string formatted = RewardDisplayFormatter.FormatReward(achievement, abilityCatalog);
                descriptionText.text = !string.IsNullOrEmpty(formatted)
                    ? $"<b>{Loc.Title(achievement)}</b>\n{formatted}"
                    : Loc.Get("ui.achievements.reward_unlocked", "Reward Unlocked!");
            }

            if (rewardIconImage != null)
            {
                Sprite reward = RewardDisplayFormatter.ResolveRewardIcon(achievement, abilityCatalog);
                rewardIconImage.sprite = reward != null ? reward : achievement.icon;
                rewardIconImage.preserveAspect = true;
            }

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }
            if (window != null)
            {
                UiTween.Scale(window, Vector3.one * 0.85f, Vector3.one, UiTheme.Current.durationSlow, 0f, UiEase.OutBack);
            }
            UiAudio.Confirm();
            UiFocusHelper.SetFirstSelected(closeButton);
        }

        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            OnClosed?.Invoke();
        }
    }
}
