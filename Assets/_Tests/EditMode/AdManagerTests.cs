using NUnit.Framework;
using UnityEngine;
using ProjectB.Ads;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class AdManagerTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;
        private AdConfig config;
        private MockAdProvider provider;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            saveManager = new SaveManager();

            config = ScriptableObject.CreateInstance<AdConfig>();
            config.maxRewardedPerSession = 2;
            config.maxRewardedPerDay = 3;
            config.runsPerInterstitial = 3;
            config.reviveInvulnerabilitySeconds = 3f;

            provider = new MockAdProvider();
        }

        [TearDown]
        public void TearDown()
        {
            saveManager?.Dispose();
            if (config != null) Object.DestroyImmediate(config);

            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void AdConfig_LoadsFromResourcesWithValidDefaults()
        {
            var loaded = Resources.Load<AdConfig>(AdManager.CONFIG_RESOURCE_PATH);
            Assert.IsNotNull(loaded, "AdConfig.asset must exist in Assets/Resources/AdConfig.asset");
            Assert.IsFalse(string.IsNullOrEmpty(loaded.androidRewardedAdUnitId));
            Assert.IsFalse(string.IsNullOrEmpty(loaded.androidInterstitialAdUnitId));
            Assert.Greater(loaded.maxRewardedPerSession, 0);
            Assert.GreaterOrEqual(loaded.maxRewardedPerDay, loaded.maxRewardedPerSession);
            Assert.AreEqual(3, loaded.runsPerInterstitial);
        }

        [Test]
        public void ShowRewarded_EnforcesSessionAndDailyLimits_AndPersistsDailyCount()
        {
            var manager = new AdManager(saveManager, config, provider);

            int granted = 0;
            Assert.IsTrue(manager.CanShowRewarded());
            Assert.IsTrue(manager.ShowRewarded(AdPlacement.GameOverDoubleCoins, () => granted++));
            Assert.IsTrue(manager.ShowRewarded(AdPlacement.GameOverRevive, () => granted++));

            // Session limit = 2 reached
            Assert.AreEqual(2, granted);
            Assert.AreEqual(2, manager.SessionRewardedCount);
            Assert.AreEqual(2, manager.DailyRewardedCount);
            Assert.IsFalse(manager.CanShowRewarded());
            Assert.IsFalse(manager.ShowRewarded(AdPlacement.DailyBonusDouble, () => granted++));
            Assert.AreEqual(2, granted);

            // New session (reload SaveManager) keeps daily count = 2, allows 1 more up to daily limit = 3
            var nextSessionSave = new SaveManager();
            try
            {
                var nextSessionMgr = new AdManager(nextSessionSave, config, new MockAdProvider());
                Assert.AreEqual(2, nextSessionMgr.DailyRewardedCount);
                Assert.IsTrue(nextSessionMgr.CanShowRewarded());
                Assert.IsTrue(nextSessionMgr.ShowRewarded(AdPlacement.DailyBonusDouble, () => granted++));
                Assert.AreEqual(3, granted);
                Assert.AreEqual(3, nextSessionMgr.DailyRewardedCount);
                Assert.IsFalse(nextSessionMgr.CanShowRewarded());
            }
            finally
            {
                nextSessionSave.Dispose();
            }
        }

        [Test]
        public void ShowRewarded_ReturnsFalseAndBlocksReward_WhenProviderUnavailable()
        {
            provider.IsRewardedReady = false;
            var manager = new AdManager(saveManager, config, provider);

            bool rewardCalled = false;
            bool failedCalled = false;

            Assert.IsFalse(manager.CanShowRewarded());
            bool started = manager.ShowRewarded(
                AdPlacement.GameOverRevive,
                () => rewardCalled = true,
                () => failedCalled = true);

            Assert.IsFalse(started);
            Assert.IsFalse(rewardCalled);
            Assert.IsTrue(failedCalled);
            Assert.AreEqual(0, provider.RewardedShowCalls);
        }

        [Test]
        public void Interstitial_ShowsEveryThirdRun_ResetsCounter_AndRespectsAdsRemoved()
        {
            var manager = new AdManager(saveManager, config, provider);

            // Runs 1 & 2 -> no interstitial
            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());
            bool completed = false;
            manager.TryShowInterstitialBetweenRuns(() => completed = true);
            Assert.IsTrue(completed);
            Assert.AreEqual(0, provider.InterstitialShowCalls);

            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());

            // Run 3 -> shows interstitial and resets counter
            manager.NotifyRunCompleted();
            Assert.IsTrue(manager.ShouldShowInterstitial());
            completed = false;
            manager.TryShowInterstitialBetweenRuns(() => completed = true);
            Assert.IsTrue(completed);
            Assert.AreEqual(1, provider.InterstitialShowCalls);
            Assert.AreEqual(0, saveManager.Data.runsSinceLastInterstitial);

            // When adsRemoved = true -> never shows even after 3 runs
            saveManager.Data.adsRemoved = true;
            manager.NotifyRunCompleted();
            manager.NotifyRunCompleted();
            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());
        }
    }
}
