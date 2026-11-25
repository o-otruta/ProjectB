using System;
using ProjectB.Data;

namespace ProjectB.Ads
{
    /// <summary>
    /// Ad provider used when no advertising SDK is compiled in (e.g. Web Basic Launch without SDK).
    /// Reports ads unavailable so ad-gated UI controls stay hidden cleanly.
    /// </summary>
    public class NullAdProvider : IAdProvider
    {
        public bool IsRewardedReady => false;
        public bool IsInterstitialReady => false;

        public void Initialize(AdConfig config)
        {
        }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            onComplete?.Invoke(false);
        }

        public void ShowInterstitial(string placement, Action onComplete)
        {
            onComplete?.Invoke();
        }
    }
}
