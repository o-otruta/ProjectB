using System;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Player
{
    /// <summary>
    /// Единая модель рантайм-статов героя на время забега.
    /// Инициализируется от MetaUpgradeManager и накапливает модификаторы карточек.
    /// </summary>
    public class HeroRuntimeStats
    {
        public float MetaHpMultiplier { get; }
        public float MetaSpeedMultiplier { get; }
        public float MetaMagnetMultiplier { get; }
        public float MetaWeaponDamageMultiplier { get; }
        public float MetaAbilityDamageMultiplier { get; }
        public float XpMultiplier { get; }
        public int StartingBonusLevels { get; }

        public float GlobalDamageMultiplier { get; private set; } = 1f;
        public float Armor { get; private set; } = 0f;
        public float MoveSpeedFlatBonus { get; private set; } = 0f;
        public float MagnetRadiusFlatBonus { get; private set; } = 0f;
        public int MaxHpFlatBonus { get; private set; } = 0;

        /// <summary>
        /// Итоговый множитель урона базового оружия: мета-множитель * забеговый GlobalDamage.
        /// </summary>
        public float WeaponDamageMultiplier => MetaWeaponDamageMultiplier * GlobalDamageMultiplier;

        /// <summary>
        /// Итоговый множитель урона всех активных способностей: мета-множитель * забеговый GlobalDamage.
        /// </summary>
        public float AbilityDamageMultiplier => MetaAbilityDamageMultiplier * GlobalDamageMultiplier;

        public event Action OnStatsChanged;

        public HeroRuntimeStats(MetaUpgradeManager metaUpgradeManager = null)
        {
            if (metaUpgradeManager != null)
            {
                MetaHpMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroHP) / 100f;
                MetaSpeedMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroSpeed) / 100f;
                MetaMagnetMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.MagnetRadius) / 100f;
                MetaWeaponDamageMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroDamage) / 100f;
                MetaAbilityDamageMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.AbilityDamage) / 100f;
                XpMultiplier = 1f + metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.XPBonus) / 100f;
                StartingBonusLevels = Mathf.Max(0, Mathf.RoundToInt(metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.StartLevel)));
            }
            else
            {
                MetaHpMultiplier = 1f;
                MetaSpeedMultiplier = 1f;
                MetaMagnetMultiplier = 1f;
                MetaWeaponDamageMultiplier = 1f;
                MetaAbilityDamageMultiplier = 1f;
                XpMultiplier = 1f;
                StartingBonusLevels = 0;
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

        public int GetMaxHp(int baseMaxHp)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * MetaHpMultiplier) + MaxHpFlatBonus);
        }

        public float GetMoveSpeed(float baseSpeed)
        {
            return Mathf.Max(0.1f, baseSpeed * MetaSpeedMultiplier + MoveSpeedFlatBonus);
        }

        public float GetMagnetRadius(float baseRadius)
        {
            return Mathf.Max(0f, baseRadius * MetaMagnetMultiplier + MagnetRadiusFlatBonus);
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
