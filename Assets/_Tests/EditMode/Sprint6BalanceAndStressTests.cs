using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
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
    public class Sprint6BalanceAndStressTests
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
            if (obj != null)
            {
                spawned.Add(obj);
            }
            return obj;
        }

        [Test]
        public void AllFourHeroUltimates_ScaleWithRuntimeStatsCritAndTransitionToCooldown()
        {
            using var bus = new GameEventBus();
            int meteorEvents = 0;
            int arrowWaveEvents = 0;
            int soulNovaEvents = 0;
            bus.Subscribe<MeteorImpactEvent>(_ => meteorEvents++);
            bus.Subscribe<ArrowRainWaveEvent>(_ => arrowWaveEvents++);
            bus.Subscribe<SoulNovaCastEvent>(_ => soulNovaEvents++);

            using var saveManager = new SaveManager();
            // 100% Crit with 1.5x CritMultiplier and +50% Global Damage (1.5x) -> 2.25x total ability damage
            var stats = new HeroRuntimeStats();
            stats.AddGlobalDamagePercent(50f);
            stats.AddCritChancePercent(100f);
            stats.AddCritMultiplier(0.4f); // 1.1 + 0.4 = 1.5x

            var heroGo = Track(new GameObject("UltTestHero"));
            heroGo.transform.position = Vector3.zero;
            var health = heroGo.AddComponent<HeroHealth>();
            health.Construct(stats, bus, null);
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 200);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 100);

            var shieldVisual = Track(new GameObject("ShieldVisual"));
            shieldVisual.transform.SetParent(heroGo.transform, false);

            var ult = heroGo.AddComponent<HeroUltimate>();
            ult.Construct(health, null, null, bus, stats);

            var enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.hp = 5000;
            enemyData.speed = 4f;
            enemyData.contactDamage = 10;

            var enemyGo = Track(new GameObject("UltTargetEnemy"));
            enemyGo.layer = 8;
            enemyGo.transform.position = new Vector3(3.5f, 0f, 0f);
            enemyGo.AddComponent<BoxCollider>();
            var enemy = enemyGo.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData, heroGo.transform, null, bus, 1f);
            Physics.SyncTransforms();

            // 1. Knight Shield Ultimate
            var knight = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/KnightData.asset");
            Assert.IsNotNull(knight);
            ConfigureHeroUltimate(ult, knight, shieldVisual);

            Assert.IsTrue(ult.IsReady);
            Assert.IsTrue(ult.TryActivate());
            Assert.IsTrue(ult.IsActive);
            Assert.IsTrue(health.IsInvulnerable);
            Assert.IsTrue(shieldVisual.activeSelf);

            health.TakeDamage(90);
            Assert.AreEqual(100, health.CurrentHp, "Shield ultimate must block incoming damage");

            InvokeDeactivateUltimate(ult);
            Assert.IsFalse(ult.IsActive);
            Assert.IsFalse(health.IsInvulnerable);
            Assert.IsFalse(shieldVisual.activeSelf);
            Assert.AreEqual(knight.ultimateCooldown, ult.CooldownRemaining, 0.01f);
            ult.ResetCooldown();
            Assert.IsTrue(ult.IsReady);

            // 2. Mage MeteorStrike Ultimate (80 base * 1.5 global * 1.5 crit = 180 damage per meteor)
            var mage = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/MageData.asset");
            Assert.IsNotNull(mage);
            ConfigureHeroUltimate(ult, mage, shieldVisual);

            int hpBeforeMeteor = enemy.CurrentHp;
            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, meteorEvents);
            Assert.AreEqual(hpBeforeMeteor - 180, enemy.CurrentHp);

            MethodInfo dropMeteor = typeof(HeroUltimate).GetMethod("DropNextMeteor", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 5; i++)
            {
                dropMeteor.Invoke(ult, null);
            }
            Assert.AreEqual(6, meteorEvents);
            Assert.AreEqual(hpBeforeMeteor - 180 * 6, enemy.CurrentHp);
            InvokeDeactivateUltimate(ult);
            ult.ResetCooldown();

            // 3. Ranger ArrowRain Ultimate (50 base * 1.5 global * 1.5 crit = 112.5 -> 112 or 113 damage per wave, 3 waves + 50% slow)
            var ranger = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/RangerData.asset");
            Assert.IsNotNull(ranger);
            ConfigureHeroUltimate(ult, ranger, shieldVisual);

            int hpBeforeArrows = enemy.CurrentHp;
            Assert.IsTrue(ult.TryActivate());
            ult.TriggerArrowRainWave();
            ult.TriggerArrowRainWave();
            Assert.AreEqual(3, arrowWaveEvents);
            int expectedWaveDmg = Mathf.RoundToInt(50f * 1.5f * 1.5f);
            Assert.AreEqual(hpBeforeArrows - expectedWaveDmg * 3, enemy.CurrentHp);
            Assert.AreEqual(2.0f, enemy.EffectiveMoveSpeed, 0.01f, "ArrowRain must slow enemy by 50%");
            InvokeDeactivateUltimate(ult);
            ult.ResetCooldown();

            // 4. Necromancer SoulNova Ultimate (+35% MaxHP heal = +70 HP, 12 piercing bolts * 60 base * 1.5 * 1.5 = 135 damage)
            var necro = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/Heroes/NecromancerHeroData.asset");
            Assert.IsNotNull(necro);
            ConfigureHeroUltimate(ult, necro, shieldVisual);

            int hpBeforeSoul = enemy.CurrentHp;
            Assert.IsTrue(ult.TryActivate());
            Assert.AreEqual(1, soulNovaEvents);
            Assert.AreEqual(170, health.CurrentHp, "SoulNova must heal 35% of 200 MaxHP (+70 HP)");

            for (int i = 0; i < 15; i++)
            {
                ult.StepSoulNova(0.05f);
            }
            Assert.AreEqual(hpBeforeSoul - 135, enemy.CurrentHp);
        }

        [Test]
        public void WaveConfig_CompletePhasedScheduleAndWaveProgression_MatchesSprint6BalanceTargets()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(config, "WaveConfig.asset must exist");
            Assert.AreEqual(8, config.enemySchedule.Count);

            var eligible = new List<EnemyWaveEntry>();

            // Phased introduction verification across waves 1..20
            Assert.AreEqual(1, config.GetEligibleEntriesForWave(1, eligible));
            Assert.AreEqual("SlimeData", eligible[0].enemy.name);

            Assert.AreEqual(2, config.GetEligibleEntriesForWave(3, eligible));
            Assert.IsTrue(eligible.Exists(e => e.enemy.name == "GoblinData"));

            Assert.AreEqual(2, config.GetEligibleEntriesForWave(5, eligible));
            Assert.IsFalse(eligible.Exists(e => e.enemy.name == "SkeletonData"), "SkeletonData shifted to Wave 6 to avoid Mini-Boss Wave 5");

            Assert.AreEqual(3, config.GetEligibleEntriesForWave(6, eligible));
            var skelEntry = eligible.Find(e => e.enemy.name == "SkeletonData");
            Assert.IsNotNull(skelEntry);
            Assert.AreEqual(4, skelEntry.packSize);

            Assert.AreEqual(4, config.GetEligibleEntriesForWave(7, eligible));
            Assert.IsTrue(eligible.Exists(e => e.enemy.name == "BatData"));

            Assert.AreEqual(4, config.GetEligibleEntriesForWave(10, eligible));
            Assert.IsFalse(eligible.Exists(e => e.enemy.name == "BombSlimeData"), "BombSlimeData shifted to Wave 11 to avoid Mini-Boss Wave 10");

            Assert.AreEqual(5, config.GetEligibleEntriesForWave(11, eligible));
            Assert.IsTrue(eligible.Exists(e => e.enemy.name == "BombSlimeData"));

            Assert.AreEqual(6, config.GetEligibleEntriesForWave(14, eligible));
            Assert.IsTrue(eligible.Exists(e => e.enemy.name == "OrcChargerData"));

            // Deterministic weighted distribution across 100 normalized rolls on Wave 40
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < 100; i++)
            {
                float roll = i / 100f;
                var picked = config.SelectEnemyEntryForWave(40, roll);
                Assert.IsNotNull(picked);
                Assert.IsNotNull(picked.enemy);
                counts.TryGetValue(picked.enemy.name, out int c);
                counts[picked.enemy.name] = c + 1;
            }
            Assert.AreEqual(8, counts.Count, "All 8 enemy archetypes must be represented on Wave 40");

            // Mini-boss selection across 50 rolls on Wave 40 must never pick ExploderEnemyData
            for (int i = 0; i < 50; i++)
            {
                var miniBossData = config.SelectMiniBossEnemyForWave(40, i / 50f);
                Assert.IsNotNull(miniBossData);
                Assert.IsFalse(miniBossData is ExploderEnemyData);
            }

            // Verify all 8 MetaUpgrades have monotonically increasing exponential costs and positive maxLevel
            var metaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            Assert.AreEqual(8, metaUpgrades.Length);
            int totalMaxUpgradeCoins = 0;
            foreach (var upg in metaUpgrades)
            {
                Assert.IsNotNull(upg);
                Assert.Greater(upg.maxLevel, 0, upg.id);
                Assert.Greater(upg.baseCost, 0, upg.id);
                Assert.GreaterOrEqual(upg.costMultiplier, 1.15f, upg.id);

                int prevCost = 0;
                for (int lvl = 0; lvl < upg.maxLevel; lvl++)
                {
                    int cost = upg.GetCostForLevel(lvl);
                    Assert.GreaterOrEqual(cost, prevCost, $"{upg.id} cost must increase with level");
                    prevCost = cost;
                    totalMaxUpgradeCoins += cost;
                }
            }
            // Full meta progression requires substantial long-term coin investment (> 50,000 coins)
            Assert.Greater(totalMaxUpgradeCoins, 50000);
        }

        [Test]
        public void HighVolumePickups_MagnetizeAll_Collects250PickupsWithZeroCombatGc()
        {
            var bus = new GameEventBus();
            var runStats = new RunStatistics();
            var stats = new HeroRuntimeStats();
            stats.AddCoinBonusPercent(50f); // 1.5x coins

            var heroGo = Track(new GameObject("MagnetStressHero"));
            heroGo.transform.position = Vector3.zero;
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroExp = heroGo.AddComponent<HeroExperience>();
            heroExp.Construct(stats, bus);
            heroExp.EnsureStatsInitialized();
            var heroEcon = heroGo.AddComponent<HeroEconomy>();
            heroEcon.Construct(runStats, bus, stats);

            var xpGo = Track(new GameObject("StressXpManager"));
            var xpMgr = xpGo.AddComponent<XpManager>();
            xpMgr.Construct(heroHealth, heroExp);
            xpMgr.PrewarmPool(160);

            var coinGo = Track(new GameObject("StressCoinManager"));
            var coinMgr = coinGo.AddComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);
            coinMgr.PrewarmPool(120);

            // Spawn 150 XP crystals and 100 coins across the arena
            var crystals = new List<XpCrystal>(150);
            var coins = new List<CoinPickup>(100);
            for (int i = 0; i < 150; i++)
            {
                float angle = i * 0.25f;
                float dist = 10f + (i % 30);
                xpMgr.SpawnXp(new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist), 2);
            }
            for (int i = 0; i < 100; i++)
            {
                float angle = i * 0.35f;
                float dist = 12f + (i % 25);
                coinMgr.SpawnCoin(new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist), 2);
            }

            Assert.AreEqual(150, xpMgr.ActiveCount);
            Assert.AreEqual(100, coinMgr.ActiveCount);

            foreach (var c in xpMgr.ActiveCrystals) crystals.Add(c);
            foreach (var c in coinMgr.ActiveCoins) coins.Add(c);

            // Warmup call
            xpMgr.MagnetizeAll(28f);
            coinMgr.MagnetizeAll(28f);

            // Measure GC of MagnetizeAll + flight ticks across all 250 active pickups
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            int xpMag = xpMgr.MagnetizeAll(28f);
            int coinMag = coinMgr.MagnetizeAll(28f);
            for (int i = 0; i < crystals.Count; i++)
            {
                crystals[i].Tick(0.016f);
            }
            for (int i = 0; i < coins.Count; i++)
            {
                coins[i].Tick(0.016f);
            }
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

            Assert.AreEqual(150, xpMag);
            Assert.AreEqual(100, coinMag);
            Assert.AreEqual(0L, allocatedBytes, "MagnetizeAll and 250 pickup flight ticks must allocate 0 bytes of GC");

            // Step until all 250 pickups reach the hero and return to pool
            for (int step = 0; step < 60; step++)
            {
                for (int i = 0; i < crystals.Count; i++)
                {
                    if (crystals[i].gameObject.activeSelf) crystals[i].Tick(0.05f);
                }
                for (int i = 0; i < coins.Count; i++)
                {
                    if (coins[i].gameObject.activeSelf) coins[i].Tick(0.05f);
                }
            }

            Assert.AreEqual(0, xpMgr.ActiveCount, "All 150 XP crystals must be collected and returned to pool");
            Assert.AreEqual(0, coinMgr.ActiveCount, "All 100 coins must be collected and returned to pool");
            Assert.AreEqual(300, runStats.CoinsEarned, "100 coins * 2 base * 1.5x CoinMultiplier = 300 coins");
            Assert.Greater(heroExp.CurrentLevel, 5);
        }

        [Test]
        public void Wave40StressTest_120ActiveEnemies_6ActiveSkillsPlusSynergies_MeetsFrameTimingAndZeroGcInCombatLoop()
        {
            using var saveManager = new SaveManager();
            saveManager.AddBonusActiveSlots(3, saveImmediately: false);
            saveManager.AddBonusPassiveSlots(3, saveImmediately: false);
            var metaMgr = new MetaUpgradeManager(saveManager);
            using var slotService = new AbilitySlotService(saveManager, metaMgr);
            var achMgr = new AchievementManager(saveManager);
            var runStats = new RunStatistics();
            using var bus = new GameEventBus();

            var stats = new HeroRuntimeStats(metaMgr);
            var heroGo = Track(new GameObject("Wave40Hero"));
            heroGo.transform.position = Vector3.zero;
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(stats, bus, null);
            typeof(HeroHealth).GetField("currentMaxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 600);
            typeof(HeroHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(heroHealth, 450);

            var heroExp = heroGo.AddComponent<HeroExperience>();
            heroExp.Construct(stats, bus);
            heroExp.EnsureStatsInitialized();

            var heroEcon = heroGo.AddComponent<HeroEconomy>();
            heroEcon.Construct(runStats, bus, stats);

            var heroAbilities = heroGo.AddComponent<HeroAbilities>();
            heroAbilities.Construct(stats, bus, achMgr, heroHealth, slotService);

            // Equip 6 Active abilities + 6 Passive abilities + apply 5 Synergies
            string[] activeAssetPaths =
            {
                "Assets/_ScriptableObjects/Abilities/OrbitalSmallSwordData.asset",
                "Assets/_ScriptableObjects/Abilities/ChainLightningData.asset",
                "Assets/_ScriptableObjects/Abilities/PoisonFlaskData.asset",
                "Assets/_ScriptableObjects/Abilities/GuardianShieldData.asset",
                "Assets/_ScriptableObjects/Abilities/MiniDragonData.asset",
                "Assets/_ScriptableObjects/Abilities/SeismicHammerData.asset"
            };
            foreach (string path in activeAssetPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
                Assert.IsNotNull(data, $"Missing active ability: {path}");
                Assert.IsTrue(heroAbilities.AddAbility(data), $"Failed to equip active ability: {data.id}");
            }

            string[] passiveAssetPaths =
            {
                "Assets/_ScriptableObjects/Abilities/ChronoTomeData.asset",
                "Assets/_ScriptableObjects/Abilities/CriticalLensData.asset",
                "Assets/_ScriptableObjects/Abilities/TitanHeartData.asset",
                "Assets/_ScriptableObjects/Abilities/GoldenIdolData.asset",
                "Assets/_ScriptableObjects/Abilities/BattleFuryData.asset",
                "Assets/_ScriptableObjects/Abilities/IronSkinData.asset"
            };
            foreach (string path in passiveAssetPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
                Assert.IsNotNull(data, $"Missing passive ability: {path}");
                Assert.IsTrue(heroAbilities.AddAbility(data), $"Failed to equip passive ability: {data.id}");
            }

            Assert.AreEqual(6, heroAbilities.ActiveAbilities.Count);
            Assert.AreEqual(6, heroAbilities.PassiveAbilities.Count);

            var applier = new UpgradeApplier(heroHealth, stats, heroAbilities, bus);
            string[] synergyPaths =
            {
                "Assets/_ScriptableObjects/Synergies/Synergy_Thunderstorm.asset",
                "Assets/_ScriptableObjects/Synergies/Synergy_ToxicInferno.asset",
                "Assets/_ScriptableObjects/Synergies/Synergy_AegisPhalanx.asset",
                "Assets/_ScriptableObjects/Synergies/Synergy_DragonLord.asset",
                "Assets/_ScriptableObjects/Synergies/Synergy_CataclysmVortex.asset"
            };
            foreach (string synPath in synergyPaths)
            {
                var syn = AssetDatabase.LoadAssetAtPath<SynergyData>(synPath);
                Assert.IsNotNull(syn);
                heroAbilities.TryActivateSynergyById(syn.id, syn.synergyName);
            }

            for (int i = 0; i < heroAbilities.ActiveAbilities.Count; i++)
            {
                heroAbilities.ActiveAbilities[i].PrewarmPools(12);
            }

            // Set up VfxFeedbackManager + HUD + XpManager + CoinManager + WaveManager on Wave 40
            var camGo = Track(new GameObject("StressCam", typeof(Camera), typeof(CameraController)));
            camGo.transform.position = new Vector3(0f, 20f, -10f);
            camGo.transform.rotation = Quaternion.LookRotation(-new Vector3(0f, 20f, -10f));
            var camCtrl = camGo.GetComponent<CameraController>();

            var vfxGo = Track(new GameObject("StressVfxManager", typeof(VfxFeedbackManager)));
            var vfx = vfxGo.GetComponent<VfxFeedbackManager>();
            vfx.Construct(bus, null, camCtrl, heroHealth, null);
            vfx.EnsureInitialized();

            var xpGo = Track(new GameObject("Wave40XpMgr", typeof(XpManager)));
            var xpMgr = xpGo.GetComponent<XpManager>();
            xpMgr.Construct(heroHealth, heroExp);
            xpMgr.PrewarmPool(120);

            var coinGo = Track(new GameObject("Wave40CoinMgr", typeof(CoinManager)));
            var coinMgr = coinGo.GetComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);
            coinMgr.PrewarmPool(120);

            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            using var enemyProjPool = new EnemyProjectilePool();
            var enemyWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_ScriptableObjects/Weapons/EnemyShooterWeaponData.asset");
            enemyProjPool.Prewarm(enemyWeapon, 64);

            var waveGo = Track(new GameObject("Wave40Manager", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            typeof(WaveManager).GetField("waveConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(waveMgr, waveConfig);
            waveMgr.Construct(heroHealth, achMgr, bus, enemyProjPool, null);
            waveMgr.PrewarmPools(28);

            var deathHandler = new EnemyDeathHandler(xpMgr, coinMgr, runStats, achMgr, bus, waveMgr, null);
            deathHandler.Start();

            try
            {
                // Spawn 120 active enemies on Wave 40 (20 of each of the 6 archetypes, including all 3 elite types)
                const int totalEnemies = 120;
                var activeEnemyList = new List<EnemyBase>(totalEnemies);
                for (int i = 0; i < totalEnemies; i++)
                {
                    EnemyData archetypeData = waveConfig.enemySchedule[i % waveConfig.enemySchedule.Count].enemy;
                    float angle = (Mathf.PI * 2f * i) / totalEnemies;
                    float radius = 2.0f + (i % 12) * 0.75f;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                    bool isElite = (i % 5) == 0;
                    EliteArchetype eliteType = (i % 3) switch
                    {
                        0 => EliteArchetype.Berserker,
                        1 => EliteArchetype.Juggernaut,
                        _ => EliteArchetype.HasteAura
                    };

                    EnemyBase enemy = waveMgr.SpawnEnemyForWave(archetypeData, pos, waveNumber: 40, makeElite: isElite, eliteArchetype: eliteType);
                    // Give high HP so enemies survive multiple frames of heavy AoE bombardment during the stress test
                    typeof(EnemyBase).GetField("maxHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 50000);
                    typeof(EnemyBase).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 50000);
                    activeEnemyList.Add(enemy);
                }

                Physics.SyncTransforms();
                Assert.AreEqual(totalEnemies, waveMgr.EnemiesAlive);

                // Warmup pass: execute all 6 active abilities + enemy ticks + VFX damage number recycling once
                StepWave40CombatTick(activeEnemyList, heroAbilities, vfx);
                Physics.SyncTransforms();

                // Measure Zero-GC steady-state combat tick with 120 active enemies and 6 active skills + synergies
                long gcBefore = GC.GetAllocatedBytesForCurrentThread();
                StepWave40CombatTick(activeEnemyList, heroAbilities, vfx);
                long gcAllocated = GC.GetAllocatedBytesForCurrentThread() - gcBefore;

                Assert.AreEqual(0L, gcAllocated, $"Expected 0 bytes of GC allocation in Wave 40 steady-state combat tick, but allocated {gcAllocated} bytes");

                // Measure Frame Timing across 60 simulated combat frames with 120 active enemies + 6 active skills
                var sw = Stopwatch.StartNew();
                const int benchmarkFrames = 60;
                for (int frame = 0; frame < benchmarkFrames; frame++)
                {
                    StepWave40CombatTick(activeEnemyList, heroAbilities, vfx);
                }
                sw.Stop();

                double avgFrameMs = sw.Elapsed.TotalMilliseconds / benchmarkFrames;
                Assert.Less(avgFrameMs, 16.67, $"Wave 40 stress test average frame time ({avgFrameMs:F2} ms) exceeded 16.67 ms (60 FPS budget)");
                Assert.LessOrEqual(vfx.ActiveDamageNumberCount, VfxFeedbackManager.MAX_ACTIVE_DAMAGE_NUMBERS);
            }
            finally
            {
                deathHandler.Dispose();
            }
        }

        [Test]
        public void HudWidgets_ZeroGcStringFormatting_WorksForHpBossWaveLevelCoinsAndUltimate()
        {
            var hpGo = Track(new GameObject("TestHpText", typeof(RectTransform), typeof(TextMeshProUGUI)));
            var hpTmp = hpGo.GetComponent<TextMeshProUGUI>();
            hpTmp.text = "000 / 000";
            char[] buf = new char[24];
            int len0 = HpBarUI.WriteRatioToBuffer(buf, 200, 200);
            hpTmp.SetCharArray(buf, 0, len0);

            long b0 = GC.GetAllocatedBytesForCurrentThread();
            for (int hp = 199; hp >= 150; hp--)
            {
                int len = HpBarUI.WriteRatioToBuffer(buf, hp, 200);
                hpTmp.SetCharArray(buf, 0, len);
            }
            string waveStr = WaveHudUI.GetCachedWaveText(42);
            string lvlStr = XpBarUI.GetCachedLevelText(18);
            string tenthsStr = UltimateButtonUI.GetCachedTenthsText(24);
            string secStr = UltimateButtonUI.GetCachedSecondsText(19);
            string coinStr = DamageNumberItem.GetCachedNumberText(1850);
            string critStr = DamageNumberItem.GetCachedCritNumberText(2240);
            string healStr = DamageNumberItem.GetCachedHealText(70);
            long b1 = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, b1 - b0, "HUD cached string lookups and SetCharArray updates must allocate 0 bytes");
            Assert.AreEqual("Wave 42", waveStr);
            Assert.AreEqual("Lv 18", lvlStr);
            Assert.AreEqual("2.4s", tenthsStr);
            Assert.AreEqual("19s", secStr);
            Assert.AreEqual("1850", coinStr);
            Assert.AreEqual("2240!", critStr);
            Assert.AreEqual("+70 HP", healStr);
        }

        private static void StepWave40CombatTick(
            List<EnemyBase> enemies,
            HeroAbilities heroAbilities,
            VfxFeedbackManager vfx)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyBase e = enemies[i];
                if (e != null && !e.IsDead)
                {
                    e.SimulateFrameTick();
                }
            }

            var actives = heroAbilities.ActiveAbilities;
            for (int i = 0; i < actives.Count; i++)
            {
                ActiveAbility ability = actives[i];
                if (ability is ChainLightningAbility chain)
                {
                    chain.TriggerChainCast();
                }
                else if (ability is GuardianShieldAbility shield)
                {
                    shield.ApplyShieldScan();
                }
                else if (ability is SeismicHammerAbility hammer)
                {
                    hammer.TriggerSlam();
                }
                else if (ability is MiniDragonAbility dragon)
                {
                    dragon.FireAllDragons();
                }
                else
                {
                    ability.SimulateFrameTick();
                }
            }

            vfx.TickDamageNumbers(0.016f);
        }

        private static void ConfigureHeroUltimate(HeroUltimate ult, HeroData heroData, GameObject shieldVisual)
        {
            var so = new SerializedObject(ult);
            so.FindProperty("fallbackHeroData").objectReferenceValue = heroData;
            so.FindProperty("shieldVisual").objectReferenceValue = shieldVisual;
            so.FindProperty("enemyLayer").intValue = 1 << 8;
            so.ApplyModifiedPropertiesWithoutUndo();
            typeof(HeroUltimate).GetField("activeHeroData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ult, heroData);
            ult.Prewarm();
        }

        private static void InvokeDeactivateUltimate(HeroUltimate ult)
        {
            typeof(HeroUltimate)
                .GetMethod("DeactivateUltimate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ult, null);
        }
    }
}
