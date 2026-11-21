using System;
using UnityEngine;
using ProjectB.Data;

namespace ProjectB.Ads
{
    /// <summary>
    /// In-editor / offline fallback ad provider for testing ad flows and caps without the AdMob SDK.
    /// </summary>
    public class MockAdProvider : IAdProvider
    {
        public bool IsRewardedReady { get; set; } = true;
        public bool IsInterstitialReady { get; set; } = true;
        public bool SimulateRewardGranted { get; set; } = true;

        public int RewardedShowCalls { get; private set; }
        public int InterstitialShowCalls { get; private set; }

        public void Initialize(AdConfig config)
        {
        }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            RewardedShowCalls++;
            Debug.Log($"[MockAdProvider] Showing Rewarded ({placement})");
            onComplete?.Invoke(IsRewardedReady && SimulateRewardGranted);
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            InterstitialShowCalls++;
            Debug.Log($"[MockAdProvider] Showing Interstitial ({placement})");
            onClosed?.Invoke();
        }
    }
}
