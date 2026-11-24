using UnityEngine;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Run-scoped combat multipliers for enemies and bosses (replaces global static fields on EnemyBase).
    /// Registered as a scoped service in GameLifetimeScope so values never leak across runs or tests.
    /// </summary>
    public sealed class EnemyRuntimeModifiers
    {
        private float eventSpeedMultiplier = 1f;
        private float bossDamageTakenMultiplier = 1f;
        private float mutatorEnemyHpMultiplier = 1f;
        private float mutatorEnemyCountMultiplier = 1f;
        private float mutatorEliteChanceMultiplier = 1f;
        private float mutatorXpMultiplier = 1f;
        private float mutatorCoinMultiplier = 1f;
        private int mutatorMiniBossShardMultiplier = 1;

        public float EventSpeedMultiplier
        {
            get => eventSpeedMultiplier;
            set => eventSpeedMultiplier = Mathf.Max(0.1f, value);
        }

        public float BossDamageTakenMultiplier
        {
            get => bossDamageTakenMultiplier;
            set => bossDamageTakenMultiplier = Mathf.Max(0.1f, value);
        }

        public float MutatorEnemyHpMultiplier
        {
            get => mutatorEnemyHpMultiplier;
            set => mutatorEnemyHpMultiplier = Mathf.Max(0.1f, value);
        }

        public float MutatorEnemyCountMultiplier
        {
            get => mutatorEnemyCountMultiplier;
            set => mutatorEnemyCountMultiplier = Mathf.Max(0.1f, value);
        }

        public float MutatorEliteChanceMultiplier
        {
            get => mutatorEliteChanceMultiplier;
            set => mutatorEliteChanceMultiplier = Mathf.Max(0f, value);
        }

        public float MutatorXpMultiplier
        {
            get => mutatorXpMultiplier;
            set => mutatorXpMultiplier = Mathf.Max(0.1f, value);
        }

        public float MutatorCoinMultiplier
        {
            get => mutatorCoinMultiplier;
            set => mutatorCoinMultiplier = Mathf.Max(0.1f, value);
        }

        public int MutatorMiniBossShardMultiplier
        {
            get => mutatorMiniBossShardMultiplier;
            set => mutatorMiniBossShardMultiplier = Mathf.Max(1, value);
        }

        public void ResetMutatorModifiers()
        {
            mutatorEnemyHpMultiplier = 1f;
            mutatorEnemyCountMultiplier = 1f;
            mutatorEliteChanceMultiplier = 1f;
            mutatorXpMultiplier = 1f;
            mutatorCoinMultiplier = 1f;
            mutatorMiniBossShardMultiplier = 1;
        }

        public void Reset()
        {
            eventSpeedMultiplier = 1f;
            bossDamageTakenMultiplier = 1f;
            ResetMutatorModifiers();
        }
    }
}
