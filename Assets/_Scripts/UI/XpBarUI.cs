using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectB.UI
{
    public class XpBarUI : MonoBehaviour
    {
        private static readonly string[] CachedLevelLabels = BuildLevelLabels(201);

        private LevelUp.HeroExperience heroExperience;
        [SerializeField] private Slider xpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI levelText;
        private bool isInitialized = false;
        private int lastDisplayedLevel = int.MinValue;

        private static string[] BuildLevelLabels(int count)
        {
            var arr = new string[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = $"Lv {i}";
            }
            return arr;
        }

        public static string GetCachedLevelText(int level)
        {
            if (level >= 0 && level < CachedLevelLabels.Length)
            {
                return CachedLevelLabels[level];
            }
            return $"Lv {level}";
        }

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
            if (levelText != null && newLevel != lastDisplayedLevel)
            {
                lastDisplayedLevel = newLevel;
                levelText.text = GetCachedLevelText(newLevel);
            }
        }
    }
}
