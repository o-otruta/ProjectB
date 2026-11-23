using UnityEngine;
using ProjectB.Player;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    public abstract class PassiveAbility : AbilityBase
    {
        public PassiveAbilityData PassiveData => Data as PassiveAbilityData;

        protected HeroRuntimeStats runtimeStats;
        protected HeroHealth heroHealth;
        protected GameEventBus eventBus;

        public void SetEventBus(GameEventBus bus)
        {
            eventBus = bus;
        }

        public void SetContext(HeroRuntimeStats stats, HeroHealth health)
        {
            runtimeStats = stats;
            heroHealth = health;
        }
    }
}
