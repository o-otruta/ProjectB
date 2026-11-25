#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using ProjectB.Abilities;
using ProjectB.LevelUp;
using UnityEngine;

namespace ProjectB.Core.BalanceBot
{
    /// <summary>
    /// Greedy DPS strategy:
    /// 1) New Active ability while an active slot is open.
    /// 2) Damage / Count / Cooldown modifier for the equipped active ability with the highest damage share.
    /// 3) Other offensive modifiers, weapon upgrades, offensive passives, then fallback cards.
    /// Uses a free reroll if an active slot is open and no strong offensive pick is offered.
    /// </summary>
    public sealed class GreedyDamageCardPickStrategy : ICardPickStrategy
    {
        public string StrategyId => "GreedyDamage";

        public CardPickDecision Choose(CardPickContext context)
        {
            if (context == null || context.FreeOfferCount <= 0)
            {
                return CardPickDecision.PickSlot(0);
            }

            string topActiveId = ResolveTopDamageActiveId(context, out long totalActiveDmg);
            int freeCount = context.FreeOfferCount;
            int bestIndex = 0;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < freeCount; i++)
            {
                CardData card = context.OfferedCards[i];
                if (card == null) continue;

                float score = ScoreCard(card, context, topActiveId, totalActiveDmg);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (context.CanFreeReroll && context.CanAddActive && bestScore < 750f)
            {
                return CardPickDecision.Reroll();
            }

            return CardPickDecision.PickSlot(bestIndex);
        }

        public float ScoreCard(CardData card, CardPickContext context, string topActiveId, long totalActiveDamage)
        {
            float rarityBonus = (int)card.rarity * 4f;

            switch (card)
            {
                case AbilityCardData abilityCard when abilityCard.abilityData != null:
                    if (abilityCard.abilityData.type == AbilityType.Active)
                    {
                        return context.CanAddActive
                            ? 1000f + rarityBonus
                            : 50f;
                    }
                    // Offensive passives rank above defensive passives
                    return (IsOffensivePassiveId(abilityCard.abilityData.id) ? 520f : 310f) + rarityBonus;

                case AbilityModifierCardData modCard:
                    bool isDpsMod = IsPrimaryDpsModifier(modCard.modifierType) ||
                                    (modCard.hasSecondaryModifier && IsPrimaryDpsModifier(modCard.secondaryModifierType));
                    bool isTopSource = !string.IsNullOrEmpty(topActiveId) &&
                                       string.Equals(modCard.targetAbilityId, topActiveId, StringComparison.Ordinal);

                    float shareBonus = 0f;
                    if (totalActiveDamage > 0 && context.GetAbilityDamage != null)
                    {
                        long dmg = context.GetAbilityDamage(modCard.targetAbilityId);
                        shareBonus = Mathf.Clamp01((float)dmg / totalActiveDamage) * 80f;
                    }

                    if (isDpsMod && isTopSource)
                    {
                        return 860f + shareBonus + rarityBonus;
                    }
                    if (isDpsMod)
                    {
                        return 680f + shareBonus + rarityBonus;
                    }
                    if (isTopSource)
                    {
                        return 560f + shareBonus + rarityBonus;
                    }
                    return 420f + shareBonus + rarityBonus;

                case WeaponUpgradeCardData weaponCard:
                    float weaponBase = weaponCard.upgradeType switch
                    {
                        WeaponUpgradeType.Multishot => 640f,
                        WeaponUpgradeType.Power => 590f,
                        WeaponUpgradeType.FireRate => 580f,
                        WeaponUpgradeType.Radius => 570f,
                        _ => 500f
                    };
                    return weaponBase + rarityBonus;

                case StatUpgradeCardData statCard:
                    float statBase = statCard.statType switch
                    {
                        StatType.GlobalDamage => 540f,
                        StatType.MaxHP => 260f,
                        StatType.MoveSpeed => 240f,
                        StatType.Armor => 230f,
                        StatType.MagnetRadius => 200f,
                        _ => 200f
                    };
                    return statBase + rarityBonus;

                default:
                    return 100f + rarityBonus;
            }
        }

        public static bool IsPrimaryDpsModifier(ModifierType type)
        {
            return type == ModifierType.Damage ||
                   type == ModifierType.Count ||
                   type == ModifierType.Cooldown ||
                   type == ModifierType.CritMultiplier;
        }

        private static bool IsOffensivePassiveId(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            return string.Equals(abilityId, "BattleFury", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abilityId, "ChronoTome", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abilityId, "CriticalLens", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(abilityId, "AreaTome", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTopDamageActiveId(CardPickContext context, out long totalDamage)
        {
            totalDamage = 0;
            if (context.EquippedActiveIds == null || context.EquippedActiveIds.Count == 0)
            {
                return null;
            }

            string bestId = null;
            long bestDmg = -1;
            int bestLevel = -1;

            for (int i = 0; i < context.EquippedActiveIds.Count; i++)
            {
                string id = context.EquippedActiveIds[i];
                if (string.IsNullOrEmpty(id)) continue;

                long dmg = context.GetAbilityDamage != null ? context.GetAbilityDamage(id) : 0L;
                totalDamage += dmg;
                int lvl = context.GetAbilityLevel != null ? context.GetAbilityLevel(id) : 1;

                if (dmg > bestDmg || (dmg == bestDmg && lvl > bestLevel))
                {
                    bestDmg = dmg;
                    bestLevel = lvl;
                    bestId = id;
                }
            }

            return bestId;
        }
    }
}
#endif
