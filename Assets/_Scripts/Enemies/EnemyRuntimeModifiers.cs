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

        public void Reset()
        {
            eventSpeedMultiplier = 1f;
            bossDamageTakenMultiplier = 1f;
        }
    }
}
