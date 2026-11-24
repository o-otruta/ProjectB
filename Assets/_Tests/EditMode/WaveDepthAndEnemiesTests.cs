using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class WaveDepthAndEnemiesTests
    {
        private readonly List<UnityEngine.Object> tracked = new List<UnityEngine.Object>();
        private bool savedFogEnabled;
        private FogMode savedFogMode;
        private Color savedFogColor;
        private float savedFogStart;
        private float savedFogEnd;
        private float savedFogDensity;

        [SetUp]
        public void SetUp()
        {
            savedFogEnabled = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;
            savedFogDensity = RenderSettings.fogDensity;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                if (tracked[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(tracked[i]);
                }
            }
            tracked.Clear();
            RenderSettings.fog = savedFogEnabled;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
            RenderSettings.fogDensity = savedFogDensity;
            Physics.SyncTransforms();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) tracked.Add(obj);
            return obj;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private (GameObject heroGo, HeroHealth heroHealth, HeroMovement heroMovement, GameEventBus bus) CreateHero()
        {
            var bus = new GameEventBus();
            var runtimeStats = new HeroRuntimeStats();
            var heroData = Track(ScriptableObject.CreateInstance<HeroData>());
            heroData.maxHp = 200;
            heroData.moveSpeed = 6f;

            var heroGo = Track(new GameObject("Sprint16Hero"));
            heroGo.transform.position = Vector3.zero;
            var health = heroGo.AddComponent<HeroHealth>();
            var movement = heroGo.AddComponent<HeroMovement>();

            SetPrivateField(health, "heroData", heroData);
            var zeroIframe = Track(ScriptableObject.CreateInstance<RunRulesConfig>());
            zeroIframe.postHitInvulnerabilityDuration = 0f;
            health.SetRunRulesConfig(zeroIframe);
            health.Construct(runtimeStats, bus, null);
            typeof(HeroHealth).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);

            SetPrivateField(movement, "heroData", heroData);
            movement.Construct(null, runtimeStats, null);
            typeof(HeroMovement).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(movement, null);

            return (heroGo, health, movement, bus);
        }

        private WaveConfig CreateMinimalWaveConfig(EnemyData enemy)
        {
            var cfg = Track(ScriptableObject.CreateInstance<WaveConfig>());
            cfg.baseEnemyCount = 6;
            cfg.enemiesPerWaveMultiplier = 1.2f;
            cfg.maxEnemiesPerWave = 50;
            cfg.spawnDelay = 0.5f;
            cfg.minSpawnDelay = 0.16f;
            cfg.spawnDelayReductionPerWave = 0.012f;
            cfg.enableWaveOverlap = true;
            cfg.waveOverlapThreshold = 0.30f;
            cfg.packSizeScalingStartWave = 20;
            cfg.packSizeBonusWaveInterval = 20;
            cfg.maxPackSizeBonus = 2;
            cfg.waveDelay = 0.05f;
            cfg.maxWaveDuration = 0f;
            cfg.maxActiveEnemies = 300;
            cfg.corridorHordeEnabled = true;
            cfg.firstCorridorHordeWave = 19;
            cfg.corridorHordeInterval = 10;
            cfg.corridorHordeDuration = 12f;
            cfg.corridorHordeSpawnInterval = 0.30f;
            cfg.corridorHordeBatchSize = 2;
            cfg.corridorHordeHpMultiplier = 0.55f;
            cfg.corridorHordeCorridorHalfWidth = 3.5f;
            cfg.corridorHordeEnemyData = enemy;
            cfg.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry
                {
                    enemy = enemy,
                    minWave = 1,
                    maxWave = 0,
                    spawnWeight = 1f,
                    packSize = 1,
                    triggerIntroWave = false,
                    scalePackSizeWithWave = true
                }
            };
            return cfg;
        }

        // --- Sprint 16.1: Density Scaling, Wave Overlap & Corridor Horde ---

        [Test]
        public void WaveConfig_DensityScaling_SpawnDelayRampsAfterSaturation_AndOverlapThresholdApplies()
        {
            var assetCfg = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(assetCfg);

            int satWave = assetCfg.GetDensitySaturationWave();
            Assert.That(satWave, Is.InRange(12, 16), "6 * 1.2^(W-1) saturates at 50 around W13");
            Assert.IsFalse(assetCfg.IsWaveSaturated(satWave - 1, isFtueRun: false));
            Assert.IsTrue(assetCfg.IsWaveSaturated(satWave, isFtueRun: false));
            Assert.IsFalse(assetCfg.IsWaveSaturated(satWave, isFtueRun: true));

            Assert.AreEqual(assetCfg.spawnDelay, assetCfg.GetWaveSpawnDelay(satWave, false), 0.0001f);
            float w25Delay = assetCfg.GetWaveSpawnDelay(25, false);
            float w60Delay = assetCfg.GetWaveSpawnDelay(60, false);
            Assert.Less(w25Delay, assetCfg.spawnDelay);
            Assert.AreEqual(assetCfg.minSpawnDelay, w60Delay, 0.0001f);

            // Overlap threshold is 0 before saturation, on FTUE, or on Boss waves, and 30% on saturated regular waves
            Assert.AreEqual(0, assetCfg.GetOverlapAliveThreshold(satWave - 1, 45, isFtueRun: false, isBossWave: false));
            Assert.AreEqual(0, assetCfg.GetOverlapAliveThreshold(25, 50, isFtueRun: false, isBossWave: true));
            Assert.AreEqual(0, assetCfg.GetOverlapAliveThreshold(25, 50, isFtueRun: true, isBossWave: false));
            Assert.AreEqual(15, assetCfg.GetOverlapAliveThreshold(25, 50, isFtueRun: false, isBossWave: false));

            // Pack scaling applies only to flagged entries
            var scaledEntry = new EnemyWaveEntry { packSize = 2, scalePackSizeWithWave = true };
            var unscaledEntry = new EnemyWaveEntry { packSize = 2, scalePackSizeWithWave = false };
            Assert.AreEqual(2, assetCfg.GetEffectivePackSize(scaledEntry, 10));
            Assert.AreEqual(3, assetCfg.GetEffectivePackSize(scaledEntry, 20));
            Assert.AreEqual(4, assetCfg.GetEffectivePackSize(scaledEntry, 40));
            Assert.AreEqual(4, assetCfg.GetEffectivePackSize(scaledEntry, 80));
            Assert.AreEqual(2, assetCfg.GetEffectivePackSize(unscaledEntry, 80));
        }

        [Test]
        public void WaveManager_SaturatedWave_AdvancesEarlyWhenAliveDropsToOverlapThreshold()
        {
            var dummyEnemy = Track(ScriptableObject.CreateInstance<EnemyData>());
            dummyEnemy.hp = 100;
            dummyEnemy.speed = 1f;
            dummyEnemy.contactDamage = 5;

            var cfg = CreateMinimalWaveConfig(dummyEnemy);
            cfg.baseEnemyCount = 10;
            cfg.maxEnemiesPerWave = 10; // saturates immediately at W1

            var (_, heroHealth, _, bus) = CreateHero();
            int completedEvents = 0;
            bus.Subscribe<WaveCompletedEvent>(_ => completedEvents++);

            var waveGo = Track(new GameObject("OverlapWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();
            SetPrivateField(waveMgr, "currentWave", 14);

            MethodInfo spawnWave = typeof(WaveManager).GetMethod("SpawnWaveCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
            IEnumerator routine = (IEnumerator)spawnWave.Invoke(waveMgr, null);
            for (int step = 0; step < 30; step++)
            {
                if (!routine.MoveNext()) break;
            }

            Assert.AreEqual(10, waveMgr.EnemiesAlive);
            Assert.AreEqual(3, waveMgr.CurrentWaveOverlapThreshold, "30% of 10 = 3 alive threshold");
            Assert.AreEqual(14, waveMgr.CurrentWave);

            // Despawn 6 enemies -> 4 alive (> 3): wave must NOT advance yet
            var buffer = new List<EnemyBase>(waveMgr.ActiveEnemies);
            for (int i = 0; i < 6; i++)
            {
                waveMgr.DespawnTrackedEnemy(buffer[i]);
            }
            Assert.AreEqual(4, waveMgr.EnemiesAlive);
            Assert.AreEqual(14, waveMgr.CurrentWave);
            Assert.AreEqual(0, completedEvents);

            // Despawn 1 more -> 3 alive (<= 3): wave must advance to 15 immediately!
            waveMgr.DespawnTrackedEnemy(buffer[6]);
            Assert.AreEqual(15, waveMgr.CurrentWave);
            Assert.AreEqual(1, completedEvents);
        }

        [Test]
        public void WaveEventManager_CorridorHorde_StartsAtW19_SkipsBlockedWaves_AndSpawnsFromSingleCorridor()
        {
            var realCfg = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(realCfg);

            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 10, false), "Must not fire before W19");
            Assert.IsTrue(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 19, false), "W19 is clean regular wave");
            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 22, false), "Intro wave W22 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 25, false), "Boss wave W25 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 30, false), "Mini-boss wave W30 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 32, false), "Intro wave W32 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 46, false), "Intro wave W46 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerCorridorHordeOnWave(realCfg, 61, false), "Intro wave W61 must be skipped");

            var hordeMob = Track(ScriptableObject.CreateInstance<EnemyData>());
            hordeMob.hp = 100;
            hordeMob.speed = 3f;
            hordeMob.contactDamage = 5;

            var cfg = CreateMinimalWaveConfig(hordeMob);
            cfg.waveEventsEnabled = false;
            cfg.spawnRadiusMax = 15f;
            cfg.corridorHordeBatchSize = 3;
            cfg.corridorHordeSpawnInterval = 0.25f;
            cfg.corridorHordeDuration = 5f;
            cfg.corridorHordeHpMultiplier = 0.50f;
            cfg.corridorHordeCorridorHalfWidth = 3.0f;

            var (_, heroHealth, _, bus) = CreateHero();
            var waveGo = Track(new GameObject("CorridorWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();

            var evtGo = Track(new GameObject("CorridorEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.GetComponent<WaveEventManager>();
            SetPrivateField(evtMgr, "overrideConfig", cfg);
            evtMgr.Construct(waveMgr, heroHealth, null, null, null, bus, null, new EnemyRuntimeModifiers());

            // Wave 18 -> nothing yet; Wave 19 -> CorridorHorde triggers!
            bus.Publish(new WaveCompletedEvent(17, 18));
            Assert.AreEqual(WaveEventType.None, evtMgr.ActiveEventType);

            bus.Publish(new WaveCompletedEvent(18, 19));
            Assert.AreEqual(WaveEventType.CorridorHorde, evtMgr.ActiveEventType);
            Assert.AreEqual(3, evtMgr.CorridorSpawnedTotal);
            Assert.AreEqual(3, waveMgr.EnemiesAlive);

            // All spawned enemies must have reduced HP (100 * 0.50 = 50) and lie on the same side (dot > 0 with ActiveCorridorDirection)
            Vector3 dir = evtMgr.ActiveCorridorDirection;
            foreach (EnemyBase e in waveMgr.ActiveEnemies)
            {
                Assert.AreEqual(50, e.MaxHp, "Corridor Horde mobs must use corridorHordeHpMultiplier");
                float forwardProj = Vector3.Dot(e.transform.position, dir);
                Assert.Greater(forwardProj, 8f, "All Corridor Horde mobs should arrive from the chosen corridor direction");
            }

            // Tick 0.30s -> second batch spawns
            evtMgr.TickEvent(0.30f);
            Assert.AreEqual(6, evtMgr.CorridorSpawnedTotal);
            Assert.AreEqual(6, waveMgr.EnemiesAlive);

            // Tick past remaining duration -> event ends cleanly
            evtMgr.TickEvent(6.0f);
            Assert.AreEqual(WaveEventType.None, evtMgr.ActiveEventType);

            // While another event is already active, Corridor Horde must not interrupt/overlap it
            Assert.IsTrue(evtMgr.StartEvent(WaveEventType.BloodMoon));
            SetPrivateField(evtMgr, "nextCorridorHordeWave", 29);
            bus.Publish(new WaveCompletedEvent(28, 29));
            Assert.AreEqual(WaveEventType.BloodMoon, evtMgr.ActiveEventType);
        }

        // --- Sprint 16.2: Splitter Slime, Frost Wisp, Siege Golemite ---

        [Test]
        public void SplitterEnemy_SplitsIntoChildrenOnDeath_AndRespectsMaxActiveEnemiesCap()
        {
            var childData = Track(ScriptableObject.CreateInstance<EnemyData>());
            childData.name = "TestSlimelet";
            childData.hp = 40;
            childData.speed = 3.5f;
            childData.contactDamage = 8;

            var splitterData = Track(ScriptableObject.CreateInstance<SplitterEnemyData>());
            splitterData.name = "TestSplitter";
            splitterData.hp = 100;
            splitterData.speed = 2.5f;
            splitterData.contactDamage = 12;
            splitterData.splitCountMin = 2;
            splitterData.splitCountMax = 3;
            splitterData.splitChildData = childData;
            splitterData.childHpMultiplier = 0.5f;
            splitterData.childDamageMultiplier = 0.75f;

            var splitterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/SplitterSlime.prefab");
            var childPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Enemies/SplitSlimelet.prefab");
            Assert.IsNotNull(splitterPrefab);
            Assert.IsNotNull(childPrefab);
            splitterData.modelPrefab = splitterPrefab;
            childData.modelPrefab = childPrefab;

            var cfg = CreateMinimalWaveConfig(splitterData);
            cfg.maxActiveEnemies = 10;

            var (_, heroHealth, _, bus) = CreateHero();
            int splitEvents = 0;
            bus.Subscribe<EnemySplitEvent>(_ => splitEvents++);

            var waveGo = Track(new GameObject("SplitterWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();

            var splitter = waveMgr.SpawnEventEnemy(splitterData, Vector3.right * 4f) as SplitterEnemy;
            Assert.IsNotNull(splitter);
            Assert.AreEqual(1, waveMgr.EnemiesAlive);

            splitter.TakeDamage(9999);
            Assert.AreEqual(1, splitEvents);
            Assert.That(waveMgr.EnemiesAlive, Is.InRange(2, 3), "Dying splitter must spawn 2-3 slimelets");

            // Fill up to global cap (10) and kill a splitter: EnemiesAlive must never exceed maxActiveEnemies (10)
            while (waveMgr.EnemiesAlive < 9)
            {
                waveMgr.SpawnEventEnemy(childData, Vector3.left * 3f);
            }
            var cappedSplitter = waveMgr.SpawnEventEnemy(splitterData, Vector3.forward * 4f);
            Assert.IsNotNull(cappedSplitter);
            Assert.AreEqual(10, waveMgr.EnemiesAlive);

            cappedSplitter.TakeDamage(9999);
            Assert.LessOrEqual(waveMgr.EnemiesAlive, 10, "Splitter children must never breach maxActiveEnemies");
        }

        [Test]
        public void FrostWispEnemy_AndFrostSlowZone_SlowHeroOnDeath_ExpireCleanly_AndSkipOnDespawn()
        {
            var wispData = AssetDatabase.LoadAssetAtPath<FrostWispEnemyData>("Assets/_ScriptableObjects/Enemies/FrostWispData.asset");
            Assert.IsNotNull(wispData);

            var cfg = CreateMinimalWaveConfig(wispData);
            var (heroGo, heroHealth, heroMovement, bus) = CreateHero();
            heroGo.transform.position = Vector3.zero;
            float baseSpeed = heroMovement.EffectiveMoveSpeed;

            int zoneEvents = 0;
            bus.Subscribe<FrostWispZoneSpawnedEvent>(_ => zoneEvents++);

            var waveGo = Track(new GameObject("WispWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();

            // Despawn must NOT spawn a frost zone
            var despawnWisp = waveMgr.SpawnEventEnemy(wispData, Vector3.zero) as FrostWispEnemy;
            Assert.IsNotNull(despawnWisp);
            waveMgr.DespawnTrackedEnemy(despawnWisp);
            Assert.AreEqual(0, zoneEvents);
            Assert.IsFalse(heroMovement.IsSlowed);

            // Real death right next to hero spawns a frost zone and slows hero
            var realWisp = waveMgr.SpawnEventEnemy(wispData, new Vector3(0.5f, 0f, 0f)) as FrostWispEnemy;
            Assert.IsNotNull(realWisp);
            realWisp.TakeDamage(9999);

            Assert.AreEqual(1, zoneEvents);
            Assert.IsTrue(heroMovement.IsSlowed, "Hero standing inside FrostWisp death zone must be slowed");
            Assert.Less(heroMovement.EffectiveMoveSpeed, baseSpeed * 0.7f);

            var activeZones = typeof(WaveManager)
                .GetField("activeFrostZones", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(waveMgr) as List<FrostSlowZone>;
            Assert.IsNotNull(activeZones);
            Assert.AreEqual(1, activeZones.Count);

            // Tick past zone duration -> expires and returns to pool
            FrostSlowZone zone = activeZones[0];
            zone.TickZone(wispData.frostZoneDuration + 0.5f);
            Assert.IsFalse(zone.IsActive);
            Assert.AreEqual(0, activeZones.Count);
        }

        [Test]
        public void SiegeGolemiteEnemy_TwoPhases_MitigatesSmallHits_BreaksShell_BoostsSpeed_AndResetsFromPool()
        {
            var siegeData = AssetDatabase.LoadAssetAtPath<SiegeGolemiteEnemyData>("Assets/_ScriptableObjects/Enemies/SiegeGolemiteData.asset");
            Assert.IsNotNull(siegeData);

            var cfg = CreateMinimalWaveConfig(siegeData);
            var (_, heroHealth, _, bus) = CreateHero();
            int shellBreaks = 0;
            bus.Subscribe<SiegeShellBrokenEvent>(_ => shellBreaks++);

            var waveGo = Track(new GameObject("SiegeWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();

            var golemite = waveMgr.SpawnEventEnemy(siegeData, Vector3.right * 5f) as SiegeGolemiteEnemy;
            Assert.IsNotNull(golemite);

            Transform shellTr = golemite.transform.Find("Visuals/ShellVisuals");
            Transform coreTr = golemite.transform.Find("Visuals/CoreVisuals");
            Assert.IsNotNull(shellTr);
            Assert.IsNotNull(coreTr);
            Assert.IsTrue(shellTr.gameObject.activeSelf, "Phase 1 must show ShellVisuals");
            Assert.IsFalse(coreTr.gameObject.activeSelf, "Phase 1 must hide CoreVisuals");

            Assert.IsTrue(golemite.IsShellIntact);
            int initialShell = golemite.CurrentShellHp;
            int initialBodyHp = golemite.CurrentHp;
            float phase1Speed = golemite.EffectiveMoveSpeed;

            // Small hit (10 dmg vs flatArmor 18): clamped to 15% = 2 dmg (never zeroed!)
            bool sawBlockedEvent = false;
            int lastReportedDamage = 0;
            bus.Subscribe<EnemyDamagedEvent>(e =>
            {
                sawBlockedEvent = e.IsBlocked;
                lastReportedDamage = e.Damage;
            });

            int dealtSmall = golemite.TakeDamageFrom(10, isCrit: false, Vector3.zero, isAoE: false);
            Assert.AreEqual(2, dealtSmall, "10 * 0.15 = 1.5 -> 2 minimum mitigated damage");
            Assert.IsTrue(sawBlockedEvent);
            Assert.AreEqual(2, lastReportedDamage);
            Assert.AreEqual(initialShell - 2, golemite.CurrentShellHp);
            Assert.AreEqual(initialBodyHp, golemite.CurrentHp, "Body HP must remain untouched while shell is intact");

            // Large hit shatters shell + carries overflow into body HP
            int bigRaw = initialShell + siegeData.shellFlatArmor + 20;
            int dealtBig = golemite.TakeDamageFrom(bigRaw, isCrit: true, Vector3.zero, isAoE: false);
            Assert.GreaterOrEqual(dealtBig, initialShell + 15);
            Assert.IsTrue(golemite.IsShellBroken);
            Assert.AreEqual(0, golemite.CurrentShellHp);
            Assert.Less(golemite.CurrentHp, initialBodyHp, "Overflow past shellHp must damage core body HP");
            Assert.AreEqual(1, shellBreaks);

            Assert.IsFalse(shellTr.gameObject.activeSelf, "Phase 2 must hide ShellVisuals");
            Assert.IsTrue(coreTr.gameObject.activeSelf, "Phase 2 must show CoreVisuals");
            Assert.AreEqual(phase1Speed * siegeData.coreSpeedMultiplier, golemite.EffectiveMoveSpeed, 0.01f);

            // Phase 2 hit has no flat armor reduction
            int bodyBeforePhase2Hit = golemite.CurrentHp;
            int phase2Dealt = golemite.TakeDamageFrom(10, isCrit: false, Vector3.zero, isAoE: false);
            Assert.AreEqual(10, phase2Dealt);
            Assert.IsFalse(sawBlockedEvent);
            Assert.AreEqual(bodyBeforePhase2Hit - 10, golemite.CurrentHp);

            // Kill golemite -> returns to pool, then spawn again at wave 1 -> resets cleanly to Phase 1!
            golemite.TakeDamage(9999);
            Assert.IsTrue(golemite.IsDead);

            var reused = waveMgr.SpawnEnemyForWave(siegeData, Vector3.left * 5f, waveNumber: 1) as SiegeGolemiteEnemy;
            Assert.AreSame(golemite, reused, "Pooled SiegeGolemite instance should be reused");
            Assert.IsTrue(reused.IsShellIntact);
            Assert.IsFalse(reused.IsShellBroken);
            Assert.AreEqual(initialShell, reused.CurrentShellHp);
            Assert.AreEqual(initialBodyHp, reused.CurrentHp);
            Assert.IsTrue(shellTr.gameObject.activeSelf);
            Assert.IsFalse(coreTr.gameObject.activeSelf);
            Assert.AreEqual(phase1Speed, reused.EffectiveMoveSpeed, 0.01f);

            // Shell HP scales with wave difficulty multiplier
            var highWaveGolemite = waveMgr.SpawnEnemyForWave(siegeData, Vector3.right * 7f, waveNumber: 61) as SiegeGolemiteEnemy;
            Assert.IsNotNull(highWaveGolemite);
            Assert.Greater(highWaveGolemite.MaxShellHp, initialShell * 4, "Shell durability must scale with wave HP multiplier");
        }

        [Test]
        public void Sprint16Assets_ModelsMaterialsPrefabsIconsAndWaveSchedule_AreValid()
        {
            string[] blends = { "SplitterSlime", "SplitSlimelet", "FrostWisp", "SiegeGolemite" };
            foreach (string b in blends)
            {
                Assert.IsTrue(File.Exists($"ArtSources/Blender/{b}.blend"), $"Missing {b}.blend");
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{b}_Mat.mat");
                var glow = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Materials/Voxels/{b}_Glow_Mat.mat");
                Assert.IsNotNull(mat, $"Missing {b}_Mat.mat");
                Assert.IsNotNull(glow, $"Missing {b}_Glow_Mat.mat");
                Assert.AreEqual("ProjectB/VoxelLit", mat.shader.name);
                Assert.IsTrue(mat.enableInstancing);
                Assert.IsTrue(glow.enableInstancing);
            }

            var cfg = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(cfg);
            Assert.AreEqual(11, cfg.enemySchedule.Count);
            Assert.IsTrue(cfg.enemySchedule.Exists(e => e.enemy is SplitterEnemyData && e.minWave == 32 && e.triggerIntroWave));
            Assert.IsTrue(cfg.enemySchedule.Exists(e => e.enemy is FrostWispEnemyData && e.minWave == 46 && e.triggerIntroWave));
            Assert.IsTrue(cfg.enemySchedule.Exists(e => e.enemy is SiegeGolemiteEnemyData && e.minWave == 61 && e.triggerIntroWave));

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/_ScriptableObjects/Enemies" }))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(enemy);
                Assert.IsNotNull(enemy.icon, $"EnemyData '{enemy.name}' is missing collection icon");
            }
        }

        // --- Sprint 16.3: Wave Mutators (Fog, Elite Surge, Glass Horde) ---

        private WaveMutatorData CreateTestMutator(WaveMutatorType type)
        {
            var m = Track(ScriptableObject.CreateInstance<WaveMutatorData>());
            m.mutatorType = type;
            m.minWave = 30;
            m.selectionWeight = 1f;
            m.durationWaves = 1;
            switch (type)
            {
                case WaveMutatorType.Fog:
                    m.mutatorId = "fog";
                    m.titleKey = "mutator.fog.title";
                    m.subtitleKey = "mutator.fog.subtitle";
                    m.titleFallback = "DENSE FOG!";
                    m.subtitleFallback = "REDUCED VISIBILITY  •  +50% XP";
                    m.xpMultiplier = 1.5f;
                    m.enableFogVisuals = true;
                    m.fogStartDistance = 12f;
                    m.fogEndDistance = 26f;
                    m.fogOverlayAlpha = 0.88f;
                    break;
                case WaveMutatorType.EliteSurge:
                    m.mutatorId = "elite_surge";
                    m.titleKey = "mutator.elite_surge.title";
                    m.subtitleKey = "mutator.elite_surge.subtitle";
                    m.titleFallback = "ELITE SURGE!";
                    m.subtitleFallback = "x2 ELITE CHANCE  •  x2 MINI-BOSS SHARDS";
                    m.eliteChanceMultiplier = 2f;
                    m.miniBossShardMultiplier = 2;
                    break;
                case WaveMutatorType.GlassHorde:
                    m.mutatorId = "glass_horde";
                    m.titleKey = "mutator.glass_horde.title";
                    m.subtitleKey = "mutator.glass_horde.subtitle";
                    m.titleFallback = "GLASS HORDE!";
                    m.subtitleFallback = "x0.5 ENEMY HP  •  x2 ENEMY COUNT";
                    m.enemyHpMultiplier = 0.5f;
                    m.enemyCountMultiplier = 2f;
                    break;
            }
            return m;
        }

        [Test]
        public void WaveMutators_AssetsSceneAndDi_AreValidAndWired()
        {
            var fog = AssetDatabase.LoadAssetAtPath<WaveMutatorData>("Assets/_ScriptableObjects/WaveMutators/Mutator_Fog.asset");
            var elite = AssetDatabase.LoadAssetAtPath<WaveMutatorData>("Assets/_ScriptableObjects/WaveMutators/Mutator_EliteSurge.asset");
            var glass = AssetDatabase.LoadAssetAtPath<WaveMutatorData>("Assets/_ScriptableObjects/WaveMutators/Mutator_GlassHorde.asset");

            Assert.IsNotNull(fog);
            Assert.IsNotNull(elite);
            Assert.IsNotNull(glass);
            Assert.AreEqual(WaveMutatorType.Fog, fog.mutatorType);
            Assert.AreEqual(1.5f, fog.xpMultiplier, 0.001f);
            Assert.IsTrue(fog.enableFogVisuals);
            Assert.AreEqual(WaveMutatorType.EliteSurge, elite.mutatorType);
            Assert.AreEqual(2f, elite.eliteChanceMultiplier, 0.001f);
            Assert.AreEqual(2, elite.miniBossShardMultiplier);
            Assert.AreEqual(WaveMutatorType.GlassHorde, glass.mutatorType);
            Assert.AreEqual(0.5f, glass.enemyHpMultiplier, 0.001f);
            Assert.AreEqual(2f, glass.enemyCountMultiplier, 0.001f);

            var cfg = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(cfg);
            Assert.IsTrue(cfg.waveMutatorsEnabled);
            Assert.AreEqual(30, cfg.firstWaveMutatorWave);
            Assert.AreEqual(3, cfg.waveMutators.Count);
            Assert.AreSame(fog, cfg.FindMutatorByType(WaveMutatorType.Fog));
            Assert.AreSame(elite, cfg.FindMutatorByType(WaveMutatorType.EliteSurge));
            Assert.AreSame(glass, cfg.FindMutatorByType(WaveMutatorType.GlassHorde));

            Scene gameplayScene = SceneManager.GetSceneByPath("Assets/_Scenes/Gameplay.unity");
            bool openedAdditively = !gameplayScene.isLoaded;
            if (openedAdditively)
            {
                gameplayScene = EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive);
            }

            var rootGo = Track(new GameObject("MutatorTestRootScope"));
            rootGo.SetActive(false);
            var rootScope = rootGo.AddComponent<RootLifetimeScope>();
            GameLifetimeScope gameScope = null;
            try
            {
                WaveMutatorFogOverlayUI fogOverlay = null;
                foreach (GameObject root in gameplayScene.GetRootGameObjects())
                {
                    if (fogOverlay == null) fogOverlay = root.GetComponentInChildren<WaveMutatorFogOverlayUI>(true);
                    if (gameScope == null) gameScope = root.GetComponentInChildren<GameLifetimeScope>(true);
                }

                Assert.IsNotNull(fogOverlay, "Gameplay.unity must contain FogMutatorOverlay with WaveMutatorFogOverlayUI");
                Assert.IsNotNull(fogOverlay.FogImage);
                Assert.IsFalse(fogOverlay.FogImage.raycastTarget);
                Assert.IsFalse(fogOverlay.FogImage.enabled, "Fog overlay image must start disabled");
                Assert.AreSame(UiTheme.Current.vignetteSprite, fogOverlay.FogImage.sprite);

                Assert.IsNotNull(gameScope);
                using (LifetimeScope.EnqueueParent(null))
                {
                    rootScope.Build();
                }
                using (LifetimeScope.EnqueueParent(rootScope))
                {
                    gameScope.Build();
                }

                Assert.IsNotNull(gameScope.Container.Resolve(typeof(WaveMutatorFogOverlayUI)));
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
        public void WaveMutators_Scheduling_StartsAtW30_AllowsMiniBoss_SkipsBossAndIntroWaves()
        {
            var realCfg = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(realCfg);

            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 1));
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 29));
            Assert.IsTrue(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 30), "W30 (mini-boss) must allow mutators");
            Assert.IsTrue(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 31));
            Assert.IsTrue(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 35), "W35 (mini-boss) must allow mutators");
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 32), "Intro wave W32 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 46), "Intro wave W46 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 61), "Intro wave W61 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 50), "Boss wave W50 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 75), "Boss wave W75 must be skipped");
            Assert.IsFalse(WaveEventManager.CanTriggerMutatorOnWave(realCfg, 100), "Boss wave W100 must be skipped");
        }

        [Test]
        public void WaveMutator_Fog_BoostsXp_DrivesFogVisualsAndOverlay_AndRestoresCleanlyWithoutLeaks()
        {
            RenderSettings.fog = false;
            RenderSettings.fogColor = Color.gray;

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.speed = 3f;
            enemyData.contactDamage = 5;
            enemyData.xpDrop = 10;
            enemyData.coinDrop = 0;

            var cfg = CreateMinimalWaveConfig(enemyData);
            cfg.waveEventsEnabled = false;
            cfg.corridorHordeEnabled = false;
            cfg.waveMutatorsEnabled = true;
            cfg.firstWaveMutatorWave = 30;
            cfg.waveMutators = new List<WaveMutatorData> { CreateTestMutator(WaveMutatorType.Fog) };

            var (heroGo, heroHealth, _, bus) = CreateHero();
            var heroExp = heroGo.AddComponent<HeroExperience>();
            heroExp.Construct(new HeroRuntimeStats(), bus, null);
            heroExp.EnsureStatsInitialized();

            var xpGo = Track(new GameObject("FogXpMgr"));
            var xpMgr = xpGo.AddComponent<XpManager>();
            var xpSo = new SerializedObject(xpMgr);
            xpSo.FindProperty("xpCrystalPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Pickups/XpCrystal.prefab");
            xpSo.ApplyModifiedPropertiesWithoutUndo();
            xpMgr.Construct(heroHealth, heroExp);
            xpMgr.EnsureInitialized();

            var waveGo = Track(new GameObject("FogWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);

            var lightGo = Track(new GameObject("FogDirLight"));
            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = Color.white;
            dirLight.intensity = 1.2f;

            var evtGo = Track(new GameObject("FogEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.AddComponent<WaveEventManager>();
            SetPrivateField(evtMgr, "overrideConfig", cfg);
            evtMgr.SetDirectionalLight(dirLight);
            evtMgr.Construct(waveMgr, heroHealth, null, null, null, bus, null, waveMgr.RuntimeModifiers);

            var overlayGo = Track(new GameObject("FogOverlayTest", typeof(RectTransform), typeof(Image), typeof(WaveMutatorFogOverlayUI)));
            var overlay = overlayGo.GetComponent<WaveMutatorFogOverlayUI>();
            SetPrivateField(overlay, "fogImage", overlayGo.GetComponent<Image>());
            overlay.Construct(evtMgr);
            Assert.IsFalse(overlay.FogImage.enabled);

            var deathHandler = new EnemyDeathHandler(xpMgr, null, new RunStatistics(), null, bus, waveMgr, null, null, evtMgr);
            deathHandler.Start();

            try
            {
                bus.Publish(new WaveCompletedEvent(29, 30));
                Assert.AreEqual(WaveMutatorType.Fog, evtMgr.ActiveMutatorType);
                Assert.AreEqual(1.5f, evtMgr.ActiveXpMultiplier, 0.001f);

                evtMgr.TickEvent(1.0f);
                overlay.TickOverlay();
                Assert.IsTrue(RenderSettings.fog);
                Assert.AreEqual(1f, evtMgr.FogBlend, 0.001f);
                Assert.IsTrue(overlay.FogImage.enabled);
                Assert.Greater(overlay.CurrentAlpha, 0.8f);
                Assert.Less(dirLight.intensity, 1.0f);

                bus.Publish(new EnemyDiedEvent(Vector3.zero, enemyData, wasElite: false));
                Assert.AreEqual(1, xpMgr.ActiveCount);
                XpCrystal spawnedCrystal = null;
                foreach (XpCrystal c in xpMgr.ActiveCrystals)
                {
                    spawnedCrystal = c;
                    break;
                }
                Assert.IsNotNull(spawnedCrystal);
                int boostedXp = (int)typeof(XpCrystal).GetField("xpAmount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawnedCrystal);
                Assert.AreEqual(15, boostedXp, "Fog mutator must grant 1.5x XP");

                // Completing wave 30 ends 1-wave Fog and fades visuals + restores RenderSettings
                bus.Publish(new WaveCompletedEvent(30, 31));
                Assert.AreEqual(WaveMutatorType.None, evtMgr.ActiveMutatorType);
                evtMgr.TickEvent(1.0f);
                overlay.TickOverlay();
                Assert.IsFalse(RenderSettings.fog);
                Assert.AreEqual(0f, evtMgr.FogBlend, 0.001f);
                Assert.IsFalse(overlay.FogImage.enabled);
                Assert.AreEqual(1.2f, dirLight.intensity, 0.01f);

                // Re-trigger and verify GameOverEvent immediately restores fog & modifiers
                Assert.IsTrue(evtMgr.StartMutator(WaveMutatorType.Fog));
                evtMgr.TickEvent(1.0f);
                Assert.IsTrue(RenderSettings.fog);
                bus.Publish(new GameOverEvent(31, 0));
                Assert.AreEqual(WaveMutatorType.None, evtMgr.ActiveMutatorType);
                Assert.IsFalse(RenderSettings.fog);
                Assert.AreEqual(1f, waveMgr.RuntimeModifiers.MutatorXpMultiplier, 0.001f);
            }
            finally
            {
                deathHandler.Dispose();
            }
        }

        [Test]
        public void WaveMutator_EliteSurge_DoublesEliteChance_AndDoublesMiniBossShardsAcrossWaveOverlap()
        {
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.speed = 3f;
            enemyData.contactDamage = 5;

            var cfg = CreateMinimalWaveConfig(enemyData);
            cfg.waveEventsEnabled = false;
            cfg.corridorHordeEnabled = false;
            cfg.waveMutatorsEnabled = true;
            cfg.firstWaveMutatorWave = 30;
            cfg.minWaveForElites = 10;
            cfg.eliteChancePerWave = 0.025f;
            cfg.maxEliteChance = 0.35f;
            cfg.miniBossInterval = 5;
            cfg.bossInterval = 25;
            cfg.miniBossShardReward = 1;
            cfg.waveMutators = new List<WaveMutatorData> { CreateTestMutator(WaveMutatorType.EliteSurge) };

            var (_, heroHealth, _, bus) = CreateHero();
            var runStats = new RunStatistics();
            var waveGo = Track(new GameObject("EliteSurgeWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();

            var evtGo = Track(new GameObject("EliteSurgeEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.AddComponent<WaveEventManager>();
            SetPrivateField(evtMgr, "overrideConfig", cfg);
            evtMgr.Construct(waveMgr, heroHealth, null, null, null, bus, null, waveMgr.RuntimeModifiers);

            var rewardsHandler = new MetaRunRewardsHandler(bus, runStats, waveMgr, null, null, null, waveMgr.RuntimeModifiers, null);
            rewardsHandler.Start();

            try
            {
                bus.Publish(new WaveCompletedEvent(29, 30));
                Assert.AreEqual(WaveMutatorType.EliteSurge, evtMgr.ActiveMutatorType);
                Assert.AreEqual(2f, waveMgr.RuntimeModifiers.MutatorEliteChanceMultiplier, 0.001f);
                Assert.AreEqual(2, waveMgr.RuntimeModifiers.MutatorMiniBossShardMultiplier);

                // Spawn W30 mini-boss while Elite Surge is active
                SetPrivateField(waveMgr, "currentWave", 30);
                typeof(WaveManager).GetMethod("SpawnMiniBoss", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(waveMgr, null);
                EnemyBase miniBossA = null;
                foreach (var e in waveMgr.ActiveEnemies)
                {
                    if (e != null && e.IsMiniBoss) { miniBossA = e; break; }
                }
                Assert.IsNotNull(miniBossA);
                Assert.AreEqual(2, miniBossA.ShardRewardMultiplier);

                // Even if wave 30 ends via wave overlap before mini-boss dies, its stamped ShardRewardMultiplier yields 2 shards!
                bus.Publish(new WaveCompletedEvent(30, 31));
                Assert.AreEqual(WaveMutatorType.None, evtMgr.ActiveMutatorType);
                Assert.AreEqual(1, waveMgr.RuntimeModifiers.MutatorMiniBossShardMultiplier);

                miniBossA.TakeDamage(99999);
                Assert.AreEqual(2, runStats.BossShardsEarned, "Mini-boss spawned during Elite Surge must still drop 2x shards");

                // Pooled reuse outside Elite Surge must reset ShardRewardMultiplier to 1
                SetPrivateField(waveMgr, "currentWave", 35);
                typeof(WaveManager).GetMethod("SpawnMiniBoss", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(waveMgr, null);
                EnemyBase miniBossB = null;
                foreach (var e in waveMgr.ActiveEnemies)
                {
                    if (e != null && e.IsMiniBoss) { miniBossB = e; break; }
                }
                Assert.IsNotNull(miniBossB);
                Assert.AreEqual(1, miniBossB.ShardRewardMultiplier);
                miniBossB.TakeDamage(99999);
                Assert.AreEqual(3, runStats.BossShardsEarned, "Normal mini-boss after Elite Surge must drop 1 shard");
            }
            finally
            {
                rewardsHandler.Dispose();
            }
        }

        [Test]
        public void WaveMutator_GlassHorde_HalvesEnemyHp_DoublesWaveCount_RespectsGlobalCap_AndResetsCleanly()
        {
            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 100;
            enemyData.speed = 2f;
            enemyData.contactDamage = 5;

            var cfg = CreateMinimalWaveConfig(enemyData);
            cfg.baseEnemyCount = 10;
            cfg.enemiesPerWaveMultiplier = 1f;
            cfg.maxEnemiesPerWave = 50;
            cfg.maxActiveEnemies = 16; // test global cap enforcement under x2 count (10 * 2 = 20 -> capped to 16)
            cfg.hpLinearPerWave = 0f;
            cfg.hpQuadraticPerWave = 0f;
            cfg.eliteChancePerWave = 0f;
            cfg.maxEliteChance = 0f;
            cfg.waveEventsEnabled = false;
            cfg.corridorHordeEnabled = false;
            cfg.waveMutatorsEnabled = true;
            cfg.firstWaveMutatorWave = 30;
            cfg.waveMutators = new List<WaveMutatorData> { CreateTestMutator(WaveMutatorType.GlassHorde) };

            var (_, heroHealth, _, bus) = CreateHero();
            var waveGo = Track(new GameObject("GlassWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);
            waveMgr.EnsurePoolsInitialized();

            var evtGo = Track(new GameObject("GlassEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.AddComponent<WaveEventManager>();
            SetPrivateField(evtMgr, "overrideConfig", cfg);
            evtMgr.Construct(waveMgr, heroHealth, null, null, null, bus, null, waveMgr.RuntimeModifiers);

            bus.Publish(new WaveCompletedEvent(30, 31));
            Assert.AreEqual(WaveMutatorType.GlassHorde, evtMgr.ActiveMutatorType);

            SetPrivateField(waveMgr, "currentWave", 31);
            MethodInfo spawnWave = typeof(WaveManager).GetMethod("SpawnWaveCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
            IEnumerator routine = (IEnumerator)spawnWave.Invoke(waveMgr, null);
            for (int i = 0; i < 40; i++)
            {
                if (!routine.MoveNext()) break;
            }

            Assert.AreEqual(16, waveMgr.EnemiesAlive, "Glass Horde doubles 10 -> 20, clamped cleanly to MaxActiveEnemies=16");
            foreach (var e in waveMgr.ActiveEnemies)
            {
                Assert.AreEqual(50, e.MaxHp, "Glass Horde must halve enemy HP (100 * 0.5 = 50)");
            }

            // Despawn all W31 enemies -> CheckWaveEnd advances to W32 and ends Glass Horde cleanly
            var w31Enemies = new List<EnemyBase>(waveMgr.ActiveEnemies);
            for (int i = 0; i < w31Enemies.Count; i++)
            {
                waveMgr.DespawnTrackedEnemy(w31Enemies[i]);
            }
            Assert.AreEqual(0, waveMgr.EnemiesAlive);
            Assert.AreEqual(32, waveMgr.CurrentWave);
            Assert.AreEqual(WaveMutatorType.None, evtMgr.ActiveMutatorType);

            IEnumerator routineNext = (IEnumerator)spawnWave.Invoke(waveMgr, null);
            for (int i = 0; i < 30; i++)
            {
                if (!routineNext.MoveNext()) break;
            }

            Assert.AreEqual(10, waveMgr.EnemiesAlive);
            foreach (var e in waveMgr.ActiveEnemies)
            {
                Assert.AreEqual(100, e.MaxHp);
            }
        }

        [Test]
        public void WaveEventBannerUI_DisplaysAndHidesWaveMutators_WithZeroGcRefresh()
        {
            var bus = new GameEventBus();
            var cfg = CreateMinimalWaveConfig(Track(ScriptableObject.CreateInstance<EnemyData>()));
            cfg.waveMutators = new List<WaveMutatorData> { CreateTestMutator(WaveMutatorType.GlassHorde) };

            var (_, heroHealth, _, _) = CreateHero();
            var waveGo = Track(new GameObject("BannerWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            SetPrivateField(waveMgr, "waveConfig", cfg);
            waveMgr.Construct(heroHealth, null, bus, null);

            var evtGo = Track(new GameObject("BannerEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.AddComponent<WaveEventManager>();
            SetPrivateField(evtMgr, "overrideConfig", cfg);
            evtMgr.Construct(waveMgr, heroHealth, null, null, null, bus, null, waveMgr.RuntimeModifiers);

            var bannerGo = Track(new GameObject("BannerRoot", typeof(RectTransform)));
            var contentGo = Track(new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(Outline)));
            contentGo.transform.SetParent(bannerGo.transform, false);
            var titleGo = Track(new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)));
            titleGo.transform.SetParent(contentGo.transform, false);
            var subGo = Track(new GameObject("Sub", typeof(RectTransform), typeof(TextMeshProUGUI)));
            subGo.transform.SetParent(contentGo.transform, false);
            var statusGo = Track(new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI)));
            statusGo.transform.SetParent(contentGo.transform, false);
            var fillGo = Track(new GameObject("Fill", typeof(RectTransform), typeof(Image)));
            fillGo.transform.SetParent(contentGo.transform, false);

            var banner = bannerGo.AddComponent<WaveEventBannerUI>();
            SetPrivateField(banner, "bannerRoot", contentGo);
            SetPrivateField(banner, "backgroundImage", contentGo.GetComponent<Image>());
            SetPrivateField(banner, "borderOutline", contentGo.GetComponent<Outline>());
            SetPrivateField(banner, "titleText", titleGo.GetComponent<TextMeshProUGUI>());
            SetPrivateField(banner, "subtitleText", subGo.GetComponent<TextMeshProUGUI>());
            SetPrivateField(banner, "statusText", statusGo.GetComponent<TextMeshProUGUI>());
            SetPrivateField(banner, "progressFillImage", fillGo.GetComponent<Image>());
            banner.Construct(evtMgr, bus);

            Assert.IsTrue(evtMgr.StartMutator(WaveMutatorType.GlassHorde));
            Assert.IsTrue(contentGo.activeSelf);
            Assert.AreEqual("GLASS HORDE!", titleGo.GetComponent<TextMeshProUGUI>().text);
            Assert.AreEqual("1 WAVE", statusGo.GetComponent<TextMeshProUGUI>().text);

            MethodInfo refreshMethod = typeof(WaveEventBannerUI).GetMethod("RefreshStatusText", BindingFlags.Instance | BindingFlags.NonPublic);
            refreshMethod.Invoke(banner, null); // warmup
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                refreshMethod.Invoke(banner, null);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "WaveEventBannerUI mutator status refresh must be Zero-GC");

            evtMgr.EndCurrentMutator(completed: true);
            Assert.IsFalse(contentGo.activeSelf);
        }

        [Test]
        public void GraphicsSettings_FogStripping_IsCustomLinearOnly()
        {
            var gs = GraphicsSettings.GetGraphicsSettings();
            Assert.IsNotNull(gs);
            var so = new SerializedObject(gs);
            Assert.AreEqual(1, so.FindProperty("m_FogStripping").intValue, "Fog Modes must be Custom (1) so Linear fog variants are not stripped in builds");
            Assert.IsTrue(so.FindProperty("m_FogKeepLinear").boolValue, "Linear fog shader variants must be kept");
            Assert.IsFalse(so.FindProperty("m_FogKeepExp").boolValue, "Exp fog variants should be stripped");
            Assert.IsFalse(so.FindProperty("m_FogKeepExp2").boolValue, "Exp2 fog variants should be stripped");
        }

        [Test]
        public void WaveMutators_EliteSurge_OnlySelectedOnMiniBossWaves()
        {
            var dummyEnemy = Track(ScriptableObject.CreateInstance<EnemyData>());
            var cfg = CreateMinimalWaveConfig(dummyEnemy);
            cfg.miniBossInterval = 5;
            cfg.bossInterval = 25;
            cfg.firstWaveMutatorWave = 30;

            WaveMutatorData eliteSurge = CreateTestMutator(WaveMutatorType.EliteSurge);
            eliteSurge.selectionWeight = 100f;
            WaveMutatorData fog = CreateTestMutator(WaveMutatorType.Fog);
            fog.selectionWeight = 1f;
            WaveMutatorData glass = CreateTestMutator(WaveMutatorType.GlassHorde);
            glass.selectionWeight = 1f;

            var evtGo = Track(new GameObject("EliteSurgeFilterMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.AddComponent<WaveEventManager>();
            MethodInfo pickMethod = typeof(WaveEventManager).GetMethod("PickNextMutator", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(pickMethod);

            // Only EliteSurge configured -> returns null on non-mini-boss waves, EliteSurge on mini-boss waves
            cfg.waveMutators = new List<WaveMutatorData> { eliteSurge };
            Assert.IsNull(pickMethod.Invoke(evtMgr, new object[] { cfg, 31 }));
            Assert.IsNull(pickMethod.Invoke(evtMgr, new object[] { cfg, 33 }));
            Assert.IsNull(pickMethod.Invoke(evtMgr, new object[] { cfg, 34 }));
            Assert.AreSame(eliteSurge, pickMethod.Invoke(evtMgr, new object[] { cfg, 30 }));
            Assert.AreSame(eliteSurge, pickMethod.Invoke(evtMgr, new object[] { cfg, 35 }));

            // With all 3 mutators and overwhelming weight (100 vs 1), EliteSurge must never be chosen on a non-mini-boss wave
            cfg.waveMutators = new List<WaveMutatorData> { eliteSurge, fog, glass };
            for (int i = 0; i < 50; i++)
            {
                var picked = pickMethod.Invoke(evtMgr, new object[] { cfg, 31 }) as WaveMutatorData;
                Assert.IsNotNull(picked);
                Assert.AreNotEqual(WaveMutatorType.EliteSurge, picked.mutatorType, "Elite Surge must not be selected on non-mini-boss waves");
            }
        }
    }
}
