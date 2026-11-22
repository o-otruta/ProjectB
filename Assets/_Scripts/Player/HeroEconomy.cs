using UnityEngine;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;

namespace ProjectB.Player
{
    public class HeroEconomy : MonoBehaviour
    {
        private RunStatistics runStatistics;
        private GameEventBus eventBus;
        private HeroRuntimeStats runtimeStats;
        private RunComboTracker comboTracker;

        [Inject]
        public void Construct(
            RunStatistics runStatistics,
            GameEventBus eventBus,
            HeroRuntimeStats runtimeStats = null,
            RunComboTracker comboTracker = null)
        {
            this.runStatistics = runStatistics;
            this.eventBus = eventBus;
            this.runtimeStats = runtimeStats;
            this.comboTracker = comboTracker;
        }

        public void AddCoin(int amount)
        {
            if (amount <= 0) return;
            int finalAmount = runtimeStats != null ? runtimeStats.ApplyCoinMultiplier(amount) : amount;
            if (comboTracker != null && comboTracker.RewardMultiplier > 1f)
            {
                finalAmount = Mathf.Max(1, Mathf.RoundToInt(finalAmount * comboTracker.RewardMultiplier));
            }
            if (runStatistics != null && finalAmount > 0)
            {
                runStatistics.AddCoin(finalAmount);
                eventBus?.Publish(new CoinCollectedEvent(finalAmount));
            }
        }
    }
}
