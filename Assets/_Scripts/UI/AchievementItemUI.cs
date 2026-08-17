using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;
using System;

namespace ProjectB.UI
{
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
        
        [Header("Colors")]
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color inProgressColor = Color.white;
        [SerializeField] private Color completedColor = new Color(1f, 0.8f, 0.2f); // Золотой

        public event Action<AchievementData> OnItemClicked;
        private AchievementData currentData;

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

        public void Setup(AchievementData data, int currentProgress, bool isCompleted)
        {
            currentData = data;

            if (iconImage != null) iconImage.sprite = data.icon;
            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;
            
            if (isCompleted)
            {
                if (progressText != null) progressText.text = "Completed!";
                if (progressBarFill != null) progressBarFill.gameObject.SetActive(false);
                if (progressBarBackground != null) progressBarBackground.SetActive(false);
                if (completedMark != null) completedMark.SetActive(true);
                
                if (iconImage != null) iconImage.color = completedColor;
                if (itemButton != null) itemButton.interactable = true;
            }
            else
            {
                if (progressText != null) progressText.text = $"{currentProgress} / {data.targetValue}";
                
                if (progressBarBackground != null) progressBarBackground.SetActive(true);
                if (progressBarFill != null) 
                {
                    progressBarFill.gameObject.SetActive(true);
                    progressBarFill.fillAmount = Mathf.Clamp01((float)currentProgress / data.targetValue);
                }

                if (completedMark != null) completedMark.SetActive(false);

                if (iconImage != null) iconImage.color = currentProgress > 0 ? inProgressColor : lockedColor;
                if (itemButton != null) itemButton.interactable = true;
            }
        }
    }
}
