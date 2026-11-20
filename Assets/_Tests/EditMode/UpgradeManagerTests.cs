using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.LevelUp;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class UpgradeManagerTests
    {
        private bool hadOriginalSave;
        private string originalSaveJson;
        private SaveManager saveManager;
        private readonly List<Object> createdObjects = new List<Object>();

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
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();

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

        private T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        [Test]
        public void InitializeRunPool_FiltersLockedAbilitiesUntilUnlockedNextRun()
        {
            var unlockedAbility = Track(ScriptableObject.CreateInstance<ShooterAbilityData>());
            unlockedAbility.id = "test_open";
            unlockedAbility.unlockedByDefault = true;

            var lockedAbility = Track(ScriptableObject.CreateInstance<LaserTurretAbilityData>());
            lockedAbility.id = "test_locked";
            lockedAbility.unlockedByDefault = false;

            var openCard = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            openCard.cardName = "Open";
            openCard.abilityData = unlockedAbility;

            var lockedCard = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            lockedCard.cardName = "Locked";
            lockedCard.abilityData = lockedAbility;

            var go = Track(new GameObject("TestUpgradeManager"));
            var manager = go.AddComponent<UpgradeManager>();
            manager.cardPool = new List<CardData> { openCard, lockedCard };
            manager.Construct(null, null, null, null, null, saveManager);

            // Run 1: lockedAbility is not unlocked yet
            manager.InitializeRunPool();
            var run1Cards = manager.GetRandomCards(10);
            Assert.Contains(openCard, run1Cards);
            Assert.IsFalse(run1Cards.Contains(lockedCard));

            // Unlock mid-run: should NOT appear in current run's pool
            saveManager.UnlockAbility("test_locked", saveImmediately: false);
            var sameRunCards = manager.GetRandomCards(10);
            Assert.IsFalse(sameRunCards.Contains(lockedCard));

            // Run 2: InitializeRunPool picks up the newly unlocked ability
            manager.InitializeRunPool();
            var run2Cards = manager.GetRandomCards(10);
            Assert.Contains(openCard, run2Cards);
            Assert.Contains(lockedCard, run2Cards);
        }

        [Test]
        public void GetRandomCards_ExcludesOrphanModifierCardsWithoutBaseAbility()
        {
            var statCard = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            statCard.cardName = "HP";
            statCard.statType = StatType.MaxHP;
            statCard.value = 20;

            var orphanMod = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            orphanMod.cardName = "Orphan Mod";
            orphanMod.targetAbilityId = "non_equipped_ability";
            orphanMod.modifierType = ModifierType.Damage;
            orphanMod.value = 5f;

            var heroGo = Track(new GameObject("TestHero"));
            var heroAbilities = heroGo.AddComponent<HeroAbilities>();

            var mgrGo = Track(new GameObject("TestUpgradeManager"));
            var manager = mgrGo.AddComponent<UpgradeManager>();
            manager.cardPool = new List<CardData> { statCard, orphanMod };
            manager.Construct(null, null, null, heroAbilities, null, saveManager);
            manager.InitializeRunPool();

            var offered = manager.GetRandomCards(10);
            Assert.AreEqual(1, offered.Count);
            Assert.AreSame(statCard, offered[0]);
        }
    }
}
