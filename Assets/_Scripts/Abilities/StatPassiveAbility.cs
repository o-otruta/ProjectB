using UnityEngine;

namespace ProjectB.Abilities
{
    public class StatPassiveAbility : PassiveAbility
    {
        private StatPassiveAbilityData statData;
        private float regenPerSecond;
        private float regenAccumulator;

        public float CurrentRegenPerSecond => regenPerSecond;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            statData = data as StatPassiveAbilityData;
            regenPerSecond = 0f;
            regenAccumulator = 0f;

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
            }
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
