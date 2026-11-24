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
        [Tooltip("Sprint 13: themed thin XP bar (replaces the Slider when assigned).")]
        [SerializeField] private ProgressBarUI progressBar;
        [Tooltip("Sprint 13: \"LV 2\" label centred inside the XP bar.")]
        [SerializeField] private TMPro.TextMeshProUGUI levelBadgeText;
        private readonly char[] levelBuffer = new char[12];
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
            if (progressBar != null)
            {
                progressBar.SetValue(currentXp, Mathf.Max(1, nextLevelXp));
            }
        }

        private void UpdateLevelText(int newLevel)
        {
            if (levelText != null && newLevel != lastDisplayedLevel)
            {
                lastDisplayedLevel = newLevel;
                levelText.text = GetCachedLevelText(newLevel);
            }
            if (levelBadgeText != null)
            {
                int len = UiFormat.WriteString(levelBuffer, 0, "LV ");
                len = UiFormat.WriteInt(levelBuffer, len, Mathf.Max(0, newLevel));
                if (progressBar != null && progressBar.HasSplitLabel && progressBar.Label == levelBadgeText)
                {
                    // Split-colour label: white over the empty part, dark over the green fill.
                    progressBar.SetLabel(levelBuffer, len);
                    if (Application.isPlaying && newLevel > 1) progressBar.PunchLabel();
                }
                else
                {
                    levelBadgeText.SetCharArray(levelBuffer, 0, len);
                    if (Application.isPlaying && newLevel > 1) UiTween.Punch(levelBadgeText.transform, 0.3f, 0.3f);
                }
            }
        }
    }
}
