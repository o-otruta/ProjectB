using System;
using UnityEngine;
using VContainer;
using ProjectB.Core.Events;

namespace ProjectB.Core
{
    /// <summary>
    /// Tracks consecutive enemy kills within a rolling 3.0s window and grants milestone XP/Coin bonuses.
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

        public int CurrentCombo { get; private set; }
        public int BestCombo { get; private set; }
        public float RemainingTime { get; private set; }
        public float NormalizedRemainingTime => CurrentCombo > 0 ? Mathf.Clamp01(RemainingTime / COMBO_WINDOW_SECONDS) : 0f;
        public int CurrentMilestoneTier { get; private set; }
        public float RewardBonusPercent => GetBonusPercentForTier(CurrentMilestoneTier);
        public float RewardMultiplier => 1f + RewardBonusPercent / 100f;

        public event Action<int, int, float> OnComboChanged;

        [Inject]
        public RunComboTracker(GameEventBus eventBus = null, RunStatistics runStatistics = null)
        {
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
        }

        public void RegisterKill()
        {
            CurrentCombo++;
            RemainingTime = COMBO_WINDOW_SECONDS;

            if (CurrentCombo > BestCombo)
            {
                BestCombo = CurrentCombo;
                runStatistics?.UpdateBestCombo(BestCombo);
            }

            int previousTier = CurrentMilestoneTier;
            int newTier = GetTierForCombo(CurrentCombo);
            CurrentMilestoneTier = newTier;

            float bonusPercent = GetBonusPercentForTier(newTier);
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
