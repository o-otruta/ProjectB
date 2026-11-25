using System;
using UnityEngine;

namespace ProjectB.Data
{
    /// <summary>
    /// Optional run restrictions for <see cref="AchievementConditionType.ReachWave"/> achievements
    /// ("reach W15 with a single active ability", "reach W10 within 4 minutes", "reach W30 as the Ranger").
    /// </summary>
    [Serializable]
    public class AchievementRunConstraint
    {
        [Tooltip("Hero id the run must use; empty = any hero")]
        public string heroId = "";
        [Tooltip("Max active abilities owned during the run; -1 = no limit")]
        public int maxActiveAbilities = -1;
        [Tooltip("Max passive abilities owned during the run; -1 = no limit")]
        public int maxPassiveAbilities = -1;
        [Tooltip("The run must not use any card reroll")]
        public bool noRerolls;
        [Tooltip("The wave must be reached within this many seconds of run time; 0 = no limit")]
        public int maxRunSeconds;

        public bool HasAny =>
            !string.IsNullOrEmpty(heroId) ||
            maxActiveAbilities >= 0 ||
            maxPassiveAbilities >= 0 ||
            noRerolls ||
            maxRunSeconds > 0;

        public bool IsSatisfiedBy(in AchievementRunContext context)
        {
            if (!context.IsValid) return false;
            if (!string.IsNullOrEmpty(heroId) && !string.Equals(heroId, context.HeroId, StringComparison.Ordinal)) return false;
            if (maxActiveAbilities >= 0 && context.ActiveAbilities > maxActiveAbilities) return false;
            if (maxPassiveAbilities >= 0 && context.PassiveAbilities > maxPassiveAbilities) return false;
            if (noRerolls && context.UsedReroll) return false;
            if (maxRunSeconds > 0 && context.RunSeconds > maxRunSeconds) return false;
            return true;
        }
    }

    /// <summary>Snapshot of the current run used to evaluate <see cref="AchievementRunConstraint"/>.</summary>
    public readonly struct AchievementRunContext
    {
        public readonly bool IsValid;
        public readonly string HeroId;
        public readonly int ActiveAbilities;
        public readonly int PassiveAbilities;
        public readonly bool UsedReroll;
        public readonly float RunSeconds;

        public AchievementRunContext(string heroId, int activeAbilities, int passiveAbilities, bool usedReroll, float runSeconds)
        {
            IsValid = true;
            HeroId = heroId;
            ActiveAbilities = activeAbilities;
            PassiveAbilities = passiveAbilities;
            UsedReroll = usedReroll;
            RunSeconds = runSeconds;
        }
    }

    [CreateAssetMenu(fileName = "New AchievementData", menuName = "ProjectB/Data/AchievementData")]
    public class AchievementData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string title;
        [TextArea]
        public string description;
        public AchievementCategory category;
        [Tooltip("Order inside the category on the Achievements screen")]
        public int sortOrder;
        [Header("Localization")]
        public string nameKey;
        public string descriptionKey;
        public Sprite icon;

        [Header("Condition")]
        public AchievementConditionType conditionType;
        public int targetValue;
        [Tooltip("Used for specific targets, e.g. Boss ID, Ability ID, WaveEventType name")]
        public string targetId;
        [Tooltip("Extra numeric parameter: HP percent for BossAtLowHp, wave for HeroesReachedWave")]
        public int conditionParam;
        [Tooltip("Run restrictions (ReachWave only)")]
        public AchievementRunConstraint runConstraint = new AchievementRunConstraint();

        [Header("Reward")]
        public AchievementRewardType rewardType;
        public int rewardValue;
        [Tooltip("Used for specific rewards, e.g. Ability ID to unlock")]
        public string rewardId;
    }
}
