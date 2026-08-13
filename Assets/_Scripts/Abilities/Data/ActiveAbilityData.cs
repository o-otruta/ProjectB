using UnityEngine;

namespace ProjectB.Abilities
{
    public abstract class ActiveAbilityData : AbilityData
    {
        [Header("Active Ability Stats")]
        public float baseDamage = 10f;
        public float baseCooldown = 1f;
        public float baseArea = 1f; // e.g., radius of aura or scale of projectile
        public LayerMask targetLayer;

        private void OnEnable()
        {
            type = AbilityType.Active;
        }
    }
}
