using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Data.Meta;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class Sprint1PolishTests
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
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
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
            spawned.Add(obj);
            return obj;
        }

        [Test]
        public void GoblinAndBatData_ConfiguredAndGoblinWiredIntoGameplayWaveManager()
        {
            var goblin = AssetDatabase.LoadAssetAtPath<ShootingEnemyData>("Assets/_ScriptableObjects/GoblinData.asset");
            Assert.IsNotNull(goblin, "GoblinData.asset must exist");
            Assert.AreEqual(28, goblin.hp);
            Assert.AreEqual(2.8f, goblin.speed, 0.01f);
            Assert.AreEqual(5.5f, goblin.stopDistance, 0.01f);
            Assert.IsNotNull(goblin.modelPrefab);
            Assert.IsNotNull(goblin.weaponData);

            var bat = AssetDatabase.LoadAssetAtPath<ShootingEnemyData>("Assets/_ScriptableObjects/BatData.asset");
            Assert.IsNotNull(bat);
            Assert.AreEqual(38, bat.hp);
            Assert.AreEqual(3.2f, bat.speed, 0.01f);

            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                WaveManager waveMgr = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    waveMgr = root.GetComponentInChildren<WaveManager>(true);
                    if (waveMgr != null) break;
                }

                Assert.IsNotNull(waveMgr, "WaveManager missing in Gameplay.unity");
                Assert.IsNotNull(waveMgr.Config, "WaveManager.Config missing in Gameplay.unity");
                bool hasGoblin = false;
                foreach (var entry in waveMgr.Config.enemySchedule)
                {
                    if (entry != null && entry.enemy == goblin)
                    {
                        hasGoblin = true;
                        Assert.AreEqual(3, entry.minWave, "GoblinData should enter on Wave 3");
                        break;
                    }
                }
                Assert.IsTrue(hasGoblin, "WaveConfig.enemySchedule must contain GoblinData.asset");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MageHeroAndReachWave25Achievement_ConfiguredForUnlockAndMeteorStrike()
        {
            var mage = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/MageData.asset");
            var mageWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/MageWeaponData.asset");
            var reach25 = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/ReachWave25.asset");

            Assert.IsNotNull(mage);
            Assert.IsNotNull(mageWeapon);
            Assert.IsNotNull(reach25);

            Assert.IsFalse(mage.unlockedByDefault, "Mage must be locked by default");
            Assert.AreEqual(HeroPassiveType.GlobalDamagePercent, mage.passiveType);
            Assert.AreEqual(20f, mage.passiveValue, 0.01f);
            Assert.AreEqual(HeroUltimateType.MeteorStrike, mage.ultimateType);
            Assert.AreEqual("Meteor Shower", mage.ultimateName);
            Assert.AreSame(mageWeapon, mage.startingWeapon);
            Assert.AreEqual(14, mageWeapon.damage);
            Assert.Greater(mageWeapon.splashRadius, 0f);

            Assert.AreEqual(AchievementRewardType.UnlockHero, reach25.rewardType);
            Assert.AreEqual("mage", reach25.rewardId);

            using (var saveManager = new SaveManager())
            {
                var achMgr = new AchievementManager(saveManager);
                Assert.IsFalse(saveManager.IsHeroUnlocked("mage"));
                achMgr.OnWaveReached(25);
                Assert.IsTrue(saveManager.IsHeroUnlocked("mage"));
            }
        }

        [Test]
        public void HeroUltimate_MeteorStrike_DamagesNearbyEnemyAndPublishesMeteorImpactEvent()
        {
            var bus = new GameEventBus();
            int impactEvents = 0;
            bus.Subscribe<MeteorImpactEvent>(_ => impactEvents++);

            var mage = Track(ScriptableObject.CreateInstance<HeroData>());
            mage.id = "mage";
            mage.ultimateType = HeroUltimateType.MeteorStrike;
            mage.ultimateDuration = 3f;
            mage.ultimateCooldown = 22f;

            var heroGo = Track(new GameObject("TestMageHero"));
            heroGo.transform.position = Vector3.zero;
            var health = heroGo.AddComponent<HeroHealth>();
            var ult = heroGo.AddComponent<HeroUltimate>();
            var stats = new HeroRuntimeStats();
            ult.Construct(health, null, null, bus, stats);

            var ultSo = new SerializedObject(ult);
            ultSo.FindProperty("fallbackHeroData").objectReferenceValue = mage;
            ultSo.FindProperty("enemyLayer").intValue = 1 << 8;
            ultSo.ApplyModifiedPropertiesWithoutUndo();

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 200;
            enemyData.contactDamage = 5;

            var enemyGo = Track(new GameObject("TestEnemy"));
            enemyGo.layer = 8;
            enemyGo.transform.position = new Vector3(3f, 0f, 0f);
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            Physics.SyncTransforms();

            Assert.IsTrue(ult.TryActivate());
            Assert.IsTrue(ult.IsActive);
            Assert.IsFalse(health.IsInvulnerable, "MeteorStrike must not grant Shield invulnerability");
            Assert.AreEqual(1, impactEvents);
            Assert.Less(enemy.CurrentHp, 200);
        }

        [Test]
        public void FireballProjectile_PublishesFireballExplodedEventOnHit()
        {
            var bus = new GameEventBus();
            int explosionCount = 0;
            float recordedRadius = 0f;
            Vector3 recordedPos = Vector3.zero;
            bus.Subscribe<FireballExplodedEvent>(evt =>
            {
                explosionCount++;
                recordedRadius = evt.Radius;
                recordedPos = evt.Position;
            });

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.contactDamage = 5;

            var enemyGo = Track(new GameObject("FireballTarget"));
            enemyGo.layer = 8;
            enemyGo.transform.position = new Vector3(2f, 0f, 4f);
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, null, null, bus, 1f);

            Physics.SyncTransforms();

            var projGo = Track(new GameObject("TestFireball"));
            projGo.transform.position = enemyGo.transform.position;
            var fireball = projGo.AddComponent<FireballProjectile>();
            fireball.InitializeFireball(25f, 10f, 3f, enemyGo.transform, 1 << 8, null, bus);

            var hitMethod = typeof(FireballProjectile).GetMethod("HitTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            hitMethod.Invoke(fireball, null);

            Assert.AreEqual(1, explosionCount);
            Assert.AreEqual(3f, recordedRadius, 0.001f);
            Assert.AreEqual(enemyGo.transform.position, recordedPos);
            Assert.AreEqual(75, enemy.CurrentHp);
        }

        [Test]
        public void DamageNumberItem_ShowText_DisplaysRewardSummary()
        {
            var go = Track(new GameObject("TestRewardPopup", typeof(RectTransform), typeof(TextMeshProUGUI)));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            var item = go.AddComponent<DamageNumberItem>();
            item.SetTextMesh(tmp);

            item.ShowText(Vector3.zero, "+100 Coins", Color.yellow, 1.35f, 1.6f, null, null);
            Assert.AreEqual("+100 Coins", tmp.text);
        }

        [Test]
        public void SecretCompassIndicatorUI_ExistsInSceneAndEvaluatesDistancesProperly()
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                SecretCompassIndicatorUI sceneCompass = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    sceneCompass = root.GetComponentInChildren<SecretCompassIndicatorUI>(true);
                    if (sceneCompass != null) break;
                }
                Assert.IsNotNull(sceneCompass, "SecretCompassIndicatorUI missing in Gameplay.unity");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            var camGo = Track(new GameObject("TestCam", typeof(Camera)));
            camGo.transform.position = new Vector3(0f, 20f, -10f);
            camGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0f, 20f, -10f));
            var cam = camGo.GetComponent<Camera>();
            cam.aspect = 9f / 16f;
            cam.fieldOfView = 60f;

            var uiGo = Track(new GameObject("TestCompass", typeof(RectTransform), typeof(SecretCompassIndicatorUI)));
            var compass = uiGo.GetComponent<SecretCompassIndicatorUI>();
            Vector2 canvasSize = new Vector2(1080f, 1920f);

            // Too far (> 45m)
            Assert.IsFalse(compass.EvaluateIndicatorState(
                Vector3.zero,
                new Vector3(50f, 0f, 0f),
                cam,
                canvasSize,
                out _,
                out _,
                out _));

            // On screen
            Assert.IsFalse(compass.EvaluateIndicatorState(
                Vector3.zero,
                new Vector3(0.5f, 0f, 0.5f),
                cam,
                canvasSize,
                out _,
                out _,
                out _));

            // Within 45m and off-screen to the right
            bool visible = compass.EvaluateIndicatorState(
                Vector3.zero,
                new Vector3(25f, 0f, 0f),
                cam,
                canvasSize,
                out Vector2 anchoredPos,
                out float angleDeg,
                out int distMeters);

            Assert.IsTrue(visible);
            Assert.AreEqual(25, distMeters);
            Assert.Greater(anchoredPos.x, 300f);
            Assert.Less(angleDeg, 0f); // pointing right => -90 deg relative to UP
        }

        [Test]
        public void AllScriptableObjects_HaveGeneratedSpriteIconsAssigned()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AbilityData", new[] { "Assets/_ScriptableObjects/Abilities" }))
            {
                var ab = AssetDatabase.LoadAssetAtPath<AbilityData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(ab.icon, $"AbilityData missing icon: {ab.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/_ScriptableObjects/Cards" }))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(card.icon, $"CardData missing icon: {card.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:HeroData", new[] { "Assets/_ScriptableObjects" }))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(hero.icon, $"HeroData missing icon: {hero.name}");
                Assert.IsNotNull(hero.ultimateIcon, $"HeroData missing ultimateIcon: {hero.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AchievementData", new[] { "Assets/Resources/Achievements" }))
            {
                var ach = AssetDatabase.LoadAssetAtPath<AchievementData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(ach.icon, $"AchievementData missing icon: {ach.name}");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:MetaUpgradeData", new[] { "Assets/Resources/MetaUpgrades" }))
            {
                var meta = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(meta.icon, $"MetaUpgradeData missing icon: {meta.name}");
            }

            var daily = AssetDatabase.LoadAssetAtPath<DailyBonusRewardData>("Assets/Resources/DailyBonusRewardData.asset");
            Assert.IsNotNull(daily);
            Assert.AreEqual(7, daily.rewards.Length);
            for (int i = 0; i < daily.rewards.Length; i++)
            {
                Assert.IsNotNull(daily.rewards[i].icon, $"DailyBonus day {i + 1} missing icon");
            }
        }

        [Test]
        public void FloorMat_LaserTurret_Slime_DefaultVolume_AndManifest_ArePolished()
        {
            // 1. FloorMat + FloorGrid
            var floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Materials/FloorGrid.png");
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/FloorMat.mat");
            Assert.IsNotNull(floorTex);
            Assert.IsNotNull(floorMat);
            Assert.AreEqual(FilterMode.Point, floorTex.filterMode);
            Assert.AreSame(floorTex, floorMat.GetTexture("_BaseMap"));
            Assert.AreEqual(new Vector2(125f, 125f), floorMat.GetTextureScale("_BaseMap"));
            Assert.AreEqual(Color.white, floorMat.GetColor("_BaseColor"));
            Assert.IsTrue(floorMat.enableInstancing);

            // 2. LaserTurret shared instanced material
            var turretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/LaserTurret.prefab");
            var turretMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/Voxels/Turret_Mat.mat");
            Assert.IsNotNull(turretPrefab);
            Assert.IsNotNull(turretMat);
            Assert.IsTrue(turretMat.enableInstancing);
            var turretMr = turretPrefab.transform.Find("Visuals").GetComponent<MeshRenderer>();
            Assert.AreSame(turretMat, turretMr.sharedMaterial);

            // 3. Slime prefab position + bobbing
            var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/Slime.prefab");
            Assert.IsNotNull(slimePrefab);
            Assert.AreEqual(Vector3.zero, slimePrefab.transform.localPosition);
            var slimeChild = slimePrefab.transform.Find("slime");
            Assert.IsNotNull(slimeChild);
            var slimeAnim = slimeChild.GetComponent<VoxelMeshAnimator>();
            Assert.IsNotNull(slimeAnim);
            Assert.IsTrue(slimeAnim.EnableBobbing);
            Assert.AreSame(slimeChild, slimeAnim.BobbingTransform);

            // 4. DefaultVolumeProfile clean & configured
            string volYaml = File.ReadAllText("Assets/Settings/DefaultVolumeProfile.asset");
            Assert.IsFalse(volYaml.Contains("m_Script: {fileID: 0}"), "DefaultVolumeProfile.asset contains broken m_Script references");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            Assert.IsNotNull(profile);
            foreach (var comp in profile.components)
            {
                Assert.IsNotNull(comp, "VolumeProfile must not contain null components");
            }
            Assert.IsTrue(profile.TryGet<Bloom>(out var bloom) && bloom.active);
            Assert.IsTrue(profile.TryGet<Vignette>(out var vignette) && vignette.active);
            Assert.AreEqual(0.22f, vignette.intensity.value, 0.005f);
            Assert.IsTrue(profile.TryGet<ColorAdjustments>(out var colorAdj) && colorAdj.active);
            Assert.AreEqual(12f, colorAdj.contrast.value, 0.1f);
            Assert.AreEqual(10f, colorAdj.saturation.value, 0.1f);

            // 5. Packages/manifest.json pruned
            string manifest = File.ReadAllText("Packages/manifest.json");
            Assert.IsFalse(manifest.Contains("com.unity.visualscripting"));
            Assert.IsFalse(manifest.Contains("com.unity.timeline"));
            Assert.IsFalse(manifest.Contains("com.unity.multiplayer.center"));
            Assert.IsFalse(manifest.Contains("com.unity.ai.navigation"));
        }
    }
}
