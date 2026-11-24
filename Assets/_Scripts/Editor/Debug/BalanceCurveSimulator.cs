using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Data.Meta;
using ProjectB.Enemies;
using ProjectB.LevelUp;

namespace ProjectB.EditorScripts
{
    public struct EconomySimulationSummary
    {
        public int TotalMetaCoins;
        public int TotalMetaLevels;
        public int InnerConstellationCoins;
        public int TotalConstellationShards;
        public int HalfLevelsCount;
        public int HalfLevelsCoinCost;
        public float FirstFiveRunsCoins;
        public int FirstFiveRunsPurchases;
        public float EstimatedHoursToHalfMeta;
        public float EstimatedHoursToFullMeta;
        public float EstimatedHoursToHalfMetaWithDaily;
        public float EstimatedHoursToFullMetaWithDaily;
        public float CoinsPerHourWave8;
        public float CoinsPerHourWave15;
        public float CoinsPerHourWave25;
        public float CoinsPerHourWave50;
        public float CoinsPerHourWave100;
        public float ShardsPerHourWave25;
    }

    public struct BuildDpsWallEntry
    {
        public string ArchetypeName;
        public float AoeDps;
        public float SingleTargetDps;
        public int WallWave;
        public float RequiredDpsAtWall;
        public bool ClearedWave100;
    }

    public struct DpsSimulationSummary
    {
        public BuildDpsWallEntry[] Archetypes;
        public int GolemWave25Hp;
        public int NecromancerWave50Hp;
        public int FrostLichWave75Hp;
        public int DemonLordWave100Hp;
    }

    public struct XpSimulationSummary
    {
        public float XpMultiplier;
        public int BaseXp;
        public float XpExponent;
        public int LevelAtWave5;
        public int LevelAtWave25;
        public int LevelAtWave100;
        public float AvgLevelUpsPerWaveAfter30;
        public float AvgSecondsPerLevelUpAfter30;
        public float MinSecondsPerLevelUpAfter30;
    }

    public class BalanceCurveSimulator : EditorWindow
    {
        public static readonly int[] DefaultSampleWaves = { 1, 5, 10, 25, 50, 75, 100 };
        public static readonly int[] ExtendedSampleWaves = { 1, 3, 5, 8, 10, 12, 15, 20, 25, 30, 40, 50, 75, 100 };

        private enum ReportTab
        {
            FullReport = 0,
            WavesAndHp = 1,
            EconomyAndMeta = 2,
            DpsAndWalls = 3,
            XpAndLevelUp = 4
        }

        private const float COIN_PICKUP_EFFICIENCY = 0.85f;
        private const float XP_PICKUP_EFFICIENCY = 0.90f;
        private const float MENU_OVERHEAD_MULTIPLIER = 1.25f;
        private const float SLAYER_ACHIEVEMENT_COINS = 200f;
        private const float DAILY_BONUS_COINS_PER_HOUR = 300f;
        private const float BOSS_TARGET_FIGHT_SECONDS = 40f;

        private static readonly string[] TabLabels =
        {
            "Full Report",
            "Waves & HP",
            "Economy & Meta",
            "DPS & Walls",
            "XP & LevelUp"
        };

        private WaveConfig waveConfig;
        private ReportTab activeTab = ReportTab.FullReport;
        private Vector2 scrollPosition;
        private string cachedReport = string.Empty;

        [MenuItem("ProjectB/Debug/Balance Curve Simulator")]
        public static void OpenWindow()
        {
            var window = GetWindow<BalanceCurveSimulator>("Balance Curve Simulator");
            window.minSize = new Vector2(760f, 560f);
            window.RefreshReport();
            window.Show();
        }

        [MenuItem("ProjectB/Debug/Log Balance Curve Report")]
        public static void LogReportToConsole()
        {
            var config = LoadDefaultWaveConfig();
            Debug.Log(BuildFullReport(config));
        }

