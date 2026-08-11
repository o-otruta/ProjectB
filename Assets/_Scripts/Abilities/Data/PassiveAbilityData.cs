using UnityEngine;

namespace ProjectB.Abilities
{
    public abstract class PassiveAbilityData : AbilityData
    {
        [Header("Passive Ability Stats")]
        public float damageMultiplierBonus = 0f;
        public float cooldownReductionBonus = 0f;
        public float moveSpeedBonus = 0f;

        private void OnEnable()
        {
            type = AbilityType.Passive;
        }
    }
}
