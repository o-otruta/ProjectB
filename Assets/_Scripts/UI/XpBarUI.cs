using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectB.UI
{
    public class XpBarUI : MonoBehaviour
    {
        private LevelUp.HeroExperience heroExperience;
        [SerializeField] private Slider xpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI levelText;
        private bool isInitialized = false;

        [Inject]
        public void Construct(LevelUp.HeroExperience heroExperience)
        {
            this.heroExperience = heroExperience;
            isInitialized = true;
            if (gameObject.activeInHierarchy)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (heroExperience != null)
            {
                heroExperience.OnXPChanged -= UpdateXpBar;
                heroExperience.OnLevelUp -= UpdateLevelText;
            }
        }

        private void Start()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            if (heroExperience == null) return;

            if (heroExperience != null)
            {
                heroExperience.OnXPChanged -= UpdateXpBar;
                heroExperience.OnLevelUp -= UpdateLevelText;
                
                heroExperience.OnXPChanged += UpdateXpBar;
                heroExperience.OnLevelUp += UpdateLevelText;
                
                UpdateLevelText(heroExperience.CurrentLevel);
                UpdateXpBar(heroExperience.CurrentXP, heroExperience.XPToNextLevel);
            }
        }

        private void UpdateXpBar(int currentXp, int nextLevelXp)
        {
            if (xpSlider != null)
            {
                xpSlider.maxValue = nextLevelXp;
                xpSlider.value = currentXp;
            }
        }

        private void UpdateLevelText(int newLevel)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv {newLevel}";
            }
        }
    }
}
