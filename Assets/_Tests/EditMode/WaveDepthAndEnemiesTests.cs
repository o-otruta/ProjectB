using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    public class WaveDepthAndEnemiesTests
    {
        private readonly List<UnityEngine.Object> tracked = new List<UnityEngine.Object>();

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
    }
}
