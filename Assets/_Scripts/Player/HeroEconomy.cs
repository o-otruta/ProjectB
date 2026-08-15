using UnityEngine;
using VContainer;
using ProjectB.Core;

namespace ProjectB.Player
{
    public class HeroEconomy : MonoBehaviour
    {
        private RunStatistics runStatistics;

        [Inject]
        public void Construct(RunStatistics runStatistics)
        {
            this.runStatistics = runStatistics;
        }

        public void AddCoin(int amount)
        {
            if (runStatistics != null)
            {
                runStatistics.AddCoin(amount);
                // Можно добавить событие, чтобы UI монет в забеге обновлялся
            }
        }
    }
}
