using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;

namespace ProjectB.UI
{
    public class AchievementItemUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private GameObject completedMark;
        
        [Header("Colors")]
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color inProgressColor = Color.white;
        [SerializeField] private Color completedColor = new Color(1f, 0.8f, 0.2f); // Золотой

        public void Setup(AchievementData data, int currentProgress, bool isCompleted)
        {
            if (iconImage != null) iconImage.sprite = data.icon;
            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;
            
            if (isCompleted)
            {
                if (progressText != null) progressText.text = "Completed!";
                if (progressBarFill != null) progressBarFill.fillAmount = 1f;
                if (completedMark != null) completedMark.SetActive(true);
                
                if (iconImage != null) iconImage.color = completedColor;
            }
            else
            {
                if (progressText != null) progressText.text = $"{currentProgress} / {data.targetValue}";
                if (progressBarFill != null) progressBarFill.fillAmount = Mathf.Clamp01((float)currentProgress / data.targetValue);
                if (completedMark != null) completedMark.SetActive(false);

                if (iconImage != null) iconImage.color = currentProgress > 0 ? inProgressColor : lockedColor;
            }
        }
    }
}
