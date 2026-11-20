using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;
using TMPro;

namespace ProjectB.UI
{
    public class AchievementPopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float delayBetweenPopups = 0.25f;

        private AchievementManager achievementManager;
        private readonly Queue<AchievementData> popupQueue = new Queue<AchievementData>();
        private Coroutine displayCoroutine;
        private WaitForSecondsRealtime displayWait;
        private WaitForSecondsRealtime betweenWait;

        [Inject]
        public void Construct(AchievementManager achievementManager)
        {
            if (this.achievementManager != null)
            {
                this.achievementManager.OnAchievementUnlocked -= EnqueuePopup;
            }

            this.achievementManager = achievementManager;

            if (this.achievementManager != null)
            {
                this.achievementManager.OnAchievementUnlocked += EnqueuePopup;
            }
        }

        private void Awake()
        {
            displayWait = new WaitForSecondsRealtime(Mathf.Max(0.5f, displayDuration));
            betweenWait = new WaitForSecondsRealtime(Mathf.Max(0.05f, delayBetweenPopups));
            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void OnDisable()
        {
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
                displayCoroutine = null;
            }
            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked -= EnqueuePopup;
            }
        }

        private void EnqueuePopup(AchievementData data)
        {
            if (data == null || popupPanel == null) return;

            popupQueue.Enqueue(data);

            if (displayCoroutine == null && isActiveAndEnabled)
            {
                displayCoroutine = StartCoroutine(ProcessQueueRoutine());
            }
        }

        private IEnumerator ProcessQueueRoutine()
        {
            while (popupQueue.Count > 0)
            {
                AchievementData data = popupQueue.Dequeue();
                ApplyPopupData(data);

                popupPanel.SetActive(true);
                yield return displayWait;
                popupPanel.SetActive(false);

                if (popupQueue.Count > 0)
                {
                    yield return betweenWait;
                }
            }

            displayCoroutine = null;
        }

        private void ApplyPopupData(AchievementData data)
        {
            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                iconImage.enabled = data.icon != null;
            }

            if (titleText != null)
            {
                titleText.text = data.title;
            }

            if (subtitleText != null)
            {
                subtitleText.text = FormatSubtitle(data);
            }
        }

        private static string FormatSubtitle(AchievementData data)
        {
            switch (data.rewardType)
            {
                case AchievementRewardType.UnlockAbility:
                    return !string.IsNullOrEmpty(data.rewardId)
                        ? $"Unlocked: {data.rewardId}"
                        : "New Ability Unlocked!";
                case AchievementRewardType.UnlockHero:
                    return !string.IsNullOrEmpty(data.rewardId)
                        ? $"Hero Unlocked: {data.rewardId}"
                        : "New Hero Unlocked!";
                case AchievementRewardType.GiveCoins:
                    return $"Achievement Unlocked! +{data.rewardValue} Coins";
                default:
                    return "Achievement Unlocked!";
            }
        }
    }
}
