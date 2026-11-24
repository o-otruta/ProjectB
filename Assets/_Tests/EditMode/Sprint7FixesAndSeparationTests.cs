using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class Sprint7FixesAndSeparationTests
    {
        private readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        private readonly HashSet<GameObject> preExistingRoots = new HashSet<GameObject>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            Time.timeScale = 1f;

            preExistingRoots.Clear();
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (GameObject root in activeScene.GetRootGameObjects())
                {
                    if (root != null) preExistingRoots.Add(root);
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(spawned[i]);
                }
            }
            spawned.Clear();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                foreach (GameObject root in activeScene.GetRootGameObjects())
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
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        [Test]
        public void EnemySeparationSystem_SeparatesStackedEnemiesRespectsObstacles_AndAllocatesZeroGc()
        {
            var separation = new EnemySeparationSystem();
            var bus = new GameEventBus();
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.speed = 3f;
            enemyData.contactDamage = 5;

            var enemyList = new List<EnemyBase>(120);
            for (int i = 0; i < 120; i++)
            {
                var go = Track(new GameObject($"SepEnemy_{i}"));
                go.layer = 8;
                // First 40 stacked in two tight piles to guarantee deterministic collision resolution
                if (i < 20)
                {
                    go.transform.position = Vector3.zero;
                }
                else if (i < 40)
                {
                    go.transform.position = new Vector3(2f, 0f, 2f);
                }
                else
                {
                    float angle = (Mathf.PI * 2f * i) / 80f;
                    float radius = 4f + (i % 6) * 0.45f;
                    go.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }

                var enemy = go.AddComponent<EnemyBase>();
                enemy.Initialize(enemyData, null, null, bus, 1f);
                enemyList.Add(enemy);
            }

            // Obstacle blocking +X push for an enemy next to a wall
            int obsLayer = LayerMask.NameToLayer("Obstacles");
            Assert.GreaterOrEqual(obsLayer, 0, "Obstacles layer must exist");
            var wallGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wallGo.layer = obsLayer;
            wallGo.transform.position = new Vector3(11.0f, 0.5f, 0f);
            wallGo.transform.localScale = new Vector3(1f, 2f, 4f);

            var blockedEnemyGo = Track(new GameObject("WallEnemy"));
            blockedEnemyGo.layer = 8;
            blockedEnemyGo.transform.position = new Vector3(10.0f, 0f, 0f);
            var blockedEnemy = blockedEnemyGo.AddComponent<EnemyBase>();
            blockedEnemy.Initialize(enemyData, null, null, bus, 1f);

            var pusherEnemyGo = Track(new GameObject("PusherEnemy"));
            pusherEnemyGo.layer = 8;
            pusherEnemyGo.transform.position = new Vector3(9.6f, 0f, 0f);
            var pusherEnemy = pusherEnemyGo.AddComponent<EnemyBase>();
            pusherEnemy.Initialize(enemyData, null, null, bus, 1f);

            enemyList.Add(blockedEnemy);
            enemyList.Add(pusherEnemy);
            Physics.SyncTransforms();

            int obstacleMask = 1 << obsLayer;

            // Warmup step
            int movedWarmup = separation.Step(enemyList, obstacleMask);
            Assert.Greater(movedWarmup, 0);

            // Zero-GC verification in steady state
            long gcBefore = GC.GetAllocatedBytesForCurrentThread();
            separation.Step(enemyList, obstacleMask);
            long gcAfter = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, gcAfter - gcBefore, "EnemySeparationSystem.Step must allocate 0 bytes");

            // Step a few times so stacked enemies fully spread out
            for (int step = 0; step < 8; step++)
            {
                separation.Step(enemyList, obstacleMask);
            }

            // Stacked pair (0 vs 1) must no longer sit on the exact same point
            float pairDist = Vector3.Distance(enemyList[0].transform.position, enemyList[1].transform.position);
            Assert.Greater(pairDist, 0.35f, "Stacked enemies must separate cleanly");
            Assert.Less(blockedEnemy.transform.position.x, 10.45f, "Separation push must respect obstacle walls");

            // Also verify WaveManager.RunSeparationStep() zero-GC integration
            var waveConfig = Track(ScriptableObject.CreateInstance<WaveConfig>());
            waveConfig.waveDelay = 999f;
            waveConfig.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = enemyData, minWave = 1, maxWave = 0, spawnWeight = 1f, packSize = 1 }
            };

            var waveGo = Track(new GameObject("SepWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", waveConfig);
            waveMgr.Construct(null, null, bus, null, null);
            waveMgr.PrewarmPools(120);

            for (int i = 0; i < 120; i++)
            {
                Vector3 spawnPos = new Vector3((i % 10) * 0.2f, 0f, (i / 10) * 0.2f);
                waveMgr.SpawnEnemyForWave(enemyData, spawnPos, waveNumber: 5);
            }

            waveMgr.RunSeparationStep(); // warmup
            long waveGc0 = GC.GetAllocatedBytesForCurrentThread();
            int waveMoved = waveMgr.RunSeparationStep();
            long waveGc1 = GC.GetAllocatedBytesForCurrentThread();

            Assert.Greater(waveMoved, 0);
            Assert.AreEqual(0L, waveGc1 - waveGc0, "WaveManager.RunSeparationStep must allocate 0 bytes");
        }

        [Test]
        public void IntroWaves_ScheduleAvoidsMiniBossWaves_AndGuaranteesFiftyPercentNewEnemyShare()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(config);

            var skeletonEntry = config.enemySchedule.Find(e => e != null && e.enemy != null && e.enemy.name == "SkeletonData");
            var bombEntry = config.enemySchedule.Find(e => e != null && e.enemy != null && e.enemy.name == "BombSlimeData");
            var goblinEntry = config.enemySchedule.Find(e => e != null && e.enemy != null && e.enemy.name == "GoblinData");
            var batEntry = config.enemySchedule.Find(e => e != null && e.enemy != null && e.enemy.name == "BatData");
            var orcEntry = config.enemySchedule.Find(e => e != null && e.enemy != null && e.enemy.name == "OrcChargerData");

            Assert.IsNotNull(skeletonEntry);
            Assert.IsNotNull(bombEntry);
            Assert.IsNotNull(goblinEntry);
            Assert.IsNotNull(batEntry);
            Assert.IsNotNull(orcEntry);

            Assert.AreEqual(6, skeletonEntry.minWave);
            Assert.IsFalse(WaveManager.IsMiniBossWave(skeletonEntry.minWave, config.miniBossInterval, config.bossInterval));
            Assert.AreEqual(11, bombEntry.minWave);
            Assert.IsFalse(WaveManager.IsMiniBossWave(bombEntry.minWave, config.miniBossInterval, config.bossInterval));

            Assert.AreSame(goblinEntry, config.GetIntroEntryForWave(3));
            Assert.AreSame(skeletonEntry, config.GetIntroEntryForWave(6));
            Assert.AreSame(batEntry, config.GetIntroEntryForWave(7));
            Assert.AreSame(bombEntry, config.GetIntroEntryForWave(11));
            Assert.AreSame(orcEntry, config.GetIntroEntryForWave(14));
            Assert.IsNull(config.GetIntroEntryForWave(1));
            Assert.IsNull(config.GetIntroEntryForWave(5));
            Assert.IsNull(config.GetIntroEntryForWave(10));

            // Simulate Wave 3 (Goblin intro, packSize 1)
            SimulateAndAssertIntroShare(config, wave: 3, totalEnemies: 20, expectedIntroEnemy: goblinEntry.enemy);

            // Simulate Wave 6 (Skeleton intro, packSize 4)
            SimulateAndAssertIntroShare(config, wave: 6, totalEnemies: 24, expectedIntroEnemy: skeletonEntry.enemy);
        }

        private static void SimulateAndAssertIntroShare(WaveConfig config, int wave, int totalEnemies, EnemyData expectedIntroEnemy)
        {
            EnemyWaveEntry introEntry = config.GetIntroEntryForWave(wave);
            Assert.IsNotNull(introEntry);
            int introRemaining = Mathf.RoundToInt(totalEnemies * introEntry.introSpawnShare);
            int spawnedTotal = 0;
            int spawnedIntro = 0;

            var rng = new System.Random(1337 + wave);
            while (spawnedTotal < totalEnemies)
            {
                EnemyWaveEntry picked = WaveManager.SelectEntryForWaveSpawn(
                    config,
                    wave,
                    totalEnemies - spawnedTotal,
                    introEntry,
                    ref introRemaining,
                    (float)rng.NextDouble(),
                    (float)rng.NextDouble(),
                    out int packCount);

                Assert.IsNotNull(picked);
                spawnedTotal += packCount;
                if (picked.enemy == expectedIntroEnemy)
                {
                    spawnedIntro += packCount;
                }
            }

            Assert.AreEqual(totalEnemies, spawnedTotal);
            float actualShare = (float)spawnedIntro / spawnedTotal;
            Assert.AreEqual(0.5f, actualShare, 0.05f, $"Intro wave {wave} must spawn ~50% {expectedIntroEnemy.name}");
        }

        [Test]
        public void PauseMenuAndGameManager_AbandonRunSavesCoinsAndProgressOnce_AndShowsConfirmation()
        {
            using var saveManager = new SaveManager();
            var runStats = new RunStatistics();
            var bus = new GameEventBus();

            var gmGo = Track(new GameObject("AbandonTestGM"));
            var gm = gmGo.AddComponent<GameManager>();

            var waveGo = Track(new GameObject("AbandonTestWaveMgr"));
            var waveMgr = waveGo.AddComponent<WaveManager>();
            SetPrivateField(waveMgr, "currentWave", 7);

            gm.Construct(null, waveMgr, saveManager, runStats, null, bus, null, null);

            runStats.AddCoin(125);
            for (int i = 0; i < 14; i++) runStats.AddKill();
            runStats.UpdatePlayTime(95f);

            var pauseRoot = Track(new GameObject("PauseMenuRoot", typeof(RectTransform)));
            var pausePanel = Track(new GameObject("PausePanel", typeof(RectTransform)));
            pausePanel.transform.SetParent(pauseRoot.transform, false);
            var confirmPanel = Track(new GameObject("QuitConfirmPanel", typeof(RectTransform)));
            confirmPanel.transform.SetParent(pausePanel.transform, false);

            var pauseUi = pauseRoot.AddComponent<PauseMenuUI>();
            SetPrivateField(pauseUi, "pausePanel", pausePanel);
            SetPrivateField(pauseUi, "quitConfirmPanel", confirmPanel);
            pauseUi.Construct(gm, null);
            typeof(PauseMenuUI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pauseUi, null);

            Assert.IsFalse(pauseUi.IsPanelOpen);
            Assert.IsFalse(pauseUi.IsQuitConfirmOpen);

            pauseUi.OnPauseClicked();
            Assert.IsTrue(pauseUi.IsPanelOpen);
            Assert.IsTrue(gm.IsPaused);

            // Quit opens confirmation instead of silently dropping coins
            pauseUi.OnQuitToMenuClicked();
            Assert.IsTrue(pauseUi.IsQuitConfirmOpen);
            Assert.AreEqual(0, saveManager.Data.coins);

            // Cancel closes confirmation and stays paused
            pauseUi.OnCancelQuitClicked();
            Assert.IsFalse(pauseUi.IsQuitConfirmOpen);
            Assert.IsTrue(pauseUi.IsPanelOpen);
            Assert.IsTrue(gm.IsPaused);

            // Confirm saves earned coins, kills, wave, and closes panels
            pauseUi.OnQuitToMenuClicked();
            Assert.IsTrue(pauseUi.IsQuitConfirmOpen);
            pauseUi.OnConfirmQuitClicked();

            Assert.IsFalse(pauseUi.IsQuitConfirmOpen);
            Assert.IsFalse(pauseUi.IsPanelOpen);
            Assert.AreEqual(125, saveManager.Data.coins);
            Assert.AreEqual(14, saveManager.Data.totalKills);
            Assert.AreEqual(7, saveManager.Data.bestWave);
            Assert.AreEqual(1, saveManager.Data.totalRuns);
            Assert.AreEqual(95f, saveManager.Data.totalPlayTime, 0.01f);

            // Calling AbandonRunAndGoToMenu again must not double-count
            gm.AbandonRunAndGoToMenu();
            Assert.AreEqual(125, saveManager.Data.coins);
            Assert.AreEqual(1, saveManager.Data.totalRuns);

            // Verify Gameplay.unity has quit confirmation wired and inactive at start
            Scene alreadyLoaded = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive)
                : alreadyLoaded;
            try
            {
                PauseMenuUI scenePause = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    scenePause = root.GetComponentInChildren<PauseMenuUI>(true);
                    if (scenePause != null) break;
                }

                Assert.IsNotNull(scenePause);
                var sceneConfirmPanel = GetPrivateField(scenePause, "quitConfirmPanel") as GameObject;
                Assert.IsNotNull(sceneConfirmPanel, "Gameplay.unity PauseMenuUI.quitConfirmPanel must be wired");
                Assert.IsFalse(sceneConfirmPanel.activeSelf, "QuitConfirmPanel must start inactive");
                Assert.IsNotNull(GetPrivateField(scenePause, "confirmQuitButton"), "PauseMenuUI.confirmQuitButton must be wired");
                Assert.IsNotNull(GetPrivateField(scenePause, "cancelQuitButton"), "PauseMenuUI.cancelQuitButton must be wired");
                Assert.IsNotNull(GetPrivateField(scenePause, "quitConfirmText"), "PauseMenuUI.quitConfirmText must be wired");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CooldownReduction_AppliesToWeaponControllerAndHeroUltimate()
        {
            var stats = new HeroRuntimeStats();
            stats.AddCooldownReductionPercent(30f); // 0.7x cooldown multiplier
            Assert.AreEqual(0.7f, stats.CooldownReductionMultiplier, 0.001f);

            var weaponData = Track(ScriptableObject.CreateInstance<WeaponData>());
            weaponData.attackCooldown = 1.0f;
            weaponData.range = 10f;

            var ownerGo = Track(new GameObject("CdrWeaponOwner"));
            var projRoot = Track(new GameObject("CdrProjRoot"));
            var controller = new WeaponController(weaponData, 1 << 8, ownerGo.transform, ownerGo.transform, projRoot.transform, 1f, stats);
            Assert.AreEqual(0.7f, controller.EffectiveAttackCooldown, 0.001f);

            var heroData = Track(ScriptableObject.CreateInstance<HeroData>());
            heroData.ultimateType = HeroUltimateType.Shield;
            heroData.ultimateDuration = 5f;
            heroData.ultimateCooldown = 20f;
            heroData.shieldPulseRadius = 0f;

            var bus = new GameEventBus();
            var health = ownerGo.AddComponent<HeroHealth>();
            var ult = ownerGo.AddComponent<HeroUltimate>();
            ult.Construct(health, null, null, bus, stats);
            SetPrivateField(ult, "fallbackHeroData", heroData);
            SetPrivateField(ult, "activeHeroData", heroData);

            Assert.AreEqual(14f, ult.CooldownDuration, 0.001f);
            Assert.IsTrue(ult.TryActivate());
            typeof(HeroUltimate).GetMethod("DeactivateUltimate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ult, null);
            Assert.AreEqual(14f, ult.CooldownRemaining, 0.001f);
        }

        [Test]
        public void HeroUltimate_IsDataDrivenFromHeroData_AndKnightShieldPulsesKnockbackAndDamage()
        {
            var bus = new GameEventBus();
            int shieldEvents = 0;
            int meteorEvents = 0;
            int arrowEvents = 0;
            int soulEvents = 0;
            bus.Subscribe<ShieldShockwaveEvent>(_ => shieldEvents++);
            bus.Subscribe<MeteorImpactEvent>(_ => meteorEvents++);
            bus.Subscribe<ArrowRainWaveEvent>(_ => arrowEvents++);
            bus.Subscribe<SoulNovaCastEvent>(_ => soulEvents++);

            var stats = new HeroRuntimeStats();
            var heroGo = Track(new GameObject("DataDrivenUltHero"));
            heroGo.transform.position = Vector3.zero;
            var health = heroGo.AddComponent<HeroHealth>();
            health.Construct(stats, bus, null);
            SetPrivateField(health, "currentMaxHp", 200);
            SetPrivateField(health, "currentHp", 100);

            var ult = heroGo.AddComponent<HeroUltimate>();
            ult.Construct(health, null, null, bus, stats);

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 1000;
            enemyData.speed = 3f;
            enemyData.contactDamage = 5;

            var nearGo = Track(new GameObject("NearEnemy"));
            nearGo.layer = 8;
            nearGo.transform.position = new Vector3(2.0f, 0f, 0f);
            nearGo.AddComponent<BoxCollider>();
            var nearEnemy = nearGo.AddComponent<EnemyBase>();
            nearEnemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);

            var farGo = Track(new GameObject("FarEnemy"));
            farGo.layer = 8;
            farGo.transform.position = new Vector3(6.0f, 0f, 0f);
            farGo.AddComponent<BoxCollider>();
            var farEnemy = farGo.AddComponent<EnemyBase>();
            farEnemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);
            Physics.SyncTransforms();

            // 1. Knight Shield shockwave (radius 4.5m, 40 dmg, 2.5m knockback)
            var knight = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/KnightData.asset");
            Assert.IsNotNull(knight);
            SetHeroForUlt(ult, knight);

            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, shieldEvents);
            Assert.AreEqual(960, nearEnemy.CurrentHp, "Near enemy within 4.5m must take 40 shockwave damage");
            Assert.GreaterOrEqual(nearGo.transform.position.x, 4.49f, "Near enemy must be knocked back by 2.5m");
            Assert.AreEqual(1000, farEnemy.CurrentHp, "Far enemy outside 4.5m must not take shockwave damage");
            Assert.AreEqual(6.0f, farGo.transform.position.x, 0.001f);
            InvokeDeactivateUltimate(ult);
            ult.ResetCooldown();

            // 2. Custom Mage data (meteorCount = 2, meteorBaseDamage = 120)
            nearGo.transform.position = new Vector3(2.5f, 0f, 0f);
            Physics.SyncTransforms();
            var customMage = Track(ScriptableObject.CreateInstance<HeroData>());
            customMage.ultimateType = HeroUltimateType.MeteorStrike;
            customMage.ultimateDuration = 2f;
            customMage.meteorCount = 2;
            customMage.meteorSearchRadius = 5f;
            customMage.meteorExplosionRadius = 4f;
            customMage.meteorBaseDamage = 120f;
            SetHeroForUlt(ult, customMage);

            int hpBeforeCustomMeteor = nearEnemy.CurrentHp;
            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, meteorEvents);
            Assert.AreEqual(hpBeforeCustomMeteor - 120, nearEnemy.CurrentHp);
            Assert.AreEqual(1, (int)GetPrivateField(ult, "meteorsRemaining"), "Remaining meteor count must come from HeroData.meteorCount");
            InvokeDeactivateUltimate(ult);
            ult.ResetCooldown();

            // 3. Custom Ranger data (arrowRainWaveCount = 2, arrowRainWaveDamage = 75)
            var customRanger = Track(ScriptableObject.CreateInstance<HeroData>());
            customRanger.ultimateType = HeroUltimateType.ArrowRain;
            customRanger.ultimateDuration = 2f;
            customRanger.arrowRainWaveCount = 2;
            customRanger.arrowRainRadius = 5f;
            customRanger.arrowRainWaveDamage = 75f;
            SetHeroForUlt(ult, customRanger);

            int hpBeforeCustomArrow = nearEnemy.CurrentHp;
            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, arrowEvents);
            Assert.AreEqual(hpBeforeCustomArrow - 75, nearEnemy.CurrentHp);
            Assert.AreEqual(1, (int)GetPrivateField(ult, "arrowWavesRemaining"));
            InvokeDeactivateUltimate(ult);
            ult.ResetCooldown();

            // 4. Custom Necromancer data (soulNovaBoltCount = 4, soulNovaBaseDamage = 90, soulNovaHealPercent = 0.2)
            var customNecro = Track(ScriptableObject.CreateInstance<HeroData>());
            customNecro.ultimateType = HeroUltimateType.SoulNova;
            customNecro.ultimateDuration = 1f;
            customNecro.soulNovaBoltCount = 4;
            customNecro.soulNovaBaseDamage = 90f;
            customNecro.soulNovaRange = 8f;
            customNecro.soulNovaSpeed = 16f;
            customNecro.soulNovaHitRadius = 0.5f;
            customNecro.soulNovaHealPercent = 0.2f;
            SetHeroForUlt(ult, customNecro);

            int hpBeforeSoul = nearEnemy.CurrentHp;
            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, soulEvents);
            Assert.AreEqual(140, health.CurrentHp, "20% of 200 MaxHp = +40 heal");
            for (int i = 0; i < 10; i++) ult.StepSoulNova(0.04f);
            Assert.AreEqual(hpBeforeSoul - 90, nearEnemy.CurrentHp);
        }

        [Test]
        public void CombatTargetFilter_IgnoresIsolatedBarrelsTargetsOccupiedBarrels_AndOrbitalDamagesBarrels()
        {
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("TargetFilterHero"));
            heroGo.transform.position = Vector3.zero;
            var heroHealth = heroGo.AddComponent<HeroHealth>();

            var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Arena/ExplosiveBarrel.prefab");
            Assert.IsNotNull(barrelPrefab);

            var barrelGo = Track(UnityEngine.Object.Instantiate(barrelPrefab));
            barrelGo.transform.position = new Vector3(2.0f, 0f, 0f);
            var barrel = barrelGo.GetComponent<ExplosiveBarrel>();
            barrel.Initialize(heroHealth, bus);
            var barrelCol = barrelGo.GetComponent<Collider>();
            Physics.SyncTransforms();

            int enemyMask = 1 << 8;

            // 1. Isolated barrel (0 nearby enemies): auto-aim filter returns false
            Assert.IsFalse(CombatTargetFilter.IsValidAutoAimTarget(barrelCol, barrel, enemyMask));

            var weaponData = Track(ScriptableObject.CreateInstance<WeaponData>());
            weaponData.range = 12f;
            weaponData.attackCooldown = 1f;
            var projRoot = Track(new GameObject("FilterProjRoot"));
            var weaponCtrl = new WeaponController(weaponData, enemyMask, heroGo.transform, heroGo.transform, projRoot.transform);
            MethodInfo findClosestWeapon = typeof(WeaponController).GetMethod("FindClosestEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNull(findClosestWeapon.Invoke(weaponCtrl, null), "WeaponController must ignore isolated ExplosiveBarrel");

            // Shooting skills must also ignore isolated barrel
            var shooterData = Track(ScriptableObject.CreateInstance<ShooterAbilityData>());
            shooterData.id = "Shooter";
            shooterData.searchRadius = 12f;
            shooterData.targetLayer = enemyMask;
            var shooter = (ShooterAbility)shooterData.CreateAbility(heroGo.transform);
            shooter.Initialize(shooterData);
            MethodInfo findShooterTarget = typeof(ProjectileAbilityBase<ShooterAbilityData>).GetMethod("FindClosestTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNull(findShooterTarget.Invoke(shooter, new object[] { 12f, shooterData.targetLayer }), "ShooterAbility must ignore isolated barrel");
            var shooterProj = GameObject.Find("Projectiles_Shooter");
            if (shooterProj != null) Track(shooterProj);

            var chainData = Track(ScriptableObject.CreateInstance<ChainLightningAbilityData>());
            chainData.id = "ChainLightning";
            chainData.baseDamage = 25f;
            chainData.searchRadius = 12f;
            chainData.targetLayer = enemyMask;
            var chain = (ChainLightningAbility)chainData.CreateAbility(heroGo.transform);
            chain.Initialize(chainData);
            Assert.AreEqual(0, chain.TriggerChainCast(), "ChainLightning must ignore isolated barrel");
            Assert.IsFalse(barrel.IsDead);

            var turretGo = Track(new GameObject("FilterTurret"));
            turretGo.transform.position = Vector3.zero;
            var turret = turretGo.AddComponent<LaserTurret>();
            turret.Initialize(12f, 20f, 5f, enemyMask, null, null);
            typeof(LaserTurret).GetMethod("FindTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turret, null);
            Assert.IsNull(GetPrivateField(turret, "currentTarget"), "LaserTurret must ignore isolated barrel");

            var poisonData = Track(ScriptableObject.CreateInstance<PoisonFlaskAbilityData>());
            poisonData.id = "PoisonAlchemist";
            poisonData.throwRange = 12f;
            poisonData.flaskCount = 1;
            poisonData.targetLayer = enemyMask;
            var poison = (PoisonFlaskAbility)poisonData.CreateAbility(heroGo.transform);
            poison.Initialize(poisonData);
            Assert.AreEqual(0, poison.ThrowFlasks(), "PoisonFlaskAbility must ignore isolated barrel");
            var poisonZones = GameObject.Find("PoisonZones_PoisonAlchemist");
            if (poisonZones != null) Track(poisonZones);

            var dragonData = Track(ScriptableObject.CreateInstance<MiniDragonAbilityData>());
            dragonData.id = "MiniDragon";
            dragonData.searchRadius = 12f;
            dragonData.dragonCount = 1;
            dragonData.targetLayer = enemyMask;
            var dragon = (MiniDragonAbility)dragonData.CreateAbility(heroGo.transform);
            dragon.Initialize(dragonData);
            Assert.AreEqual(0, dragon.FireAllDragons(), "MiniDragonAbility must ignore isolated barrel");
            var dragonsRoot = GameObject.Find("Dragons_MiniDragon");
            if (dragonsRoot != null) Track(dragonsRoot);

            var tornadoData = Track(ScriptableObject.CreateInstance<VoxelTornadoAbilityData>());
            tornadoData.id = "VoxelTornado";
            tornadoData.vortexRadius = 3f;
            tornadoData.tornadoCount = 1;
            tornadoData.targetLayer = enemyMask;
            var tornado = (VoxelTornadoAbility)tornadoData.CreateAbility(heroGo.transform);
            tornado.Initialize(tornadoData);
            Assert.AreEqual(0, tornado.SpawnTornadoes(), "VoxelTornadoAbility must ignore isolated barrel");
            var tornadoRoot = GameObject.Find("Tornadoes_VoxelTornado");
            if (tornadoRoot != null) Track(tornadoRoot);

            // 2. Enemy enters within 3.5m of barrel -> barrel becomes valid target (and is closer than the enemy)
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 300;
            enemyData.contactDamage = 5;
            var enemyGo = Track(new GameObject("BarrelNeighborEnemy"));
            enemyGo.layer = 8;
            enemyGo.transform.position = new Vector3(4.5f, 0f, 0f);
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);
            Physics.SyncTransforms();

            Assert.IsTrue(CombatTargetFilter.IsValidAutoAimTarget(barrelCol, barrel, enemyMask));
            Assert.AreSame(barrel.transform, findClosestWeapon.Invoke(weaponCtrl, null), "WeaponController should aim at closer barrel when enemy is nearby");
            Assert.Greater(chain.TriggerChainCast(), 0, "ChainLightning should strike & detonate barrel when enemy is within 3.5m");
            Assert.IsTrue(barrel.IsDead, "Barrel should detonate once an enemy is near it");

            // 3. OrbitalCompanionAbility supports IDamageable and detonates ExplosiveBarrel even without nearby enemies
            var barrel2Go = Track(UnityEngine.Object.Instantiate(barrelPrefab));
            barrel2Go.transform.position = new Vector3(2.0f, 0f, 0f);
            var barrel2 = barrel2Go.GetComponent<ExplosiveBarrel>();
            barrel2.Initialize(heroHealth, bus);
            Physics.SyncTransforms();

            var orbitalData = Track(ScriptableObject.CreateInstance<OrbitalCompanionAbilityData>());
            orbitalData.id = "orbital_small_sword";
            orbitalData.type = AbilityType.Active;
            orbitalData.count = 4;
            orbitalData.baseDamage = 15f;
            orbitalData.orbitDistance = 2.0f;
            orbitalData.baseArea = 2.0f;
            orbitalData.targetLayer = enemyMask;

            var orbital = (OrbitalCompanionAbility)orbitalData.CreateAbility(heroGo.transform);
            orbital.Initialize(orbitalData);
            typeof(OrbitalCompanionAbility).GetMethod("ApplyDamageTick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(orbital, null);
            Assert.IsTrue(barrel2.IsDead, "OrbitalCompanionAbility must damage IDamageable barrels");
        }

        [Test]
        public void HeroHealth_PostHitInvulnerability_BlocksImmediateMultiHitAndExpiresAfter018s()
        {
            var bus = new GameEventBus();
            var go = Track(new GameObject("IFrameHero"));
            var health = go.AddComponent<HeroHealth>();
            health.Construct(new HeroRuntimeStats(), bus, null);
            SetPrivateField(health, "currentMaxHp", 200);
            SetPrivateField(health, "currentHp", 200);

            Assert.AreEqual(0.18f, health.PostHitInvulnerabilityDuration, 0.001f);

            int damageEvents = 0;
            health.OnDamageTaken += _ => damageEvents++;

            health.TakeDamage(20);
            Assert.AreEqual(180, health.CurrentHp);
            Assert.AreEqual(1, damageEvents);
            Assert.IsTrue(health.IsPostHitInvulnerable);
            Assert.IsTrue(health.IsInvulnerable);

            // Simultaneous hits in same frame must be blocked
            health.TakeDamage(20);
            health.TakeDamage(20);
            Assert.AreEqual(180, health.CurrentHp);
            Assert.AreEqual(1, damageEvents);

            // Halfway through i-frame window -> still invulnerable
            health.TickInvulnerability(0.10f);
            health.TakeDamage(20);
            Assert.AreEqual(180, health.CurrentHp);
            Assert.IsTrue(health.IsPostHitInvulnerable);

            // Past 0.18s -> vulnerable again
            health.TickInvulnerability(0.10f);
            Assert.IsFalse(health.IsPostHitInvulnerable);
            health.TakeDamage(20);
            Assert.AreEqual(160, health.CurrentHp);
            Assert.AreEqual(2, damageEvents);
        }

        [Test]
        public void WaveConfig_SplitCurves_BalanceSimulator_AndSprint7RepoHygiene_PassValidation()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(config);
            Assert.IsTrue(config.useSeparateDifficultyScaling);

            float hp1 = config.GetHpDifficultyMultiplier(1);
            float dmg1 = config.GetDamageDifficultyMultiplier(1);
            float hp50 = config.GetHpDifficultyMultiplier(50);
            float dmg50 = config.GetDamageDifficultyMultiplier(50);
            float hp100 = config.GetHpDifficultyMultiplier(100);
            float dmg100 = config.GetDamageDifficultyMultiplier(100);

            Assert.AreEqual(1f, hp1, 0.001f);
            Assert.AreEqual(1f, dmg1, 0.001f);
            Assert.Greater(hp50, dmg50);
            Assert.Greater(hp100, hp50);
            Assert.Greater(hp100, dmg100);
            Assert.LessOrEqual(dmg100, config.maxDamageMultiplier + 0.001f);

            // EnemyBase applies split HP and Damage multipliers separately
            var bus = new GameEventBus();
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.contactDamage = 10;
            var enemyGo = Track(new GameObject("SplitScalingEnemy"));
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, null, null, bus, difficultyMultiplier: 3.0f, damageDifficultyMultiplier: 1.5f);
            Assert.AreEqual(300, enemy.MaxHp);
            Assert.AreEqual(15, (int)GetPrivateField(enemy, "currentDamage"));

            // BalanceCurveSimulator report generation
            Type simType = Type.GetType("ProjectB.EditorScripts.BalanceCurveSimulator, ProjectB.Editor");
            Assert.IsNotNull(simType, "BalanceCurveSimulator must compile in ProjectB.Editor");
            MethodInfo buildReport = simType.GetMethod("BuildSimulationReport", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(buildReport);
            string report = buildReport.Invoke(null, new object[] { config, null }) as string;
            Assert.IsFalse(string.IsNullOrEmpty(report));
            Assert.IsTrue(report.Contains("Wave   1"));
            Assert.IsTrue(report.Contains("Wave  25"));
            Assert.IsTrue(report.Contains("Wave  50"));
            Assert.IsTrue(report.Contains("Wave  75"));
            Assert.IsTrue(report.Contains("Wave 100"));

            // Hygiene 1: ProjectB_PostFX clean (only Bloom, Vignette, ColorAdjustments)
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/ProjectB_PostFX.asset");
            Assert.IsNotNull(profile);
            Assert.AreEqual(3, profile.components.Count, "ProjectB_PostFX must contain only Bloom, Vignette, ColorAdjustments");
            Assert.IsTrue(profile.TryGet<Bloom>(out _));
            Assert.IsTrue(profile.TryGet<Vignette>(out _));
            Assert.IsTrue(profile.TryGet<ColorAdjustments>(out _));

            // Hygiene 2: EliteAuraMat deleted
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/EliteAuraMat.mat"));
            Assert.IsFalse(File.Exists("Assets/_Materials/EliteAuraMat.mat"));

            // Hygiene 3: No Assembly-CSharp:: legacy ScriptableObjects in _ScriptableObjects or Resources
            string[] soDirs = { "Assets/_ScriptableObjects", "Assets/Resources" };
            foreach (string dir in soDirs)
            {
                foreach (string file in Directory.GetFiles(dir, "*.asset", SearchOption.AllDirectories))
                {
                    string yaml = File.ReadAllText(file);
                    Assert.IsFalse(yaml.Contains("Assembly-CSharp::"), $"Legacy Assembly-CSharp:: identifier found in {file}");
                }
            }

            // Hygiene 4: No raw Debug.Log( in runtime _Scripts (excluding Editor and DevLog.cs)
            foreach (string csFile in Directory.GetFiles("Assets/_Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains($"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}") ||
                    csFile.EndsWith("DevLog.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string src = File.ReadAllText(csFile);
                Assert.IsFalse(src.Contains("Debug.Log("), $"Raw Debug.Log( found in runtime script {csFile}; use DevLog.Log");
            }
        }

        private static void SetHeroForUlt(HeroUltimate ult, HeroData heroData)
        {
            SetPrivateField(ult, "fallbackHeroData", heroData);
            SetPrivateField(ult, "activeHeroData", heroData);
            SetPrivateField(ult, "enemyLayer", (LayerMask)(1 << 8));
            ult.Prewarm();
        }

        private static void InvokeDeactivateUltimate(HeroUltimate ult)
        {
            typeof(HeroUltimate)
                .GetMethod("DeactivateUltimate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ult, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }
    }
}
