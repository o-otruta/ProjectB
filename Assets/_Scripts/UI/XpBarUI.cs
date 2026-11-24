using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    public class XpBarUI : MonoBehaviour
    {
        private static readonly string[] CachedLevelLabels = BuildLevelLabels("Lv ");
        private static SystemLanguage cachedLevelLabelsLang = SystemLanguage.English;

        private LevelUp.HeroExperience heroExperience;
        [SerializeField] private Slider xpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI levelText;
        [Tooltip("Sprint 13: themed thin XP bar (replaces the Slider when assigned).")]
        [SerializeField] private ProgressBarUI progressBar;
        [Tooltip("Sprint 13: \"LV 2\" label centred inside the XP bar.")]
        [SerializeField] private TMPro.TextMeshProUGUI levelBadgeText;
        private readonly char[] levelBuffer = new char[20];
        private bool isInitialized = false;
        private int lastDisplayedLevel = int.MinValue;
        private string cachedLvUpperPrefix = "LV ";
        private Action cachedLanguageChanged;

        private static string[] BuildLevelLabels(string prefix)
        {
            var arr = new string[201];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = $"{prefix}{i}";
            }
            return arr;
        }

        public static string GetCachedLevelText(int level)
        {
            SystemLanguage lang = LocalizationService.Current.CurrentLanguage;
            if (cachedLevelLabelsLang != lang)
            {
                cachedLevelLabelsLang = lang;
                string prefix = Loc.Get("ui.hud.lv_prefix", "Lv ");
                for (int i = 0; i < CachedLevelLabels.Length; i++)
                {
                    CachedLevelLabels[i] = $"{prefix}{i}";
                }
            }

            if (level >= 0 && level < CachedLevelLabels.Length)
            {
                return CachedLevelLabels[level];
            }
            return Loc.Format("ui.hud.lv_title", level);
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
            RefreshPrefix();
            cachedLanguageChanged ??= HandleLanguageChanged;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
            if (isInitialized)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
            if (heroExperience != null)
            {
                heroExperience.OnXPChanged -= UpdateXpBar;
                heroExperience.OnLevelUp -= UpdateLevelText;
            }
        }

        private void HandleLanguageChanged()
        {
            RefreshPrefix();
            lastDisplayedLevel = int.MinValue;
            if (heroExperience != null)
            {
                UpdateLevelText(heroExperience.CurrentLevel);
            }
        }

        private void RefreshPrefix()
        {
            cachedLvUpperPrefix = Loc.Get("ui.hud.lv_upper_prefix", "LV ");
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
                int len = UiFormat.WriteString(levelBuffer, 0, cachedLvUpperPrefix);
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
