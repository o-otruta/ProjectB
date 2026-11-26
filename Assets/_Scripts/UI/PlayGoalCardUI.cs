using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// Play tab goal card (UI redesign, variant A): best wave so far, progress to the next unconstrained
    /// "Reach wave N" achievement and its reward, so the player always sees what the next run is for.
    /// Refreshed on enable, on save changes and on language change (no per-frame work).
    /// </summary>
    public class PlayGoalCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text bestValueText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image rewardIcon;
        [Tooltip("Whole card: opens the Achievements tab focused on the goal achievement.")]
        [SerializeField] private Button cardButton;

        private SaveManager saveManager;
        private AchievementManager achievementManager;
        private MainMenuUI mainMenu;
        private Action cachedRefresh;
        private string currentGoalId;

        /// <summary>Achievement the card points at (null when every wave reward is collected).</summary>
        public string CurrentGoalId => currentGoalId;

        [Inject]
        public void Construct(SaveManager saveManager = null, AchievementManager achievementManager = null, MainMenuUI mainMenu = null)
        {
            Unsubscribe();
            this.saveManager = saveManager;
            this.achievementManager = achievementManager;
            this.mainMenu = mainMenu;
            if (isActiveAndEnabled)
            {
                Subscribe();
                Refresh();
            }
        }

        private void Awake()
        {
            if (cardButton != null) cardButton.onClick.AddListener(OpenGoal);
        }

        private void OnDestroy()
        {
            if (cardButton != null) cardButton.onClick.RemoveListener(OpenGoal);
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        /// <summary>Jumps to the goal achievement (or the Achievements tab when no wave goal is left).</summary>
        public void OpenGoal()
        {
            if (mainMenu == null) return;
            UiAudio.Click();
            UiTween.Punch(transform, 0.04f, 0.2f);
            mainMenu.OpenAchievement(currentGoalId);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            cachedRefresh ??= Refresh;
            if (saveManager != null)
            {
                saveManager.OnDataChanged -= cachedRefresh;
                saveManager.OnDataChanged += cachedRefresh;
            }
            LocalizationService.Current.LanguageChanged -= cachedRefresh;
            LocalizationService.Current.LanguageChanged += cachedRefresh;
        }

        private void Unsubscribe()
        {
            if (cachedRefresh == null) return;
            if (saveManager != null) saveManager.OnDataChanged -= cachedRefresh;
            LocalizationService.Current.LanguageChanged -= cachedRefresh;
        }

        public void Refresh()
        {
            int best = saveManager != null && saveManager.Data != null ? Mathf.Max(0, saveManager.Data.bestWave) : 0;
            AchievementData goal = achievementManager != null
                ? FindNextWaveGoal(achievementManager.GetAllAchievements(), achievementManager.IsAchievementCompleted, best)
                : null;
            currentGoalId = goal != null ? goal.id : null;

            if (goal == null)
            {
                if (bestValueText != null) bestValueText.text = Loc.Format("ui.play.best_wave", best);
                if (progressFill != null) progressFill.fillAmount = best > 0 ? 1f : 0f;
                if (rewardText != null) rewardText.text = Loc.Get("ui.play.goals_done", "All wave rewards collected");
                if (rewardIcon != null) rewardIcon.enabled = false;
                return;
            }

            int target = Mathf.Max(1, goal.targetValue);
            if (bestValueText != null)
            {
                bestValueText.text = Core.LocalizationService.Current.ActiveEntries.ContainsKey("ui.play.wave_progress_dim")
                    ? Loc.Format("ui.play.wave_progress_dim", best, target)
                    : Loc.Format("ui.play.wave_progress", best, target);
            }
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01((float)best / target);
            if (rewardText != null)
            {
                rewardText.text = Loc.Format("ui.play.next_reward", target,
                    $"<color=#{ColorUtility.ToHtmlStringRGB(UiTheme.Current.gold)}>{RewardDisplayFormatter.FormatRewardShort(goal)}</color>");
            }
            if (rewardIcon != null)
            {
                Sprite icon = RewardDisplayFormatter.ResolveRewardIcon(goal);
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
            }
        }

        /// <summary>
        /// The nearest not-yet-completed "Reach wave N" achievement without run constraints (hero, ability limits,
        /// no rerolls, time limit), preferring targets above <paramref name="bestWave"/>; null when none is left.
        /// </summary>
        public static AchievementData FindNextWaveGoal(IReadOnlyList<AchievementData> achievements, Func<string, bool> isCompleted, int bestWave)
        {
            if (achievements == null) return null;
            AchievementData above = null;
            AchievementData any = null;
            for (int i = 0; i < achievements.Count; i++)
            {
                AchievementData a = achievements[i];
                if (a == null || a.conditionType != AchievementConditionType.ReachWave) continue;
                if (a.runConstraint != null && a.runConstraint.HasAny) continue;
                if (isCompleted != null && isCompleted(a.id)) continue;

                if (any == null || a.targetValue < any.targetValue) any = a;
                if (a.targetValue > bestWave && (above == null || a.targetValue < above.targetValue)) above = a;
            }
            return above ?? any;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text best, Image fill, TMP_Text reward, Image icon, Button button)
        {
            bestValueText = best;
            progressFill = fill;
            rewardText = reward;
            rewardIcon = icon;
            cardButton = button;
        }
#endif
    }
}
