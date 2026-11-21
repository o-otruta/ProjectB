#if GOOGLE_MOBILE_ADS
using System;
using UnityEngine;
using GoogleMobileAds.Api;
using ProjectB.Data;

namespace ProjectB.Ads
{
    /// <summary>
    /// Google Mobile Ads (AdMob) v9+ adapter. Active when GOOGLE_MOBILE_ADS scripting define is enabled.
    /// </summary>
    public class AdMobAdProvider : IAdProvider
    {
        private AdConfig config;
        private RewardedAd rewardedAd;
        private InterstitialAd interstitialAd;
        private bool isLoadingRewarded;
        private bool isLoadingInterstitial;

        public bool IsRewardedReady => rewardedAd != null && rewardedAd.CanShowAd();
        public bool IsInterstitialReady => interstitialAd != null && interstitialAd.CanShowAd();

        public void Initialize(AdConfig adConfig)
        {
            config = adConfig;
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ =>
            {
                LoadRewarded();
                LoadInterstitial();
            });
        }

        private void LoadRewarded()
        {
            if (isLoadingRewarded || config == null) return;
            string unitId = config.GetRewardedUnitId();
            if (string.IsNullOrEmpty(unitId)) return;

            isLoadingRewarded = true;
            rewardedAd?.Destroy();
            rewardedAd = null;

            RewardedAd.Load(unitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
            {
                isLoadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[AdMobAdProvider] Failed to load rewarded ad: {error}");
                    return;
                }
                rewardedAd = ad;
            });
        }

        private void LoadInterstitial()
        {
            if (isLoadingInterstitial || config == null) return;
            string unitId = config.GetInterstitialUnitId();
            if (string.IsNullOrEmpty(unitId)) return;

            isLoadingInterstitial = true;
            interstitialAd?.Destroy();
            interstitialAd = null;

            InterstitialAd.Load(unitId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
            {
                isLoadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[AdMobAdProvider] Failed to load interstitial ad: {error}");
                    return;
                }
                interstitialAd = ad;
            });
        }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (!IsRewardedReady)
            {
                LoadRewarded();
                onComplete?.Invoke(false);
                return;
            }

            bool rewardEarned = false;
            bool callbackSent = false;
            RewardedAd adToShow = rewardedAd;
            rewardedAd = null;

            void Finish()
            {
                if (callbackSent) return;
                callbackSent = true;
                adToShow.Destroy();
                LoadRewarded();
                onComplete?.Invoke(rewardEarned);
            }

            adToShow.OnAdFullScreenContentClosed += Finish;
            adToShow.OnAdFullScreenContentFailed += _ => Finish();

            adToShow.Show(_ =>
            {
                rewardEarned = true;
            });
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            if (!IsInterstitialReady)
            {
                LoadInterstitial();
                onClosed?.Invoke();
                return;
            }

            bool callbackSent = false;
            InterstitialAd adToShow = interstitialAd;
            interstitialAd = null;

            void Finish()
            {
                if (callbackSent) return;
                callbackSent = true;
                adToShow.Destroy();
                LoadInterstitial();
                onClosed?.Invoke();
            }

            adToShow.OnAdFullScreenContentClosed += Finish;
            adToShow.OnAdFullScreenContentFailed += _ => Finish();

            adToShow.Show();
        }
    }
}
#endif
