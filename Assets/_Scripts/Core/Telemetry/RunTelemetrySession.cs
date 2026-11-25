#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ProjectB.Core.Telemetry
{
    public struct CompletedRunSummary
    {
        public string RunId;
        public string HeroId;
        public string Strategy;
        public string MetaProfile;
        public int Seed;
        public int DeathWave;
        public string DeathCause;
        public float RunTimeSeconds;
        public int FinalLevel;
        public int TotalKills;
        public int TotalCoins;
        public int TotalShards;
        public int BestCombo;
        public double AvgLevelUpIntervalW1_10;
        public double AvgLevelUpIntervalW10_30;
        public double AvgLevelUpIntervalW30Plus;
        public int LevelUpCountW1_10;
        public int LevelUpCountW10_30;
        public int LevelUpCountW30Plus;
        public KeyValuePair<string, float>[] TopDamageShares;
    }

    public struct CardOfferStat
    {
        public int OfferedFreeCount;
        public int PickedCount;
    }

    public static class RunTelemetrySession
    {
        private static readonly List<CompletedRunSummary> s_completedRuns = new List<CompletedRunSummary>(64);
        private static readonly Dictionary<string, CardOfferStat> s_cardStats = new Dictionary<string, CardOfferStat>(StringComparer.Ordinal);
        private static int s_runCounter;

        public static bool IsEnabled { get; private set; }
        public static bool IsBatchSession { get; private set; }
        public static string SessionDirectory { get; private set; }
        public static string ActiveRunIdOverride { get; set; }
        public static string ActiveStrategyLabel { get; set; } = "Manual";
        public static string ActiveMetaProfileLabel { get; set; } = "PlayerSave";
        public static int? ActiveSeedOverride { get; set; }

        public static IReadOnlyList<CompletedRunSummary> CompletedRuns => s_completedRuns;
        public static IReadOnlyDictionary<string, CardOfferStat> CardStats => s_cardStats;

        public static string WavesCsvPath => string.IsNullOrEmpty(SessionDirectory) ? null : Path.Combine(SessionDirectory, "waves.csv");
        public static string RunsCsvPath => string.IsNullOrEmpty(SessionDirectory) ? null : Path.Combine(SessionDirectory, "runs.csv");
        public static string CardsCsvPath => string.IsNullOrEmpty(SessionDirectory) ? null : Path.Combine(SessionDirectory, "cards.csv");

        public static void EnableManualSession()
        {
            if (IsEnabled && !IsBatchSession && !string.IsNullOrEmpty(SessionDirectory))
            {
                return;
            }

            IsEnabled = true;
            IsBatchSession = false;
            ActiveRunIdOverride = null;
            ActiveStrategyLabel = "Manual";
            ActiveMetaProfileLabel = "PlayerSave";
            ActiveSeedOverride = null;
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            SessionDirectory = Path.Combine(Application.persistentDataPath, "BalanceLogs", stamp);
            Directory.CreateDirectory(SessionDirectory);
        }

        public static void Disable()
        {
            IsEnabled = false;
            IsBatchSession = false;
            ActiveRunIdOverride = null;
            ActiveStrategyLabel = "Manual";
            ActiveMetaProfileLabel = "PlayerSave";
            ActiveSeedOverride = null;
        }

        public static string BeginBatchSession(string customDirectory = null)
        {
            s_completedRuns.Clear();
            s_cardStats.Clear();
            s_runCounter = 0;

            IsEnabled = true;
            IsBatchSession = true;
            ActiveRunIdOverride = null;
            ActiveSeedOverride = null;

            if (!string.IsNullOrEmpty(customDirectory))
            {
                SessionDirectory = Path.GetFullPath(customDirectory);
            }
            else
            {
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
#if UNITY_EDITOR
                SessionDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BalanceReports", stamp));
#else
                SessionDirectory = Path.Combine(Application.persistentDataPath, "BalanceLogs", stamp);
#endif
            }

            Directory.CreateDirectory(SessionDirectory);
            return SessionDirectory;
        }

        public static void EndBatchSession()
        {
            IsEnabled = false;
            IsBatchSession = false;
            ActiveRunIdOverride = null;
            ActiveStrategyLabel = "Manual";
            ActiveMetaProfileLabel = "PlayerSave";
            ActiveSeedOverride = null;
        }

        public static string AllocateRunId(string heroId)
        {
            if (!string.IsNullOrEmpty(ActiveRunIdOverride))
            {
                return ActiveRunIdOverride;
            }

            s_runCounter++;
            string safeHero = string.IsNullOrEmpty(heroId) ? "hero" : heroId;
            return $"{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}_{safeHero}_{s_runCounter:D4}";
        }

        public static void RecordFreeCardOffered(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            s_cardStats.TryGetValue(cardId, out var stat);
            stat.OfferedFreeCount++;
            s_cardStats[cardId] = stat;
        }

        public static void RecordCardPicked(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            s_cardStats.TryGetValue(cardId, out var stat);
            stat.PickedCount++;
            s_cardStats[cardId] = stat;
        }

        public static void RecordCompletedRun(CompletedRunSummary summary)
        {
            s_completedRuns.Add(summary);
        }

        public static void ClearStats()
        {
            s_completedRuns.Clear();
            s_cardStats.Clear();
        }
    }
}
#endif
