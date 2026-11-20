using NUnit.Framework;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class MetaUpgradeManagerTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;
        private MetaUpgradeManager metaManager;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            saveManager = new SaveManager();
            metaManager = new MetaUpgradeManager(saveManager);
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
        public void LoadsAllSevenMetaUpgradesFromResources()
        {
            Assert.GreaterOrEqual(metaManager.GetAllUpgrades().Count, 7);
            Assert.IsNotNull(metaManager.GetUpgradeData("hero_hp"));
            Assert.IsNotNull(metaManager.GetUpgradeData("ability_damage"));
            Assert.IsNotNull(metaManager.GetUpgradeData("magnet_radius"));
        }

        [Test]
        public void TryBuyUpgrade_ScalesCostAndIncreasesBonus()
        {
            var hpData = metaManager.GetUpgradeData("hero_hp");
            Assert.IsNotNull(hpData);

            int level0Cost = metaManager.GetUpgradeCost("hero_hp");
            Assert.AreEqual(hpData.baseCost, level0Cost);
            Assert.IsFalse(metaManager.TryBuyUpgrade("hero_hp"));

            saveManager.AddCoins(level0Cost * 10, saveImmediately: false);
            Assert.IsTrue(metaManager.TryBuyUpgrade("hero_hp"));

            Assert.AreEqual(1, metaManager.GetCurrentLevel("hero_hp"));
            Assert.AreEqual(hpData.effectPerLevel, metaManager.GetTotalBonus(MetaUpgradeEffectType.HeroHP), 0.001f);

            int expectedLevel1Cost = Mathf.RoundToInt(hpData.baseCost * Mathf.Pow(hpData.costMultiplier, 1));
            Assert.AreEqual(expectedLevel1Cost, metaManager.GetUpgradeCost("hero_hp"));
        }

        [Test]
        public void MaxLevel_ReturnsNegativeCostAndBlocksPurchase()
        {
            var startLevelData = metaManager.GetUpgradeData("start_level");
            Assert.IsNotNull(startLevelData);

            saveManager.SetMetaUpgradeLevel("start_level", startLevelData.maxLevel, saveImmediately: false);
            saveManager.AddCoins(999999, saveImmediately: false);

            Assert.AreEqual(-1, metaManager.GetUpgradeCost("start_level"));
            Assert.IsFalse(metaManager.CanAfford("start_level"));
            Assert.IsFalse(metaManager.TryBuyUpgrade("start_level"));
        }
    }
}
