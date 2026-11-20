using UnityEngine;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public abstract class ActiveAbility : AbilityBase
    {
        public ActiveAbilityData ActiveData => Data as ActiveAbilityData;
        protected HeroRuntimeStats runtimeStats;

        public void SetRuntimeStats(HeroRuntimeStats stats)
        {
            runtimeStats = stats;
        }

        protected float GetScaledDamage(float rawDamage)
        {
            float multiplier = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            return rawDamage * multiplier;
        }
    }
}
