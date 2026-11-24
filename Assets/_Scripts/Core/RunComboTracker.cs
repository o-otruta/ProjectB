using System;
using UnityEngine;
using VContainer;
using ProjectB.Core.Events;
using ProjectB.Data;

namespace ProjectB.Core
{
    /// <summary>
    /// Tracks consecutive enemy kills within a rolling configurable window and grants milestone XP/Coin bonuses.
    /// Zero-GC during steady-state RegisterKill and Tick calls.
    /// </summary>
    public sealed class RunComboTracker
    {
        public const float COMBO_WINDOW_SECONDS = 3.0f;
        public const int MILESTONE_1 = 15;
        public const int MILESTONE_2 = 30;
        public const int MILESTONE_3 = 50;
        public const int MILESTONE_4 = 100;

        private readonly GameEventBus eventBus;
        private readonly RunStatistics runStatistics;
        private ComboConfig comboConfig;
        private bool triedLoadDefaultConfig;

        public int CurrentCombo { get; private set; }
        public int BestCombo { get; private set; }
        public float RemainingTime { get; private set; }
        public float ComboWindowSeconds
        {
            get
            {
                ComboConfig cfg = ResolveConfig();
                return cfg != null && cfg.comboWindowSeconds > 0.05f ? cfg.comboWindowSeconds : COMBO_WINDOW_SECONDS;
            }
        }
        public float NormalizedRemainingTime => CurrentCombo > 0 ? Mathf.Clamp01(RemainingTime / ComboWindowSeconds) : 0f;
        public int CurrentMilestoneTier { get; private set; }
        public float RewardBonusPercent => GetBonusPercentForTierInstance(CurrentMilestoneTier);
        public float RewardMultiplier => 1f + RewardBonusPercent / 100f;

        public event Action<int, int, float> OnComboChanged;

        [Inject]
        public RunComboTracker(GameEventBus eventBus = null, RunStatistics runStatistics = null)
            : this(eventBus, runStatistics, null)
        {
        }

        public RunComboTracker(GameEventBus eventBus, RunStatistics runStatistics, ComboConfig comboConfig)
        {
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            this.comboConfig = comboConfig;
        }

        public void SetConfig(ComboConfig config)
        {
            comboConfig = config;
        }

        private ComboConfig ResolveConfig()
        {
            if (comboConfig != null) return comboConfig;
            if (!triedLoadDefaultConfig)
            {
                triedLoadDefaultConfig = true;
                comboConfig = Resources.Load<ComboConfig>(ComboConfig.DEFAULT_RESOURCE_PATH);
            }
            return comboConfig;
        }

        public void RegisterKill()
        {
            CurrentCombo++;
            RemainingTime = ComboWindowSeconds;

            if (CurrentCombo > BestCombo)
            {
                BestCombo = CurrentCombo;
                runStatistics?.UpdateBestCombo(BestCombo);
            }

            int previousTier = CurrentMilestoneTier;
            int newTier = GetTierForComboInstance(CurrentCombo);
            CurrentMilestoneTier = newTier;

            float bonusPercent = GetBonusPercentForTierInstance(newTier);
            if (newTier > previousTier)
            {
                eventBus?.Publish(new ComboMilestoneEvent(CurrentCombo, newTier, bonusPercent));
            }

            OnComboChanged?.Invoke(CurrentCombo, CurrentMilestoneTier, bonusPercent);
        }

        public void Tick(float deltaTime)
        {
            if (CurrentCombo <= 0 || deltaTime <= 0f) return;

            RemainingTime -= deltaTime;
            if (RemainingTime <= 0f)
            {
                ResetCombo();
            }
        }

        public void ResetCombo()
        {
            if (CurrentCombo == 0 && RemainingTime <= 0f && CurrentMilestoneTier == 0) return;

            CurrentCombo = 0;
            RemainingTime = 0f;
            CurrentMilestoneTier = 0;
            OnComboChanged?.Invoke(0, 0, 0f);
        }

        public void ResetAll()
        {
            BestCombo = 0;
            ResetCombo();
        }

        private int GetTierForComboInstance(int combo)
        {
            ComboConfig cfg = ResolveConfig();
            return cfg != null ? cfg.GetTierForCombo(combo) : GetTierForCombo(combo);
        }

        private float GetBonusPercentForTierInstance(int tier)
        {
            ComboConfig cfg = ResolveConfig();
            return cfg != null ? cfg.GetBonusPercentForTier(tier) : GetBonusPercentForTier(tier);
        }

        public static int GetTierForCombo(int combo)
        {
            if (combo >= MILESTONE_4) return 4;
            if (combo >= MILESTONE_3) return 3;
            if (combo >= MILESTONE_2) return 2;
            if (combo >= MILESTONE_1) return 1;
            return 0;
        }

        public static float GetBonusPercentForTier(int tier)
        {
            return tier switch
            {
                1 => 10f,
                2 => 15f,
                3 => 20f,
                4 => 30f,
                _ => 0f
            };
        }
    }
}
