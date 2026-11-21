using UnityEngine;
using ProjectB.Core.Events;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public abstract class ActiveAbility : AbilityBase
    {
        public ActiveAbilityData ActiveData => Data as ActiveAbilityData;
        protected HeroRuntimeStats runtimeStats;
        protected GameEventBus eventBus;

        public void SetRuntimeStats(HeroRuntimeStats stats)
        {
            runtimeStats = stats;
        }

        public void SetEventBus(GameEventBus bus)
        {
            eventBus = bus;
        }

        protected float GetScaledDamage(float rawDamage)
        {
            float multiplier = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            return rawDamage * multiplier;
        }
    }
}