        private void OnEnable()
        {
            if (waveConfig == null)
            {
                waveConfig = LoadDefaultWaveConfig();
            }
            RefreshReport();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            waveConfig = (WaveConfig)EditorGUILayout.ObjectField("Wave Config", waveConfig, typeof(WaveConfig), false);
            activeTab = (ReportTab)GUILayout.Toolbar((int)activeTab, TabLabels);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshReport();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(26f)))
            {
                RefreshReport();
            }
            if (GUILayout.Button("Log to Console", GUILayout.Height(26f)))
            {
                RefreshReport();
                Debug.Log(cachedReport);
            }
            if (GUILayout.Button("Copy Report", GUILayout.Height(26f)))
            {
                RefreshReport();
                EditorGUIUtility.systemCopyBuffer = cachedReport;
            }
            if (GUILayout.Button("Export Snapshot (.txt)", GUILayout.Height(26f)))
            {
                RefreshReport();
                ExportSnapshot();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(cachedReport, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RefreshReport()
        {
            cachedReport = activeTab switch
            {
                ReportTab.WavesAndHp => BuildSimulationReport(waveConfig),
                ReportTab.EconomyAndMeta => BuildEconomyReport(waveConfig),
                ReportTab.DpsAndWalls => BuildDpsReport(waveConfig),
                ReportTab.XpAndLevelUp => BuildXpReport(waveConfig),
                _ => BuildFullReport(waveConfig)
            };
        }

        private void ExportSnapshot()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Docs", "Balance");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "SimulatorSnapshot.txt");
            File.WriteAllText(path, BuildFullReport(waveConfig), Encoding.UTF8);
            Debug.Log($"[BalanceCurveSimulator] Exported snapshot to {Path.GetFullPath(path)}");
        }

        public static WaveConfig LoadDefaultWaveConfig()
        {
            return AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_ScriptableObjects/WaveConfig.asset");
        }

        public static void GetDefaultXpCurveParameters(out int baseXp, out float xpExponent)
        {
            baseXp = 9;
            xpExponent = 1.12f;

            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            if (heroPrefab != null && heroPrefab.TryGetComponent<HeroExperience>(out var exp))
            {
                baseXp = Mathf.Max(1, exp.BaseXP);
                xpExponent = exp.XpExponent > 0f ? exp.XpExponent : 1.12f;
            }
        }

        public static string BuildFullReport(WaveConfig config)
        {
            if (config == null)
            {
                return "WaveConfig is not assigned.";
            }

            var sb = new StringBuilder(6144);
            sb.AppendLine(BuildSimulationReport(config, ExtendedSampleWaves));
            sb.AppendLine();
            sb.AppendLine(BuildEconomyReport(config));
            sb.AppendLine();
            sb.AppendLine(BuildDpsReport(config));
            sb.AppendLine();
            sb.AppendLine(BuildXpReport(config));
            return sb.ToString();
        }

        public static string BuildSimulationReport(WaveConfig config, int[] waves = null)
        {
            if (config == null)
            {
                return "WaveConfig is not assigned.";
            }

            int[] sampleWaves = (waves != null && waves.Length > 0) ? waves : DefaultSampleWaves;
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== ProjectB Balance Curve Simulation ===");
            sb.AppendLine(
                $"BaseCount={config.baseEnemyCount}, Mult={config.enemiesPerWaveMultiplier:F2}, MaxWave={config.maxEnemiesPerWave}, " +
                $"HpLinear={config.hpLinearPerWave:F3}, HpQuad={config.hpQuadraticPerWave:F4}, " +
                $"DmgLinear={config.damageLinearPerWave:F3}, MaxDmgMult={config.maxDamageMultiplier:F2}, " +
                $"CoinLinear={config.coinLinearPerWave:F3}, MaxCoinMult={config.maxCoinMultiplier:F2}");
            sb.AppendLine();

            for (int i = 0; i < sampleWaves.Length; i++)
            {
                int wave = Mathf.Max(1, sampleWaves[i]);
                float hpMult = config.GetHpDifficultyMultiplier(wave);
                float dmgMult = config.GetDamageDifficultyMultiplier(wave);
                float coinMult = config.GetCoinMultiplier(wave);
                float effSpawnDelay = config.GetWaveSpawnDelay(wave, false);
                float elitePct = config.GetEliteChanceForWave(wave) * 100f;
                int enemyCount = Mathf.Min(
                    Mathf.RoundToInt(config.baseEnemyCount * Mathf.Pow(config.enemiesPerWaveMultiplier, wave - 1)),
                    config.maxEnemiesPerWave);

                bool isBoss = config.bosses != null && config.bosses.Count > 0 && WaveManager.IsBossWave(wave, config.bossInterval);
                bool isMiniBoss = WaveManager.IsMiniBossWave(wave, config.miniBossInterval, config.bossInterval);
                EnemyWaveEntry intro = config.GetIntroEntryForWave(wave);

                sb.Append($"Wave {wave,3} | HP x{hpMult:F2} | Dmg x{dmgMult:F2} | Coin x{coinMult:F2} | Count {enemyCount,2} | Delay {effSpawnDelay:F2}s | Elite {elitePct,2:F0}%");
                if (intro != null && intro.enemy != null)
                {
                    sb.Append($" | INTRO: {intro.enemy.name} ({Mathf.RoundToInt(intro.introSpawnShare * 100f)}%)");
                }
                if (isMiniBoss) sb.Append(" | MINI-BOSS");
                if (isBoss) sb.Append(" | BOSS");
                sb.AppendLine();

                EnemyData highestNonExploder = null;
                if (config.enemySchedule != null)
                {
                    for (int e = 0; e < config.enemySchedule.Count; e++)
                    {
                        EnemyWaveEntry entry = config.enemySchedule[e];
                        if (entry == null || !entry.IsEligibleForWave(wave)) continue;

                        EnemyData ed = entry.enemy;
                        int scaledHp = Mathf.Max(1, Mathf.RoundToInt(ed.hp * hpMult));
                        int scaledDmg = Mathf.Max(1, Mathf.RoundToInt(ed.contactDamage * dmgMult));
                        sb.AppendLine($"    - {ed.name,-18}: HP {scaledHp,6} | ContactDmg {scaledDmg,4}");

                        if (!(ed is ExploderEnemyData) &&
                            (highestNonExploder == null || ed.hp > highestNonExploder.hp))
                        {
                            highestNonExploder = ed;
                        }
                    }
                }

                if (wave >= config.minWaveForElites && highestNonExploder != null)
                {
                    int berserkerHp = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.hp * hpMult * config.eliteHpMultiplier));
                    int berserkerDmg = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.contactDamage * dmgMult * config.eliteDamageMultiplier));
                    int juggHp = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.hp * hpMult * config.juggernautHpMultiplier));
                    int juggDmg = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.contactDamage * dmgMult * config.juggernautDamageMultiplier));
                    sb.AppendLine($"    * Elite ({highestNonExploder.name}): Berserker HP {berserkerHp} / Dmg {berserkerDmg} | Juggernaut HP {juggHp} / Dmg {juggDmg}");
                }

                if (isMiniBoss && highestNonExploder != null)
                {
                    int mbHp = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.hp * hpMult * config.miniBossHpMultiplier));
                    int mbDmg = Mathf.Max(1, Mathf.RoundToInt(highestNonExploder.contactDamage * dmgMult * config.miniBossDamageMultiplier));
                    sb.AppendLine($"    * Mini-Boss ({highestNonExploder.name}): HP {mbHp} | Dmg {mbDmg}");
                }

                if (isBoss)
                {
                    int bossIdx = WaveManager.GetBossIndexForWave(wave, config.bossInterval, config.bosses.Count);
                    BossData boss = config.bosses[bossIdx];
                    if (boss != null)
                    {
                        int bossHp = Mathf.Max(1, Mathf.RoundToInt(boss.hp * hpMult));
                        int bossDmg = Mathf.Max(1, Mathf.RoundToInt(boss.contactDamage * dmgMult));
                        sb.AppendLine($"    # BOSS [{boss.bossDisplayName}]: HP {bossHp} | ContactDmg {bossDmg}");
                    }
                }
            }

            return sb.ToString();
        }

        public static EconomySimulationSummary SimulateEconomy(
            WaveConfig config,
            MetaUpgradeData[] metaUpgrades = null,
            TalentNodeData[] talents = null,
            DailyBonusRewardData dailyBonus = null,
            ComboConfig comboConfig = null)
        {
            if (config == null) config = LoadDefaultWaveConfig();
            if (metaUpgrades == null || metaUpgrades.Length == 0)
            {
                metaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            }
            if (talents == null || talents.Length == 0)
            {
                var tree = Resources.Load<TalentTreeData>("TalentTree");
                talents = tree != null && tree.nodes != null ? tree.nodes.ToArray() : Array.Empty<TalentNodeData>();
            }
            if (dailyBonus == null)
            {
                dailyBonus = Resources.Load<DailyBonusRewardData>("DailyBonusRewardData");
            }
            if (comboConfig == null)
            {
                comboConfig = Resources.Load<ComboConfig>(ComboConfig.DEFAULT_RESOURCE_PATH);
            }

            var allBuyCosts = new List<int>(128);
            int totalMetaCoins = 0;
            int totalMetaLevels = 0;

            if (metaUpgrades != null)
            {
                for (int i = 0; i < metaUpgrades.Length; i++)
                {
                    MetaUpgradeData upg = metaUpgrades[i];
                    if (upg == null || upg.maxLevel <= 0) continue;
                    for (int lvl = 0; lvl < upg.maxLevel; lvl++)
                    {
                        int cost = Mathf.Max(0, upg.GetCostForLevel(lvl));
                        allBuyCosts.Add(cost);
                        totalMetaCoins += cost;
                        totalMetaLevels++;
                    }
                }
            }

            allBuyCosts.Sort();
            int halfLevels = totalMetaLevels / 2;
            int halfCost = 0;
            for (int i = 0; i < halfLevels && i < allBuyCosts.Count; i++)
            {
                halfCost += allBuyCosts[i];
            }

            int innerCoins = 0;
            int totalShards = 0;
            if (talents != null)
            {
                for (int i = 0; i < talents.Length; i++)
                {
                    TalentNodeData node = talents[i];
                    if (node == null || node.IsOrigin) continue;
                    if (node.coinCost > 0) innerCoins += node.coinCost;
                    if (node.shardCost > 0) totalShards += node.shardCost;
                }
            }

            GetDefaultXpCurveParameters(out int baseXp, out float xpExp);

            float first5Coins =
                SimulateRunToWave(config, 5, 1f, 1f, true, 0f, baseXp, xpExp).CumulativeCoins +
                SimulateRunToWave(config, 6, 1f, 1f, true, 0f, baseXp, xpExp).CumulativeCoins +
                SimulateRunToWave(config, 7, 1f, 1f, true, 0f, baseXp, xpExp).CumulativeCoins +
                2f * SimulateRunToWave(config, 8, 1f, 1f, true, 0f, baseXp, xpExp).CumulativeCoins +
                SLAYER_ACHIEVEMENT_COINS;

            int first5Purchases = 0;
            float remainingBudget = first5Coins;
            for (int i = 0; i < allBuyCosts.Count; i++)
            {
                if (remainingBudget >= allBuyCosts[i])
                {
                    remainingBudget -= allBuyCosts[i];
                    first5Purchases++;
                }
                else
                {
                    break;
                }
            }

            RunAggregate w8 = SimulateRunToWave(config, 8, 1.0f, 1.00f, true, 0.00f, baseXp, xpExp);
            RunAggregate w15 = SimulateRunToWave(config, 15, 1.2f, 1.05f, true, 0.15f, baseXp, xpExp);
            RunAggregate w25 = SimulateRunToWave(config, 25, 1.4f, 1.15f, true, 0.30f, baseXp, xpExp);
            RunAggregate w50 = SimulateRunToWave(config, 50, 1.8f, 1.30f, true, 0.50f, baseXp, xpExp);
            RunAggregate w100 = SimulateRunToWave(config, 100, 2.2f, 1.45f, true, 0.65f, baseXp, xpExp);

            float cph8 = CoinsPerHour(w8);
            float cph15 = CoinsPerHour(w15);
            float cph25 = CoinsPerHour(w25);
            float cph50 = CoinsPerHour(w50);
            float cph100 = CoinsPerHour(w100);
            float sph25 = w25.TotalMinutes > 0f ? w25.CumulativeShards / (w25.TotalMinutes * MENU_OVERHEAD_MULTIPLIER / 60f) : 0f;

            float dailyPerHour = EstimateDailyCoinsPerHour(dailyBonus);
            int fullCoinTarget = totalMetaCoins + innerCoins;

            SimulateCareerHours(halfCost, fullCoinTarget, cph8, cph15, cph25, cph50, cph100, 0f, out float halfNoDaily, out float fullNoDaily);
            SimulateCareerHours(halfCost, fullCoinTarget, cph8, cph15, cph25, cph50, cph100, dailyPerHour, out float halfWithDaily, out float fullWithDaily);

            return new EconomySimulationSummary
            {
                TotalMetaCoins = totalMetaCoins,
                TotalMetaLevels = totalMetaLevels,
                InnerConstellationCoins = innerCoins,
                TotalConstellationShards = totalShards,
                HalfLevelsCount = halfLevels,
                HalfLevelsCoinCost = halfCost,
                FirstFiveRunsCoins = first5Coins,
                FirstFiveRunsPurchases = first5Purchases,
                EstimatedHoursToHalfMeta = halfNoDaily,
                EstimatedHoursToFullMeta = fullNoDaily,
                EstimatedHoursToHalfMetaWithDaily = halfWithDaily,
                EstimatedHoursToFullMetaWithDaily = fullWithDaily,
                CoinsPerHourWave8 = cph8,
                CoinsPerHourWave15 = cph15,
                CoinsPerHourWave25 = cph25,
                CoinsPerHourWave50 = cph50,
                CoinsPerHourWave100 = cph100,
                ShardsPerHourWave25 = sph25
            };
        }

        public static DpsSimulationSummary SimulateBuildDps(WaveConfig config)
        {
            if (config == null) config = LoadDefaultWaveConfig();
            GetDefaultXpCurveParameters(out int baseXp, out float xpExp);

            var rows = SimulateWavesDetailed(config, 100, 1f, 1f, false, 0f, baseXp, xpExp);

            // ASSUMPTION: Archetype AoE and Single-Target DPS values below are analytical estimates
            // (not measured from live combat simulation) and MUST be validated/calibrated via playtesting.
            var specs = new (string Name, float AoeDps, float SingleTargetDps)[]
            {
                ("Run 1 Fresh (3 active Lv 2, 0% meta)", 110f, 55f),
                ("Early Meta (3 active Lv 5, 20% meta)", 385f, 180f),
                ("Mid Meta (3-4 active Lv 10 Overdrive, 40% meta)", 950f, 420f),
                ("Late-Mid (6 active Lv 10 + Synergies, 65% meta)", 2400f, 1050f),
                ("Endgame (6 active Lv 20 Awakened + Synergies, 100% meta)", 7500f, 3500f)
            };

            var entries = new BuildDpsWallEntry[specs.Length];
            for (int a = 0; a < specs.Length; a++)
            {
                int wallWave = 100;
                bool cleared100 = true;
                float reqAtWall = 0f;

                for (int i = 0; i < rows.Count; i++)
                {
                    WaveSimRow row = rows[i];
                    float required = row.IsBoss
                        ? row.TotalHp / BOSS_TARGET_FIGHT_SECONDS
                        : row.RequiredAoeDps;
                    float buildDps = row.IsBoss ? specs[a].SingleTargetDps : specs[a].AoeDps;

                    if (buildDps < required)
                    {
                        wallWave = row.Wave;
                        reqAtWall = required;
                        cleared100 = false;
                        break;
                    }
                    reqAtWall = required;
                }

                entries[a] = new BuildDpsWallEntry
                {
                    ArchetypeName = specs[a].Name,
                    AoeDps = specs[a].AoeDps,
                    SingleTargetDps = specs[a].SingleTargetDps,
                    WallWave = wallWave,
                    RequiredDpsAtWall = reqAtWall,
                    ClearedWave100 = cleared100
                };
            }

            return new DpsSimulationSummary
            {
                Archetypes = entries,
                GolemWave25Hp = GetBossScaledHp(config, 25),
                NecromancerWave50Hp = GetBossScaledHp(config, 50),
                FrostLichWave75Hp = GetBossScaledHp(config, 75),
                DemonLordWave100Hp = GetBossScaledHp(config, 100)
            };
        }

        public static XpSimulationSummary SimulateXpCurve(
            WaveConfig config,
            float xpMultiplier = 1f,
            int baseXp = 0,
            float xpExponent = 0f)
        {
            if (config == null) config = LoadDefaultWaveConfig();
            if (baseXp <= 0 || xpExponent <= 0f)
            {
                GetDefaultXpCurveParameters(out int defaultBase, out float defaultExp);
                if (baseXp <= 0) baseXp = defaultBase;
                if (xpExponent <= 0f) xpExponent = defaultExp;
            }

            var rows = SimulateWavesDetailed(config, 100, xpMultiplier, 1f, true, 0f, baseXp, xpExponent);
            int lvl5 = rows.Count >= 5 ? rows[4].HeroLevel : 1;
            int lvl25 = rows.Count >= 25 ? rows[24].HeroLevel : 1;
            int lvl100 = rows.Count >= 100 ? rows[99].HeroLevel : 1;

            int post30Waves = 0;
            int post30LevelUps = 0;
            float minSecPerLu = float.MaxValue;

            for (int i = 0; i < rows.Count; i++)
            {
                WaveSimRow row = rows[i];
                if (row.Wave < 30 || row.IsBoss) continue;
                post30Waves++;
                post30LevelUps += row.LevelUps;
                if (row.LevelUps > 0)
                {
                    float secPerLu = row.DurationSeconds / row.LevelUps;
                    if (secPerLu < minSecPerLu) minSecPerLu = secPerLu;
                }
            }

            float avgLuPerWave = post30Waves > 0 ? (float)post30LevelUps / post30Waves : 0f;
            float avgSecPerLu = avgLuPerWave > 0f ? 32f / avgLuPerWave : 999f;
            if (minSecPerLu == float.MaxValue) minSecPerLu = 999f;

            return new XpSimulationSummary
            {
                XpMultiplier = xpMultiplier,
                BaseXp = baseXp,
                XpExponent = xpExponent,
                LevelAtWave5 = lvl5,
                LevelAtWave25 = lvl25,
                LevelAtWave100 = lvl100,
                AvgLevelUpsPerWaveAfter30 = avgLuPerWave,
                AvgSecondsPerLevelUpAfter30 = avgSecPerLu,
                MinSecondsPerLevelUpAfter30 = minSecPerLu
            };
        }

        public static string BuildEconomyReport(WaveConfig config)
        {
            if (config == null) return "WaveConfig is not assigned.";

            var metaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            Array.Sort(metaUpgrades, (a, b) => string.CompareOrdinal(a != null ? a.id : "", b != null ? b.id : ""));
            EconomySimulationSummary summary = SimulateEconomy(config, metaUpgrades);

            var sb = new StringBuilder(2048);
            sb.AppendLine("=== Economy & Meta Progression Simulation ===");
            sb.AppendLine("Meta Upgrades:");
            for (int i = 0; i < metaUpgrades.Length; i++)
            {
                MetaUpgradeData upg = metaUpgrades[i];
                if (upg == null) continue;
                int sum = 0;
                for (int lvl = 0; lvl < upg.maxLevel; lvl++) sum += upg.GetCostForLevel(lvl);
                int first = upg.GetCostForLevel(0);
                int last = upg.GetCostForLevel(Mathf.Max(0, upg.maxLevel - 1));
                sb.AppendLine($"  - {upg.displayName,-16} ({upg.maxLevel,2} lvls): base={upg.baseCost,4} x{upg.costMultiplier:F2} | L0={first,4} .. Lmax={last,5} | Total={sum,6}");
            }

            sb.AppendLine();
            sb.AppendLine($"Total Meta Coins: {summary.TotalMetaCoins} ({summary.TotalMetaLevels} levels) + Inner Constellation {summary.InnerConstellationCoins} = {summary.TotalMetaCoins + summary.InnerConstellationCoins}");
            sb.AppendLine($"Constellation Total Shards: {summary.TotalConstellationShards}");
            sb.AppendLine($"50% Levels Threshold ({summary.HalfLevelsCount} cheapest buys): {summary.HalfLevelsCoinCost} coins");
            sb.AppendLine($"First 5 Runs (W5,6,7,8,8 + Slayer 200): {summary.FirstFiveRunsCoins:F0} coins -> {summary.FirstFiveRunsPurchases} meta purchases (target: 3-5)");
            sb.AppendLine();
            sb.AppendLine("Stage Income Rates (85% pickup eff, 1.25x menu/LU overhead):");
            sb.AppendLine($"  - W8   (Run 2-5):   {summary.CoinsPerHourWave8,6:F0} coins/hr");
            sb.AppendLine($"  - W15  (Early):     {summary.CoinsPerHourWave15,6:F0} coins/hr");
            sb.AppendLine($"  - W25  (Golem):     {summary.CoinsPerHourWave25,6:F0} coins/hr | {summary.ShardsPerHourWave25:F1} shards/hr");
            sb.AppendLine($"  - W50  (Mid-Late):  {summary.CoinsPerHourWave50,6:F0} coins/hr");
            sb.AppendLine($"  - W100 (Endgame):   {summary.CoinsPerHourWave100,6:F0} coins/hr");
            sb.AppendLine();
            sb.AppendLine($"Estimated Hours (Pure F2P, 0 daily):     50% = {summary.EstimatedHoursToHalfMeta:F1}h | 100% = {summary.EstimatedHoursToFullMeta:F1}h");
            sb.AppendLine($"Estimated Hours (F2P + ~300/h daily):    50% = {summary.EstimatedHoursToHalfMetaWithDaily:F1}h | 100% = {summary.EstimatedHoursToFullMetaWithDaily:F1}h");
            return sb.ToString();
        }

        public static string BuildDpsReport(WaveConfig config)
        {
            if (config == null) return "WaveConfig is not assigned.";

            DpsSimulationSummary summary = SimulateBuildDps(config);
            var sb = new StringBuilder(1024);
            sb.AppendLine("=== Build DPS & Wall Wave Model ===");
            sb.AppendLine("NOTE: Archetype DPS figures are analytical assumptions to be verified in playtests.");
            sb.AppendLine($"Boss Scaled HP: W25 Golem={summary.GolemWave25Hp} | W50 Necro={summary.NecromancerWave50Hp} | W75 Lich={summary.FrostLichWave75Hp} | W100 DemonLord={summary.DemonLordWave100Hp}");
            sb.AppendLine();
            if (summary.Archetypes != null)
            {
                for (int i = 0; i < summary.Archetypes.Length; i++)
                {
                    BuildDpsWallEntry e = summary.Archetypes[i];
                    string status = e.ClearedWave100 ? "CLEARS W100" : $"Wall @ W{e.WallWave}";
                    sb.AppendLine($"  - {e.ArchetypeName,-56} | AoE {e.AoeDps,4:F0} / ST {e.SingleTargetDps,4:F0} | {status} (ReqDPS {e.RequiredDpsAtWall:F0})");
                }
            }
            return sb.ToString();
        }

        public static string BuildXpReport(WaveConfig config)
        {
            if (config == null) return "WaveConfig is not assigned.";

            GetDefaultXpCurveParameters(out int baseXp, out float xpExp);
            var sb = new StringBuilder(1024);
            sb.AppendLine($"=== XP & LevelUp Frequency Model (Formula: {baseXp} * level^{xpExp:F2}) ===");

            float[] mults = { 1.0f, 1.8f, 2.5f, 3.0f };
            for (int i = 0; i < mults.Length; i++)
            {
                XpSimulationSummary s = SimulateXpCurve(config, mults[i], baseXp, xpExp);
                sb.AppendLine(
                    $"  - XP x{s.XpMultiplier:F1}: W5 Lv={s.LevelAtWave5,2} | W25 Lv={s.LevelAtWave25,2} | W100 Lv={s.LevelAtWave100,2} | " +
                    $"W30+ avg {s.AvgLevelUpsPerWaveAfter30:F2} LU/wave (~{s.AvgSecondsPerLevelUpAfter30:F1}s/LU, min={s.MinSecondsPerLevelUpAfter30:F1}s)");
            }
            return sb.ToString();
        }

        private struct WaveSimRow
        {
            public int Wave;
            public bool IsBoss;
            public float TotalHp;
            public float RequiredAoeDps;
            public float DurationSeconds;
            public int HeroLevel;
            public int LevelUps;
            public float CumulativeCoins;
            public float CumulativeShards;
        }

        private struct RunAggregate
        {
            public float CumulativeCoins;
            public float CumulativeShards;
            public float TotalMinutes;
        }

        private static int GetBossScaledHp(WaveConfig config, int wave)
        {
            if (config == null || config.bosses == null || config.bosses.Count == 0) return 0;
            int idx = WaveManager.GetBossIndexForWave(wave, config.bossInterval, config.bosses.Count);
            BossData boss = config.bosses[idx];
            if (boss == null) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(boss.hp * config.GetHpDifficultyMultiplier(wave)));
        }

        private static float CoinsPerHour(RunAggregate run)
        {
            float realMinutes = run.TotalMinutes * MENU_OVERHEAD_MULTIPLIER;
            return realMinutes > 0f ? run.CumulativeCoins / (realMinutes / 60f) : 0f;
        }

        private static float EstimateDailyCoinsPerHour(DailyBonusRewardData daily)
        {
            if (daily == null || daily.rewards == null || daily.rewards.Length == 0) return DAILY_BONUS_COINS_PER_HOUR;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < daily.rewards.Length; i++)
            {
                if (daily.rewards[i].coinsAmount > 0)
                {
                    sum += daily.rewards[i].coinsAmount;
                    count++;
                }
            }
            if (count == 0) return DAILY_BONUS_COINS_PER_HOUR;
            float avgDay = sum / count;
            // Assume ~1.33 hours played per day -> ~0.75 daily bonus claims per play-hour
            return avgDay * 0.75f;
        }

        private static void SimulateCareerHours(
            float halfTarget,
            float fullTarget,
            float cph8,
            float cph15,
            float cph25,
            float cph50,
            float cph100,
            float dailyPerHour,
            out float hoursToHalf,
            out float hoursToFull)
        {
            float coins = 0f;
            hoursToHalf = -1f;
            hoursToFull = -1f;

            for (int hr = 1; hr <= 200; hr++)
            {
                float cph = hr <= 1 ? cph8
                    : hr <= 5 ? cph15
                    : hr <= 15 ? cph25
                    : hr <= 40 ? cph50
                    : 0.5f * cph50 + 0.5f * cph100;

                float prevCoins = coins;
                coins += cph + dailyPerHour;
                if (hr == 1) coins += SLAYER_ACHIEVEMENT_COINS;
                if (hr == 8) coins += 400f;
                if (hr == 25) coins += 800f;

                float gained = Mathf.Max(1f, coins - prevCoins);
                if (hoursToHalf < 0f && coins >= halfTarget)
                {
                    float frac = Mathf.Clamp01((halfTarget - prevCoins) / gained);
                    hoursToHalf = (hr - 1) + frac;
                }
                if (hoursToFull < 0f && coins >= fullTarget)
                {
                    float frac = Mathf.Clamp01((fullTarget - prevCoins) / gained);
                    hoursToFull = (hr - 1) + frac;
                    break;
                }
            }

            if (hoursToHalf < 0f) hoursToHalf = 200f;
            if (hoursToFull < 0f) hoursToFull = 200f;
        }

        private static RunAggregate SimulateRunToWave(
            WaveConfig config,
            int maxWave,
            float xpMult,
            float coinBonusMult,
            bool includeEvents,
            float shrineClearRate,
            int baseXp,
            float xpExponent)
        {
            var rows = SimulateWavesDetailed(config, maxWave, xpMult, coinBonusMult, includeEvents, shrineClearRate, baseXp, xpExponent);
            if (rows.Count == 0) return default;
            float totalSec = 0f;
            for (int i = 0; i < rows.Count; i++) totalSec += rows[i].DurationSeconds;
            WaveSimRow last = rows[rows.Count - 1];
            return new RunAggregate
            {
                CumulativeCoins = last.CumulativeCoins,
                CumulativeShards = last.CumulativeShards,
                TotalMinutes = totalSec / 60f
            };
        }

        private static List<WaveSimRow> SimulateWavesDetailed(
            WaveConfig config,
            int maxWave,
            float xpMult,
            float coinBonusMult,
            bool includeEvents,
            float shrineClearRate,
            int baseXp,
            float xpExponent)
        {
            var rows = new List<WaveSimRow>(maxWave);
            if (config == null || maxWave <= 0) return rows;

            float cumCoins = 0f;
            float cumShards = 0f;
            int level = 1;
            float xpBuffer = 0f;

            float avgEliteHpMult = (config.eliteHpMultiplier + config.juggernautHpMultiplier + config.hasteAuraHpMultiplier) / 3f;
            float eventInterval = Mathf.Max(1f, (config.minWavesBetweenEvents + config.maxWavesBetweenEvents) * 0.5f);

            for (int wave = 1; wave <= maxWave; wave++)
            {
                bool hasBosses = config.bosses != null && config.bosses.Count > 0;
                bool isBoss = hasBosses && WaveManager.IsBossWave(wave, config.bossInterval);
                bool isMiniBoss = WaveManager.IsMiniBossWave(wave, config.miniBossInterval, config.bossInterval);

                float hpMult = config.GetHpDifficultyMultiplier(wave);
                float coinMult = config.GetCoinMultiplier(wave);

                float waveHp;
                float waveXp;
                float waveCoins;
                float waveDuration;

                if (isBoss)
                {
                    int bossIdx = WaveManager.GetBossIndexForWave(wave, config.bossInterval, config.bosses.Count);
                    BossData boss = config.bosses[bossIdx];
                    float bossBaseHp = boss != null ? boss.hp : 1000f;
                    int bossXp = boss != null ? boss.xpDrop : 6;
                    int bossCoin = boss != null ? boss.coinDrop : 3;

                    waveHp = bossBaseHp * hpMult;
                    waveXp = bossXp * Mathf.Max(1, config.bossXpBurstCount);
                    // Matches probabilistic rounding in EnemyDeathHandler: E[coins] = bossCoin * coinMult
                    waveCoins = Mathf.Max(1, config.bossCoinBurstCount) * (bossCoin * coinMult);
                    cumShards += config.bossShardReward;
                    waveDuration = 25f;
                }
                else
                {
                    int count = Mathf.Min(
                        Mathf.RoundToInt(config.baseEnemyCount * Mathf.Pow(config.enemiesPerWaveMultiplier, wave - 1)),
                        config.maxEnemiesPerWave);

                    float totalWeight = 0f;
                    float weightedHp = 0f;
                    float weightedXp = 0f;
                    float weightedCoinChanceValue = 0f;
                    float weightedCoinDrop = 0f;

                    if (config.enemySchedule != null)
                    {
                        for (int i = 0; i < config.enemySchedule.Count; i++)
                        {
                            EnemyWaveEntry entry = config.enemySchedule[i];
                            if (entry == null || !entry.IsEligibleForWave(wave)) continue;
                            float w = entry.spawnWeight;
                            totalWeight += w;
                            weightedHp += w * entry.enemy.hp;
                            weightedXp += w * entry.enemy.xpDrop;
                            // Probabilistic rounding expectation: E[drop] = coinDrop * coinMult
                            float scaledDrop = entry.enemy.coinDrop > 0 ? entry.enemy.coinDrop * coinMult : 0f;
                            weightedCoinChanceValue += w * entry.enemy.coinDropChance * scaledDrop;
                            weightedCoinDrop += w * scaledDrop;
                        }
                    }

                    float avgHp = totalWeight > 0f ? weightedHp / totalWeight : 20f;
                    float avgXp = totalWeight > 0f ? weightedXp / totalWeight : 2f;
                    float avgCoinExpected = totalWeight > 0f ? weightedCoinChanceValue / totalWeight : 0.25f;
                    float avgCoinDrop = totalWeight > 0f ? weightedCoinDrop / totalWeight : 1f;

                    float eliteChance = config.GetEliteChanceForWave(wave);
                    float effectiveHpMult = hpMult * (1f - eliteChance + eliteChance * avgEliteHpMult);

                    waveHp = count * avgHp * effectiveHpMult;
                    waveXp = count * avgXp;
                    waveCoins = count * avgCoinExpected;

                    if (isMiniBoss)
                    {
                        waveHp += avgHp * hpMult * config.miniBossHpMultiplier;
                        waveXp += config.miniBossXpBurstCount * avgXp;
                        waveCoins += config.miniBossCoinBurstCount * avgCoinDrop;
                        cumShards += config.miniBossShardReward;
                    }

                    float effSpawnDelay = config.GetWaveSpawnDelay(wave, false);
                    float overlapFactor = config.IsWaveSaturated(wave, false) && config.enableWaveOverlap
                        ? 1f - Mathf.Clamp01(config.waveOverlapThreshold) * 0.5f
                        : 1f;
                    float activeCombatTime = Mathf.Min(config.maxWaveDuration, Mathf.Max(12f, (count * effSpawnDelay + 8f) * overlapFactor));
                    waveDuration = activeCombatTime + config.waveDelay;

                    if (includeEvents && config.waveEventsEnabled && wave >= config.firstWaveEventWave && !isMiniBoss)
                    {
                        float swarmAvgCount = (config.swarmRushMinCount + config.swarmRushMaxCount) * 0.5f;
                        float swarmCoins = swarmAvgCount * 0.22f * coinMult;
                        float goblinCoins = config.goldGoblinData != null
                            ? (config.goldGoblinData.deathCoinMin + config.goldGoblinData.deathCoinMax) * 0.4f
                            : 45f;
                        float bloodMoonExtraCoins = waveCoins * Mathf.Max(0f, config.bloodMoonCoinMultiplier - 1f);
                        float ambushCoins = config.eliteAmbushCoinReward;

                        float avgEventCoins = (swarmCoins + goblinCoins + bloodMoonExtraCoins + ambushCoins) * 0.25f;
                        float avgEventXp = (swarmAvgCount * 2f + 8f + waveXp * Mathf.Max(0f, config.bloodMoonXpMultiplier - 1f) + 12f) * 0.25f;

                        waveCoins += avgEventCoins / eventInterval;
                        waveXp += avgEventXp / eventInterval;
                    }

                    if (shrineClearRate > 0f && wave % 12 == 0)
                    {
                        waveCoins += 100f * shrineClearRate;
                        cumShards += config.shrineShardReward * shrineClearRate;
                    }
                }

                float finalWaveCoins = waveCoins * coinBonusMult * COIN_PICKUP_EFFICIENCY;
                float finalWaveXp = waveXp * xpMult * XP_PICKUP_EFFICIENCY;

                cumCoins += finalWaveCoins;
                xpBuffer += finalWaveXp;

                int levelUps = 0;
                while (xpBuffer >= HeroExperience.CalculateXPForLevel(level, baseXp, xpExponent))
                {
                    xpBuffer -= HeroExperience.CalculateXPForLevel(level, baseXp, xpExponent);
                    level++;
                    levelUps++;
                }

                float combatSec = isBoss ? waveDuration : Mathf.Max(1f, waveDuration - config.waveDelay);
                rows.Add(new WaveSimRow
                {
                    Wave = wave,
                    IsBoss = isBoss,
                    TotalHp = waveHp,
                    RequiredAoeDps = waveHp / combatSec,
                    DurationSeconds = waveDuration,
                    HeroLevel = level,
                    LevelUps = levelUps,
                    CumulativeCoins = cumCoins,
                    CumulativeShards = cumShards
                });
            }

            return rows;
        }
    }
}
