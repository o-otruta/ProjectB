using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    public class Sprint10EventsAndDemonLordTests
    {
        private readonly List<UnityEngine.Object> createdAssetsOrObjects = new List<UnityEngine.Object>();
        private readonly HashSet<GameObject> preExistingRoots = new HashSet<GameObject>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            SecretCompassIndicatorUI.ClearDynamicPois();
            Time.timeScale = 1f;

            preExistingRoots.Clear();
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded)
            {
                foreach (GameObject root in active.GetRootGameObjects())
                {
                    if (root != null) preExistingRoots.Add(root);
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            SecretCompassIndicatorUI.ClearDynamicPois();
            Time.timeScale = 1f;

            for (int i = createdAssetsOrObjects.Count - 1; i >= 0; i--)
            {
                if (createdAssetsOrObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdAssetsOrObjects[i]);
                }
            }
            createdAssetsOrObjects.Clear();

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded)
            {
                foreach (GameObject root in active.GetRootGameObjects())
                {
                    if (root != null && !preExistingRoots.Contains(root))
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
            preExistingRoots.Clear();
            Physics.SyncTransforms();

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

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) createdAssetsOrObjects.Add(obj);
            return obj;
        }

        private static int GetDynamicPoiCount()
        {
            FieldInfo field = typeof(SecretCompassIndicatorUI).GetField("dynamicPoiCount", BindingFlags.NonPublic | BindingFlags.Static);
            return field != null ? (int)field.GetValue(null) : -1;
        }

        private static GameObject LoadPrefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private (GameObject heroGo, HeroHealth heroHealth, HeroEconomy heroEconomy, HeroExperience heroExperience, RunStatistics stats, GameEventBus bus) CreateTestHero()
        {
            var bus = new GameEventBus();
            var stats = new RunStatistics();
            var runtimeStats = new HeroRuntimeStats();
            var heroData = Track(ScriptableObject.CreateInstance<HeroData>());
            heroData.maxHp = 200;

            var heroGo = Track(new GameObject("TestHero"));
            heroGo.transform.position = Vector3.zero;
            int heroLayer = LayerMask.NameToLayer("Hero");
            if (heroLayer >= 0) heroGo.layer = heroLayer;

            var health = heroGo.AddComponent<HeroHealth>();
            var economy = heroGo.AddComponent<HeroEconomy>();
            var exp = heroGo.AddComponent<HeroExperience>();

            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("heroData").objectReferenceValue = heroData;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var zeroIframeRules = Track(ScriptableObject.CreateInstance<RunRulesConfig>());
            zeroIframeRules.postHitInvulnerabilityDuration = 0f;
            health.SetRunRulesConfig(zeroIframeRules);
            health.Construct(runtimeStats, bus, null);
            typeof(HeroHealth).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);

            economy.Construct(stats, bus, runtimeStats, null);
            exp.Construct(runtimeStats, bus, null);
            exp.EnsureStatsInitialized();

            return (heroGo, health, economy, exp, stats, bus);
        }

        private (CoinManager coinManager, XpManager xpManager, VacuumMagnetSpawner vacuumSpawner) CreatePickupManagers(
            HeroHealth heroHealth,
            HeroEconomy heroEconomy,
            HeroExperience heroExperience,
            GameEventBus bus,
            WaveManager waveManager)
        {
            var coinGo = Track(new GameObject("TestCoinManager"));
            var coinManager = coinGo.AddComponent<CoinManager>();
            var coinSo = new SerializedObject(coinManager);
            coinSo.FindProperty("coinPrefab").objectReferenceValue = LoadPrefab("Assets/_Prefabs/Pickups/CoinPickup.prefab");
            coinSo.ApplyModifiedPropertiesWithoutUndo();
            coinManager.Construct(heroHealth, heroEconomy);
            coinManager.EnsureInitialized();

            var xpGo = Track(new GameObject("TestXpManager"));
            var xpManager = xpGo.AddComponent<XpManager>();
            var xpSo = new SerializedObject(xpManager);
            xpSo.FindProperty("xpCrystalPrefab").objectReferenceValue = LoadPrefab("Assets/_Prefabs/Pickups/XpCrystal.prefab");
            xpSo.ApplyModifiedPropertiesWithoutUndo();
            xpManager.Construct(heroHealth, heroExperience);
            xpManager.EnsureInitialized();

            var vacGo = Track(new GameObject("TestVacuumSpawner"));
            var vacuumSpawner = vacGo.AddComponent<VacuumMagnetSpawner>();
            var vacSo = new SerializedObject(vacuumSpawner);
            vacSo.FindProperty("vacuumMagnetPrefab").objectReferenceValue = LoadPrefab("Assets/_Prefabs/Pickups/VacuumMagnetPickup.prefab")?.GetComponent<VacuumMagnetPickup>();
            vacSo.ApplyModifiedPropertiesWithoutUndo();
            vacuumSpawner.Construct(heroHealth, xpManager, coinManager, bus, waveManager);

            return (coinManager, xpManager, vacuumSpawner);
        }

        [Test]
        public void Sprint10Assets_BlenderSourcesMaterialsPrefabsWaveConfigAchievementsAndScene_AreWired()
        {
            // 1. Blender sources & OBJ models
            string[] expectedModels = { "ShieldOrc", "VoidAcolyte", "GoldGoblin", "DemonLord", "ForestMushroom", "WildFlower", "MossyRuin" };
            foreach (string name in expectedModels)
            {
                Assert.IsTrue(File.Exists($"ArtSources/Blender/{name}.blend"), $"Missing {name}.blend");
            }

            // 2. Materials (VoxelLit + instancing + URP/Lit props + telegraphs)
            string[] voxelMatNames = { "ShieldOrc", "VoidAcolyte", "GoldGoblin", "DemonLord" };
            foreach (string name in voxelMatNames)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{name}_Mat.mat");
                var glow = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{name}_Glow_Mat.mat");
                Assert.IsNotNull(mat, $"{name}_Mat missing");
                Assert.IsNotNull(glow, $"{name}_Glow_Mat missing");
                Assert.AreEqual("ProjectB/VoxelLit", mat.shader.name);
                Assert.IsTrue(mat.enableInstancing, $"{name}_Mat should enable GPU instancing");
                Assert.IsTrue(glow.enableInstancing, $"{name}_Glow_Mat should enable GPU instancing");
            }

            string[] propNames = { "ForestMushroom", "WildFlower", "MossyRuin" };
            foreach (string name in propNames)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{name}_Mat.mat");
                Assert.IsNotNull(mat, $"{name}_Mat missing");
                Assert.AreEqual("Universal Render Pipeline/Lit", mat.shader.name);
                Assert.IsTrue(mat.enableInstancing, $"{name}_Mat must support GPU instancing");
            }

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/DemonChargeTelegraphMat.mat"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/DemonMeteorTelegraphMat.mat"));

            // 3. Prefabs & ScriptableObjects
            GameObject shieldPrefab = LoadPrefab("Assets/_Prefabs/Enemies/ShieldBearer.prefab");
            GameObject summonerPrefab = LoadPrefab("Assets/_Prefabs/Enemies/VoidSummoner.prefab");
            GameObject goblinPrefab = LoadPrefab("Assets/_Prefabs/Enemies/GoldGoblin.prefab");
            GameObject demonPrefab = LoadPrefab("Assets/_Prefabs/Enemies/DemonLordBoss.prefab");
            GameObject fireballPrefab = LoadPrefab("Assets/_Prefabs/Projectiles/DemonFireballProjectile.prefab");

            Assert.IsNotNull(shieldPrefab?.GetComponent<ShieldedEnemy>());
            Assert.IsNotNull(summonerPrefab?.GetComponent<SummonerEnemy>());
            Assert.IsNotNull(goblinPrefab?.GetComponent<FleeingLootEnemy>());
            Assert.IsNotNull(demonPrefab?.GetComponent<DemonLordBoss>());
            Assert.IsNotNull(fireballPrefab?.GetComponent<EnemyProjectile>());

            foreach (string name in propNames)
            {
                GameObject prop = LoadPrefab($"Assets/_Prefabs/Decorations/{name}.prefab");
                Assert.IsNotNull(prop, $"{name}.prefab missing");
                var mr = prop.GetComponentInChildren<MeshRenderer>(true);
                Assert.IsNotNull(mr);
                Assert.AreEqual(ShadowCastingMode.Off, mr.shadowCastingMode, $"{name} should not cast shadows");
            }

            // 4. WaveConfig & ArenaConfig
            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(waveConfig);
            Assert.AreEqual(8, waveConfig.enemySchedule.Count);
            Assert.IsTrue(waveConfig.enemySchedule.Exists(e => e.enemy is ShieldedEnemyData && e.minWave == 18 && e.triggerIntroWave));
            Assert.IsTrue(waveConfig.enemySchedule.Exists(e => e.enemy is SummonerEnemyData && e.minWave == 22 && e.triggerIntroWave));

            Assert.AreEqual(4, waveConfig.bosses.Count);
            Assert.AreEqual("demon_lord", waveConfig.bosses[3].bossId);
            Assert.Greater(waveConfig.bosses[3].hp, waveConfig.bosses[2].hp, "Wave 100 Demon Lord HP should exceed Wave 75 Frost Lich HP");
            Assert.IsNotNull(waveConfig.bosses[3].demonFireballWeaponData);
            Assert.IsNotNull(waveConfig.bosses[3].demonFireballWeaponData.projectilePrefab);
            Assert.IsTrue(waveConfig.waveEventsEnabled);
            Assert.IsNotNull(waveConfig.swarmRushEnemyData);
            Assert.IsNotNull(waveConfig.goldGoblinData);

            var arenaConfig = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.IsNotNull(arenaConfig);
            Assert.AreEqual(7, arenaConfig.ObstaclePrefabs.Length, "Existing 7 obstacle prefabs must remain unchanged");
            Assert.AreEqual(3, arenaConfig.PropDecorationPrefabs.Length);
            Assert.AreEqual(12, arenaConfig.DynamicRespawnWaveInterval);
            Assert.Greater(arenaConfig.ObstacleClearancePadding, 0f);

            // 5. Boss Achievements (Wave 75 Frost Lich & Wave 100 Demon Lord)
            var frostAch = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/DefeatFrostLich.asset");
            var demonAch = AssetDatabase.LoadAssetAtPath<AchievementData>("Assets/Resources/Achievements/DefeatDemonLord.asset");
            Assert.IsNotNull(frostAch);
            Assert.IsNotNull(demonAch);
            Assert.IsNotNull(frostAch.icon, "DefeatFrostLich icon should be generated");
            Assert.IsNotNull(demonAch.icon, "DefeatDemonLord icon should be generated");

            var saveManager = new SaveManager();
            var achManager = new AchievementManager(saveManager);
            achManager.OnBossDefeated("frost_lich");
            achManager.OnBossDefeated("demon_lord");
            Assert.IsTrue(saveManager.Data.completedAchievements.Contains("defeat_frost_lich"));
            Assert.IsTrue(saveManager.Data.completedAchievements.Contains("defeat_demon_lord"));

            // 6. Gameplay Scene wiring & DI resolution
            Scene gameplayScene = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedAdditively = false;
            if (!gameplayScene.isLoaded)
            {
                gameplayScene = EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive);
                openedAdditively = true;
            }

            var rootGo = Track(new GameObject("TempRootScope"));
            rootGo.SetActive(false);
            var rootScope = rootGo.AddComponent<RootLifetimeScope>();
            GameLifetimeScope gameScope = null;
            try
            {
                WaveEventManager sceneEventManager = null;
                WaveEventBannerUI sceneBanner = null;
                foreach (GameObject root in gameplayScene.GetRootGameObjects())
                {
                    if (sceneEventManager == null) sceneEventManager = root.GetComponentInChildren<WaveEventManager>(true);
                    if (sceneBanner == null) sceneBanner = root.GetComponentInChildren<WaveEventBannerUI>(true);
                    if (gameScope == null) gameScope = root.GetComponentInChildren<GameLifetimeScope>(true);
                }

                Assert.IsNotNull(sceneEventManager, "Gameplay.unity must have WaveEventManager");
                Assert.IsNotNull(sceneEventManager.ArenaDirectionalLight, "WaveEventManager must reference directional Light");
                Assert.IsNotNull(sceneBanner, "Gameplay.unity must have WaveEventBannerUI");

                var bannerRootProp = new SerializedObject(sceneBanner).FindProperty("bannerRoot").objectReferenceValue as GameObject;
                Assert.IsNotNull(bannerRootProp);
                Assert.IsFalse(bannerRootProp.activeSelf, "WaveEventBanner Content root should start hidden");

                foreach (Graphic g in sceneBanner.GetComponentsInChildren<Graphic>(true))
                {
                    Assert.IsFalse(g.raycastTarget, $"Banner graphic {g.name} must disable raycastTarget");
                }

                Assert.IsNotNull(gameScope);
                using (LifetimeScope.EnqueueParent(null))
                {
                    rootScope.Build();
                }
                using (LifetimeScope.EnqueueParent(rootScope))
                {
                    gameScope.Build();
                }

                Assert.IsNotNull(gameScope.Container.Resolve(typeof(WaveEventManager)));
                Assert.IsNotNull(gameScope.Container.Resolve(typeof(WaveEventBannerUI)));
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

        [Test]
        public void WaveEventManager_SchedulingSkipsSpecialWavesAndRunsAllFourEvents()
        {
            WaveConfig realConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            WaveConfig cfg = Track(Object.Instantiate(realConfig));

            // Skips early waves (1-3), mini-boss (5,10,15,20), intro (3,6,7,11,14,18,22), and boss (25,50,75,100)
            int[] skippedWaves = { 1, 2, 3, 5, 6, 7, 10, 11, 14, 15, 18, 20, 22, 25, 50, 75, 100 };
            foreach (int w in skippedWaves)
            {
                Assert.IsFalse(WaveEventManager.CanTriggerEventOnWave(cfg, w), $"Wave {w} must NOT trigger a Wave Event");
            }
            int[] allowedWaves = { 4, 8, 12, 16, 19, 23 };
            foreach (int w in allowedWaves)
            {
                Assert.IsTrue(WaveEventManager.CanTriggerEventOnWave(cfg, w), $"Regular wave {w} should allow Wave Events");
            }

            var (heroGo, heroHealth, heroEconomy, heroExp, stats, bus) = CreateTestHero();
            var saveManager = new SaveManager();
            var achManager = new AchievementManager(saveManager);
            var projPool = new EnemyProjectilePool();

            var wmGo = Track(new GameObject("TestWaveManager"));
            var wm = wmGo.AddComponent<WaveManager>();
            new SerializedObject(wm).ApplyModifiedPropertiesWithoutUndo();
            var wmSo = new SerializedObject(wm);
            wmSo.FindProperty("waveConfig").objectReferenceValue = cfg;
            wmSo.ApplyModifiedPropertiesWithoutUndo();

            var (coinManager, xpManager, vacuumSpawner) = CreatePickupManagers(heroHealth, heroEconomy, heroExp, bus, wm);
            wm.Construct(heroHealth, achManager, bus, projPool, null, coinManager);
            wm.SetVacuumMagnetSpawner(vacuumSpawner);

            var deathHandler = new EnemyDeathHandler(xpManager, coinManager, stats, achManager, bus, wm, vacuumSpawner, null, null);
            var lightGo = Track(new GameObject("TestDirLight"));
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = Color.white;
            dirLight.intensity = 1f;

            var emGo = Track(new GameObject("TestWaveEventManager"));
            var em = emGo.AddComponent<WaveEventManager>();
            var emSo = new SerializedObject(em);
            emSo.FindProperty("overrideConfig").objectReferenceValue = cfg;
            emSo.FindProperty("arenaDirectionalLight").objectReferenceValue = dirLight;
            emSo.ApplyModifiedPropertiesWithoutUndo();
            em.Construct(wm, heroHealth, heroEconomy, coinManager, vacuumSpawner, bus, null);

            deathHandler = new EnemyDeathHandler(xpManager, coinManager, stats, achManager, bus, wm, vacuumSpawner, null, em);
            deathHandler.Start();

            int startedCount = 0;
            int endedCount = 0;
            WaveEventEndedEvent lastEnded = default;
            bus.Subscribe<WaveEventStartedEvent>(_ => startedCount++);
            bus.Subscribe<WaveEventEndedEvent>(e => { endedCount++; lastEnded = e; });

            try
            {
                // A. Swarm Rush spawns 24-32 mobs
                Assert.IsTrue(em.StartEvent(WaveEventType.SwarmRush));
                Assert.AreEqual(WaveEventType.SwarmRush, em.ActiveEventType);
                Assert.GreaterOrEqual(wm.EnemiesAlive, cfg.swarmRushMinCount);
                Assert.LessOrEqual(wm.EnemiesAlive, cfg.swarmRushMaxCount);
                em.EndCurrentEvent(completed: true, Vector3.zero);
                wm.KillAllActiveEnemies();

                // B. Gold Goblin: hit coins, kill burst 40-80 + Magnet, compass POI, and timeout escape without death loot
                Assert.IsTrue(em.StartEvent(WaveEventType.GoldGoblin));
                FleeingLootEnemy goblin = em.ActiveGoldGoblin;
                Assert.IsNotNull(goblin);
                Assert.IsFalse(goblin.CanBeLeashed);
                Assert.GreaterOrEqual(GetDynamicPoiCount(), 1, "GoldGoblin must register a Compass POI");

                goblin.TakeDamageFrom(10, false, heroGo.transform.position, isAoE: false);
                Assert.Greater(goblin.HitCoinsDropped, 0, "Damaging Gold Goblin should spill coins");

                int magnetsBeforeKill = Object.FindObjectsByType<VacuumMagnetPickup>(FindObjectsInactive.Exclude).Length;
                goblin.TakeDamageFrom(99999, false, heroGo.transform.position, isAoE: false);
                Assert.IsTrue(goblin.IsDead);
                Assert.GreaterOrEqual(goblin.LastDeathCoinsDropped, cfg.goldGoblinData.deathCoinMin);
                Assert.LessOrEqual(goblin.LastDeathCoinsDropped, cfg.goldGoblinData.deathCoinMax);
                Assert.AreEqual(0, GetDynamicPoiCount(), "Compass POI should unregister on GoldGoblin death");

                int magnetsAfterKill = Object.FindObjectsByType<VacuumMagnetPickup>(FindObjectsInactive.Exclude).Length;
                Assert.AreEqual(magnetsBeforeKill + 1, magnetsAfterKill, "Killing GoldGoblin should guarantee a VacuumMagnet drop");

                // Timeout escape test
                Assert.IsTrue(em.StartEvent(WaveEventType.GoldGoblin));
                em.TickEvent(cfg.goldGoblinDuration + 0.5f);
                Assert.IsFalse(em.IsEventActive);
                Assert.IsNull(em.ActiveGoldGoblin);
                Assert.IsFalse(lastEnded.Completed);
                Assert.AreEqual(0, GetDynamicPoiCount());
                Assert.AreEqual(magnetsAfterKill, Object.FindObjectsByType<VacuumMagnetPickup>(FindObjectsInactive.Exclude).Length, "Escaped GoldGoblin must not drop VacuumMagnet");

                // Flush any coin pickups spilled during earlier sub-tests before measuring Blood Moon x2 drops
                foreach (CoinPickup coin in Object.FindObjectsByType<CoinPickup>(FindObjectsInactive.Exclude))
                {
                    coin.Magnetize(1000f);
                    coin.Tick(1f);
                }

                // C. Blood Moon: +20% speed, red directional light, x2 XP & Coins, clean restore
                var deterministicEnemy = Track(ScriptableObject.CreateInstance<EnemyData>());
                deterministicEnemy.xpDrop = 5;
                deterministicEnemy.coinDrop = 3;
                deterministicEnemy.coinDropChance = 1f;

                int startCoins = stats.CoinsEarned;
                bus.Publish(new EnemyDiedEvent(Vector3.zero, deterministicEnemy, wasElite: false));
                foreach (CoinPickup coin in Object.FindObjectsByType<CoinPickup>(FindObjectsInactive.Exclude))
                {
                    coin.Magnetize(1000f);
                    coin.Tick(1f);
                }
                int normalCoinGain = stats.CoinsEarned - startCoins;
                Assert.AreEqual(deterministicEnemy.coinDrop, normalCoinGain, "Baseline non-BloodMoon kill should drop exact deterministic coins");

                Assert.IsTrue(em.StartEvent(WaveEventType.BloodMoon));
                Assert.AreEqual(1.2f, wm.RuntimeModifiers.EventSpeedMultiplier, 0.001f);
                em.TickEvent(1.0f);
                Assert.Greater(dirLight.color.r, dirLight.color.g, "Blood Moon should shift directional light toward crimson");

                int beforeBloodCoins = stats.CoinsEarned;
                bus.Publish(new EnemyDiedEvent(Vector3.zero, deterministicEnemy, wasElite: false));
                foreach (CoinPickup coin in Object.FindObjectsByType<CoinPickup>(FindObjectsInactive.Exclude))
                {
                    coin.Magnetize(1000f);
                    coin.Tick(1f);
                }
                int bloodCoinGain = stats.CoinsEarned - beforeBloodCoins;
                Assert.AreEqual(normalCoinGain * 2, bloodCoinGain, "Blood Moon must double dropped coin values");

                em.EndCurrentEvent(completed: true, Vector3.zero);
                Assert.AreEqual(1f, wm.RuntimeModifiers.EventSpeedMultiplier, 0.001f);
                em.TickEvent(2.0f);
                Assert.AreEqual(Color.white.g, dirLight.color.g, 0.02f);

                // D. Elite Ambush: 3 elites (Berserker, Juggernaut, HasteAura) & magnet+coins on final kill
                Assert.IsTrue(em.StartEvent(WaveEventType.EliteAmbush));
                Assert.AreEqual(3, em.AmbushRemainingElites);
                int ambushTypesMask = 0;
                var elitesCopy = new List<EnemyBase>(wm.ActiveEnemies);
                foreach (EnemyBase e in elitesCopy)
                {
                    if (e != null && e.IsElite)
                    {
                        ambushTypesMask |= 1 << (int)e.EliteType;
                    }
                }
                int expectedMask = (1 << (int)EliteArchetype.Berserker)
                    | (1 << (int)EliteArchetype.Juggernaut)
                    | (1 << (int)EliteArchetype.HasteAura);
                Assert.AreEqual(expectedMask, ambushTypesMask, "Elite Ambush should spawn all 3 distinct elite archetypes");

                int magnetsBeforeAmbush = Object.FindObjectsByType<VacuumMagnetPickup>(FindObjectsInactive.Exclude).Length;
                foreach (EnemyBase e in elitesCopy)
                {
                    if (e != null && !e.IsDead)
                    {
                        e.TakeDamageFrom(999999, false, e.transform.position, isAoE: true);
                    }
                }
                em.TickEvent(0.3f);
                Assert.IsFalse(em.IsEventActive);
                Assert.IsTrue(lastEnded.Completed);
                Assert.AreEqual(magnetsBeforeAmbush + 1, Object.FindObjectsByType<VacuumMagnetPickup>(FindObjectsInactive.Exclude).Length);
                Assert.GreaterOrEqual(startedCount, 5);
                Assert.GreaterOrEqual(endedCount, 5);
            }
            finally
            {
                deathHandler.Dispose();
                projPool.Dispose();
            }
        }

        [Test]
        public void ShieldedEnemy_Blocks65PercentFrontalNonAoEDamage_AllowsFlankRearAoEAndResetsOnPoolReuse()
        {
            var data = AssetDatabase.LoadAssetAtPath<ShieldedEnemyData>("Assets/_ScriptableObjects/Enemies/ShieldBearerData.asset");
            Assert.IsNotNull(data);

            var (_, heroHealth, _, _, _, bus) = CreateTestHero();
            var poolGo = Track(new GameObject("ShieldTest"));
            poolGo.transform.position = Vector3.zero;
            poolGo.transform.forward = Vector3.forward;
            var shieldEnemy = poolGo.AddComponent<ShieldedEnemy>();

            var fakeData = Track(Object.Instantiate(data));
            fakeData.hp = 1000;

            bool blockedFlag = false;
            int lastDamage = 0;
            bus.Subscribe<EnemyDamagedEvent>(e => { blockedFlag = e.IsBlocked; lastDamage = e.Damage; });

            var dummyPool = new UnityEngine.Pool.ObjectPool<EnemyBase>(() => shieldEnemy);
            shieldEnemy.Initialize(fakeData, heroHealth.transform, dummyPool, bus);
            shieldEnemy.transform.forward = Vector3.forward;

            // 0 degrees straight front -> 35 (65% blocked)
            int d0 = shieldEnemy.TakeDamageFrom(100, false, new Vector3(0f, 0f, 5f), isAoE: false);
            Assert.AreEqual(35, d0);
            Assert.IsTrue(blockedFlag);

            // 50 degrees front-right (within +-60) -> 35 (blocked)
            Vector3 deg50 = Quaternion.Euler(0f, 50f, 0f) * Vector3.forward * 5f;
            int d50 = shieldEnemy.TakeDamageFrom(100, false, deg50, isAoE: false);
            Assert.AreEqual(35, d50);
            Assert.IsTrue(blockedFlag);

            // 75 degrees flank (outside +-60) -> full 100
            Vector3 deg75 = Quaternion.Euler(0f, 75f, 0f) * Vector3.forward * 5f;
            int d75 = shieldEnemy.TakeDamageFrom(100, false, deg75, isAoE: false);
            Assert.AreEqual(100, d75);
            Assert.IsFalse(blockedFlag);

            // 180 degrees rear -> full 100
            int d180 = shieldEnemy.TakeDamageFrom(100, false, new Vector3(0f, 0f, -5f), isAoE: false);
            Assert.AreEqual(100, d180);
            Assert.IsFalse(blockedFlag);

            // AoE from 0 degrees front -> full 100 (bypasses shield)
            int dAoE = shieldEnemy.TakeDamageFrom(100, false, new Vector3(0f, 0f, 5f), isAoE: true);
            Assert.AreEqual(100, dAoE);
            Assert.IsFalse(blockedFlag);
            Assert.AreEqual(100, lastDamage);

            // Pool reuse resets HP cleanly
            shieldEnemy.Initialize(fakeData, heroHealth.transform, dummyPool, bus);
            Assert.AreEqual(1000, shieldEnemy.CurrentHp);
        }

        [Test]
        public void SummonerEnemy_ExecuteSummonAndHealPulse_SummonsTwoMinionsAndHealsDamagedAlliesOnly()
        {
            var summonerData = AssetDatabase.LoadAssetAtPath<SummonerEnemyData>("Assets/_ScriptableObjects/Enemies/VoidSummonerData.asset");
            Assert.IsNotNull(summonerData);

            var (_, heroHealth, _, _, _, bus) = CreateTestHero();
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer < 0) enemyLayer = 8;

            var summonerGo = Track(new GameObject("TestSummoner") { layer = enemyLayer });
            summonerGo.transform.position = Vector3.zero;
            var summonerCol = summonerGo.AddComponent<CapsuleCollider>();
            summonerCol.radius = 0.5f;
            summonerCol.height = 1.8f;
            var summoner = summonerGo.AddComponent<SummonerEnemy>();
            var dummyPool = new UnityEngine.Pool.ObjectPool<EnemyBase>(() => summoner);
            summoner.Initialize(summonerData, heroHealth.transform, dummyPool, bus);
            summoner.TakeDamageFrom(30, false, new Vector3(0f, 0f, 4f), isAoE: true);
            int summonerHpBefore = summoner.CurrentHp;

            var allyGo = Track(new GameObject("DamagedAlly") { layer = enemyLayer });
            allyGo.transform.position = new Vector3(2.5f, 0f, 0f);
            var allyCol = allyGo.AddComponent<CapsuleCollider>();
            allyCol.radius = 0.5f;
            allyCol.height = 1.5f;
            var ally = allyGo.AddComponent<EnemyBase>();
            var allyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            allyData.hp = 100;
            var allyPool = new UnityEngine.Pool.ObjectPool<EnemyBase>(() => ally);
            ally.Initialize(allyData, heroHealth.transform, allyPool, bus);
            ally.TakeDamageFrom(40, false, new Vector3(0f, 0f, 4f), isAoE: true); // Now at 60/100 HP
            Physics.SyncTransforms();

            int summons = 0;
            EnemyData lastSummonData = null;
            summoner.SetSummonCallback((data, _) => { summons++; lastSummonData = data; });

            int healEvents = 0;
            bus.Subscribe<EnemySummonHealEvent>(_ => healEvents++);

            summoner.ExecuteSummonAndHealPulse();

            Assert.AreEqual(2, summons, "Summoner should spawn 2 minions per pulse");
            Assert.AreSame(summonerData.minionData, lastSummonData);
            Assert.AreEqual(75, ally.CurrentHp, "Nearby ally at 60/100 HP should heal +15 HP (15% of MaxHP)");
            Assert.AreEqual(summonerHpBefore, summoner.CurrentHp, "Summoner should not heal itself");
            Assert.AreEqual(1, healEvents);
        }

        [Test]
        public void DemonLordBoss_Wave100SelectionTwoPhasesChargeFireballsMeteorsAndPoolReuse()
        {
            Assert.AreEqual(3, WaveManager.GetBossIndexForWave(100, 25, 4), "Wave 100 must select index 3 (Demon Lord)");

            BossData realDemon = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/DemonLordBossData.asset");
            Assert.IsNotNull(realDemon);
            BossData cfg = Track(Object.Instantiate(realDemon));
            cfg.hp = 400;
            cfg.demonFireballWeaponData = Track(Object.Instantiate(realDemon.demonFireballWeaponData));

            var (heroGo, heroHealth, _, _, _, bus) = CreateTestHero();
            heroGo.transform.position = new Vector3(0f, 0f, 4f);

            var projPool = new EnemyProjectilePool();
            var bossGo = Track(Object.Instantiate(realDemon.modelPrefab, Vector3.zero, Quaternion.identity));
            DemonLordBoss demon = bossGo.GetComponent<DemonLordBoss>();
            Assert.IsNotNull(demon);

            int enrageEvents = 0;
            int meteorEvents = 0;
            bus.Subscribe<DemonLordEnragedEvent>(_ => enrageEvents++);
            bus.Subscribe<MeteorImpactEvent>(_ => meteorEvents++);

            try
            {
                var dummyPool = new UnityEngine.Pool.ObjectPool<EnemyBase>(() => demon);
                demon.InitializeBoss(cfg, heroHealth.transform, dummyPool, bus, projPool, null, null);
                Assert.AreEqual(1, demon.CurrentPhase);
                Assert.IsFalse(demon.IsEnraged);
                float phase1Speed = demon.EffectiveMoveSpeed;

                // Phase 1 Fireball Ring = 8
                int ring1 = demon.ExecuteFireballRing();
                Assert.AreEqual(8, ring1);
                Assert.AreEqual(8, demon.LastFireballRingCount);
                Assert.AreEqual(8, Object.FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Exclude).Length);

                // Phase 1 Fire Charge winds up in 0.425s and hits hero once
                Assert.AreEqual(0.425f, realDemon.demonChargeWindupDuration, 0.001f);
                int heroHpBeforeCharge = heroHealth.CurrentHp;
                demon.BeginFireChargeWindup();
                Assert.AreEqual(DemonLordBoss.DemonLordState.WindingUpCharge, demon.CurrentState);
                demon.TickBossAbilities(0.43f);
                Assert.AreEqual(DemonLordBoss.DemonLordState.Charging, demon.CurrentState);
                demon.TickBossAbilities(0.35f); // Travels ~4.55m straight through hero at (0,0,4)
                demon.TickBossAbilities(0.15f);
                Assert.AreEqual(heroHpBeforeCharge - cfg.demonChargeDamage, heroHealth.CurrentHp, "Charge must damage hero once along dash path");

                // Phase 2 Enrage (<50% HP)
                demon.TakeDamageFrom(250, false, heroGo.transform.position, isAoE: true);
                Assert.IsTrue(demon.IsEnraged);
                Assert.AreEqual(2, demon.CurrentPhase);
                Assert.AreEqual(1, enrageEvents);
                Assert.Greater(demon.EffectiveMoveSpeed, phase1Speed * 1.3f, "Enrage should boost move speed by +35%");

                // Phase 2 Fireball Ring = 12
                int ring2 = demon.ExecuteFireballRing();
                Assert.AreEqual(12, ring2);
                Assert.AreEqual(12, demon.LastFireballRingCount);

                // Phase 2 Meteor Barrage = 5 zones, hits hero inside zone 0 and misses outside
                int zoneCount = demon.BeginMeteorBarrageTelegraph();
                Assert.AreEqual(5, zoneCount);
                Assert.AreEqual(5, demon.ActiveMeteorZoneCount);

                heroGo.transform.position = demon.GetMeteorZonePosition(0);
                int hpBeforeMeteorHit = heroHealth.CurrentHp;
                Assert.IsTrue(demon.ExecuteMeteorBarrage());
                Assert.AreEqual(5, meteorEvents);
                Assert.AreEqual(hpBeforeMeteorHit - cfg.demonMeteorDamage, heroHealth.CurrentHp);

                demon.BeginMeteorBarrageTelegraph();
                heroGo.transform.position = demon.GetMeteorZonePosition(0) + new Vector3(60f, 0f, 60f);
                Assert.IsFalse(demon.ExecuteMeteorBarrage(), "Hero outside all 5 meteor zones must not take meteor damage");

                // Pool reuse reset
                demon.InitializeBoss(cfg, heroHealth.transform, dummyPool, bus, projPool, null, null);
                Assert.AreEqual(1, demon.CurrentPhase);
                Assert.IsFalse(demon.IsEnraged);
                Assert.AreEqual(DemonLordBoss.DemonLordState.Idle, demon.CurrentState);
                Assert.AreEqual(0, demon.ActiveMeteorZoneCount);
            }
            finally
            {
                projPool.Dispose();
            }
        }

        [Test]
        public void LivingArena_PreventsObstacleOverlap_BuildsPropInstances_AndRespawnsNearHeroEvery12Waves()
        {
            ArenaConfig realConfig = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.IsNotNull(realConfig);
            ArenaConfig cfg = Track(Object.Instantiate(realConfig));
            cfg.ArenaSize = 140f;
            cfg.MinObstacles = 24;
            cfg.MaxObstacles = 24;
            cfg.MinDecorations = 120;
            cfg.MaxDecorations = 120;
            cfg.MinPropDecorations = 1400;
            cfg.MaxPropDecorations = 1500;
            cfg.MinExplosiveBarrels = 3;
            cfg.MaxExplosiveBarrels = 3;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            var preExistingObstacle = Track(new GameObject("PreExistingObstacle") { layer = obstacleLayer >= 0 ? obstacleLayer : 10 });
            preExistingObstacle.transform.position = new Vector3(20f, 0f, 20f);
            var preCol = preExistingObstacle.AddComponent<BoxCollider>();
            preCol.size = new Vector3(3.5f, 2f, 3.5f);
            Physics.SyncTransforms();

            var (heroGo, heroHealth, heroEconomy, heroExp, stats, bus) = CreateTestHero();
            var genGo = Track(new GameObject("TestArenaGenerator"));
            var gen = genGo.AddComponent<ArenaGenerator>();
            var genSo = new SerializedObject(gen);
            genSo.FindProperty("_config").objectReferenceValue = cfg;
            genSo.FindProperty("_generateOnStart").boolValue = false;
            genSo.ApplyModifiedPropertiesWithoutUndo();
            gen.Construct(null, bus, heroHealth, heroEconomy, heroExp, stats);

            gen.GenerateArena(overrideSeed: 4242);

            // 1. Overlap-free obstacle placement + pre-existing collider respect
            Assert.Greater(gen.SpawnedObstacles.Count, 0);
            for (int i = 0; i < gen.SpawnedObstacles.Count; i++)
            {
                GameObject obsA = gen.SpawnedObstacles[i];
                Assert.IsNotNull(obsA);
                float distToExisting = Vector2.Distance(new Vector2(obsA.transform.position.x, obsA.transform.position.z), new Vector2(20f, 20f));
                Assert.Greater(distToExisting, 1.8f, "Spawned obstacle should avoid pre-existing Obstacles collider");

                for (int j = i + 1; j < gen.SpawnedObstacles.Count; j++)
                {
                    GameObject obsB = gen.SpawnedObstacles[j];
                    float minSep = ArenaGenerator.ComputeObstacleClearanceRadius(obsA)
                        + ArenaGenerator.ComputeObstacleClearanceRadius(obsB)
                        + cfg.ObstacleClearancePadding;
                    float dist = Vector2.Distance(
                        new Vector2(obsA.transform.position.x, obsA.transform.position.z),
                        new Vector2(obsB.transform.position.x, obsB.transform.position.z));
                    Assert.GreaterOrEqual(dist, minSep - 0.02f, $"Obstacles {obsA.name} and {obsB.name} overlap");
                }
            }

            // 2. Instanced grass + 3 new decorative props
            Assert.IsNotNull(gen.DecorationRenderer);
            Assert.AreEqual(120, gen.DecorationRenderer.PrimaryInstanceCount);
            Assert.GreaterOrEqual(gen.DecorationRenderer.PropInstanceCount, 1400);
            Assert.LessOrEqual(gen.DecorationRenderer.PropInstanceCount, 1500);

            // 3. Dynamic barrel & shrine respawn every 12 waves around hero's current position
            Vector3 heroPos = new Vector3(15f, 0f, -15f);
            heroGo.transform.position = heroPos;
            int barrelsBefore = gen.SpawnedBarrels.Count;
            ChallengeShrine initialShrine = gen.ActiveShrine;

            // Mark initial shrine active/completed so Wave 12 triggers a fresh respawn, and first verify Wave 11 skips
            if (initialShrine != null)
            {
                typeof(ChallengeShrine).GetProperty("State")?.SetValue(initialShrine, ShrineState.Completed);
            }

            bus.Publish(new WaveCompletedEvent(11, 12));
            Assert.AreEqual(barrelsBefore, gen.SpawnedBarrels.Count, "Wave 11 must NOT trigger 12-wave respawn");

            bus.Publish(new WaveCompletedEvent(12, 13));
            int barrelsAdded = gen.SpawnedBarrels.Count - barrelsBefore;
            Assert.GreaterOrEqual(barrelsAdded, cfg.DynamicRespawnMinBarrels);
            Assert.LessOrEqual(barrelsAdded, cfg.DynamicRespawnMaxBarrels);
            Assert.IsNotNull(gen.ActiveShrine);
            Assert.AreNotSame(initialShrine, gen.ActiveShrine, "Wave 12 should spawn a fresh ChallengeShrine near the hero");

            for (int i = barrelsBefore; i < gen.SpawnedBarrels.Count; i++)
            {
                float d = Vector2.Distance(
                    new Vector2(gen.SpawnedBarrels[i].transform.position.x, gen.SpawnedBarrels[i].transform.position.z),
                    new Vector2(heroPos.x, heroPos.z));
                Assert.GreaterOrEqual(d, cfg.DynamicRespawnMinRadius - 0.5f);
                Assert.LessOrEqual(d, cfg.DynamicRespawnMaxRadius + 0.5f);
            }

            float shrineDist = Vector2.Distance(
                new Vector2(gen.ActiveShrine.transform.position.x, gen.ActiveShrine.transform.position.z),
                new Vector2(heroPos.x, heroPos.z));
            Assert.GreaterOrEqual(shrineDist, cfg.DynamicRespawnMinRadius - 0.5f);
            Assert.LessOrEqual(shrineDist, cfg.DynamicRespawnMaxRadius + 0.5f);
        }
    }
}
