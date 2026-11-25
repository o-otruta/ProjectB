#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ProjectB.Core.BalanceBot
{
    public enum BalanceBotStrategyKind
    {
        Random = 0,
        GreedyDamage = 1,
        FixedBuild = 2
    }
}
#endif
