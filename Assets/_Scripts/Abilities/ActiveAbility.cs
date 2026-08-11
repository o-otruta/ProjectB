using UnityEngine;

namespace ProjectB.Abilities
{
    public abstract class ActiveAbility : AbilityBase
    {
        public ActiveAbilityData ActiveData => Data as ActiveAbilityData;
        
        // Example structure for active abilities
        // You might want a reference to HeroStats here later
    }
}
