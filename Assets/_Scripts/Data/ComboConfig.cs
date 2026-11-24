using System;
using UnityEngine;

namespace ProjectB.Data
{
    [Serializable]
    public struct ComboMilestoneConfig
    {
        [Min(1)]
        public int killsRequired;

        [Min(0f)]
        public float rewardBonusPercent;
    }

    [CreateAssetMenu(fileName = "ComboConfig", menuName = "ProjectB/Data/ComboConfig")]
    public class ComboConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "ComboConfig";

        [Min(0.1f)]
        [Tooltip("Окно в секундах до сброса счётчика комбо.")]
        public float comboWindowSeconds = 3.0f;

        [Tooltip("Отсортированные по возрастанию вехи комбо и их бонусы в % к XP и монетам.")]
        public ComboMilestoneConfig[] milestones = new ComboMilestoneConfig[]
        {
            new ComboMilestoneConfig { killsRequired = 15, rewardBonusPercent = 10f },
            new ComboMilestoneConfig { killsRequired = 30, rewardBonusPercent = 15f },
            new ComboMilestoneConfig { killsRequired = 50, rewardBonusPercent = 20f },
            new ComboMilestoneConfig { killsRequired = 100, rewardBonusPercent = 30f }
        };

        public int GetTierForCombo(int combo)
        {
            if (milestones == null || milestones.Length == 0 || combo <= 0) return 0;

            int tier = 0;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (combo >= milestones[i].killsRequired)
                {
                    tier = i + 1;
                }
            }
            return tier;
        }

        public float GetBonusPercentForTier(int tier)
        {
            if (milestones == null || tier <= 0 || tier > milestones.Length) return 0f;
            return Mathf.Max(0f, milestones[tier - 1].rewardBonusPercent);
        }
    }
}
