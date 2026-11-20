using System;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class DailyBonusManagerTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;
        private DailyBonusManager bonusManager;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            saveManager = new SaveManager();
            bonusManager = new DailyBonusManager(saveManager);
        }

        [TearDown]
        public void TearDown()
        {
            saveManager?.Dispose();
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
        public void ClaimReward_FirstDay_GrantsCoinsAndBlocksSecondClaimSameDay()
        {
            Assert.IsTrue(bonusManager.IsRewardAvailable());
            Assert.AreEqual(1, bonusManager.GetCurrentDay());

            Assert.IsTrue(bonusManager.ClaimReward(watchAdForDouble: false));
            Assert.Greater(saveManager.Data.coins, 0);
            Assert.AreEqual(2, bonusManager.GetCurrentDay());
            Assert.IsTrue(bonusManager.HasClaimedToday());
            Assert.IsFalse(bonusManager.IsRewardAvailable());
            Assert.IsFalse(bonusManager.ClaimReward(watchAdForDouble: false));
        }

        [Test]
        public void SkippingMultipleDays_DoesNotResetCycleProgress()
        {
            saveManager.Data.dailyBonusDay = 4;
            saveManager.Data.lastDailyClaimDate = DateTime.UtcNow.Date.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            bonusManager.ValidateStreak();

            Assert.AreEqual(4, bonusManager.GetCurrentDay());
            Assert.IsTrue(bonusManager.IsRewardAvailable());
        }

        [Test]
        public void FutureClaimDate_BlocksRewardAgainstClockRollback()
        {
            saveManager.Data.lastDailyClaimDate = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            Assert.IsFalse(bonusManager.IsRewardAvailable());
            Assert.IsFalse(bonusManager.ClaimReward());
        }

        [Test]
        public void ClaimingDay7_WrapsToDay1AndIncrementsCycleWithHigherReward()
        {
            var data = bonusManager.GetRewardData();
            Assert.IsNotNull(data);

            int cycle0Day1Coins = bonusManager.GetCoinsRewardAmount(data.rewards[0]);

            saveManager.Data.dailyBonusDay = bonusManager.GetMaxDays();
            saveManager.Data.lastDailyClaimDate = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            Assert.IsTrue(bonusManager.ClaimReward());
            Assert.AreEqual(1, bonusManager.GetCurrentDay());
            Assert.AreEqual(1, bonusManager.GetCurrentCycle());

            int cycle1Day1Coins = bonusManager.GetCoinsRewardAmount(data.rewards[0]);
            Assert.Greater(cycle1Day1Coins, cycle0Day1Coins);
        }
    }
}
