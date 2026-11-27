using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Visuals;

namespace ProjectB.Tests.EditMode
{
    public class Sprint5HeroesAndArenaTests
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
        public void Sprint5Assets_BlenderSourcesMaterialsPrefabsCatalogAchievementsAndScene_AreWired()
        {
            // 1. Verify all 7 Blender source files
            string[] expectedBlends =
            {
                "ArtSources/Blender/Ranger.blend",
                "ArtSources/Blender/NecromancerHero.blend",
                "ArtSources/Blender/RangerArrow.blend",
                "ArtSources/Blender/SoulBolt.blend",
                "ArtSources/Blender/ExplosiveBarrel.blend",
                "ArtSources/Blender/VacuumMagnet.blend",
                "ArtSources/Blender/ChallengeShrine.blend"
            };
            foreach (string blendPath in expectedBlends)
            {
                Assert.IsTrue(File.Exists(blendPath), $"Missing Blender source file: {blendPath}");
            }

            // 2. Verify all 14 voxel materials (VoxelLit for heroes and the explosive barrel — it needs _HitFlash/_RimColor
            //    for its fuse flash and idle glow; URP Lit for other props/projectiles + GPU Instancing + textures)
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader voxelLit = Shader.Find("ProjectB/VoxelLit");
            Assert.IsNotNull(urpLit);
            Assert.IsNotNull(voxelLit);
            string[] voxelPrefixes =
            {
                "Ranger", "NecromancerHero", "RangerArrow", "SoulBolt",
                "ExplosiveBarrel", "VacuumMagnet", "ChallengeShrine"
            };
            foreach (string prefix in voxelPrefixes)
            {
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{prefix}_Mat.mat");
                Material glowMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{prefix}_Glow_Mat.mat");
                Assert.IsNotNull(baseMat, $"Missing {prefix}_Mat.mat");
                Assert.IsNotNull(glowMat, $"Missing {prefix}_Glow_Mat.mat");
                Shader expectedShader = (prefix == "Ranger" || prefix == "NecromancerHero" || prefix == "ExplosiveBarrel") ? voxelLit : urpLit;
                Assert.AreEqual(expectedShader, baseMat.shader);
                Assert.AreEqual(expectedShader, glowMat.shader);
                Assert.IsTrue(baseMat.enableInstancing);
                Assert.IsTrue(glowMat.enableInstancing);
                Assert.IsNotNull(baseMat.GetTexture("_BaseMap"));
                Assert.IsTrue(glowMat.IsKeywordEnabled("_EMISSION"));
                Assert.IsNotNull(glowMat.GetTexture("_EmissionMap"));
            }

            // 3. Verify 3 FX materials
            string[] fxMatPaths =
            {
                "Assets/_Materials/ShrineRingMat.mat",
                "Assets/_Materials/ShrineBeaconMat.mat",
                "Assets/_Materials/VacuumBeaconMat.mat"
            };
            foreach (string path in fxMatPaths)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"Missing FX material: {path}");
                Assert.IsTrue(mat.enableInstancing, $"{path} must enable GPU instancing");
            }

            // 4. Verify Hero visual prefabs
            VerifyHeroVisualPrefab("Assets/_Prefabs/Heroes/RangerVisual.prefab", "RangerModel");
            VerifyHeroVisualPrefab("Assets/_Prefabs/Heroes/NecromancerVisual.prefab", "NecromancerModel");

            // 5. Verify projectile prefabs
            string[] projPrefabs =
            {
                "Assets/_Prefabs/Projectiles/RangerArrowProjectile.prefab",
                "Assets/_Prefabs/Projectiles/SoulBoltProjectile.prefab"
            };
            foreach (string path in projPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing {path}");
                Assert.IsNotNull(prefab.GetComponent<Projectile>());
                Transform visuals = prefab.transform.Find("Visuals");
                Assert.IsNotNull(visuals);
                var mf = visuals.GetComponent<MeshFilter>();
                var mr = visuals.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mf?.sharedMesh);
                Assert.AreEqual(mf.sharedMesh.subMeshCount, mr.sharedMaterials.Length);
            }

            // 6. Verify Hero.prefab SoulNova visuals wired
            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            Assert.IsNotNull(heroPrefab);
            var heroUltSo = new SerializedObject(heroPrefab.GetComponent<HeroUltimate>());
            Assert.IsNotNull(heroUltSo.FindProperty("soulNovaMesh").objectReferenceValue);
            Assert.IsNotNull(heroUltSo.FindProperty("soulNovaBaseMaterial").objectReferenceValue);
            Assert.IsNotNull(heroUltSo.FindProperty("soulNovaGlowMaterial").objectReferenceValue);

            // 7. Verify interactive arena prefabs
            GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Arena/ExplosiveBarrel.prefab");
            Assert.IsNotNull(barrelPrefab);
            Assert.AreEqual(LayerMask.NameToLayer("Enemy"), barrelPrefab.layer);
            Assert.IsNotNull(barrelPrefab.GetComponent<ExplosiveBarrel>());
            Assert.IsTrue(barrelPrefab.GetComponent<Collider>().isTrigger);

            GameObject vacuumPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Pickups/VacuumMagnetPickup.prefab");
            Assert.IsNotNull(vacuumPrefab);
            Assert.AreEqual(LayerMask.NameToLayer("Pickups"), vacuumPrefab.layer);
            Assert.IsNotNull(vacuumPrefab.GetComponent<VacuumMagnetPickup>());
            Assert.IsNotNull(vacuumPrefab.transform.Find("BeaconBeam"));

            GameObject shrinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Arena/ChallengeShrine.prefab");
            Assert.IsNotNull(shrinePrefab);
            Assert.AreEqual(LayerMask.NameToLayer("Pickups"), shrinePrefab.layer);
            Assert.IsNotNull(shrinePrefab.GetComponent<ChallengeShrine>());
            Assert.IsNotNull(shrinePrefab.transform.Find("BeaconBeam"));
            Assert.IsNotNull(shrinePrefab.transform.Find("RuneRing"));

            // 8. Verify HeroData, WeaponData, HeroCatalog, Achievements, ArenaConfig, WaveConfig
            var ranger = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/RangerData.asset");
            var necro = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/NecromancerHeroData.asset");
            var bow = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/Weapons/RapidBowWeaponData.asset");
            var soul = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/Weapons/SoulBoltWeaponData.asset");

            Assert.IsNotNull(ranger);
            Assert.IsNotNull(necro);
            Assert.IsNotNull(bow);
            Assert.IsNotNull(soul);

            Assert.IsFalse(ranger.unlockedByDefault);
            Assert.AreEqual(HeroPassiveType.MoveSpeedPercent, ranger.passiveType);
            Assert.AreEqual(HeroUltimateType.ArrowRain, ranger.ultimateType);
            Assert.AreSame(bow, ranger.startingWeapon);
            Assert.IsNotNull(ranger.icon);
            Assert.IsNotNull(ranger.ultimateIcon);

            Assert.IsFalse(necro.unlockedByDefault);
            Assert.AreEqual(HeroPassiveType.XpAndMagnetPercent, necro.passiveType);
            Assert.AreEqual(HeroUltimateType.SoulNova, necro.ultimateType);
            Assert.AreSame(soul, necro.startingWeapon);
            Assert.Greater(soul.lifestealChance, 0f);
            Assert.Greater(soul.lifestealAmount, 0);
            Assert.IsNotNull(necro.icon);
            Assert.IsNotNull(necro.ultimateIcon);

            var catalog = AssetDatabase.LoadAssetAtPath<HeroCatalog>("Assets/Resources/HeroCatalog.asset");
            Assert.IsNotNull(catalog);
            Assert.AreEqual(4, catalog.heroes.Count, "Knight, Mage, Ranger and Necromancer");
            Assert.IsTrue(catalog.heroes.Contains(ranger));
            Assert.IsTrue(catalog.heroes.Contains(necro));

            var kill5000 = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/clear_3_shrines.asset");
            var defeatNecro = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/defeat_3_bosses.asset");
            Assert.AreEqual(AchievementRewardType.UnlockHero, kill5000.rewardType);
            Assert.AreEqual("ranger", kill5000.rewardId);
            Assert.AreEqual(AchievementRewardType.UnlockHero, defeatNecro.rewardType);
            Assert.AreEqual("necromancer", defeatNecro.rewardId);

            using (var saveManager = new SaveManager())
            {
                var achMgr = new AchievementManager(saveManager);
                Assert.IsFalse(saveManager.IsHeroUnlocked("ranger"));
                Assert.IsFalse(saveManager.IsHeroUnlocked("necromancer"));

                for (int i = 0; i < 3; i++) achMgr.OnShrineCleared();
                achMgr.OnBossDefeated("golem");
                achMgr.OnBossDefeated("necromancer");
                achMgr.OnBossDefeated("frost_lich");

                Assert.IsTrue(saveManager.IsHeroUnlocked("ranger"));
                Assert.IsTrue(saveManager.IsHeroUnlocked("necromancer"));
            }

            var arenaConfig = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.AreSame(barrelPrefab, arenaConfig.ExplosiveBarrelPrefab);
            Assert.AreSame(shrinePrefab, arenaConfig.ChallengeShrinePrefab);

            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.AreEqual(0.25f, waveConfig.miniBossVacuumDropChance, 0.001f);
            Assert.AreEqual(1.0f, waveConfig.bossVacuumDropChance, 0.001f);

            // 9. Verify Gameplay.unity VacuumMagnetSpawner
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;
            try
            {
                VacuumMagnetSpawner spawner = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    spawner = root.GetComponentInChildren<VacuumMagnetSpawner>(true);
                    if (spawner != null) break;
                }
                Assert.IsNotNull(spawner, "Gameplay.unity must contain VacuumMagnetSpawner");
                var spawnerSo = new SerializedObject(spawner);
                Assert.IsNotNull(spawnerSo.FindProperty("vacuumMagnetPrefab").objectReferenceValue);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void VerifyHeroVisualPrefab(string prefabPath, string modelChildName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"Missing {prefabPath}");
            Transform model = prefab.transform.Find(modelChildName);
            Assert.IsNotNull(model, $"{prefabPath} missing {modelChildName}");
            Assert.AreEqual(new Vector3(0f, -1f, 0f), model.localPosition);
            var mf = model.GetComponent<MeshFilter>();
            var mr = model.GetComponent<MeshRenderer>();
            var anim = model.GetComponent<VoxelMeshAnimator>();
            Assert.IsNotNull(mf?.sharedMesh);
            Assert.AreEqual(mf.sharedMesh.subMeshCount, mr.sharedMaterials.Length);
            Assert.IsNotNull(anim);
            Assert.AreEqual(4, anim.Frames.Length);
        }

        [Test]
        public void HeroRuntimeStats_RangerAndNecromancerPassives_ApplySpeedXpAndMagnetBonuses()
        {
            using (var saveManager = new SaveManager())
            {
                saveManager.SetMetaUpgradeLevel("hero_speed", 4, saveImmediately: false);   // +20% (1.2x)
                saveManager.SetMetaUpgradeLevel("xp_bonus", 2, saveImmediately: false);     // +20% (1.2x)
                saveManager.SetMetaUpgradeLevel("magnet_radius", 2, saveImmediately: false);// +30% (1.3x)

                var meta = new MetaUpgradeManager(saveManager);
                var stats = new HeroRuntimeStats(meta);

                var ranger = Track(ScriptableObject.CreateInstance<HeroData>());
                ranger.id = "ranger";
                ranger.moveSpeed = 5.6f;
                ranger.passiveType = HeroPassiveType.MoveSpeedPercent;
                ranger.passiveValue = 25f;

                stats.ApplyHeroPassive(ranger, notify: false);
                // 5.6 * 1.25 * 1.20 = 8.4
                Assert.AreEqual(8.4f, stats.GetMoveSpeed(ranger.moveSpeed), 0.001f);
                Assert.AreEqual(1.2f, stats.XpMultiplier, 0.001f);

                var necro = Track(ScriptableObject.CreateInstance<HeroData>());
                necro.id = "necromancer";
                necro.moveSpeed = 5.0f;
                necro.passiveType = HeroPassiveType.XpAndMagnetPercent;
                necro.passiveValue = 35f;

                stats.ApplyHeroPassive(necro, notify: false);
                // Speed passive cleared: 5.0 * 1.20 = 6.0
                Assert.AreEqual(6.0f, stats.GetMoveSpeed(necro.moveSpeed), 0.001f);
                // XP: 1.2 * 1.35 = 1.62; Magnet: 3.0 * 1.35 * 1.30 = 5.265
                Assert.AreEqual(1.62f, stats.XpMultiplier, 0.001f);
                Assert.AreEqual(5.265f, stats.GetMagnetRadius(3.0f), 0.001f);
            }
        }

        [Test]
        public void HeroUltimate_ArrowRain_Deals3WavesAndSlowsEnemies()
        {
            var bus = new GameEventBus();
            int waveEvents = 0;
            bus.Subscribe<ArrowRainWaveEvent>(_ => waveEvents++);

            var ranger = Track(ScriptableObject.CreateInstance<HeroData>());
            ranger.id = "ranger";
            ranger.ultimateType = HeroUltimateType.ArrowRain;
            ranger.ultimateDuration = 2.4f;
            ranger.ultimateCooldown = 20f;

            var heroGo = Track(new GameObject("TestRangerHero"));
            heroGo.transform.position = Vector3.zero;
            var health = heroGo.AddComponent<HeroHealth>();
            var ult = heroGo.AddComponent<HeroUltimate>();
            var stats = new HeroRuntimeStats();
            ult.Construct(health, null, null, bus, stats);

            var ultSo = new SerializedObject(ult);
            ultSo.FindProperty("fallbackHeroData").objectReferenceValue = ranger;
            ultSo.FindProperty("enemyLayer").intValue = 1 << 8;
            ultSo.ApplyModifiedPropertiesWithoutUndo();

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 300;
            enemyData.speed = 4f;
            enemyData.contactDamage = 5;

            var enemyGo = Track(new GameObject("ArrowRainTarget"));
            enemyGo.layer = 8;
            enemyGo.transform.position = new Vector3(4f, 0f, 0f);
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            Physics.SyncTransforms();

            Assert.IsTrue(ult.TryActivate());
            Assert.IsTrue(ult.IsActive);
            Assert.IsFalse(health.IsInvulnerable);
            Assert.AreEqual(1, waveEvents);
            Assert.AreEqual(250, enemy.CurrentHp);
            Assert.AreEqual(2.0f, enemy.EffectiveMoveSpeed, 0.01f);

            ult.TriggerArrowRainWave();
            ult.TriggerArrowRainWave();

            Assert.AreEqual(3, waveEvents);
            Assert.AreEqual(150, enemy.CurrentHp);
        }

        [Test]
        public void HeroUltimate_SoulNova_Heals35PercentAndPiercesMultipleEnemies()
        {
            var bus = new GameEventBus();
            int castEvents = 0;
            int recordedHeal = 0;
            bus.Subscribe<SoulNovaCastEvent>(evt =>
            {
                castEvents++;
                recordedHeal = evt.HealAmount;
            });

            var necro = Track(ScriptableObject.CreateInstance<HeroData>());
            necro.id = "necromancer";
            necro.ultimateType = HeroUltimateType.SoulNova;
            necro.ultimateDuration = 1.0f;
            necro.ultimateCooldown = 24f;

            var heroGo = Track(new GameObject("TestNecroHero"));
            heroGo.transform.position = Vector3.zero;
            var health = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 200);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 100);

            var ult = heroGo.AddComponent<HeroUltimate>();
            var stats = new HeroRuntimeStats();
            ult.Construct(health, null, null, bus, stats);

            var ultSo = new SerializedObject(ult);
            ultSo.FindProperty("fallbackHeroData").objectReferenceValue = necro;
            ultSo.FindProperty("enemyLayer").intValue = 1 << 8;
            ultSo.ApplyModifiedPropertiesWithoutUndo();

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 200;
            enemyData.contactDamage = 5;

            var enemy1Go = Track(new GameObject("SoulTarget1"));
            enemy1Go.layer = 8;
            enemy1Go.transform.position = new Vector3(3.5f, 0f, 0f);
            enemy1Go.AddComponent<BoxCollider>();
            var enemy1 = enemy1Go.AddComponent<EnemyBase>();
            enemy1.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            var enemy2Go = Track(new GameObject("SoulTarget2"));
            enemy2Go.layer = 8;
            enemy2Go.transform.position = new Vector3(6.5f, 0f, 0f);
            enemy2Go.AddComponent<BoxCollider>();
            var enemy2 = enemy2Go.AddComponent<EnemyBase>();
            enemy2.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            Physics.SyncTransforms();

            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, castEvents);
            Assert.AreEqual(70, recordedHeal);
            Assert.AreEqual(170, health.CurrentHp);

            for (int i = 0; i < 12; i++)
            {
                ult.StepSoulNova(0.05f);
            }

            Assert.AreEqual(140, enemy1.CurrentHp, "Bolt 0 (+X) must pierce enemy 1 exactly once for 60 damage");
            Assert.AreEqual(140, enemy2.CurrentHp, "Bolt 0 (+X) must continue piercing through enemy 2 for 60 damage");
        }

        [Test]
        public void Projectile_SoulBoltWeapon_LifestealHealsHeroOnHit()
        {
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("LifestealHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 100);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 50);

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.contactDamage = 5;

            var enemyGo = Track(new GameObject("LifestealTarget"));
            enemyGo.layer = 8;
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            var weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            weapon.damage = 11;
            weapon.projectileSpeed = 16f;
            weapon.lifestealChance = 1f;
            weapon.lifestealAmount = 2;

            var projGo = Track(new GameObject("TestSoulProj"));
            var proj = projGo.AddComponent<Projectile>();
            proj.Initialize(weapon, enemyGo.transform, null, 1f, 1 << 8, null, heroHealth);

            var hitMethod = typeof(Projectile).GetMethod("HitTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            hitMethod.Invoke(proj, null);
            Assert.AreEqual(52, heroHealth.CurrentHp);

            weapon.lifestealChance = 0f;
            proj.Initialize(weapon, enemyGo.transform, null, 1f, 1 << 8, null, heroHealth);
            hitMethod.Invoke(proj, null);
            Assert.AreEqual(52, heroHealth.CurrentHp);
        }

        [Test]
        public void XpAndCoinManagers_MagnetizeAll_AndVacuumMagnetPickup_MagnetizesAllActivePickups()
        {
            var bus = new GameEventBus();
            int vacuumEvents = 0;
            int lastXp = 0;
            int lastCoins = 0;
            bus.Subscribe<VacuumMagnetCollectedEvent>(evt =>
            {
                vacuumEvents++;
                lastXp = evt.XpMagnetized;
                lastCoins = evt.CoinsMagnetized;
            });

            var heroGo = Track(new GameObject("VacuumHero"));
            heroGo.transform.position = Vector3.zero;
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroExp = heroGo.AddComponent<HeroExperience>();
            var heroEcon = heroGo.AddComponent<HeroEconomy>();

            var xpGo = Track(new GameObject("TestXpMgr"));
            var xpMgr = xpGo.AddComponent<XpManager>();
            xpMgr.Construct(heroHealth, heroExp);

            var coinGo = Track(new GameObject("TestCoinMgr"));
            var coinMgr = coinGo.AddComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);

            var xpPool = new ObjectPool<XpCrystal>(() =>
            {
                var go = Track(new GameObject("TestXpCrystal"));
                return go.AddComponent<XpCrystal>();
            });
            var coinPool = new ObjectPool<CoinPickup>(() =>
            {
                var go = Track(new GameObject("TestCoinPickup"));
                return go.AddComponent<CoinPickup>();
            });

            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(XpManager).GetField("xpPool", flags).SetValue(xpMgr, xpPool);
            typeof(CoinManager).GetField("coinPool", flags).SetValue(coinMgr, coinPool);

            xpMgr.SpawnXp(new Vector3(12f, 0f, 0f), 2);
            xpMgr.SpawnXp(new Vector3(-15f, 0f, 8f), 2);
            xpMgr.SpawnXp(new Vector3(0f, 0f, 20f), 2);
            coinMgr.SpawnCoin(new Vector3(18f, 0f, -5f), 1);
            coinMgr.SpawnCoin(new Vector3(-9f, 0f, -11f), 1);

            Assert.AreEqual(3, xpMgr.ActiveCount);
            Assert.AreEqual(2, coinMgr.ActiveCount);

            var spawnerGo = Track(new GameObject("TestVacuumSpawner"));
            var spawner = spawnerGo.AddComponent<VacuumMagnetSpawner>();
            spawner.Construct(heroHealth, xpMgr, coinMgr, bus, null);

            var vacuum = spawner.SpawnVacuumMagnet(new Vector3(2f, 0f, 0f));
            Assert.IsNotNull(vacuum);
            vacuum.Collect();
            Assert.IsTrue(vacuum.IsCollected);

            Assert.AreEqual(1, vacuumEvents);
            Assert.AreEqual(3, lastXp);
            Assert.AreEqual(2, lastCoins);

            // Drop rate behavior: boss 100%, mini-boss 25%
            Assert.IsTrue(spawner.TryDropFromEnemyDeath(Vector3.zero, wasBoss: true, wasMiniBoss: false, roll: 0.99f));
            Assert.IsTrue(spawner.TryDropFromEnemyDeath(Vector3.zero, wasBoss: false, wasMiniBoss: true, roll: 0.1f));
            Assert.IsFalse(spawner.TryDropFromEnemyDeath(Vector3.zero, wasBoss: false, wasMiniBoss: true, roll: 0.5f));
        }

        [Test]
        public void ExplosiveBarrel_DetonatesOnDamageAndChainDetonates_WithoutDamagingHero()
        {
            var bus = new GameEventBus();
            int barrelEvents = 0;
            bus.Subscribe<BarrelExplodedEvent>(_ => barrelEvents++);

            var heroGo = Track(new GameObject("BarrelHero"));
            heroGo.transform.position = new Vector3(1.5f, 0f, 0f);
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 200);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 200);

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 500;
            enemyData.contactDamage = 5;

            var enemyGo = Track(new GameObject("BarrelEnemy"));
            enemyGo.layer = 8;
            enemyGo.transform.position = new Vector3(3.2f, 0f, 0f);
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Arena/ExplosiveBarrel.prefab");
            var barrel1Go = Track(Object.Instantiate(barrelPrefab));
            barrel1Go.transform.position = Vector3.zero;
            var barrel1 = barrel1Go.GetComponent<ExplosiveBarrel>();
            barrel1.Initialize(heroHealth, bus);

            var barrel2Go = Track(Object.Instantiate(barrelPrefab));
            barrel2Go.transform.position = new Vector3(2.5f, 0f, 0f);
            var barrel2 = barrel2Go.GetComponent<ExplosiveBarrel>();
            barrel2.Initialize(heroHealth, bus);

            Physics.SyncTransforms();

            barrel1.TakeDamage(1);

            Assert.IsTrue(barrel1.IsDead);
            Assert.IsTrue(barrel2.IsDead);
            Assert.IsFalse(barrel1Go.activeSelf);
            Assert.IsFalse(barrel2Go.activeSelf);
            Assert.AreEqual(2, barrelEvents);
            Assert.AreEqual(200, enemy.CurrentHp, "Both 150-dmg barrels hit enemy (500 - 300 = 200)");
            Assert.AreEqual(200, heroHealth.CurrentHp, "Explosive barrels must not damage the hero");
        }

        [Test]
        public void ChallengeShrine_Spawns6ElitesAndGrantsCoinsPlusLevelUpOnClear()
        {
            var bus = new GameEventBus();
            var shrineEvents = new List<ShrineEventType>();
            bus.Subscribe<ChallengeShrineEvent>(evt => shrineEvents.Add(evt.EventType));

            var runStats = new RunStatistics();
            var heroGo = Track(new GameObject("ShrineHero"));
            heroGo.transform.position = Vector3.zero;
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroEcon = heroGo.AddComponent<HeroEconomy>();
            heroEcon.Construct(runStats, bus);
            var heroExp = heroGo.AddComponent<HeroExperience>();
            heroExp.Construct(new HeroRuntimeStats(), bus);
            heroExp.EnsureStatsInitialized();
            int startLevel = heroExp.CurrentLevel;

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.contactDamage = 5;

            var spawnedElites = new List<EnemyBase>();
            var shrinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Arena/ChallengeShrine.prefab");
            var shrineGo = Track(Object.Instantiate(shrinePrefab));
            shrineGo.transform.position = Vector3.zero;
            var shrine = shrineGo.GetComponent<ChallengeShrine>();

            shrine.Initialize(
                heroHealth,
                heroEcon,
                heroExp,
                null,
                null,
                bus,
                (pos, archetype) =>
                {
                    var eGo = Track(new GameObject($"ShrineElite_{spawnedElites.Count}"));
                    eGo.layer = 8;
                    eGo.transform.position = pos;
                    eGo.AddComponent<BoxCollider>();
                    var e = eGo.AddComponent<EnemyBase>();
                    e.Initialize(enemyData, heroGo.transform, null, bus, 1f);
                    e.MakeElite(archetype, null);
                    spawnedElites.Add(e);
                    return e;
                });

            Assert.AreEqual(ShrineState.Idle, shrine.State);
            Assert.IsTrue(shrine.ActivateChallenge());
            Assert.AreEqual(ShrineState.Active, shrine.State);
            Assert.AreEqual(6, shrine.SpawnedEliteCount);
            Assert.AreEqual(6, shrine.RemainingEliteCount);
            Assert.AreEqual(6, spawnedElites.Count);

            for (int i = 0; i < 5; i++)
            {
                spawnedElites[i].TakeDamage(9999);
            }

            Assert.AreEqual(ShrineState.Active, shrine.State);
            Assert.AreEqual(1, shrine.RemainingEliteCount);
            Assert.AreEqual(0, runStats.CoinsEarned);
            Assert.AreEqual(startLevel, heroExp.CurrentLevel);

            spawnedElites[5].TakeDamage(9999);

            Assert.AreEqual(ShrineState.Completed, shrine.State);
            Assert.AreEqual(0, shrine.RemainingEliteCount);
            Assert.AreEqual(100, runStats.CoinsEarned);
            Assert.AreEqual(startLevel + 1, heroExp.CurrentLevel);
            Assert.IsFalse(shrine.ActivateChallenge());
            Assert.IsTrue(shrineEvents.Contains(ShrineEventType.Started));
            Assert.IsTrue(shrineEvents.Contains(ShrineEventType.Progress));
            Assert.IsTrue(shrineEvents.Contains(ShrineEventType.Completed));
        }

        [Test]
        public void ArenaGenerator_Generates8To12BarrelsAnd1ChallengeShrine()
        {
            var realConfig = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.IsNotNull(realConfig);

            var testConfig = Track(ScriptableObject.CreateInstance<ArenaConfig>());
            testConfig.ArenaSize = 160f;
            testConfig.Seed = 12345;
            testConfig.ObstaclePrefabs = realConfig.ObstaclePrefabs;
            testConfig.MinObstacles = 8;
            testConfig.MaxObstacles = 10;
            testConfig.SafeZoneRadius = 5f;
            testConfig.DecorationPrefabs = null;
            testConfig.MinDecorations = 0;
            testConfig.MaxDecorations = 0;
            testConfig.ExplosiveBarrelPrefab = realConfig.ExplosiveBarrelPrefab;
            testConfig.MinExplosiveBarrels = 8;
            testConfig.MaxExplosiveBarrels = 12;
            testConfig.BarrelMinDistanceFromCenter = 12f;
            testConfig.BarrelMaxDistanceFromCenter = 65f;
            testConfig.ChallengeShrinePrefab = realConfig.ChallengeShrinePrefab;
            testConfig.ShrineMinDistanceFromCenter = 18f;
            testConfig.ShrineMaxDistanceFromCenter = 48f;

            var genGo = Track(new GameObject("TestArenaGen"));
            var gen = genGo.AddComponent<ArenaGenerator>();
            var so = new SerializedObject(gen);
            so.FindProperty("_config").objectReferenceValue = testConfig;
            so.FindProperty("_generateOnStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            gen.GenerateArena(12345);

            Assert.GreaterOrEqual(gen.SpawnedBarrels.Count, 8);
            Assert.LessOrEqual(gen.SpawnedBarrels.Count, 12);
            Assert.IsNotNull(gen.ActiveShrine);

            gen.ClearArena();
        }

        [Test]
        public void GameLifetimeScope_GameplayScene_BuildsAndResolvesWithoutCircularDependencies()
        {
            const string gameplayScenePath = "Assets/_Scenes/Gameplay.unity";
            Scene gameplayScene = SceneManager.GetSceneByPath(gameplayScenePath);
            bool openedAdditively = false;
            if (!gameplayScene.isLoaded)
            {
                gameplayScene = EditorSceneManager.OpenScene(gameplayScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            var rootGo = Track(new GameObject("TestRootLifetimeScope"));
            rootGo.SetActive(false);
            var rootScope = rootGo.AddComponent<RootLifetimeScope>();

            GameLifetimeScope gameScope = null;
            try
            {
                using (LifetimeScope.EnqueueParent(null))
                {
                    rootScope.Build();
                }

                GameObject[] roots = gameplayScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    gameScope = roots[i].GetComponentInChildren<GameLifetimeScope>(true);
                    if (gameScope != null)
                    {
                        break;
                    }
                }

                Assert.IsNotNull(gameScope, "Gameplay.unity must contain GameLifetimeScope");

                using (LifetimeScope.EnqueueParent(rootScope))
                {
                    gameScope.Build();
                }

                Assert.IsNotNull(gameScope.Container);

                var waveManager = gameScope.Container.Resolve<WaveManager>();
                var arenaGenerator = gameScope.Container.Resolve<ArenaGenerator>();
                var gameManager = gameScope.Container.Resolve<GameManager>();
                var vacuumMagnetSpawner = gameScope.Container.Resolve<VacuumMagnetSpawner>();
                var secretItemSpawner = gameScope.Container.Resolve<SecretItemSpawner>();
                var heroHealth = gameScope.Container.Resolve<HeroHealth>();
                var heroCombat = gameScope.Container.Resolve<HeroCombat>();
                var heroUltimate = gameScope.Container.Resolve<HeroUltimate>();
                var debugCheatMenu = gameScope.Container.Resolve<DebugCheatMenu>();

                Assert.IsNotNull(waveManager);
                Assert.IsNotNull(arenaGenerator);
                Assert.IsNotNull(gameManager);
                Assert.IsNotNull(vacuumMagnetSpawner);
                Assert.IsNotNull(secretItemSpawner);
                Assert.IsNotNull(heroHealth);
                Assert.IsNotNull(heroCombat);
                Assert.IsNotNull(heroUltimate);
                Assert.IsNotNull(debugCheatMenu);

                Transform heroTargetField = typeof(WaveManager)
                    .GetField("heroTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(waveManager) as Transform;
                ArenaGenerator injectedArenaField = typeof(WaveManager)
                    .GetField("arenaGenerator", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(waveManager) as ArenaGenerator;

                Assert.IsNotNull(heroTargetField, "WaveManager must receive heroTarget via DI");
                Assert.AreSame(arenaGenerator, injectedArenaField, "WaveManager must receive ArenaGenerator via DI");

                MethodInfo resolveDeferred = typeof(ArenaGenerator)
                    .GetMethod("ResolveDeferredDependencies", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(resolveDeferred);
                resolveDeferred.Invoke(arenaGenerator, null);

                WaveManager deferredWave = typeof(ArenaGenerator)
                    .GetField("_waveManager", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(arenaGenerator) as WaveManager;
                GameManager deferredGame = typeof(ArenaGenerator)
                    .GetField("_gameManager", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(arenaGenerator) as GameManager;

                Assert.AreSame(waveManager, deferredWave);
                Assert.AreSame(gameManager, deferredGame);
            }
            finally
            {
                gameScope?.DisposeCore();
                rootScope?.DisposeCore();
                if (openedAdditively && gameplayScene.IsValid() && gameplayScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(gameplayScene, true);
                }
            }
        }
    }
}
