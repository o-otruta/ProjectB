using UnityEngine;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    public class StatPassiveAbility : PassiveAbility
    {
        private StatPassiveAbilityData statData;
        private float regenPerSecond;
        private float regenAccumulator;
        private int healPerTrigger;
        private int killsSinceHeal;
        private bool subscribedToKills;

        public float CurrentRegenPerSecond => regenPerSecond;
        public int HealPerTrigger => healPerTrigger;
        public int KillsPerTrigger => statData != null ? Mathf.Max(1, statData.killsPerTrigger) : 8;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            statData = data as StatPassiveAbilityData;
            regenPerSecond = 0f;
            regenAccumulator = 0f;
            healPerTrigger = 0;
            killsSinceHeal = 0;

            if (statData != null)
            {
                ApplyEffectValue(statData.baseValue);
            }
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            if (type == ModifierType.CritMultiplier)
            {
                runtimeStats?.AddCritMultiplier(value);
                return;
            }

            ApplyEffectValue(value);
        }

        private void ApplyEffectValue(float value)
        {
            if (statData == null || Mathf.Approximately(value, 0f)) return;

            switch (statData.effectType)
            {
                case PassiveEffectType.DamagePercent:
                    runtimeStats?.AddGlobalDamagePercent(value);
                    break;
                case PassiveEffectType.ArmorFlat:
                    runtimeStats?.AddArmor(value);
                    break;
                case PassiveEffectType.MoveSpeedFlat:
                    runtimeStats?.AddMoveSpeed(value);
                    break;
                case PassiveEffectType.HealthRegenPerSecond:
                    regenPerSecond = Mathf.Max(0f, regenPerSecond + value);
                    break;
                case PassiveEffectType.MagnetRadiusFlat:
                    runtimeStats?.AddMagnetRadius(value);
                    break;
                case PassiveEffectType.MaxHpFlat:
                    int hpBonus = Mathf.RoundToInt(value);
                    if (heroHealth != null)
                    {
                        heroHealth.IncreaseMaxHealth(hpBonus);
                    }
                    else
                    {
                        runtimeStats?.AddMaxHp(hpBonus);
                    }
                    break;
                case PassiveEffectType.CooldownReductionPercent:
                    runtimeStats?.AddCooldownReductionPercent(value);
                    break;
                case PassiveEffectType.CritChancePercent:
                    runtimeStats?.AddCritChancePercent(value);
                    break;
                case PassiveEffectType.CoinBonusPercent:
                    runtimeStats?.AddCoinBonusPercent(value);
                    break;
                case PassiveEffectType.AreaPercent:
                    runtimeStats?.AddAreaPercent(value);
                    break;
                case PassiveEffectType.HealOnKills:
                    healPerTrigger = Mathf.Max(0, healPerTrigger + Mathf.RoundToInt(value));
                    EnsureKillSubscription();
                    break;
            }
        }

        private void EnsureKillSubscription()
        {
            if (subscribedToKills || eventBus == null) return;
            eventBus.Subscribe<EnemyDiedEvent>(HandleEnemyDied);
            subscribedToKills = true;
        }

        private void HandleEnemyDied(EnemyDiedEvent evt)
        {
            RegisterKill();
        }

        /// <summary>Counts a kill for Vampiric Fang; heals every <see cref="KillsPerTrigger"/> kills. Returns HP healed.</summary>
        public int RegisterKill()
        {
            if (healPerTrigger <= 0 || heroHealth == null || heroHealth.IsDead) return 0;

            killsSinceHeal++;
            if (killsSinceHeal < KillsPerTrigger) return 0;

            killsSinceHeal = 0;
            int before = heroHealth.CurrentHp;
            heroHealth.Heal(healPerTrigger);
            int healed = heroHealth.CurrentHp - before;
            if (healed > 0)
            {
                eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.VampiricHeal, heroHealth.transform.position, 1f));
            }
            return healed;
        }

        private void OnDestroy()
        {
            if (subscribedToKills && eventBus != null)
            {
                eventBus.Unsubscribe<EnemyDiedEvent>(HandleEnemyDied);
            }
            subscribedToKills = false;
        }

        private void Update()
        {
            if (regenPerSecond <= 0f || Time.deltaTime <= 0f) return;
            if (heroHealth == null || heroHealth.IsDead) return;
            if (heroHealth.CurrentHp >= heroHealth.MaxHp)
            {
                regenAccumulator = 0f;
                return;
            }

            regenAccumulator += regenPerSecond * Time.deltaTime;
            if (regenAccumulator >= 1f)
            {
                int healAmount = Mathf.FloorToInt(regenAccumulator);
                regenAccumulator -= healAmount;
                heroHealth.Heal(healAmount);
            }
        }
    }
}
