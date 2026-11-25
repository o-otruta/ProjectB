#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using ProjectB.Abilities;
using ProjectB.LevelUp;

namespace ProjectB.Core.BalanceBot
{
    /// <summary>
    /// Follows a BalanceBotBuildPreset list of priority abilities in order,
    /// using free rerolls when preset abilities are missing, and falling back to GreedyDamage.
    /// </summary>
    public sealed class FixedBuildCardPickStrategy : ICardPickStrategy
    {
        private readonly BalanceBotBuildPreset preset;
        private readonly GreedyDamageCardPickStrategy fallback = new GreedyDamageCardPickStrategy();

        public string StrategyId => preset != null && !string.IsNullOrEmpty(preset.presetId)
            ? $"FixedBuild:{preset.presetId}"
            : "FixedBuild";

        public FixedBuildCardPickStrategy(BalanceBotBuildPreset preset)
        {
            this.preset = preset;
        }

        public CardPickDecision Choose(CardPickContext context)
        {
            if (context == null || context.FreeOfferCount <= 0)
            {
                return CardPickDecision.PickSlot(0);
            }

            if (preset == null || preset.priorityAbilities == null || preset.priorityAbilities.Count == 0)
            {
                return fallback.Choose(context);
            }

            int freeCount = context.FreeOfferCount;
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            bool foundPresetNewAbility = false;

            for (int i = 0; i < freeCount; i++)
            {
                CardData card = context.OfferedCards[i];
                if (card == null) continue;

                float score = ScorePresetCard(card, context, out bool isPresetNewAbility);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
                if (isPresetNewAbility)
                {
                    foundPresetNewAbility = true;
                }
            }

            if (bestIndex >= 0 && bestScore >= 1400f)
            {
                return CardPickDecision.PickSlot(bestIndex);
            }

            if (context.CanFreeReroll && preset.useFreeRerollForPreset && !foundPresetNewAbility && HasMissingPresetAbilityWithSlot(context))
            {
                return CardPickDecision.Reroll();
            }

            if (bestIndex >= 0 && bestScore >= 1000f)
            {
                return CardPickDecision.PickSlot(bestIndex);
            }

            return fallback.Choose(context);
        }

        private float ScorePresetCard(CardData card, CardPickContext context, out bool isPresetNewAbility)
        {
            isPresetNewAbility = false;
            int count = preset.priorityAbilities.Count;

            if (card is AbilityCardData abilityCard && abilityCard.abilityData != null)
            {
                int idx = FindPriorityIndex(abilityCard.abilityData.id);
                if (idx >= 0)
                {
                    bool canAdd = abilityCard.abilityData.type == AbilityType.Active ? context.CanAddActive : context.CanAddPassive;
                    if (canAdd)
                    {
                        isPresetNewAbility = true;
                        return 2000f + (count - idx) * 15f;
                    }
                }
            }
            else if (card is AbilityModifierCardData modCard && !string.IsNullOrEmpty(modCard.targetAbilityId))
            {
                int idx = FindPriorityIndex(modCard.targetAbilityId);
                if (idx >= 0)
                {
                    bool isDps = GreedyDamageCardPickStrategy.IsPrimaryDpsModifier(modCard.modifierType) ||
                                 (modCard.hasSecondaryModifier && GreedyDamageCardPickStrategy.IsPrimaryDpsModifier(modCard.secondaryModifierType));
                    float dpsBonus = isDps ? 120f : 0f;
                    return 1450f + dpsBonus + (count - idx) * 10f;
                }
            }

            return -1f;
        }

        private int FindPriorityIndex(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || preset?.priorityAbilities == null) return -1;
            for (int i = 0; i < preset.priorityAbilities.Count; i++)
            {
                AbilityData ab = preset.priorityAbilities[i];
                if (ab != null && string.Equals(ab.id, abilityId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool HasMissingPresetAbilityWithSlot(CardPickContext context)
        {
            if (preset?.priorityAbilities == null) return false;
            for (int i = 0; i < preset.priorityAbilities.Count; i++)
            {
                AbilityData ab = preset.priorityAbilities[i];
                if (ab == null || string.IsNullOrEmpty(ab.id)) continue;

                bool owned = context.HasAbility != null && context.HasAbility(ab.id);
                if (owned) continue;

                bool slotAvailable = ab.type == AbilityType.Active ? context.CanAddActive : context.CanAddPassive;
                if (slotAvailable) return true;
            }
            return false;
        }
    }
}
#endif
