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
        private readonly GameEventBus eventBus;
        private readonly RunStatistics runStatistics;
        private ComboConfig comboConfig;

        public int CurrentCombo { get; private set; }
        public int BestCombo { get; private set; }
        public float RemainingTime { get; private set; }
        public float ComboWindowSeconds => comboConfig != null && comboConfig.comboWindowSeconds > 0.05f
            ? comboConfig.comboWindowSeconds
            : ComboConfig.DEFAULT_COMBO_WINDOW_SECONDS;
        public float NormalizedRemainingTime => CurrentCombo > 0 ? Mathf.Clamp01(RemainingTime / ComboWindowSeconds) : 0f;
        public int CurrentMilestoneTier { get; private set; }
        public float RewardBonusPercent => GetBonusPercentForTierInstance(CurrentMilestoneTier);
        public float RewardMultiplier => 1f + RewardBonusPercent / 100f;

        public event Action<int, int, float> OnComboChanged;

        [Inject]
        public RunComboTracker(GameEventBus eventBus = null, RunStatistics runStatistics = null, ComboConfig comboConfig = null)
        {
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            this.comboConfig = comboConfig;
        }

        public void SetConfig(ComboConfig config)
        {
            comboConfig = config;
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
            return comboConfig != null ? comboConfig.GetTierForCombo(combo) : ComboConfig.GetDefaultTierForCombo(combo);
        }

        private float GetBonusPercentForTierInstance(int tier)
        {
            return comboConfig != null ? comboConfig.GetBonusPercentForTier(tier) : ComboConfig.GetDefaultBonusPercentForTier(tier);
        }
    }
}
