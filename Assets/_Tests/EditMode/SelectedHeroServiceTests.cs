using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class SelectedHeroServiceTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;
        private HeroCatalog catalog;
        private HeroData knight;
        private HeroData mage;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            saveManager = new SaveManager();

            knight = ScriptableObject.CreateInstance<HeroData>();
            knight.id = "knight";
            knight.heroName = "Knight";
            knight.unlockedByDefault = true;

            mage = ScriptableObject.CreateInstance<HeroData>();
            mage.id = "mage";
            mage.heroName = "Mage";
            mage.unlockedByDefault = false;

            catalog = ScriptableObject.CreateInstance<HeroCatalog>();
            catalog.heroes = new List<HeroData> { knight, mage };
        }

        [TearDown]
        public void TearDown()
        {
            saveManager?.Dispose();
            if (catalog != null) Object.DestroyImmediate(catalog);
            if (knight != null) Object.DestroyImmediate(knight);
            if (mage != null) Object.DestroyImmediate(mage);

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
        public void DefaultSelection_ReturnsUnlockedKnight()
        {
            var service = new SelectedHeroService(saveManager, catalog);
            var selected = service.GetSelectedHero();

            Assert.IsNotNull(selected);
            Assert.AreEqual("knight", selected.id);
            Assert.IsTrue(service.IsHeroUnlocked(knight));
            Assert.IsFalse(service.IsHeroUnlocked(mage));
        }

        [Test]
        public void TrySelectHero_RejectsLockedHeroAndAllowsAfterUnlock()
        {
            var service = new SelectedHeroService(saveManager, catalog);

            Assert.IsFalse(service.TrySelectHero("mage", saveImmediately: false));
            Assert.AreEqual("knight", service.GetSelectedHero().id);

            saveManager.UnlockHero("mage", saveImmediately: false);
            Assert.IsTrue(service.IsHeroUnlocked(mage));

            HeroData notifiedHero = null;
            service.OnSelectedHeroChanged += h => notifiedHero = h;

            Assert.IsTrue(service.TrySelectHero("mage", saveImmediately: true));
            Assert.AreEqual("mage", service.GetSelectedHero().id);
            Assert.AreEqual(mage, notifiedHero);

            // Verify persistence across SaveManager reload
            var reloadedSave = new SaveManager();
            try
            {
                var reloadedService = new SelectedHeroService(reloadedSave, catalog);
                Assert.AreEqual("mage", reloadedService.GetSelectedHero().id);
            }
            finally
            {
                reloadedSave.Dispose();
            }
        }
    }
}
