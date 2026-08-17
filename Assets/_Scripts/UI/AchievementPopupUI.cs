using UnityEngine;
using UnityEngine.UI;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;
using System.Collections;
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

        private AchievementManager achievementManager;

        [Inject]
        public void Construct(AchievementManager achievementManager)
        {
            this.achievementManager = achievementManager;
        }

        private void Start()
        {
            if (popupPanel != null) popupPanel.SetActive(false);

            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked += ShowPopup;
            }
        }

        private void OnDestroy()
        {
            if (achievementManager != null)
            {
                achievementManager.OnAchievementUnlocked -= ShowPopup;
            }
        }

        private void ShowPopup(AchievementData data)
        {
            if (popupPanel == null) return;

            if (iconImage != null) iconImage.sprite = data.icon;
            if (titleText != null) titleText.text = data.title;
            if (subtitleText != null) subtitleText.text = "Achievement Unlocked!";

            StopAllCoroutines();
            StartCoroutine(DisplayRoutine());
        }

        private IEnumerator DisplayRoutine()
        {
            popupPanel.SetActive(true);
            yield return new WaitForSeconds(displayDuration);
            popupPanel.SetActive(false);
        }
    }
}
