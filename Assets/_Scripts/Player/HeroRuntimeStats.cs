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

        // --- Constellation Tree (Sprint 11) ---
        public float TalentArmor { get; private set; }
        public float TalentCooldownReduction { get; private set; }
        public float TalentAreaPercent { get; private set; }
        public float TalentHpMultiplier { get; private set; } = 1f;
        public float TalentSpeedMultiplier { get; private set; } = 1f;
        public float TalentXpMultiplier { get; private set; } = 1f;
        public float TalentDamageMultiplier { get; private set; } = 1f;
        public float HealingMultiplier { get; private set; } = 1f;
        public float BossDamageMultiplier { get; private set; } = 1f;
        public float BonusIFrameSeconds { get; private set; }
        public float UltimateCooldownMultiplier { get; private set; } = 1f;
        public float SecretRewardMultiplier { get; private set; } = 1f;
        public bool HasSecretAndShrineTalent { get; private set; }
        public int BarrelRespawnBonus { get; private set; }
        public int FreeRerollBonus { get; private set; }
        /// <summary>Ability level cap from the Constellation Path of Mastery (0 = none, base cap from <c>RunRulesConfig</c>).</summary>
        public int TalentAbilityLevelCap { get; private set; }
        public bool HasSecondWind { get; private set; }
        public int CrystalCoinBonus { get; private set; }

        public float XpMultiplier => MetaXpMultiplier * HeroPassiveXpMultiplier * TalentXpMultiplier;

        public float GlobalDamageMultiplier { get; private set; } = 1f;

        private float runArmor;
        private float runCooldownReduction;
        private float runAreaPercent;
        private float appliedTalentCrit;
        private float appliedTalentCoin;

        /// <summary>Total flat armor: run cards/passives + hero passive + Constellation nodes.</summary>
        public float Armor => Mathf.Max(0f, runArmor + TalentArmor);

        /// <summary>
        /// Global radius/area multiplier for AoE abilities, weapon splash and ultimates
        /// (Area Tome passive, Constellation nodes). 1.0 = no bonus.
        /// </summary>
        public float GlobalAreaMultiplier => Mathf.Max(0.25f, 1f + (runAreaPercent + TalentAreaPercent) / 100f);
        public float MoveSpeedFlatBonus { get; private set; } = 0f;
        public float MagnetRadiusFlatBonus { get; private set; } = 0f;
        public int MaxHpFlatBonus { get; private set; } = 0;

        public float CooldownReduction => Mathf.Clamp(runCooldownReduction + TalentCooldownReduction, 0f, MAX_COOLDOWN_REDUCTION);
        public float CooldownReductionMultiplier => Mathf.Clamp(1f - CooldownReduction, 0.5f, 1f);
        public float CritChance { get; private set; } = 0f;
        public float CritMultiplier { get; private set; } = BASE_CRIT_MULTIPLIER;
        public float CoinMultiplier { get; private set; } = 1.0f;

        /// <summary>
        /// Итоговый множитель урона базового оружия: мета-множитель * пассивка героя * забеговый GlobalDamage.
        /// </summary>
        public float WeaponDamageMultiplier => MetaWeaponDamageMultiplier * HeroPassiveDamageMultiplier * TalentDamageMultiplier * GlobalDamageMultiplier;

        /// <summary>
        /// Итоговый множитель урона всех активных способностей: мета-множитель * пассивка героя * забеговый GlobalDamage.
        /// </summary>
        public float AbilityDamageMultiplier => MetaAbilityDamageMultiplier * HeroPassiveDamageMultiplier * TalentDamageMultiplier * GlobalDamageMultiplier;

        public event Action OnStatsChanged;

        public HeroRuntimeStats(
            MetaUpgradeManager metaUpgradeManager = null,
            SelectedHeroService selectedHeroService = null,
            TalentTreeManager talentTree = null)
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

            if (talentTree != null)
            {
                ApplyTalentBonuses(talentTree, notify: false);
            }
        }

        /// <summary>
        /// Applies permanent Constellation Tree node effects for the run.
        /// Idempotent: calling again replaces previously applied talent values instead of stacking them.
        /// </summary>
        public void ApplyTalentBonuses(TalentTreeManager talentTree, bool notify = true)
        {
            float crit = talentTree != null ? talentTree.GetTotalEffect(TalentEffectType.CritChancePercent) / 100f : 0f;
            float coin = talentTree != null ? talentTree.GetTotalEffect(TalentEffectType.CoinGainPercent) / 100f : 0f;

            CritChance = Mathf.Clamp01(CritChance - appliedTalentCrit + crit);
            CoinMultiplier = Mathf.Max(1f, CoinMultiplier - appliedTalentCoin + coin);
            appliedTalentCrit = crit;
            appliedTalentCoin = coin;

            if (talentTree == null)
            {
                TalentArmor = 0f;
                TalentCooldownReduction = 0f;
                TalentAreaPercent = 0f;
                TalentHpMultiplier = TalentSpeedMultiplier = TalentXpMultiplier = TalentDamageMultiplier = 1f;
                HealingMultiplier = BossDamageMultiplier = UltimateCooldownMultiplier = SecretRewardMultiplier = 1f;
                HasSecretAndShrineTalent = false;
                BonusIFrameSeconds = 0f;
                BarrelRespawnBonus = FreeRerollBonus = CrystalCoinBonus = 0;
                TalentAbilityLevelCap = 0;
                HasSecondWind = false;
            }
            else
            {
                TalentArmor = Mathf.Max(0f, talentTree.GetTotalEffect(TalentEffectType.ArmorFlat));
                TalentCooldownReduction = Mathf.Max(0f, talentTree.GetTotalEffect(TalentEffectType.CooldownReductionPercent) / 100f);
                TalentAreaPercent = talentTree.GetTotalEffect(TalentEffectType.GlobalAreaPercent);
                TalentHpMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.MaxHpPercent) / 100f;
                TalentSpeedMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.MoveSpeedPercent) / 100f;
                TalentXpMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.XpGainPercent) / 100f;
                TalentDamageMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.GlobalDamagePercent) / 100f;
                HealingMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.HealingPercent) / 100f;
                BossDamageMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.BossDamagePercent) / 100f;
                BonusIFrameSeconds = Mathf.Max(0f, talentTree.GetTotalEffect(TalentEffectType.IFrameSeconds));
                UltimateCooldownMultiplier = Mathf.Clamp(1f - talentTree.GetTotalEffect(TalentEffectType.UltimateCooldownPercent) / 100f, 0.4f, 1f);
                SecretRewardMultiplier = 1f + talentTree.GetTotalEffect(TalentEffectType.SecretAndShrineRewardPercent) / 100f;
                HasSecretAndShrineTalent = talentTree.HasEffect(TalentEffectType.SecretAndShrineRewardPercent);
                BarrelRespawnBonus = Mathf.Max(0, Mathf.RoundToInt(talentTree.GetTotalEffect(TalentEffectType.BarrelRespawnBonus)));
                FreeRerollBonus = Mathf.Max(0, Mathf.RoundToInt(talentTree.GetTotalEffect(TalentEffectType.FreeRerolls)));
                CrystalCoinBonus = Mathf.Max(0, Mathf.RoundToInt(talentTree.GetTotalEffect(TalentEffectType.CrystalCoinBonus)));
                HasSecondWind = talentTree.GetTotalEffect(TalentEffectType.SecondWind) > 0f;
                TalentAbilityLevelCap = talentTree.GetAbilityLevelCapBonus();
            }

            if (notify)
            {
                OnStatsChanged?.Invoke();
            }
        }

        public int ApplySecretRewardMultiplier(int baseReward)
        {
            if (baseReward <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(baseReward * SecretRewardMultiplier));
        }

        public int GetHealAmount(int baseAmount)
        {
            if (baseAmount <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(baseAmount * HealingMultiplier));
        }

        /// <summary>Scales an authored radius by <see cref="GlobalAreaMultiplier"/>.</summary>
        public float GetScaledArea(float baseRadius)
        {
            return baseRadius * GlobalAreaMultiplier;
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
            runArmor += amount;
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
            runCooldownReduction = Mathf.Clamp(runCooldownReduction + percent / 100f, 0f, MAX_COOLDOWN_REDUCTION);
            OnStatsChanged?.Invoke();
        }

        public void AddAreaPercent(float percent)
        {
            if (Mathf.Approximately(percent, 0f)) return;
            runAreaPercent += percent;
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
            return Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * HeroPassiveHpMultiplier * MetaHpMultiplier * TalentHpMultiplier) + MaxHpFlatBonus);
        }

        public float GetMoveSpeed(float baseSpeed)
        {
            return Mathf.Max(0.1f, baseSpeed * HeroPassiveSpeedMultiplier * MetaSpeedMultiplier * TalentSpeedMultiplier + MoveSpeedFlatBonus);
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
