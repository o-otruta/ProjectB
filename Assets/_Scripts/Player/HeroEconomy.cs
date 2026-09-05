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

        [Inject]
        public void Construct(RunStatistics runStatistics, GameEventBus eventBus)
        {
            this.runStatistics = runStatistics;
            this.eventBus = eventBus;
        }

        public void AddCoin(int amount)
        {
            if (runStatistics != null)
            {
                runStatistics.AddCoin(amount);
                eventBus?.Publish(new CoinCollectedEvent(amount));
            }
        }
    }
}
