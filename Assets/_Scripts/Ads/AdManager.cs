using System;
using System.Globalization;
using UnityEngine;
using VContainer;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Ads
{
    public class AdManager
    {
        public const string CONFIG_RESOURCE_PATH = "AdConfig";
        private const string DATE_FORMAT = "yyyy-MM-dd";

        private readonly SaveManager saveManager;
        private readonly AdConfig config;
        private readonly IAdProvider provider;

        private int sessionRewardedCount;
        private bool isShowingAd;

        public AdConfig Config => config;
        public int SessionRewardedCount => sessionRewardedCount;
        public int DailyRewardedCount
        {
            get
            {
                EnsureDailyCounterCurrent();
                return saveManager?.Data != null ? Mathf.Max(0, saveManager.Data.rewardedAdsWatchedToday) : 0;
            }
        }
        public bool IsShowingAd => isShowingAd;
        public bool IsAdsAvailable => provider != null && !(provider is NullAdProvider);
        public float ReviveInvulnerabilitySeconds => config != null ? Mathf.Max(0f, config.reviveInvulnerabilitySeconds) : 3f;

        public event Action OnAdStateChanged;

        [Inject]
        public AdManager(SaveManager saveManager)
            : this(saveManager, Resources.Load<AdConfig>(CONFIG_RESOURCE_PATH), CreateDefaultProvider())
        {
        }

        public AdManager(SaveManager saveManager, AdConfig config, IAdProvider provider)
        {
            this.saveManager = saveManager;
            this.config = config;
            this.provider = provider ?? new MockAdProvider();
            this.provider.Initialize(this.config);
            EnsureDailyCounterCurrent();
        }

        private static IAdProvider CreateDefaultProvider()
        {
#if GOOGLE_MOBILE_ADS && !UNITY_EDITOR
            return new AdMobAdProvider();
#elif UNITY_WEBGL && !UNITY_EDITOR && !PLATFORM_CRAZYGAMES
            return new NullAdProvider();
#else
            return new MockAdProvider();
#endif
        }

        private string GetTodayUtcString()
        {
            return DateTime.UtcNow.Date.ToString(DATE_FORMAT, CultureInfo.InvariantCulture);
        }

        private void EnsureDailyCounterCurrent()
        {
            if (saveManager?.Data == null) return;

            string today = GetTodayUtcString();
            if (!string.Equals(saveManager.Data.lastAdWatchDate, today, StringComparison.Ordinal))
            {
                saveManager.Data.lastAdWatchDate = today;
                saveManager.Data.rewardedAdsWatchedToday = 0;
            }
        }

        private void IncrementRewardedCounts()
        {
            EnsureDailyCounterCurrent();
            sessionRewardedCount++;

            if (saveManager?.Data != null)
            {
                saveManager.Data.rewardedAdsWatchedToday++;
                saveManager.Data.lastAdWatchDate = GetTodayUtcString();
                saveManager.Save();
            }
        }

        public bool CanShowRewarded()
        {
            EnsureDailyCounterCurrent();

            if (isShowingAd || provider == null || !provider.IsRewardedReady)
            {
                return false;
            }

            int maxPerSession = config != null ? config.maxRewardedPerSession : 10;
            int maxPerDay = config != null ? config.maxRewardedPerDay : 20;

            return sessionRewardedCount < maxPerSession && DailyRewardedCount < maxPerDay;
        }

        public bool ShowRewarded(AdPlacement placement, Action onRewardGranted, Action onFailed = null)
        {
            if (!CanShowRewarded())
            {
                onFailed?.Invoke();
                return false;
            }

            isShowingAd = true;
            provider.ShowRewarded(placement.ToString(), granted =>
            {
                isShowingAd = false;
                if (granted)
                {
                    IncrementRewardedCounts();
                    onRewardGranted?.Invoke();
                }
                else
                {
                    onFailed?.Invoke();
                }
                OnAdStateChanged?.Invoke();
            });

            return true;
        }

        public void NotifyRunCompleted()
        {
            if (saveManager?.Data == null) return;
            saveManager.Data.runsSinceLastInterstitial++;
        }

        public bool ShouldShowInterstitial()
        {
            if (isShowingAd || provider == null || !provider.IsInterstitialReady) return false;
            if (saveManager?.Data != null && saveManager.Data.adsRemoved) return false;

            int threshold = config != null ? config.runsPerInterstitial : 0;
            if (threshold <= 0) return false;

            int runs = saveManager?.Data != null ? saveManager.Data.runsSinceLastInterstitial : 0;
            return runs >= threshold;
        }

        public void TryShowInterstitialBetweenRuns(Action onComplete)
        {
            if (!ShouldShowInterstitial())
            {
                onComplete?.Invoke();
                return;
            }

            if (saveManager?.Data != null)
            {
                saveManager.Data.runsSinceLastInterstitial = 0;
                saveManager.Save();
            }

            isShowingAd = true;
            provider.ShowInterstitial(AdPlacement.BetweenRunsInterstitial.ToString(), () =>
            {
                isShowingAd = false;
                OnAdStateChanged?.Invoke();
                onComplete?.Invoke();
            });
        }
    }
}
