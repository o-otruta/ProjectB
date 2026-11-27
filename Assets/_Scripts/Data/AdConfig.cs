using UnityEngine;

namespace ProjectB.Data
{
    public enum AdPlacement
    {
        GameOverDoubleCoins = 0,
        GameOverRevive = 1,
        DailyBonusDouble = 2,
        BetweenRunsInterstitial = 3,
        LevelUpThirdCard = 4, // Legacy: all 3 LevelUp cards are now free by default
        LevelUpReroll = 5
    }

    [CreateAssetMenu(fileName = "AdConfig", menuName = "ProjectB/Data/Ad Config")]
    public class AdConfig : ScriptableObject
    {
        [Header("AdMob Unit IDs (Official Test IDs by default)")]
        public string androidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
        public string androidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
        public string iosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
        public string iosInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";

        [Header("Caps & Pacing")]
        [Min(0)] public int maxRewardedPerSession = 10;
        [Min(0)] public int maxRewardedPerDay = 20;
        [Tooltip("0 disables between-run interstitials.")]
        [Min(0)] public int runsPerInterstitial = 0;

        [Header("Revive Tuning")]
        [Min(0f)] public float reviveInvulnerabilitySeconds = 3f;

        public string GetRewardedUnitId()
        {
#if UNITY_IOS
            return iosRewardedAdUnitId;
#else
            return androidRewardedAdUnitId;
#endif
        }

        public string GetInterstitialUnitId()
        {
#if UNITY_IOS
            return iosInterstitialAdUnitId;
#else
            return androidInterstitialAdUnitId;
#endif
        }
    }
}
