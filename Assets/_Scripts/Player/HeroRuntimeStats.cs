using System;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Player
{
    /// <summary>
    /// Единая модель рантайм-статов героя на время забега.
    /// Инициализируется от MetaUpgradeManager и пассивки выбранного героя, затем накапливает модификаторы карточек.
    /// </summary>
    public class HeroRuntimeStats
    {
        public const float MAX_COOLDOWN_REDUCTION = 0.5f;
        public const float BASE_CRIT_MULTIPLIER = 1.1f;

        public float MetaHpMultiplier { get; }
        public float MetaSpeedMultiplier { get; }
        public float MetaMagnetMultiplier { get; }
        public float MetaWeaponDamageMultiplier { get; }
        public float MetaAbilityDamageMultiplier { get; }
        public float MetaXpMultiplier { get; }
        public int StartingBonusLevels { get; }

        public float HeroPassiveHpMultiplier { get; private set; } = 1f;
        public float HeroPassiveDamageMultiplier { get; private set; } = 1f;
        public float HeroPassiveSpeedMultiplier { get; private set; } = 1f;
        public float HeroPassiveXpMultiplier { get; private set; } = 1f;
        public float HeroPassiveMagnetMultiplier { get; private set; } = 1f;

        public float XpMultiplier => MetaXpMultiplier * HeroPassiveXpMultiplier;

        public float GlobalDamageMultiplier { get; private set; } = 1f;
        public float Armor { get; private set; } = 0f;
        public float MoveSpeedFlatBonus { get; private set; } = 0f;
        public float MagnetRadiusFlatBonus { get; private set; } = 0f;
        public int MaxHpFlatBonus { get; private set; } = 0;

        public float CooldownReduction { get; private set; } = 0f;
        public float CooldownReductionMultiplier => Mathf.Clamp(1f - CooldownReduction, 0.5f, 1f);
        public float CritChance { get; private set; } = 0f;
        public float CritMultiplier { get; private set; } = BASE_CRIT_MULTIPLIER;
        public float CoinMultiplier { get; private set; } = 1.0f;

        /// <summary>
        /// Итоговый множитель урона базового оружия: мета-множитель * пассивка героя * забеговый GlobalDamage.
        /// </summary>
        public float WeaponDamageMultiplier => MetaWeaponDamageMultiplier * HeroPassiveDamageMultiplier * GlobalDamageMultiplier;

        /// <summary>
        /// Итоговый множитель урона всех активных способностей: мета-множитель * пассивка героя * забеговый GlobalDamage.
        /// </summary>
        public float AbilityDamageMultiplier => MetaAbilityDamageMultiplier * HeroPassiveDamageMultiplier * GlobalDamageMultiplier;

        public event Action OnStatsChanged;

        public HeroRuntimeStats(MetaUpgradeManager metaUpgradeManager = null, SelectedHeroService selectedHeroService = null)
        {
            if (metaUpgradeManager != null)
            {
                MetaHpMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroHP) / 100f;
                MetaSpeedMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroSpeed) / 100f;
                MetaMagnetMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.MagnetRadius) / 100f;
                MetaWeaponDamageMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroDamage) / 100f;
                MetaAbilityDamageMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.AbilityDamage) / 100f;
                MetaXpMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.XPBonus) / 100f;
                StartingBonusLevels = Mathf.Max(0, Mathf.RoundToInt(metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.StartLevel)));
                CritMultiplier = Mathf.Max(1.0f, BASE_CRIT_MULTIPLIER + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.CritMultiplier));
            }
            else
            {
                MetaHpMultiplier = 1f;
                MetaSpeedMultiplier = 1f;
                MetaMagnetMultiplier = 1f;
                MetaWeaponDamageMultiplier = 1f;
                MetaAbilityDamageMultiplier = 1f;
                MetaXpMultiplier = 1f;
                StartingBonusLevels = 0;
                CritMultiplier = BASE_CRIT_MULTIPLIER;
            }

            if (selectedHeroService != null)
            {
                ApplyHeroPassive(selectedHeroService.GetSelectedHero(), notify: false);
            }
        }

        public void ApplyHeroPassive(HeroData heroData, bool notify = true)
        {
            HeroPassiveHpMultiplier = 1f;
            HeroPassiveDamageMultiplier = 1f;
            HeroPassiveSpeedMultiplier = 1f;
            HeroPassiveXpMultiplier = 1f;
            HeroPassiveMagnetMultiplier = 1f;

            if (heroData != null && heroData.passiveValue > 0f)
            {
                switch (heroData.passiveType)
                {
                    case HeroPassiveType.MaxHpPercent:
                        HeroPassiveHpMultiplier = 1f + heroData.passiveValue / 100f;
                        break;
                    case HeroPassiveType.GlobalDamagePercent:
                        HeroPassiveDamageMultiplier = 1f + heroData.passiveValue / 100f;
                        break;
                    case HeroPassiveType.MoveSpeedPercent:
                        HeroPassiveSpeedMultiplier = 1f + heroData.passiveValue / 100f;
                        break;
                    case HeroPassiveType.XpAndMagnetPercent:
                        float factor = 1f + heroData.passiveValue / 100f;
                        HeroPassiveXpMultiplier = factor;
                        HeroPassiveMagnetMultiplier = factor;
                        break;
                    case HeroPassiveType.None:
                    default:
                        break;
                }
            }

            if (notify)
            {
                OnStatsChanged?.Invoke();
            }
        }

        public void AddGlobalDamagePercent(float percent)
        {
            if (Mathf.Approximately(percent, 0f)) return;
            GlobalDamageMultiplier = Mathf.Max(0.1f, GlobalDamageMultiplier + percent / 100f);
            OnStatsChanged?.Invoke();
        }

        public void AddArmor(float amount)
        {
            if (Mathf.Approximately(amount, 0f)) return;
            Armor = Mathf.Max(0f, Armor + amount);
            OnStatsChanged?.Invoke();
        }

        public void AddMoveSpeed(float flatAmount)
        {
            if (Mathf.Approximately(flatAmount, 0f)) return;
            MoveSpeedFlatBonus += flatAmount;
            OnStatsChanged?.Invoke();
        }

        public void AddMagnetRadius(float flatAmount)
        {
            if (Mathf.Approximately(flatAmount, 0f)) return;
            MagnetRadiusFlatBonus += flatAmount;
            OnStatsChanged?.Invoke();
        }

        public void AddMaxHp(int flatAmount)
        {
            if (flatAmount == 0) return;
            MaxHpFlatBonus += flatAmount;
            OnStatsChanged?.Invoke();
        }

        public void AddCooldownReductionPercent(float percent)
        {
            if (Mathf.Approximately(percent, 0f)) return;
            CooldownReduction = Mathf.Clamp(CooldownReduction + percent / 100f, 0f, MAX_COOLDOWN_REDUCTION);
            OnStatsChanged?.Invoke();
        }

        public void AddCritChancePercent(float percent)
        {
            if (Mathf.Approximately(percent, 0f)) return;
            CritChance = Mathf.Clamp01(CritChance + percent / 100f);
            OnStatsChanged?.Invoke();
        }

        public void AddCritMultiplier(float bonusMultiplier)
        {
            if (Mathf.Approximately(bonusMultiplier, 0f)) return;
            CritMultiplier = Mathf.Max(1.0f, CritMultiplier + bonusMultiplier);
            OnStatsChanged?.Invoke();
        }

        public void AddCoinBonusPercent(float percent)
        {
            if (Mathf.Approximately(percent, 0f)) return;
            CoinMultiplier = Mathf.Max(1f, CoinMultiplier + percent / 100f);
            OnStatsChanged?.Invoke();
        }

        public float GetEffectiveCooldown(float baseCooldown, float minCooldown = 0.1f)
        {
            return Mathf.Max(minCooldown, baseCooldown * CooldownReductionMultiplier);
        }

        public int RollDamage(float rawDamage, out bool isCrit)
        {
            if (rawDamage <= 0f)
            {
                isCrit = false;
                return 0;
            }

            isCrit = CritChance > 0f && (CritChance >= 1f || UnityEngine.Random.value < CritChance);
            float finalDamage = isCrit ? rawDamage * CritMultiplier : rawDamage;
            return Mathf.Max(1, Mathf.RoundToInt(finalDamage));
        }

        public int ApplyCoinMultiplier(int baseCoins)
        {
            if (baseCoins <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(baseCoins * CoinMultiplier));
        }

        public int GetMaxHp(int baseMaxHp)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * HeroPassiveHpMultiplier * MetaHpMultiplier) + MaxHpFlatBonus);
        }

        public float GetMoveSpeed(float baseSpeed)
        {
            return Mathf.Max(0.1f, baseSpeed * HeroPassiveSpeedMultiplier * MetaSpeedMultiplier + MoveSpeedFlatBonus);
        }

        public float GetMagnetRadius(float baseRadius)
        {
            return Mathf.Max(0f, baseRadius * HeroPassiveMagnetMultiplier * MetaMagnetMultiplier + MagnetRadiusFlatBonus);
        }

        /// <summary>
        /// Процентное снижение входящего урона с убывающей отдачей:
        /// damage * 100 / (100 + armor), минимум 1 урона при положительном входящем уроне.
        /// </summary>
        public int CalculateMitigatedDamage(int incomingDamage)
        {
            if (incomingDamage <= 0) return 0;
            if (Armor <= 0f) return incomingDamage;

            float factor = 100f / (100f + Armor);
            return Mathf.Max(1, Mathf.RoundToInt(incomingDamage * factor));
        }
    }
}
