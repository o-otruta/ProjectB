#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using ProjectB.LevelUp;

namespace ProjectB.Core.BalanceBot
{
    public readonly struct CardPickDecision
    {
        public readonly bool RequestFreeReroll;
        public readonly int SlotIndex;

        private CardPickDecision(bool requestFreeReroll, int slotIndex)
        {
            RequestFreeReroll = requestFreeReroll;
            SlotIndex = slotIndex;
        }

        public static CardPickDecision Reroll() => new CardPickDecision(true, -1);
        public static CardPickDecision PickSlot(int slotIndex) => new CardPickDecision(false, slotIndex);
    }

    public sealed class CardPickContext
    {
        public IReadOnlyList<CardData> OfferedCards;
        public int FreeSlotsCount;
        public bool CanFreeReroll;
        public bool CanAddActive;
        public bool CanAddPassive;
        public Func<string, bool> HasAbility;
        public Func<string, int> GetAbilityLevel;
        public Func<string, long> GetAbilityDamage;
        public IReadOnlyList<string> EquippedActiveIds;

        public int FreeOfferCount
        {
            get
            {
                if (OfferedCards == null || FreeSlotsCount <= 0) return 0;
                return OfferedCards.Count < FreeSlotsCount ? OfferedCards.Count : FreeSlotsCount;
            }
        }
    }

    public interface ICardPickStrategy
    {
        string StrategyId { get; }
        CardPickDecision Choose(CardPickContext context);
    }
}
#endif
