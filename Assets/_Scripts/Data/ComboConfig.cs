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
        public const float DEFAULT_COMBO_WINDOW_SECONDS = 3.0f;

        private static readonly ComboMilestoneConfig[] DefaultMilestones = new ComboMilestoneConfig[]
        {
            new ComboMilestoneConfig { killsRequired = 15, rewardBonusPercent = 10f },
            new ComboMilestoneConfig { killsRequired = 30, rewardBonusPercent = 15f },
            new ComboMilestoneConfig { killsRequired = 50, rewardBonusPercent = 20f },
            new ComboMilestoneConfig { killsRequired = 100, rewardBonusPercent = 30f }
        };

        private static ComboMilestoneConfig[] CreateDefaultMilestones()
        {
            return (ComboMilestoneConfig[])DefaultMilestones.Clone();
        }

        [Min(0.1f)]
        [Tooltip("Окно в секундах до сброса счётчика комбо.")]
        public float comboWindowSeconds = DEFAULT_COMBO_WINDOW_SECONDS;

        [Tooltip("Отсортированные по возрастанию вехи комбо и их бонусы в % к XP и монетам.")]
        public ComboMilestoneConfig[] milestones = CreateDefaultMilestones();

        public int GetTierForCombo(int combo) => GetTierForCombo(milestones, combo);

        public float GetBonusPercentForTier(int tier) => GetBonusPercentForTier(milestones, tier);

        public static int GetDefaultTierForCombo(int combo) => GetTierForCombo(DefaultMilestones, combo);

        public static float GetDefaultBonusPercentForTier(int tier) => GetBonusPercentForTier(DefaultMilestones, tier);

        private static int GetTierForCombo(ComboMilestoneConfig[] list, int combo)
        {
            if (list == null || list.Length == 0 || combo <= 0) return 0;

            int tier = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (combo >= list[i].killsRequired)
                {
                    tier = i + 1;
                }
            }
            return tier;
        }

        private static float GetBonusPercentForTier(ComboMilestoneConfig[] list, int tier)
        {
            if (list == null || tier <= 0 || tier > list.Length) return 0f;
            return Mathf.Max(0f, list[tier - 1].rewardBonusPercent);
        }
    }
}
