using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Core
{
    public struct DamageSourceEntry
    {
        public string SourceId;
        public long TotalDamage;
        public float Share;
        public float Dps;
    }

    /// <summary>
    /// Zero-GC damage accumulator keyed by source id (weapon, ultimate, barrel, ability id, synergy id).
    /// </summary>
    public sealed class RunDamageTracker
    {
        public const int MAX_SOURCES = 64;

        private readonly string[] sourceIds = new string[MAX_SOURCES];
        private readonly long[] sourceTotals = new long[MAX_SOURCES];
        private int sourceCount;

        public long TotalDamage { get; private set; }
        public int SourceCount => sourceCount;

        public void RecordDamage(string sourceId, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(sourceId)) return;

            TotalDamage += amount;

            for (int i = 0; i < sourceCount; i++)
            {
                if (ReferenceEquals(sourceIds[i], sourceId) ||
                    string.Equals(sourceIds[i], sourceId, StringComparison.Ordinal))
                {
                    sourceTotals[i] += amount;
                    return;
                }
            }

            if (sourceCount < MAX_SOURCES)
            {
                sourceIds[sourceCount] = sourceId;
                sourceTotals[sourceCount] = amount;
                sourceCount++;
            }
        }

        public long GetDamage(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return 0L;

            for (int i = 0; i < sourceCount; i++)
            {
                if (ReferenceEquals(sourceIds[i], sourceId) ||
                    string.Equals(sourceIds[i], sourceId, StringComparison.Ordinal))
                {
                    return sourceTotals[i];
                }
            }

            return 0L;
        }

        public void GetTopSources(List<DamageSourceEntry> results, float playTimeSeconds, int maxCount = 6)
        {
            if (results == null) return;
            results.Clear();

            if (sourceCount == 0 || maxCount <= 0) return;

            float safeDuration = Mathf.Max(1f, playTimeSeconds);
            long total = TotalDamage;

            for (int i = 0; i < sourceCount; i++)
            {
                long dmg = sourceTotals[i];
                if (dmg <= 0) continue;

                results.Add(new DamageSourceEntry
                {
                    SourceId = sourceIds[i],
                    TotalDamage = dmg,
                    Share = total > 0 ? (float)dmg / total : 0f,
                    Dps = dmg / safeDuration
                });
            }

            results.Sort((a, b) => b.TotalDamage.CompareTo(a.TotalDamage));

            if (results.Count > maxCount)
            {
                results.RemoveRange(maxCount, results.Count - maxCount);
            }
        }

        public void Reset()
        {
            for (int i = 0; i < sourceCount; i++)
            {
                sourceIds[i] = null;
                sourceTotals[i] = 0L;
            }
            sourceCount = 0;
            TotalDamage = 0L;
        }
    }

    public class RunStatistics
    {
        public int CoinsEarned { get; private set; }
        public int EnemiesKilled { get; private set; }
        public float PlayTime { get; private set; }
        public int BestCombo { get; private set; }
        public int BossShardsEarned { get; private set; }
        public int ShrinesCleared { get; private set; }
        public RunDamageTracker DamageTracker { get; } = new RunDamageTracker();

        public void AddBossShards(int amount)
        {
            if (amount > 0) BossShardsEarned += amount;
        }

        public void AddShrineCleared()
        {
            ShrinesCleared++;
        }
        public long TotalDamageDealt => DamageTracker.TotalDamage;

        public RunStatistics()
        {
        }

        public void AddCoin(int amount = 1)
        {
            CoinsEarned += amount;
        }

        public void AddKill()
        {
            EnemiesKilled++;
        }

        public void UpdatePlayTime(float deltaTime)
        {
            PlayTime += deltaTime;
        }

        public void RecordDamage(string sourceId, int amount)
        {
            DamageTracker.RecordDamage(sourceId, amount);
        }

        public long GetDamageBySource(string sourceId)
        {
            return DamageTracker.GetDamage(sourceId);
        }

        public void GetTopDamageSources(List<DamageSourceEntry> results, int maxCount = 6)
        {
            DamageTracker.GetTopSources(results, PlayTime, maxCount);
        }

        public void UpdateBestCombo(int combo)
        {
            if (combo > BestCombo)
            {
                BestCombo = combo;
            }
        }

        public void Reset()
        {
            CoinsEarned = 0;
            EnemiesKilled = 0;
            PlayTime = 0f;
            BestCombo = 0;
            BossShardsEarned = 0;
            ShrinesCleared = 0;
            DamageTracker.Reset();
        }
    }
}
