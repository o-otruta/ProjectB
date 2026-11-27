using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class AchievementManagerTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            saveManager = new SaveManager();
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
        public void Loads50PlusAchievements_WithUniqueIdsAndValidTargets()
        {
            var manager = new AchievementManager(saveManager);
            var all = manager.GetAllAchievements();

            Assert.GreaterOrEqual(all.Count, 50);
            var seenIds = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                Assert.IsNotNull(all[i]);
                Assert.IsFalse(string.IsNullOrEmpty(all[i].id));
                Assert.IsTrue(seenIds.Add(all[i].id), $"Duplicate achievement id: {all[i].id}");
                Assert.Greater(all[i].targetValue, 0, $"Invalid targetValue on {all[i].id}");
            }
        }

        [Test]
        public void TriggeringAchievements_UnlocksAbilitiesCoinsAndTracksRunUnlocks()
        {
            var manager = new AchievementManager(saveManager);
            manager.ClearRunUnlocks();

            // Wave 10 -> LaserTurret
            manager.OnWaveReached(10);
            Assert.IsTrue(saveManager.IsAbilityUnlocked("LaserTurret"));

            // 2000 kills in one run -> BlackHole
            manager.OnKillsInRun(2000);
            Assert.IsTrue(saveManager.IsAbilityUnlocked("BlackHole"));

            // Upgrade ability to Lv 10 (Overdrive I) -> 800 coins
            int coinsBeforeOverdrive = saveManager.Data.coins;
            manager.OnAbilityUpgraded("Shooter", 9);
            Assert.AreEqual(coinsBeforeOverdrive, saveManager.Data.coins);
            Assert.IsFalse(saveManager.Data.completedAchievements.Contains("ability_lvl_10"));
            manager.OnAbilityUpgraded("Shooter", 10);
            Assert.AreEqual(coinsBeforeOverdrive + 800, saveManager.Data.coins);
            Assert.IsTrue(saveManager.Data.completedAchievements.Contains("ability_lvl_10"));

            // Find Mystery Crystal -> coins reward
            int coinsBefore = saveManager.Data.coins;
            manager.OnSecretItemFound("secret_crystal");
            Assert.Greater(saveManager.Data.coins, coinsBefore);

            Assert.GreaterOrEqual(manager.unlockedThisRun.Count, 4);
        }
    }
}
