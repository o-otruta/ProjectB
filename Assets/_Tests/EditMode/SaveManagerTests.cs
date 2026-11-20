using NUnit.Framework;
using UnityEngine;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class SaveManagerTests
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
        public void Coins_AddAndSpend_UpdatesBalanceCorrectly()
        {
            saveManager.AddCoins(150, saveImmediately: false);
            Assert.AreEqual(150, saveManager.Data.coins);
            Assert.IsTrue(saveManager.IsDirty);

            Assert.IsFalse(saveManager.SpendCoins(200, saveImmediately: false));
            Assert.AreEqual(150, saveManager.Data.coins);

            Assert.IsTrue(saveManager.SpendCoins(50, saveImmediately: false));
            Assert.AreEqual(100, saveManager.Data.coins);
        }

        [Test]
        public void UnlockAbility_PersistsAcrossReloadWithoutDuplicates()
        {
            Assert.IsFalse(saveManager.IsAbilityUnlocked("LaserTurret"));

            saveManager.UnlockAbility("LaserTurret", saveImmediately: true);
            saveManager.UnlockAbility("LaserTurret", saveImmediately: true);

            Assert.IsTrue(saveManager.IsAbilityUnlocked("LaserTurret"));
            Assert.AreEqual(1, saveManager.Data.unlockedAbilityIds.Count);

            var reloaded = new SaveManager();
            try
            {
                Assert.IsTrue(reloaded.IsAbilityUnlocked("LaserTurret"));
            }
            finally
            {
                reloaded.Dispose();
            }
        }

        [Test]
        public void RecordRunResult_UpdatesTotalsAndBestWave()
        {
            saveManager.RecordRunResult(new RunResult
            {
                waveReached = 12,
                enemiesKilled = 85,
                coinsEarned = 240,
                playTime = 180f
            });

            Assert.AreEqual(1, saveManager.Data.totalRuns);
            Assert.AreEqual(12, saveManager.Data.bestWave);
            Assert.AreEqual(85, saveManager.Data.totalKills);
            Assert.AreEqual(240, saveManager.Data.coins);
            Assert.IsFalse(saveManager.IsDirty);

            saveManager.RecordRunResult(new RunResult
            {
                waveReached = 7,
                enemiesKilled = 30,
                coinsEarned = 60,
                playTime = 90f
            });

            Assert.AreEqual(2, saveManager.Data.totalRuns);
            Assert.AreEqual(12, saveManager.Data.bestWave);
            Assert.AreEqual(115, saveManager.Data.totalKills);
            Assert.AreEqual(300, saveManager.Data.coins);
        }
    }
}
