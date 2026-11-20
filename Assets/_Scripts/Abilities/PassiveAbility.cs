using UnityEngine;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public abstract class PassiveAbility : AbilityBase
    {
        public PassiveAbilityData PassiveData => Data as PassiveAbilityData;

        protected HeroRuntimeStats runtimeStats;
        protected HeroHealth heroHealth;

        public void SetContext(HeroRuntimeStats stats, HeroHealth health)
        {
            runtimeStats = stats;
            heroHealth = health;
        }
    }
}
