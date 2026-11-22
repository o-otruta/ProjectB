using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;
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

        protected float GetEffectiveCooldown(float baseCooldown, float minCooldown = 0.1f)
        {
            return runtimeStats != null
                ? runtimeStats.GetEffectiveCooldown(baseCooldown, minCooldown)
                : Mathf.Max(minCooldown, baseCooldown);
        }

        protected int RollScaledDamage(float rawDamage, out bool isCrit)
        {
            float scaled = GetScaledDamage(rawDamage);
            if (runtimeStats != null)
            {
                return runtimeStats.RollDamage(scaled, out isCrit);
            }

            isCrit = false;
            return scaled > 0f ? Mathf.Max(1, Mathf.RoundToInt(scaled)) : 0;
        }

        protected static void DealDamage(IDamageable target, int damage, bool isCrit)
        {
            if (target == null || target.IsDead || damage <= 0) return;
            if (target is EnemyBase enemy)
            {
                enemy.TakeDamage(damage, isCrit);
            }
            else
            {
                target.TakeDamage(damage);
            }
        }
    }
}
