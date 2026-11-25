using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Arena;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class SecretSystemTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            SecretCompassIndicatorUI.ClearDynamicPois();
        }

        [TearDown]
        public void TearDown()
        {
            SecretCompassIndicatorUI.ClearDynamicPois();

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }
            spawned.Clear();

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
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        [Test]
        public void SecretConfig_LoadsFromResourcesWithExpectedDesignDefaults()
        {
            SecretConfig cfg = Resources.Load<SecretConfig>(SecretConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(cfg, "Resources/SecretConfig.asset must exist");
            Assert.AreEqual(0.10f, cfg.baseSpawnChancePerRun, 0.0001f);
            Assert.AreEqual(0.20f, cfg.constellationSpawnChanceBonus, 0.0001f);
            Assert.AreEqual(28f, cfg.minDistanceFromCenter, 0.001f);
            Assert.AreEqual(60f, cfg.maxDistanceFromCenter, 0.001f);
            Assert.AreEqual(0.10f, cfg.GetEffectiveSpawnChance(hasConstellationBonus: false), 0.0001f);
            Assert.AreEqual(0.30f, cfg.GetEffectiveSpawnChance(hasConstellationBonus: true), 0.0001f);
        }

        [Test]
        public void SecretItemSpawner_AppliesBaseChanceAndConstellationNodeBonus()
        {
            var cfg = Resources.Load<SecretConfig>(SecretConfig.DEFAULT_RESOURCE_PATH);
            Assert.IsNotNull(cfg);

            var prefabGo = Track(new GameObject("SecretChestPrefab"));
            var prefabItem = prefabGo.AddComponent<InteractableItem>();

            var heroGo = Track(new GameObject("SecretHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();

            using var save = new SaveManager();
            using var treeWithoutBonus = new TalentTreeManager(save);
            var statsWithoutBonus = new HeroRuntimeStats(null, null, treeWithoutBonus);
            Assert.IsFalse(statsWithoutBonus.HasSecretAndShrineTalent);

            var spawnerGo1 = Track(new GameObject("SecretSpawner_Base"));
            var spawner1 = spawnerGo1.AddComponent<SecretItemSpawner>();
            spawner1.SetConfig(cfg);
            SetPrivateField(spawner1, "secretItemPrefabs", new List<InteractableItem> { prefabItem });
            spawner1.Construct(null, heroHealth, null, null, null, null, new GameEventBus(), statsWithoutBonus);

            Assert.AreEqual(0.10f, spawner1.EffectiveSpawnChance, 0.0001f);
            Assert.IsFalse(spawner1.TrySpawnSecretForRun(overrideRoll: 0.15f), "Roll above 10% must not spawn");
            Assert.IsNull(spawner1.ActiveItem);
            Assert.IsTrue(spawner1.TrySpawnSecretForRun(overrideRoll: 0.05f), "Roll below 10% must spawn");
            Assert.IsNotNull(spawner1.ActiveItem);
            float spawnedDist = new Vector2(spawner1.ActiveItem.transform.position.x, spawner1.ActiveItem.transform.position.z).magnitude;
            Assert.GreaterOrEqual(spawnedDist, 27.9f, "Secret item must spawn far from center");

            // Unlock fortune_r4b -> +20% spawn chance bonus (total 30%)
            save.UnlockTalent("fortune_r4b", saveImmediately: false);
            using var treeWithBonus = new TalentTreeManager(save);
            var statsWithBonus = new HeroRuntimeStats(null, null, treeWithBonus);
            Assert.IsTrue(statsWithBonus.HasSecretAndShrineTalent);

            var spawnerGo2 = Track(new GameObject("SecretSpawner_Bonus"));
            var spawner2 = spawnerGo2.AddComponent<SecretItemSpawner>();
            spawner2.SetConfig(cfg);
            SetPrivateField(spawner2, "secretItemPrefabs", new List<InteractableItem> { prefabItem });
            spawner2.Construct(null, heroHealth, null, null, null, null, new GameEventBus(), statsWithBonus);

            Assert.AreEqual(0.30f, spawner2.EffectiveSpawnChance, 0.0001f);
            Assert.IsTrue(spawner2.TrySpawnSecretForRun(overrideRoll: 0.25f), "Roll 0.25 must succeed when fortune_r4b boosts chance to 30%");
            Assert.IsNotNull(spawner2.ActiveItem);

            var spawnerGo3 = Track(new GameObject("SecretSpawner_BonusMiss"));
            var spawner3 = spawnerGo3.AddComponent<SecretItemSpawner>();
            spawner3.SetConfig(cfg);
            SetPrivateField(spawner3, "secretItemPrefabs", new List<InteractableItem> { prefabItem });
            spawner3.Construct(null, heroHealth, null, null, null, null, new GameEventBus(), statsWithBonus);
            Assert.IsFalse(spawner3.TrySpawnSecretForRun(overrideRoll: 0.35f), "Roll 0.35 must fail at 30% effective chance");
        }

        [Test]
        public void SecretCompassIndicatorUI_ExcludesSecrets_EvenWhenActiveOrRegisteredDynamically()
        {
            var camGo = Track(new GameObject("SecretCompassCam", typeof(Camera)));
            camGo.transform.position = new Vector3(0f, 20f, -10f);
            camGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0f, 20f, -10f));
            var cam = camGo.GetComponent<Camera>();
            cam.aspect = 9f / 16f;
            cam.fieldOfView = 60f;

            var heroGo = Track(new GameObject("SecretCompassHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();

            var spawnerGo = Track(new GameObject("SecretCompassSpawner"));
            var secretSpawner = spawnerGo.AddComponent<SecretItemSpawner>();
            var secretGo = Track(new GameObject("OffscreenSecret"));
            secretGo.transform.position = new Vector3(25f, 0f, 0f);
            var secretItem = secretGo.AddComponent<InteractableItem>();
            SetPrivateField(secretSpawner, "activeItem", secretItem);

            // Even manual dynamic registration of Secret POI must be ignored
            SecretCompassIndicatorUI.RegisterDynamicPoi(secretGo.transform, CompassPoiType.Secret, 60f);

            var compassGo = Track(new GameObject("SecretExcludedCompass", typeof(RectTransform), typeof(SecretCompassIndicatorUI)));
            var compass = compassGo.GetComponent<SecretCompassIndicatorUI>();
            compass.Construct(secretSpawner, heroHealth, null, null, null);
            SetPrivateField(compass, "mainCamera", cam);

            compass.RefreshIndicators();
            Assert.AreEqual(0, compass.ActiveVisibleSlotCount, "Secret items must not appear on the compass");
        }

        [Test]
        public void SecretAchievements_HaveUpdatedTargetsAndUnlockCorrectly()
        {
            var chests = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/secret_chests_2.asset");
            var scrolls = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/secret_scroll.asset");
            var crystal = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/secret_crystal.asset");

            Assert.IsNotNull(chests);
            Assert.IsNotNull(scrolls);
            Assert.IsNotNull(crystal);
            Assert.AreEqual(2, chests.targetValue);
            Assert.AreEqual(1, scrolls.targetValue);
            Assert.AreEqual(1, crystal.targetValue);

            using var save = new SaveManager();
            var achMgr = new AchievementManager(save);

            int coinsBeforeChests = save.Data.coins;
            achMgr.OnSecretItemFound("secret_chest");
            Assert.IsFalse(save.Data.completedAchievements.Contains("secret_chests_2"));
            achMgr.OnSecretItemFound("secret_chest");
            Assert.IsTrue(save.Data.completedAchievements.Contains("secret_chests_2"));
            Assert.Greater(save.Data.coins, coinsBeforeChests);

            int coinsBeforeScroll = save.Data.coins;
            achMgr.OnSecretItemFound("secret_scroll");
            Assert.Greater(save.Data.coins, coinsBeforeScroll);
            Assert.IsTrue(save.Data.completedAchievements.Contains("secret_scroll"));
        }
    }
}
