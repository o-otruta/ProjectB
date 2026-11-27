using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectB.Ads;
using ProjectB.Data;
using ProjectB.LevelUp;
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
            config.runsPerInterstitial = 5;
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
            Assert.AreEqual(0, loaded.runsPerInterstitial);
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
        public void Interstitial_ShowsEveryFifthRun_ResetsCounter_AndRespectsAdsRemoved()
        {
            var manager = new AdManager(saveManager, config, provider);

            // Runs 1..4 -> no interstitial
            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());
            bool completed = false;
            manager.TryShowInterstitialBetweenRuns(() => completed = true);
            Assert.IsTrue(completed);
            Assert.AreEqual(0, provider.InterstitialShowCalls);

            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());
            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());
            manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());

            // Run 5 -> shows interstitial and resets counter
            manager.NotifyRunCompleted();
            Assert.IsTrue(manager.ShouldShowInterstitial());
            completed = false;
            manager.TryShowInterstitialBetweenRuns(() => completed = true);
            Assert.IsTrue(completed);
            Assert.AreEqual(1, provider.InterstitialShowCalls);
            Assert.AreEqual(0, saveManager.Data.runsSinceLastInterstitial);

            // When adsRemoved = true -> never shows even after 5 runs
            saveManager.Data.adsRemoved = true;
            for (int i = 0; i < 5; i++)
                manager.NotifyRunCompleted();
            Assert.IsFalse(manager.ShouldShowInterstitial());
        }

        [Test]
        public void Interstitial_DisabledWhenRunsPerInterstitialIsZero()
        {
            config.runsPerInterstitial = 0;
            var manager = new AdManager(saveManager, config, provider);

            for (int i = 0; i < 10; i++)
                manager.NotifyRunCompleted();

            Assert.IsFalse(manager.ShouldShowInterstitial());
            bool completed = false;
            manager.TryShowInterstitialBetweenRuns(() => completed = true);
            Assert.IsTrue(completed);
            Assert.AreEqual(0, provider.InterstitialShowCalls);
        }

        [Test]
        public void AdGatedCardSlot_WhenConfigured_RequiresRewardedAd_AndStaysOpenIfAdFails()
        {
            config.maxRewardedPerSession = 2;
            var manager = new AdManager(saveManager, config, provider);

            var root = new GameObject("CardSelectionTestRoot");
            var panel = new GameObject("SelectionPanel");
            panel.transform.SetParent(root.transform, false);
            panel.SetActive(false);

            var card1Data = ScriptableObject.CreateInstance<AdTestCardData>();
            card1Data.cardName = "Card 1";
            card1Data.description = "Free 1";
            var card2Data = ScriptableObject.CreateInstance<AdTestCardData>();
            card2Data.cardName = "Card 2";
            card2Data.description = "Free 2";
            var card3Data = ScriptableObject.CreateInstance<AdTestCardData>();
            card3Data.cardName = "Card 3";
            card3Data.description = "Rewarded 3";

            try
            {
                var card1 = CreateTestCardSlot(panel.transform, "Card_1", out _, out _);
                var card2 = CreateTestCardSlot(panel.transform, "Card_2", out _, out _);
                var card3 = CreateTestCardSlot(panel.transform, "Card_3", out GameObject badgeObj, out Button card3Button);

                var selectionUI = root.AddComponent<CardSelectionUI>();
                SetPrivateField(selectionUI, "selectionPanel", panel);
                SetPrivateField(selectionUI, "cardSlots", new[] { card1, card2, card3 });
                SetPrivateField(selectionUI, "freeCardsCount", 2);
                selectionUI.Construct(manager);

                var cards = new List<CardData> { card1Data, card2Data, card3Data };
                CardData pickedCard = null;

                // 1. Ad fails/dismissed -> 3rd card does not select, panel remains open, free card 1 still works
                provider.SimulateRewardGranted = false;
                selectionUI.ShowCards(cards, card => pickedCard = card);
                Assert.IsTrue(selectionUI.IsActive);
                Assert.IsTrue(badgeObj.activeSelf);
                Assert.AreEqual("WATCH AD", card3.AdBadgeLabel);
                Assert.IsTrue(card3.IsInteractable);
                Assert.IsTrue(card3Button.interactable);

                card3.OnPointerClick(new PointerEventData(null));
                Assert.AreEqual(1, provider.RewardedShowCalls);
                Assert.AreEqual(0, manager.SessionRewardedCount);
                Assert.IsNull(pickedCard);
                Assert.IsTrue(selectionUI.IsActive);

                card1.OnPointerClick(new PointerEventData(null));
                Assert.AreEqual(card1Data, pickedCard);
                Assert.IsFalse(selectionUI.IsActive);

                // 2. Ad granted -> 3rd card is selected and increments session rewarded count
                pickedCard = null;
                provider.SimulateRewardGranted = true;
                selectionUI.ShowCards(cards, card => pickedCard = card);
                card3.OnPointerClick(new PointerEventData(null));
                Assert.AreEqual(2, provider.RewardedShowCalls);
                Assert.AreEqual(1, manager.SessionRewardedCount);
                Assert.AreEqual(card3Data, pickedCard);
                Assert.IsFalse(selectionUI.IsActive);

                // 3. Session cap reached -> 3rd card shows AD UNAVAILABLE, disabled, click ignored, free card 2 still works
                config.maxRewardedPerSession = 1;
                Assert.IsFalse(manager.CanShowRewarded());
                pickedCard = null;
                selectionUI.ShowCards(cards, card => pickedCard = card);
                Assert.IsTrue(badgeObj.activeSelf);
                Assert.AreEqual("AD UNAVAILABLE", card3.AdBadgeLabel);
                Assert.IsFalse(card3.IsInteractable);
                Assert.IsFalse(card3Button.interactable);

                card3.OnPointerClick(new PointerEventData(null));
                Assert.AreEqual(2, provider.RewardedShowCalls);
                Assert.IsNull(pickedCard);
                Assert.IsTrue(selectionUI.IsActive);

                card2.OnPointerClick(new PointerEventData(null));
                Assert.AreEqual(card2Data, pickedCard);
                Assert.IsFalse(selectionUI.IsActive);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(card1Data);
                Object.DestroyImmediate(card2Data);
                Object.DestroyImmediate(card3Data);
            }
        }

        [Test]
        public void LevelUpThirdCard_IsFreeByDefault_SelectsWithoutRewardedAd()
        {
            config.maxRewardedPerSession = 2;
            provider.IsRewardedReady = false;
            var manager = new AdManager(saveManager, config, provider);

            var root = new GameObject("CardSelectionDefaultTestRoot");
            var panel = new GameObject("SelectionPanel");
            panel.transform.SetParent(root.transform, false);
            panel.SetActive(false);

            var card1Data = ScriptableObject.CreateInstance<AdTestCardData>();
            card1Data.cardName = "Card 1";
            card1Data.description = "Free 1";
            var card2Data = ScriptableObject.CreateInstance<AdTestCardData>();
            card2Data.cardName = "Card 2";
            card2Data.description = "Free 2";
            var card3Data = ScriptableObject.CreateInstance<AdTestCardData>();
            card3Data.cardName = "Card 3";
            card3Data.description = "Free 3";

            try
            {
                var card1 = CreateTestCardSlot(panel.transform, "Card_1", out _, out _);
                var card2 = CreateTestCardSlot(panel.transform, "Card_2", out _, out _);
                var card3 = CreateTestCardSlot(panel.transform, "Card_3", out GameObject badgeObj, out Button card3Button);

                var selectionUI = root.AddComponent<CardSelectionUI>();
                SetPrivateField(selectionUI, "selectionPanel", panel);
                SetPrivateField(selectionUI, "cardSlots", new[] { card1, card2, card3 });
                selectionUI.Construct(manager);

                Assert.AreEqual(3, selectionUI.FreeSlotsCount);

                var cards = new List<CardData> { card1Data, card2Data, card3Data };
                CardData pickedCard = null;
                selectionUI.ShowCards(cards, card => pickedCard = card);

                Assert.IsTrue(selectionUI.IsActive);
                Assert.IsFalse(badgeObj.activeSelf);
                Assert.AreEqual(string.Empty, card3.AdBadgeLabel);
                Assert.IsTrue(card3.IsInteractable);
                Assert.IsTrue(card3Button.interactable);

                card3.OnPointerClick(new PointerEventData(null));
                Assert.AreEqual(0, provider.RewardedShowCalls);
                Assert.AreEqual(card3Data, pickedCard);
                Assert.IsFalse(selectionUI.IsActive);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(card1Data);
                Object.DestroyImmediate(card2Data);
                Object.DestroyImmediate(card3Data);
            }
        }

        private static CardUI CreateTestCardSlot(
            Transform parent,
            string name,
            out GameObject badgeObj,
            out Button button)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            button = go.AddComponent<Button>();

            badgeObj = new GameObject("AdPlaceholder");
            badgeObj.transform.SetParent(go.transform, false);

            var cardUI = go.AddComponent<CardUI>();
            SetPrivateField(cardUI, "bgImage", bg);
            SetPrivateField(cardUI, "adBadgeObject", badgeObj);
            return cardUI;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }

    internal sealed class AdTestCardData : CardData
    {
    }
}
