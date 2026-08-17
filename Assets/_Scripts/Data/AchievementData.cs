using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "New AchievementData", menuName = "ProjectB/Data/AchievementData")]
    public class AchievementData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string title;
        [TextArea]
        public string description;
        public Sprite icon;

        [Header("Condition")]
        public AchievementConditionType conditionType;
        public int targetValue;
        [Tooltip("Used for specific targets, e.g. Boss ID, Ability ID")]
        public string targetId;

        [Header("Reward")]
        public AchievementRewardType rewardType;
        public int rewardValue;
        [Tooltip("Used for specific rewards, e.g. Ability ID to unlock")]
        public string rewardId;
    }
}
