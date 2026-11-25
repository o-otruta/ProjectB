#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectB.Core.Telemetry;
using UnityEngine;

namespace ProjectB.Core.BalanceBot
{
    public static class BalanceBotEditorBridge
    {
        public static Func<string> SimulatorComparisonProvider;
    }

    public static class BalanceBotReportGenerator
    {
        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public readonly string Hero;
            public readonly string Strategy;
            public readonly string Meta;

            public GroupKey(string hero, string strategy, string meta)
            {
                Hero = hero ?? string.Empty;
                Strategy = strategy ?? string.Empty;
                Meta = meta ?? string.Empty;
            }

            public bool Equals(GroupKey other)
            {
                return string.Equals(Hero, other.Hero, StringComparison.Ordinal) &&
                       string.Equals(Strategy, other.Strategy, StringComparison.Ordinal) &&
                       string.Equals(Meta, other.Meta, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Hero.GetHashCode();
                    hash = (hash * 397) ^ Strategy.GetHashCode();
                    hash = (hash * 397) ^ Meta.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class AbilityAgg
        {
            public string Id;
            public double SumShare;
            public int RunAppearances;
            public int Top3Count;
        }

        public static string WriteSummaryReport(
            string outputDirectory,
            IReadOnlyList<CompletedRunSummary> runs,
            IReadOnlyDictionary<string, CardOfferStat> cardStats,
            float timeScale = 1f,
            string simulationLabel = null)
        {
            if (string.IsNullOrEmpty(outputDirectory))
            {
                return null;
            }

            Directory.CreateDirectory(outputDirectory);
            string markdown = BuildSummaryMarkdown(runs, cardStats, timeScale, simulationLabel);
            string summaryPath = Path.Combine(outputDirectory, "summary.md");
            File.WriteAllText(summaryPath, markdown, CsvUtility.Utf8NoBom);
            return summaryPath;
        }

        public static string BuildSummaryMarkdown(
            IReadOnlyList<CompletedRunSummary> runs,
            IReadOnlyDictionary<string, CardOfferStat> cardStats,
            float timeScale = 1f,
            string simulationLabel = null)
        {
            var sb = new StringBuilder(8192);
            sb.AppendLine("# BalanceBot Batch Summary");
            sb.AppendLine();
            sb.AppendLine($"- **Generated (UTC):** {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"- **Total runs:** {(runs != null ? runs.Count : 0)}");
            if (!string.IsNullOrEmpty(simulationLabel))
            {
                sb.AppendLine($"- **Simulation:** {simulationLabel}");
            }
            else
            {
                sb.AppendLine($"- **TimeScale:** {timeScale.ToString("F1", CultureInfo.InvariantCulture)}x");
            }
            sb.AppendLine();

            if (runs == null || runs.Count == 0)
            {
                sb.AppendLine("_No completed runs recorded._");
                return sb.ToString();
            }

            AppendHeroStrategyMetaSection(sb, runs);
            AppendTopAbilitiesSection(sb, runs);
            AppendCardPickRateSection(sb, cardStats);
            AppendLevelUpIntervalSection(sb, runs);
            AppendCoinsPerHourSection(sb, runs);
            AppendSimulatorComparisonSection(sb);

            return sb.ToString();
        }

        private static void AppendHeroStrategyMetaSection(StringBuilder sb, IReadOnlyList<CompletedRunSummary> runs)
        {
            sb.AppendLine("## 1. Hero x Strategy x Meta");
            sb.AppendLine();
            sb.AppendLine("Median/P25/P75 count only runs that ended in death; runs stopped by the time/watchdog limit are listed as Stalled (with their wave).");
            sb.AppendLine();
            sb.AppendLine("| Hero | Strategy | Meta | Runs | Median Wave | P25 Wave | P75 Wave | Median Time (s) | Avg Kills | Avg Coins | Stalled |");
            sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|");

            var groups = new Dictionary<GroupKey, List<CompletedRunSummary>>();
            for (int i = 0; i < runs.Count; i++)
            {
                CompletedRunSummary r = runs[i];
                var key = new GroupKey(r.HeroId, r.Strategy, r.MetaProfile);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<CompletedRunSummary>();
                    groups[key] = list;
                }
                list.Add(r);
            }

            var keys = new List<GroupKey>(groups.Keys);
            keys.Sort((a, b) =>
            {
                int m = string.CompareOrdinal(a.Meta, b.Meta);
                if (m != 0) return m;
                int s = string.CompareOrdinal(a.Strategy, b.Strategy);
                if (s != 0) return s;
                return string.CompareOrdinal(a.Hero, b.Hero);
            });

            var wavesBuf = new List<double>();
            var timesBuf = new List<double>();

            for (int k = 0; k < keys.Count; k++)
            {
                GroupKey key = keys[k];
                List<CompletedRunSummary> list = groups[key];
                wavesBuf.Clear();
                timesBuf.Clear();

                double sumKills = 0;
                double sumCoins = 0;
                var stalled = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    if (IsStalledRun(list[i]))
                    {
                        if (stalled.Length > 0) stalled.Append(", ");
                        stalled.Append('W').Append(list[i].DeathWave.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        wavesBuf.Add(list[i].DeathWave);
                        timesBuf.Add(list[i].RunTimeSeconds);
                    }
                    sumKills += list[i].TotalKills;
                    sumCoins += list[i].TotalCoins;
                }

                double p25 = Percentile(wavesBuf, 0.25);
                double p50 = Percentile(wavesBuf, 0.50);
                double p75 = Percentile(wavesBuf, 0.75);
                double medTime = Percentile(timesBuf, 0.50);
                double avgKills = sumKills / list.Count;
                double avgCoins = sumCoins / list.Count;

                sb.AppendLine(
                    $"| {key.Hero} | {key.Strategy} | {key.Meta} | {list.Count} | " +
                    $"{p50.ToString("F1", CultureInfo.InvariantCulture)} | " +
                    $"{p25.ToString("F1", CultureInfo.InvariantCulture)} | " +
                    $"{p75.ToString("F1", CultureInfo.InvariantCulture)} | " +
                    $"{medTime.ToString("F1", CultureInfo.InvariantCulture)} | " +
                    $"{avgKills.ToString("F1", CultureInfo.InvariantCulture)} | " +
                    $"{avgCoins.ToString("F1", CultureInfo.InvariantCulture)} | " +
                    $"{(stalled.Length > 0 ? stalled.ToString() : "-")} |");
            }
            sb.AppendLine();
        }

        /// <summary>Забег остановлен лимитом времени/сторожем, а не смертью — его волна не «волна смерти».</summary>
        public static bool IsStalledRun(CompletedRunSummary run)
        {
            string cause = run.DeathCause;
            return cause == "TimeLimitReached" || cause == "WatchdogTimeout";
        }

        private static void AppendTopAbilitiesSection(StringBuilder sb, IReadOnlyList<CompletedRunSummary> runs)
        {
            sb.AppendLine("## 2. Top Damage Sources (Share & Top-3 Frequency)");
            sb.AppendLine();
            sb.AppendLine("| Source ID | Avg Damage Share (%) | Top-3 Frequency (%) | Top-3 Count | Runs With Damage |");
            sb.AppendLine("|---|---:|---:|---:|---:|");

            var aggs = new Dictionary<string, AbilityAgg>(StringComparer.Ordinal);
            for (int i = 0; i < runs.Count; i++)
            {
                var shares = runs[i].TopDamageShares;
                if (shares == null) continue;

                for (int j = 0; j < shares.Length; j++)
                {
                    string id = shares[j].Key;
                    if (string.IsNullOrEmpty(id)) continue;

                    if (!aggs.TryGetValue(id, out var agg))
                    {
                        agg = new AbilityAgg { Id = id };
                        aggs[id] = agg;
                    }

                    agg.SumShare += shares[j].Value;
                    agg.RunAppearances++;
                    if (j < 3)
                    {
                        agg.Top3Count++;
                    }
                }
            }

            var sorted = new List<AbilityAgg>(aggs.Values);
            sorted.Sort((a, b) =>
            {
                double avgA = a.SumShare / runs.Count;
                double avgB = b.SumShare / runs.Count;
                int cmp = avgB.CompareTo(avgA);
                return cmp != 0 ? cmp : b.Top3Count.CompareTo(a.Top3Count);
            });

            int limit = Math.Min(20, sorted.Count);
            for (int i = 0; i < limit; i++)
            {
                AbilityAgg agg = sorted[i];
                double avgSharePct = (agg.SumShare / runs.Count) * 100.0;
                double top3Pct = ((double)agg.Top3Count / runs.Count) * 100.0;
                sb.AppendLine(
                    $"| {agg.Id} | {avgSharePct.ToString("F1", CultureInfo.InvariantCulture)}% | " +
                    $"{top3Pct.ToString("F1", CultureInfo.InvariantCulture)}% | {agg.Top3Count} | {agg.RunAppearances} |");
            }
            sb.AppendLine();
        }

        private static void AppendCardPickRateSection(StringBuilder sb, IReadOnlyDictionary<string, CardOfferStat> cardStats)
        {
            sb.AppendLine("## 3. Card Pick Rate");
            sb.AppendLine();

            if (cardStats == null || cardStats.Count == 0)
            {
                sb.AppendLine("_No card offers recorded._");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("| Card ID | Offered (Free) | Picked | Pick Rate (%) |");
            sb.AppendLine("|---|---:|---:|---:|");

            var list = new List<KeyValuePair<string, CardOfferStat>>(cardStats);
            list.Sort((a, b) =>
            {
                double rateA = a.Value.OfferedFreeCount > 0 ? (double)a.Value.PickedCount / a.Value.OfferedFreeCount : 0.0;
                double rateB = b.Value.OfferedFreeCount > 0 ? (double)b.Value.PickedCount / b.Value.OfferedFreeCount : 0.0;
                int cmp = rateB.CompareTo(rateA);
                if (cmp != 0) return cmp;
                return b.Value.PickedCount.CompareTo(a.Value.PickedCount);
            });

            int limit = Math.Min(35, list.Count);
            for (int i = 0; i < limit; i++)
            {
                string id = list[i].Key;
                CardOfferStat st = list[i].Value;
                double rate = st.OfferedFreeCount > 0 ? (st.PickedCount * 100.0) / st.OfferedFreeCount : 0.0;
                sb.AppendLine(
                    $"| {id} | {st.OfferedFreeCount} | {st.PickedCount} | {rate.ToString("F1", CultureInfo.InvariantCulture)}% |");
            }
            sb.AppendLine();
        }

        private static void AppendLevelUpIntervalSection(StringBuilder sb, IReadOnlyList<CompletedRunSummary> runs)
        {
            sb.AppendLine("## 4. LevelUp Intervals (Sprint 12.1 Target on W30+: >= 15.0s)");
            sb.AppendLine();

            double sumW1 = 0;
            int cntW1 = 0;
            double sumW10 = 0;
            int cntW10 = 0;
            double sumW30 = 0;
            int cntW30 = 0;

            for (int i = 0; i < runs.Count; i++)
            {
                CompletedRunSummary r = runs[i];
                if (r.LevelUpCountW1_10 > 0)
                {
                    sumW1 += r.AvgLevelUpIntervalW1_10 * r.LevelUpCountW1_10;
                    cntW1 += r.LevelUpCountW1_10;
                }
                if (r.LevelUpCountW10_30 > 0)
                {
                    sumW10 += r.AvgLevelUpIntervalW10_30 * r.LevelUpCountW10_30;
                    cntW10 += r.LevelUpCountW10_30;
                }
                if (r.LevelUpCountW30Plus > 0)
                {
                    sumW30 += r.AvgLevelUpIntervalW30Plus * r.LevelUpCountW30Plus;
                    cntW30 += r.LevelUpCountW30Plus;
                }
            }

            sb.AppendLine($"- **W1–10 avg interval:** {(cntW1 > 0 ? (sumW1 / cntW1).ToString("F2", CultureInfo.InvariantCulture) + "s" : "N/A")} ({cntW1} level-ups)");
            sb.AppendLine($"- **W10–30 avg interval:** {(cntW10 > 0 ? (sumW10 / cntW10).ToString("F2", CultureInfo.InvariantCulture) + "s" : "N/A")} ({cntW10} level-ups)");
            sb.AppendLine($"- **W30+ avg interval:** {(cntW30 > 0 ? (sumW30 / cntW30).ToString("F2", CultureInfo.InvariantCulture) + "s" : "N/A (no runs reached W30+)")} ({cntW30} level-ups)");
            sb.AppendLine();
        }

        private static void AppendCoinsPerHourSection(StringBuilder sb, IReadOnlyList<CompletedRunSummary> runs)
        {
            sb.AppendLine("## 5. Coins & Shards Per Hour by Meta Profile");
            sb.AppendLine();
            sb.AppendLine("| Meta Profile | Runs | Total PlayTime (min) | Total Coins | Coins/hr (Raw) | Coins/hr (1.25x Menu Overhead) | Shards/hr (Raw) |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");

            var byMeta = new Dictionary<string, (int runs, double sec, long coins, long shards)>(StringComparer.Ordinal);
            for (int i = 0; i < runs.Count; i++)
            {
                CompletedRunSummary r = runs[i];
                string meta = string.IsNullOrEmpty(r.MetaProfile) ? "Unknown" : r.MetaProfile;
                byMeta.TryGetValue(meta, out var cur);
                cur.runs++;
                cur.sec += r.RunTimeSeconds;
                cur.coins += r.TotalCoins;
                cur.shards += r.TotalShards;
                byMeta[meta] = cur;
            }

            foreach (var kv in byMeta)
            {
                double hours = kv.Value.sec / 3600.0;
                double cphRaw = hours > 0.0001 ? kv.Value.coins / hours : 0.0;
                double cphMenu = cphRaw / 1.25;
                double sphRaw = hours > 0.0001 ? kv.Value.shards / hours : 0.0;

                sb.AppendLine(
                    $"| {kv.Key} | {kv.Value.runs} | {(kv.Value.sec / 60.0).ToString("F2", CultureInfo.InvariantCulture)} | " +
                    $"{kv.Value.coins} | {cphRaw.ToString("F0", CultureInfo.InvariantCulture)} | " +
                    $"{cphMenu.ToString("F0", CultureInfo.InvariantCulture)} | {sphRaw.ToString("F1", CultureInfo.InvariantCulture)} |");
            }
            sb.AppendLine();
        }

        private static void AppendSimulatorComparisonSection(StringBuilder sb)
        {
            sb.AppendLine("## 6. BalanceCurveSimulator Forecast Comparison");
            sb.AppendLine();

            if (BalanceBotEditorBridge.SimulatorComparisonProvider != null)
            {
                string simText = BalanceBotEditorBridge.SimulatorComparisonProvider.Invoke();
                if (!string.IsNullOrEmpty(simText))
                {
                    sb.AppendLine(simText);
                    return;
                }
            }

            sb.AppendLine("_Analytical simulator comparison is available when executed inside Unity Editor (`ProjectB.Editor`)._");
            sb.AppendLine();
        }

        public static double Percentile(List<double> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0.0;
            values.Sort();
            if (values.Count == 1) return values[0];

            double index = Math.Max(0.0, Math.Min(1.0, percentile)) * (values.Count - 1);
            int lo = (int)Math.Floor(index);
            int hi = (int)Math.Ceiling(index);
            if (lo == hi) return values[lo];

            double frac = index - lo;
            return values[lo] + (values[hi] - values[lo]) * frac;
        }
    }
}
#endif
