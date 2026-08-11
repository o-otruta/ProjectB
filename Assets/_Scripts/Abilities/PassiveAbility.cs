using UnityEngine;

namespace ProjectB.Abilities
{
    public abstract class PassiveAbility : AbilityBase
    {
        public PassiveAbilityData PassiveData => Data as PassiveAbilityData;
        
        // Passives usually apply their effects via HeroAbilities manager
    }
}
