using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.EditorScripts;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Tests.EditMode
{
    public class EconomyAndBalanceTests
    {
        private readonly System.Collections.Generic.List<Object> trackedObjects = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = trackedObjects.Count - 1; i >= 0; i--)
            {
                if (trackedObjects[i] != null)
                {
                    Object.DestroyImmediate(trackedObjects[i]);
                }
            }
            trackedObjects.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            trackedObjects.Add(obj);
            return obj;
        }

        [Test]
        public void WaveConfig_CoinMultiplier_IsOneOnWaveOne_ScalesAndClampsAtCap()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
            Assert.IsNotNull(config, "WaveConfig.asset must exist");

            Assert.AreEqual(1f, config.GetCoinMultiplier(0), 0.0001f);
            Assert.AreEqual(1f, config.GetCoinMultiplier(1), 0.0001f);

            float w25 = config.GetCoinMultiplier(25);
            float w50 = config.GetCoinMultiplier(50);
            float w200 = config.GetCoinMultiplier(200);

            Assert.Greater(w25, 1f);
            Assert.Greater(w50, w25);
            Assert.LessOrEqual(w200, config.maxCoinMultiplier + 0.0001f);
            Assert.AreEqual(config.maxCoinMultiplier, w200, 0.0001f);

            // Verify EnemyDeathHandler applies waveCoinMult on wave 50 while preserving exact drop on wave 1
            var bus = new GameEventBus();
            var runStats = new RunStatistics();

            var heroGo = Track(new GameObject("CoinScaleHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            var heroEcon = heroGo.AddComponent<HeroEconomy>();
            heroEcon.Construct(runStats, bus);

            var coinMgrGo = Track(new GameObject("CoinScaleMgr"));
            var coinMgr = coinMgrGo.AddComponent<CoinManager>();
            coinMgr.Construct(heroHealth, heroEcon);
            coinMgr.EnsureInitialized();

            var xpMgrGo = Track(new GameObject("XpScaleMgr"));
            var xpMgr = xpMgrGo.AddComponent<XpManager>();
            xpMgr.Construct(heroHealth, null);
            xpMgr.EnsureInitialized();

            var waveGo = Track(new GameObject("CoinScaleWaveMgr"));
            var waveMgr = waveGo.AddComponent<WaveManager>();
            typeof(WaveManager).GetField("waveConfig", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(waveMgr, config);
            FieldInfo waveField = typeof(WaveManager).GetField("currentWave", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(waveField);

            var deathHandler = new EnemyDeathHandler(xpMgr, coinMgr, runStats, null, bus, waveMgr);
            deathHandler.Start();

            try
            {
                var enemy = Track(ScriptableObject.CreateInstance<EnemyData>());
                enemy.xpDrop = 0;
                enemy.coinDrop = 1;
                enemy.coinDropChance = 1f;

                waveField.SetValue(waveMgr, 1);
                bus.Publish(new EnemyDiedEvent(Vector3.zero, enemy, wasElite: false));
                FlushAllCoins();
                Assert.AreEqual(1, runStats.CoinsEarned, "Wave 1 must award exact coinDrop (1.0x multiplier)");

                // Probabilistic rounding check on Wave 25 (coinDrop = 1 * 1.36 = 1.36 expected coins/kill)
                Random.InitState(12345);
                const int sampleKills = 500;
                float expectedMult25 = config.GetCoinMultiplier(25);
                waveField.SetValue(waveMgr, 25);

                int beforeW25 = runStats.CoinsEarned;
                for (int i = 0; i < sampleKills; i++)
                {
                    bus.Publish(new EnemyDiedEvent(Vector3.zero, enemy, wasElite: false));
                    if ((i + 1) % 50 == 0)
                    {
                        FlushAllCoins();
                    }
                }
                FlushAllCoins();

                float avgPerKill = (float)(runStats.CoinsEarned - beforeW25) / sampleKills;
                float expectedPerKill = enemy.coinDrop * expectedMult25;
                Assert.AreEqual(expectedPerKill, avgPerKill, 0.08f,
                    $"Average coin drop on Wave 25 should match probabilistic expectation {expectedPerKill:F2}");

                // Mini-boss burst drop also uses probabilistic rounding per coin
                const int miniBossKills = 40;
                int beforeMiniBoss = runStats.CoinsEarned;
                for (int i = 0; i < miniBossKills; i++)
                {
                    bus.Publish(new EnemyDiedEvent(Vector3.zero, enemy, wasElite: false, wasMiniBoss: true));
                    FlushAllCoins();
                }

                float avgPerBurst = (float)(runStats.CoinsEarned - beforeMiniBoss) / miniBossKills;
                float expectedPerBurst = config.miniBossCoinBurstCount * expectedPerKill;
                Assert.AreEqual(expectedPerBurst, avgPerBurst, 0.45f,
                    $"Mini-boss burst coins on Wave 25 should match expectation {expectedPerBurst:F2}");
            }
            finally
            {
                deathHandler.Dispose();
            }
        }

        [Test]
        public void MetaUpgrades_RebalancedEconomy_MeetsAllTargetMetrics()
        {
            var metaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            Assert.AreEqual(8, metaUpgrades.Length);

            int totalCoins = 0;
            for (int i = 0; i < metaUpgrades.Length; i++)
            {
                MetaUpgradeData upg = metaUpgrades[i];
                Assert.IsNotNull(upg);
                Assert.GreaterOrEqual(upg.costMultiplier, 1.15f, upg.id);
                for (int lvl = 0; lvl < upg.maxLevel; lvl++)
                {
                    totalCoins += upg.GetCostForLevel(lvl);
                }
            }

            Assert.Greater(totalCoins, 50000);
            Assert.Less(totalCoins, 200000);

            WaveConfig config = BalanceCurveSimulator.LoadDefaultWaveConfig();
            Assert.IsNotNull(config);

            EconomySimulationSummary summary = BalanceCurveSimulator.SimulateEconomy(config, metaUpgrades);

            Assert.AreEqual(totalCoins, summary.TotalMetaCoins);
            Assert.GreaterOrEqual(summary.FirstFiveRunsPurchases, 3, "First 5 runs should allow at least 3 meta purchases");
            Assert.LessOrEqual(summary.FirstFiveRunsPurchases, 6, "First 5 runs should remain paced around 3-5 purchases");
            Assert.GreaterOrEqual(summary.EstimatedHoursToHalfMeta, 5f);
            Assert.LessOrEqual(summary.EstimatedHoursToHalfMeta, 14f);
            Assert.GreaterOrEqual(summary.EstimatedHoursToFullMeta, 35f);
            Assert.LessOrEqual(summary.EstimatedHoursToFullMeta, 80f);
        }

        [Test]
        public void HeroExperience_RebalancedXpCurve_KeepsLateGameLevelUpsAtOrAbove15Seconds()
        {
            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            Assert.IsNotNull(heroPrefab);
            var heroExp = heroPrefab.GetComponent<HeroExperience>();
            Assert.IsNotNull(heroExp);

            Assert.That(heroExp.BaseXP, Is.InRange(5, 15));
            Assert.That(heroExp.XpExponent, Is.InRange(1.05f, 1.30f));

            WaveConfig config = BalanceCurveSimulator.LoadDefaultWaveConfig();
            Assert.IsNotNull(config);

            XpSimulationSummary baseSummary = BalanceCurveSimulator.SimulateXpCurve(config, 1.0f, heroExp.BaseXP, heroExp.XpExponent);
            Assert.GreaterOrEqual(baseSummary.LevelAtWave5, 4, "Wave 5 should feel active with >= 4 levels");

            float[] multipliers = { 1.0f, 1.8f, 2.5f, 3.0f };
            for (int i = 0; i < multipliers.Length; i++)
            {
                XpSimulationSummary s = BalanceCurveSimulator.SimulateXpCurve(config, multipliers[i], heroExp.BaseXP, heroExp.XpExponent);
                Assert.GreaterOrEqual(s.MinSecondsPerLevelUpAfter30, 15f, $"W30+ min LU interval too fast at {multipliers[i]}x XP");
                Assert.GreaterOrEqual(s.AvgSecondsPerLevelUpAfter30, 15f, $"W30+ avg LU interval too fast at {multipliers[i]}x XP");
            }
        }

        [Test]
        public void BuildDpsModel_AndBossScaling_ProgressMonotonicallyFromRun1ToDemonLord()
        {
            var golem = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/GolemBossData.asset");
            var necro = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/NecromancerBossData.asset");
            var lich = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/FrostLichBossData.asset");
            var demon = AssetDatabase.LoadAssetAtPath<BossData>("Assets/_ScriptableObjects/Bosses/DemonLordBossData.asset");

            Assert.IsNotNull(golem);
            Assert.IsNotNull(necro);
            Assert.IsNotNull(lich);
            Assert.IsNotNull(demon);

            Assert.Less(golem.hp, necro.hp);
            Assert.Less(necro.hp, lich.hp);
            Assert.Less(lich.hp, demon.hp);

            WaveConfig config = BalanceCurveSimulator.LoadDefaultWaveConfig();
            DpsSimulationSummary dps = BalanceCurveSimulator.SimulateBuildDps(config);

            Assert.IsNotNull(dps.Archetypes);
            Assert.AreEqual(5, dps.Archetypes.Length);

            Assert.Less(dps.GolemWave25Hp, dps.NecromancerWave50Hp);
            Assert.Less(dps.NecromancerWave50Hp, dps.FrostLichWave75Hp);
            Assert.Less(dps.FrostLichWave75Hp, dps.DemonLordWave100Hp);

            for (int i = 1; i < dps.Archetypes.Length; i++)
            {
                Assert.GreaterOrEqual(dps.Archetypes[i].WallWave, dps.Archetypes[i - 1].WallWave);
            }
            Assert.GreaterOrEqual(dps.Archetypes[dps.Archetypes.Length - 1].WallWave, dps.Archetypes[0].WallWave);
        }

        [Test]
        public void BalanceCurveSimulator_GeneratesAllExtendedReports()
        {
            WaveConfig config = BalanceCurveSimulator.LoadDefaultWaveConfig();
            Assert.IsNotNull(config);

            string waveReport = BalanceCurveSimulator.BuildSimulationReport(config);
            string econReport = BalanceCurveSimulator.BuildEconomyReport(config);
            string dpsReport = BalanceCurveSimulator.BuildDpsReport(config);
            string xpReport = BalanceCurveSimulator.BuildXpReport(config);
            string fullReport = BalanceCurveSimulator.BuildFullReport(config);

            Assert.IsTrue(waveReport.Contains("Coin x"));
            Assert.IsTrue(econReport.Contains("Total Meta Coins:"));
            Assert.IsTrue(dpsReport.Contains("Build DPS & Wall Wave Model"));
            Assert.IsTrue(xpReport.Contains("XP & LevelUp Frequency Model"));
            Assert.IsTrue(fullReport.Contains("Economy & Meta Progression Simulation"));
        }

        private static void FlushAllCoins()
        {
            foreach (CoinPickup coin in Object.FindObjectsByType<CoinPickup>(FindObjectsInactive.Exclude))
            {
                coin.Magnetize(1000f);
                coin.Tick(1f);
            }
        }
    }
}
