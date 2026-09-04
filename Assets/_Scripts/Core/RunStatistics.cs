using UnityEngine;

namespace ProjectB.Core
{
    public class RunStatistics
    {
        public int CoinsEarned { get; private set; }
        public int EnemiesKilled { get; private set; }
        public float PlayTime { get; private set; }

        public RunStatistics()
        {
        }

        public void AddCoin(int amount = 1)
        {
            CoinsEarned += amount;
        }

        public void AddKill()
        {
            EnemiesKilled++;
        }

        public void UpdatePlayTime(float deltaTime)
        {
            PlayTime += deltaTime;
        }

        public void Reset()
        {
            CoinsEarned = 0;
            EnemiesKilled = 0;
            PlayTime = 0f;
        }
    }
}
