using System;
using ProjectB.Data;

namespace ProjectB.Ads
{
    public interface IAdProvider
    {
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }

        void Initialize(AdConfig config);
        void ShowRewarded(string placement, Action<bool> onComplete);
        void ShowInterstitial(string placement, Action onClosed);
    }
}
