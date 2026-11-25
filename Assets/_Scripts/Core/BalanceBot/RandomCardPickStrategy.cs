#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace ProjectB.Core.BalanceBot
{
    public sealed class RandomCardPickStrategy : ICardPickStrategy
    {
        private readonly Random rng;

        public string StrategyId => "Random";

        public RandomCardPickStrategy(int seed = 0)
        {
            rng = seed != 0 ? new Random(seed) : new Random();
        }

        public CardPickDecision Choose(CardPickContext context)
        {
            if (context == null) return CardPickDecision.PickSlot(0);
            int freeCount = context.FreeOfferCount;
            if (freeCount <= 1) return CardPickDecision.PickSlot(0);

            int picked = rng.Next(0, freeCount);
            return CardPickDecision.PickSlot(picked);
        }
    }
}
#endif
