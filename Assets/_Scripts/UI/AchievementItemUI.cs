using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;

namespace ProjectB.UI
{
    /// <summary>
    /// Achievement row (Sprint 13.2): icon | title + description | reward chip (ability / hero / slot / coins),
    /// full-width progress bar with "3 / 5" underneath; completed rows get a check mark and are dimmed.
    /// </summary>
    public class AchievementItemUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private GameObject progressBarBackground;
        [SerializeField] private GameObject completedMark;
        [SerializeField] private Button itemButton;

        [Header("Colors (legacy)")]
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color inProgressColor = Color.white;
        [SerializeField] private Color completedColor = new Color(1f, 0.8f, 0.2f);

        [Header("Sprint 13")]
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image rewardChipBg;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image iconFrame;

        public event Action<AchievementData> OnItemClicked;
        private AchievementData currentData;
        private bool hasBoundState;
        private int lastProgress;
        private bool lastCompleted;
        private SystemLanguage lastLanguage;

        public AchievementData Data => currentData;
        public Button ItemButton => itemButton;

        private void Awake()
        {
            if (itemButton != null)
            {
                itemButton.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (itemButton != null)
            {
                itemButton.onClick.RemoveListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            if (currentData != null)
            {
                OnItemClicked?.Invoke(currentData);
            }
        }

        public void Setup(AchievementData data, int currentProgress, bool isCompleted, AbilityCatalog abilityCatalog = null)
        {
            if (data == null) return;

            SystemLanguage currentLanguage = LocalizationService.Current.CurrentLanguage;
            if (hasBoundState
                && ReferenceEquals(currentData, data)
                && lastProgress == currentProgress
                && lastCompleted == isCompleted
                && lastLanguage == currentLanguage)
            {
                return;
            }

            bool dataOrLanguageChanged = !hasBoundState || !ReferenceEquals(currentData, data) || lastLanguage != currentLanguage;
            currentData = data;
            lastProgress = currentProgress;
            lastCompleted = isCompleted;
            lastLanguage = currentLanguage;
            hasBoundState = true;

            UiTheme theme = UiTheme.Current;
            bool themed = rewardText != null;

            if (dataOrLanguageChanged)
            {
                if (iconImage != null && iconImage.sprite != data.icon) iconImage.sprite = data.icon;
                if (titleText != null) SetTextIfChanged(titleText, Loc.Title(data));
                if (descriptionText != null) SetTextIfChanged(descriptionText, Loc.Desc(data));

                if (rewardText != null) SetTextIfChanged(rewardText, RewardDisplayFormatter.FormatRewardShort(data, abilityCatalog));
                if (rewardIcon != null)
                {
                    Sprite s = RewardDisplayFormatter.ResolveRewardIcon(data, abilityCatalog);
                    if (rewardIcon.sprite != s) rewardIcon.sprite = s;
                    bool showRewardIcon = s != null;
                    if (rewardIcon.enabled != showRewardIcon) rewardIcon.enabled = showRewardIcon;
                }
            }

            if (rewardChipBg != null) rewardChipBg.color = UiTheme.WithAlpha(isCompleted ? theme.success : theme.gold, 0.18f);
            if (rewardText != null) rewardText.color = isCompleted ? theme.success : theme.gold;
            if (canvasGroup != null) canvasGroup.alpha = isCompleted ? 0.6f : 1f;
            if (iconFrame != null) iconFrame.color = isCompleted ? theme.gold : (currentProgress > 0 ? theme.accent : theme.outline);

            if (isCompleted)
            {
                if (progressText != null)
                {
                    SetTextIfChanged(progressText, themed
                        ? Loc.Get("ui.achievements.completed", "Completed")
                        : Loc.Get("ui.achievements.completed_exclaim", "Completed!"));
                }
                if (progressBarFill != null)
                {
                    if (progressBarFill.gameObject.activeSelf != themed) progressBarFill.gameObject.SetActive(themed);
                    if (themed)
                    {
                        progressBarFill.fillAmount = 1f;
                        progressBarFill.color = theme.success;
                    }
                }
                if (progressBarBackground != null && progressBarBackground.activeSelf != themed) progressBarBackground.SetActive(themed);
                if (completedMark != null && !completedMark.activeSelf) completedMark.SetActive(true);

                if (iconImage != null) iconImage.color = themed ? Color.white : completedColor;
                if (itemButton != null && !itemButton.interactable) itemButton.interactable = true;
            }
            else
            {
                if (progressText != null) SetTextIfChanged(progressText, $"{Mathf.Min(currentProgress, data.targetValue)} / {data.targetValue}");

                if (progressBarBackground != null && !progressBarBackground.activeSelf) progressBarBackground.SetActive(true);
                if (progressBarFill != null)
                {
                    if (!progressBarFill.gameObject.activeSelf) progressBarFill.gameObject.SetActive(true);
                    progressBarFill.fillAmount = data.targetValue > 0
                        ? Mathf.Clamp01((float)currentProgress / data.targetValue)
                        : 0f;
                    if (themed) progressBarFill.color = theme.accent;
                }

                if (completedMark != null && completedMark.activeSelf) completedMark.SetActive(false);

                if (iconImage != null)
                {
                    iconImage.color = themed
                        ? (currentProgress > 0 ? Color.white : UiTheme.WithAlpha(Color.white, 0.55f))
                        : (currentProgress > 0 ? inProgressColor : lockedColor);
                }
                if (itemButton != null && itemButton.interactable) itemButton.interactable = false;
            }
        }

        private static void SetTextIfChanged(TMP_Text textComponent, string value)
        {
            if (!string.Equals(textComponent.text, value, StringComparison.Ordinal))
            {
                textComponent.text = value;
            }
        }
    }
}
