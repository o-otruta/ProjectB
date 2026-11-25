#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Core.Telemetry
{
    /// <summary>
    /// Zero-GC run telemetry logger writing waves.csv, runs.csv, and cards.csv.
    /// Accumulates per-wave counters in preallocated buffers during events and only formats/writes at wave end and run end.
    /// </summary>
    public sealed class RunTelemetry : IStartable, IDisposable
    {
        private struct CardPickRecord
        {
            public int HeroLevel;
            public int Wave;
            public string Offered0;
            public string Offered1;
            public string Offered2;
            public int OfferedCount;
            public string SelectedCard;
            public bool WasRerolled;
        }

        private sealed class DamageSourceDescendingComparer : IComparer<DamageSourceEntry>
        {
            public static readonly DamageSourceDescendingComparer Instance = new DamageSourceDescendingComparer();

            public int Compare(DamageSourceEntry a, DamageSourceEntry b)
            {
                return b.TotalDamage.CompareTo(a.TotalDamage);
            }
        }

        private const string WAVES_HEADER =
            "run_id,hero,strategy,meta_profile,seed,wave,wave_duration_s,run_time_s,enemies_alive_at_end,kills_in_wave,damage_taken_in_wave,hero_hp_end,hero_max_hp,hero_level,levelups_in_wave,coins_in_wave,shards_in_wave,wave_event_mutator,top5_damage_sources,abilities";

        private const string RUNS_HEADER =
            "run_id,hero,strategy,meta_profile,seed,death_wave,death_cause,run_time_s,final_level,total_kills,total_coins,total_shards,best_combo,top_damage_sources,avg_levelup_interval_w1_10_s,avg_levelup_interval_w10_30_s,avg_levelup_interval_w30_plus_s,picked_cards";

        private const string CARDS_HEADER =
            "run_id,hero_level,wave,offered_cards,selected_card,was_rerolled";

        private readonly GameEventBus eventBus;
        private readonly RunStatistics runStatistics;
        private readonly HeroHealth heroHealth;
        private readonly HeroExperience heroExperience;
        private readonly HeroAbilities heroAbilities;
        private readonly WaveManager waveManager;
        private readonly WaveEventManager waveEventManager;
        private readonly ArenaGenerator arenaGenerator;
        private readonly SelectedHeroService selectedHeroService;

        // Preallocated zero-GC buffers
        private readonly string[] prevWaveDamageIds = new string[RunDamageTracker.MAX_SOURCES];
        private readonly long[] prevWaveDamageTotals = new long[RunDamageTracker.MAX_SOURCES];
        private int prevWaveDamageCount;

        private readonly string[] curWaveDamageIds = new string[RunDamageTracker.MAX_SOURCES];
        private readonly long[] curWaveDamageTotals = new long[RunDamageTracker.MAX_SOURCES];
        private readonly DamageSourceEntry[] sortedDamageBuffer = new DamageSourceEntry[RunDamageTracker.MAX_SOURCES];

        private readonly CardPickRecord[] pendingCardRecords = new CardPickRecord[64];
        private int pendingCardRecordCount;

        private readonly string[] pickedCardHistory = new string[512];
        private int pickedCardHistoryCount;

        private readonly StringBuilder lineSb = new StringBuilder(1024);
        private readonly StringBuilder subFieldSb = new StringBuilder(256);

        // Cached event delegates
        private readonly Action<WaveCompletedEvent> onWaveCompleted;
        private readonly Action<HeroDamagedEvent> onHeroDamaged;
        private readonly Action<HeroLeveledUpEvent> onHeroLeveledUp;
        private readonly Action<WaveEventStartedEvent> onWaveEventStarted;
        private readonly Action<WaveMutatorStartedEvent> onWaveMutatorStarted;
        private readonly Action<CardOfferShownEvent> onCardOfferShown;
        private readonly Action<CardSelectedEvent> onCardSelected;
        private readonly Action<GameOverEvent> onGameOver;

        // Run metadata & counters
        private string runId;
        private string heroId;
        private string strategyLabel;
        private string metaProfileLabel;
        private int arenaSeed;
        private bool isStarted;
        private bool isSubscribed;
        private bool isRunFinalized;
        private string lastDeathCause;

        private int currentTrackedWave = 1;
        private float waveStartPlayTime;
        private int waveStartKills;
        private int waveStartCoins;
        private int waveStartShards;
        private int damageTakenInWave;
        private int levelUpsInWave;
        private int waveEventBitmask;
        private int waveMutatorBitmask;

        private float lastLevelUpPlayTime;
        private double sumLuIntervalW1_10;
        private int countLuIntervalW1_10;
        private double sumLuIntervalW10_30;
        private int countLuIntervalW10_30;
        private double sumLuIntervalW30Plus;
        private int countLuIntervalW30Plus;

        [Inject]
        public RunTelemetry(
            GameEventBus eventBus,
            RunStatistics runStatistics,
            HeroHealth heroHealth = null,
            HeroExperience heroExperience = null,
            HeroAbilities heroAbilities = null,
            WaveManager waveManager = null,
            WaveEventManager waveEventManager = null,
            ArenaGenerator arenaGenerator = null,
            SelectedHeroService selectedHeroService = null)
        {
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            this.heroHealth = heroHealth;
            this.heroExperience = heroExperience;
            this.heroAbilities = heroAbilities;
            this.waveManager = waveManager;
            this.waveEventManager = waveEventManager;
            this.arenaGenerator = arenaGenerator;
            this.selectedHeroService = selectedHeroService;

            onWaveCompleted = HandleWaveCompleted;
            onHeroDamaged = HandleHeroDamaged;
            onHeroLeveledUp = HandleHeroLeveledUp;
            onWaveEventStarted = HandleWaveEventStarted;
            onWaveMutatorStarted = HandleWaveMutatorStarted;
            onCardOfferShown = HandleCardOfferShown;
            onCardSelected = HandleCardSelected;
            onGameOver = HandleGameOver;
        }

        public void Start()
        {
            if (isStarted) return;
            isStarted = true;

            heroId = ResolveHeroId();
            strategyLabel = !string.IsNullOrEmpty(RunTelemetrySession.ActiveStrategyLabel)
                ? RunTelemetrySession.ActiveStrategyLabel
                : "Manual";
            metaProfileLabel = !string.IsNullOrEmpty(RunTelemetrySession.ActiveMetaProfileLabel)
                ? RunTelemetrySession.ActiveMetaProfileLabel
                : "PlayerSave";
            arenaSeed = RunTelemetrySession.ActiveSeedOverride ?? (arenaGenerator != null ? arenaGenerator.CurrentSeed : 0);
            runId = RunTelemetrySession.AllocateRunId(heroId);
            currentTrackedWave = waveManager != null ? Mathf.Max(1, waveManager.CurrentWave) : 1;

            SubscribeEvents();
        }

        public void Dispose()
        {
            if (!isRunFinalized && isStarted && RunTelemetrySession.IsEnabled)
            {
                FinalizeRun("SceneExited");
            }
            UnsubscribeEvents();
        }

        public void FinalizeRun(string overrideDeathCause = null)
        {
            if (isRunFinalized || !RunTelemetrySession.IsEnabled) return;
            isRunFinalized = true;

            if (arenaSeed == 0 && arenaGenerator != null)
            {
                arenaSeed = arenaGenerator.CurrentSeed;
            }

            int endWave = waveManager != null ? Mathf.Max(currentTrackedWave, waveManager.CurrentWave) : currentTrackedWave;
            FlushPendingCardRecords();

            // Write final (or interrupted) wave row if it wasn't just written by WaveCompletedEvent
            if (endWave >= currentTrackedWave)
            {
                WriteWaveRow(endWave);
            }

            string deathCause = ResolveDeathCause(overrideDeathCause);
            WriteRunRow(endWave, deathCause);
        }

        private void SubscribeEvents()
        {
            if (isSubscribed || eventBus == null) return;
            eventBus.Subscribe(onWaveCompleted);
            eventBus.Subscribe(onHeroDamaged);
            eventBus.Subscribe(onHeroLeveledUp);
            eventBus.Subscribe(onWaveEventStarted);
            eventBus.Subscribe(onWaveMutatorStarted);
            eventBus.Subscribe(onCardOfferShown);
            eventBus.Subscribe(onCardSelected);
            eventBus.Subscribe(onGameOver);
            isSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!isSubscribed || eventBus == null) return;
            eventBus.Unsubscribe(onWaveCompleted);
            eventBus.Unsubscribe(onHeroDamaged);
            eventBus.Unsubscribe(onHeroLeveledUp);
            eventBus.Unsubscribe(onWaveEventStarted);
            eventBus.Unsubscribe(onWaveMutatorStarted);
            eventBus.Unsubscribe(onCardOfferShown);
            eventBus.Unsubscribe(onCardSelected);
            eventBus.Unsubscribe(onGameOver);
            isSubscribed = false;
        }

        private string ResolveHeroId()
        {
            var data = heroHealth != null && heroHealth.ActiveHeroData != null
                ? heroHealth.ActiveHeroData
                : selectedHeroService?.GetSelectedHero();
            if (data != null && !string.IsNullOrEmpty(data.id)) return data.id;
            return "knight";
        }

        private string ResolveDeathCause(string overrideCause)
        {
            if (!string.IsNullOrEmpty(overrideCause)) return overrideCause;
            // HeroHealth — авторитетный источник: учитывает и смертельный удар, и DebugKill(cause).
            if (heroHealth != null && !string.IsNullOrEmpty(heroHealth.LastDamageSourceId))
            {
                return heroHealth.LastDamageSourceId;
            }
            if (!string.IsNullOrEmpty(lastDeathCause)) return lastDeathCause;
            return "Unknown";
        }

        private void HandleHeroDamaged(HeroDamagedEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;
            damageTakenInWave += evt.Damage;
            if (!string.IsNullOrEmpty(evt.SourceId))
            {
                lastDeathCause = evt.SourceId;
            }
        }

        private void HandleHeroLeveledUp(HeroLeveledUpEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;
            levelUpsInWave++;

            float playTime = runStatistics != null ? runStatistics.PlayTime : 0f;
            if (playTime <= 0.001f)
            {
                // Ignore meta StartingBonusLevels awarded at t=0
                lastLevelUpPlayTime = 0f;
                return;
            }

            float interval = playTime - lastLevelUpPlayTime;
            lastLevelUpPlayTime = playTime;
            if (interval < 0f) interval = 0f;

            int wave = waveManager != null ? waveManager.CurrentWave : currentTrackedWave;
            if (wave <= 10)
            {
                sumLuIntervalW1_10 += interval;
                countLuIntervalW1_10++;
            }
            else if (wave < 30)
            {
                sumLuIntervalW10_30 += interval;
                countLuIntervalW10_30++;
            }
            else
            {
                sumLuIntervalW30Plus += interval;
                countLuIntervalW30Plus++;
            }
        }

        private void HandleWaveEventStarted(WaveEventStartedEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;
            int bit = (int)evt.EventType;
            if (bit > 0 && bit < 31)
            {
                waveEventBitmask |= (1 << bit);
            }
        }

        private void HandleWaveMutatorStarted(WaveMutatorStartedEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;
            int bit = (int)evt.MutatorType;
            if (bit > 0 && bit < 31)
            {
                waveMutatorBitmask |= (1 << bit);
            }
        }

        private void HandleCardOfferShown(CardOfferShownEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;

            int freeCount = Mathf.Min(evt.FreeSlotsCount, evt.OfferedCount);
            if (freeCount >= 1 && evt.Card0 != null) RunTelemetrySession.RecordFreeCardOffered(CsvUtility.GetCardId(evt.Card0));
            if (freeCount >= 2 && evt.Card1 != null) RunTelemetrySession.RecordFreeCardOffered(CsvUtility.GetCardId(evt.Card1));
            if (freeCount >= 3 && evt.Card2 != null) RunTelemetrySession.RecordFreeCardOffered(CsvUtility.GetCardId(evt.Card2));
        }

        private void HandleCardSelected(CardSelectedEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;

            string selectedId = CsvUtility.GetCardId(evt.SelectedCard);
            RunTelemetrySession.RecordCardPicked(selectedId);

            if (pickedCardHistoryCount < pickedCardHistory.Length)
            {
                pickedCardHistory[pickedCardHistoryCount++] = selectedId;
            }

            if (pendingCardRecordCount < pendingCardRecords.Length)
            {
                pendingCardRecords[pendingCardRecordCount++] = new CardPickRecord
                {
                    HeroLevel = evt.HeroLevel,
                    Wave = waveManager != null ? waveManager.CurrentWave : currentTrackedWave,
                    Offered0 = CsvUtility.GetCardId(evt.Offered0),
                    Offered1 = CsvUtility.GetCardId(evt.Offered1),
                    Offered2 = CsvUtility.GetCardId(evt.Offered2),
                    OfferedCount = evt.OfferedCount,
                    SelectedCard = selectedId,
                    WasRerolled = evt.WasRerolled
                };
            }
        }

        private void HandleWaveCompleted(WaveCompletedEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled || isRunFinalized) return;

            if (arenaSeed == 0 && arenaGenerator != null)
            {
                arenaSeed = arenaGenerator.CurrentSeed;
            }

            FlushPendingCardRecords();
            WriteWaveRow(evt.CompletedWave);

            currentTrackedWave = evt.NextWave;
            ResetWaveCounters();
        }

        private void HandleGameOver(GameOverEvent evt)
        {
            if (!RunTelemetrySession.IsEnabled) return;
            FinalizeRun(null);
        }

        private void ResetWaveCounters()
        {
            waveStartPlayTime = runStatistics != null ? runStatistics.PlayTime : 0f;
            waveStartKills = runStatistics != null ? runStatistics.EnemiesKilled : 0;
            waveStartCoins = runStatistics != null ? runStatistics.CoinsEarned : 0;
            waveStartShards = runStatistics != null ? runStatistics.BossShardsEarned : 0;
            damageTakenInWave = 0;
            levelUpsInWave = 0;
            waveEventBitmask = 0;
            waveMutatorBitmask = 0;
        }

        private void FlushPendingCardRecords()
        {
            string path = RunTelemetrySession.CardsCsvPath;
            if (string.IsNullOrEmpty(path) || pendingCardRecordCount == 0)
            {
                pendingCardRecordCount = 0;
                return;
            }

            for (int i = 0; i < pendingCardRecordCount; i++)
            {
                CardPickRecord rec = pendingCardRecords[i];
                subFieldSb.Clear();
                if (rec.OfferedCount >= 1 && !string.IsNullOrEmpty(rec.Offered0)) subFieldSb.Append(rec.Offered0);
                if (rec.OfferedCount >= 2 && !string.IsNullOrEmpty(rec.Offered1))
                {
                    if (subFieldSb.Length > 0) subFieldSb.Append('|');
                    subFieldSb.Append(rec.Offered1);
                }
                if (rec.OfferedCount >= 3 && !string.IsNullOrEmpty(rec.Offered2))
                {
                    if (subFieldSb.Length > 0) subFieldSb.Append('|');
                    subFieldSb.Append(rec.Offered2);
                }

                lineSb.Clear();
                CsvUtility.AppendCell(lineSb, runId);
                CsvUtility.AppendInt(lineSb, rec.HeroLevel);
                CsvUtility.AppendInt(lineSb, rec.Wave);
                CsvUtility.AppendCell(lineSb, subFieldSb.ToString());
                CsvUtility.AppendCell(lineSb, rec.SelectedCard);
                CsvUtility.AppendBool(lineSb, rec.WasRerolled, isLast: true);

                CsvUtility.EnsureHeaderAndAppendLine(path, CARDS_HEADER, lineSb.ToString());
            }

            pendingCardRecordCount = 0;
        }

        private void WriteWaveRow(int wave)
        {
            string path = RunTelemetrySession.WavesCsvPath;
            if (string.IsNullOrEmpty(path)) return;

            float runTime = runStatistics != null ? runStatistics.PlayTime : 0f;
            float waveDuration = Mathf.Max(0.01f, runTime - waveStartPlayTime);
            int killsInWave = runStatistics != null ? Mathf.Max(0, runStatistics.EnemiesKilled - waveStartKills) : 0;
            int coinsInWave = runStatistics != null ? Mathf.Max(0, runStatistics.CoinsEarned - waveStartCoins) : 0;
            int shardsInWave = runStatistics != null ? Mathf.Max(0, runStatistics.BossShardsEarned - waveStartShards) : 0;
            int enemiesAlive = waveManager != null ? waveManager.EnemiesAlive : 0;
            int hpEnd = heroHealth != null ? heroHealth.CurrentHp : 0;
            int maxHp = heroHealth != null ? heroHealth.MaxHp : 0;
            int heroLevel = heroExperience != null ? heroExperience.CurrentLevel : 1;

            string eventMutatorStr = FormatWaveEventAndMutator();
            string top5Str = FormatWaveTop5DamageSources(waveDuration);
            string abilitiesStr = FormatCurrentAbilities();

            lineSb.Clear();
            CsvUtility.AppendCell(lineSb, runId);
            CsvUtility.AppendCell(lineSb, heroId);
            CsvUtility.AppendCell(lineSb, strategyLabel);
            CsvUtility.AppendCell(lineSb, metaProfileLabel);
            CsvUtility.AppendInt(lineSb, arenaSeed);
            CsvUtility.AppendInt(lineSb, wave);
            CsvUtility.AppendFloat(lineSb, waveDuration);
            CsvUtility.AppendFloat(lineSb, runTime);
            CsvUtility.AppendInt(lineSb, enemiesAlive);
            CsvUtility.AppendInt(lineSb, killsInWave);
            CsvUtility.AppendInt(lineSb, damageTakenInWave);
            CsvUtility.AppendInt(lineSb, hpEnd);
            CsvUtility.AppendInt(lineSb, maxHp);
            CsvUtility.AppendInt(lineSb, heroLevel);
            CsvUtility.AppendInt(lineSb, levelUpsInWave);
            CsvUtility.AppendInt(lineSb, coinsInWave);
            CsvUtility.AppendInt(lineSb, shardsInWave);
            CsvUtility.AppendCell(lineSb, eventMutatorStr);
            CsvUtility.AppendCell(lineSb, top5Str);
            CsvUtility.AppendCell(lineSb, abilitiesStr, isLast: true);

            CsvUtility.EnsureHeaderAndAppendLine(path, WAVES_HEADER, lineSb.ToString());
        }

        private void WriteRunRow(int deathWave, string deathCause)
        {
            string path = RunTelemetrySession.RunsCsvPath;
            if (string.IsNullOrEmpty(path)) return;

            float runTime = runStatistics != null ? runStatistics.PlayTime : 0f;
            int finalLevel = heroExperience != null ? heroExperience.CurrentLevel : 1;
            int totalKills = runStatistics != null ? runStatistics.EnemiesKilled : 0;
            int totalCoins = runStatistics != null ? runStatistics.CoinsEarned : 0;
            int totalShards = runStatistics != null ? runStatistics.BossShardsEarned : 0;
            int bestCombo = runStatistics != null ? runStatistics.BestCombo : 0;

            string topSourcesStr = FormatRunTopDamageSources(runTime, out KeyValuePair<string, float>[] topShares);

            double avgW1_10 = countLuIntervalW1_10 > 0 ? sumLuIntervalW1_10 / countLuIntervalW1_10 : 0.0;
            double avgW10_30 = countLuIntervalW10_30 > 0 ? sumLuIntervalW10_30 / countLuIntervalW10_30 : 0.0;
            double avgW30Plus = countLuIntervalW30Plus > 0 ? sumLuIntervalW30Plus / countLuIntervalW30Plus : 0.0;

            subFieldSb.Clear();
            for (int i = 0; i < pickedCardHistoryCount; i++)
            {
                if (i > 0) subFieldSb.Append('|');
                subFieldSb.Append(pickedCardHistory[i]);
            }
            string pickedCardsStr = subFieldSb.ToString();

            lineSb.Clear();
            CsvUtility.AppendCell(lineSb, runId);
            CsvUtility.AppendCell(lineSb, heroId);
            CsvUtility.AppendCell(lineSb, strategyLabel);
            CsvUtility.AppendCell(lineSb, metaProfileLabel);
            CsvUtility.AppendInt(lineSb, arenaSeed);
            CsvUtility.AppendInt(lineSb, deathWave);
            CsvUtility.AppendCell(lineSb, deathCause);
            CsvUtility.AppendFloat(lineSb, runTime);
            CsvUtility.AppendInt(lineSb, finalLevel);
            CsvUtility.AppendInt(lineSb, totalKills);
            CsvUtility.AppendInt(lineSb, totalCoins);
            CsvUtility.AppendInt(lineSb, totalShards);
            CsvUtility.AppendInt(lineSb, bestCombo);
            CsvUtility.AppendCell(lineSb, topSourcesStr);
            CsvUtility.AppendCell(lineSb, countLuIntervalW1_10 > 0 ? avgW1_10.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
            CsvUtility.AppendCell(lineSb, countLuIntervalW10_30 > 0 ? avgW10_30.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
            CsvUtility.AppendCell(lineSb, countLuIntervalW30Plus > 0 ? avgW30Plus.ToString("F2", CultureInfo.InvariantCulture) : string.Empty);
            CsvUtility.AppendCell(lineSb, pickedCardsStr, isLast: true);

            CsvUtility.EnsureHeaderAndAppendLine(path, RUNS_HEADER, lineSb.ToString());

            RunTelemetrySession.RecordCompletedRun(new CompletedRunSummary
            {
                RunId = runId,
                HeroId = heroId,
                Strategy = strategyLabel,
                MetaProfile = metaProfileLabel,
                Seed = arenaSeed,
                DeathWave = deathWave,
                DeathCause = deathCause,
                RunTimeSeconds = runTime,
                FinalLevel = finalLevel,
                TotalKills = totalKills,
                TotalCoins = totalCoins,
                TotalShards = totalShards,
                BestCombo = bestCombo,
                AvgLevelUpIntervalW1_10 = avgW1_10,
                AvgLevelUpIntervalW10_30 = avgW10_30,
                AvgLevelUpIntervalW30Plus = avgW30Plus,
                LevelUpCountW1_10 = countLuIntervalW1_10,
                LevelUpCountW10_30 = countLuIntervalW10_30,
                LevelUpCountW30Plus = countLuIntervalW30Plus,
                TopDamageShares = topShares
            });
        }

        private string FormatWaveEventAndMutator()
        {
            if (waveEventManager != null)
            {
                if (waveEventManager.IsEventActive)
                {
                    int b = (int)waveEventManager.ActiveEventType;
                    if (b > 0 && b < 31) waveEventBitmask |= (1 << b);
                }
                if (waveEventManager.IsMutatorActive)
                {
                    int b = (int)waveEventManager.ActiveMutatorType;
                    if (b > 0 && b < 31) waveMutatorBitmask |= (1 << b);
                }
            }

            subFieldSb.Clear();
            for (int bit = 1; bit < 31; bit++)
            {
                if ((waveEventBitmask & (1 << bit)) != 0)
                {
                    if (subFieldSb.Length > 0) subFieldSb.Append('|');
                    subFieldSb.Append("Event:").Append(((WaveEventType)bit).ToString());
                }
            }
            for (int bit = 1; bit < 31; bit++)
            {
                if ((waveMutatorBitmask & (1 << bit)) != 0)
                {
                    if (subFieldSb.Length > 0) subFieldSb.Append('|');
                    subFieldSb.Append("Mutator:").Append(((WaveMutatorType)bit).ToString());
                }
            }

            return subFieldSb.Length > 0 ? subFieldSb.ToString() : "None";
        }

        private string FormatWaveTop5DamageSources(float waveDuration)
        {
            if (runStatistics == null) return string.Empty;

            int curCount = runStatistics.DamageTracker.CopySnapshot(curWaveDamageIds, curWaveDamageTotals);
            long waveTotalDamage = 0;
            int entryCount = 0;
            float safeDur = Mathf.Max(0.1f, waveDuration);

            for (int i = 0; i < curCount; i++)
            {
                string id = curWaveDamageIds[i];
                long curVal = curWaveDamageTotals[i];
                long prevVal = FindPreviousDamage(id);
                long delta = curVal - prevVal;
                if (delta <= 0) continue;

                waveTotalDamage += delta;
                sortedDamageBuffer[entryCount++] = new DamageSourceEntry
                {
                    SourceId = id,
                    TotalDamage = delta,
                    Dps = delta / safeDur
                };
            }

            // Update baseline for next wave
            for (int i = 0; i < curCount; i++)
            {
                prevWaveDamageIds[i] = curWaveDamageIds[i];
                prevWaveDamageTotals[i] = curWaveDamageTotals[i];
            }
            prevWaveDamageCount = curCount;

            if (entryCount == 0) return string.Empty;
            Array.Sort(sortedDamageBuffer, 0, entryCount, DamageSourceDescendingComparer.Instance);

            subFieldSb.Clear();
            int topN = Mathf.Min(5, entryCount);
            for (int i = 0; i < topN; i++)
            {
                DamageSourceEntry e = sortedDamageBuffer[i];
                float pct = waveTotalDamage > 0 ? (e.TotalDamage * 100f) / waveTotalDamage : 0f;
                if (i > 0) subFieldSb.Append('|');
                subFieldSb.Append(e.SourceId)
                    .Append(':')
                    .Append(pct.ToString("F1", CultureInfo.InvariantCulture))
                    .Append("%@")
                    .Append(e.Dps.ToString("F1", CultureInfo.InvariantCulture));
            }
            return subFieldSb.ToString();
        }

        private long FindPreviousDamage(string id)
        {
            for (int i = 0; i < prevWaveDamageCount; i++)
            {
                if (ReferenceEquals(prevWaveDamageIds[i], id) ||
                    string.Equals(prevWaveDamageIds[i], id, StringComparison.Ordinal))
                {
                    return prevWaveDamageTotals[i];
                }
            }
            return 0L;
        }

        private string FormatRunTopDamageSources(float runTime, out KeyValuePair<string, float>[] topShares)
        {
            topShares = Array.Empty<KeyValuePair<string, float>>();
            if (runStatistics == null) return string.Empty;

            int curCount = runStatistics.DamageTracker.CopySnapshot(curWaveDamageIds, curWaveDamageTotals);
            long total = runStatistics.TotalDamageDealt;
            int entryCount = 0;
            float safeTime = Mathf.Max(1f, runTime);

            for (int i = 0; i < curCount; i++)
            {
                long dmg = curWaveDamageTotals[i];
                if (dmg <= 0 || string.IsNullOrEmpty(curWaveDamageIds[i])) continue;

                float share = total > 0 ? (float)dmg / total : 0f;
                sortedDamageBuffer[entryCount++] = new DamageSourceEntry
                {
                    SourceId = curWaveDamageIds[i],
                    TotalDamage = dmg,
                    Share = share,
                    Dps = dmg / safeTime
                };
            }

            if (entryCount == 0) return string.Empty;
            Array.Sort(sortedDamageBuffer, 0, entryCount, DamageSourceDescendingComparer.Instance);

            topShares = new KeyValuePair<string, float>[entryCount];
            subFieldSb.Clear();
            for (int i = 0; i < entryCount; i++)
            {
                DamageSourceEntry e = sortedDamageBuffer[i];
                topShares[i] = new KeyValuePair<string, float>(e.SourceId, e.Share);
                if (i > 0) subFieldSb.Append('|');
                subFieldSb.Append(e.SourceId)
                    .Append(':')
                    .Append((e.Share * 100f).ToString("F1", CultureInfo.InvariantCulture))
                    .Append('%');
            }
            return subFieldSb.ToString();
        }

        private string FormatCurrentAbilities()
        {
            if (heroAbilities == null) return string.Empty;

            subFieldSb.Clear();
            var actives = heroAbilities.ActiveAbilities;
            for (int i = 0; i < actives.Count; i++)
            {
                ActiveAbility a = actives[i];
                if (a == null || a.Data == null) continue;
                if (subFieldSb.Length > 0) subFieldSb.Append('|');
                subFieldSb.Append(a.Data.id).Append(':').Append(a.Level);
            }

            var passives = heroAbilities.PassiveAbilities;
            for (int i = 0; i < passives.Count; i++)
            {
                PassiveAbility p = passives[i];
                if (p == null || p.Data == null) continue;
                if (subFieldSb.Length > 0) subFieldSb.Append('|');
                subFieldSb.Append(p.Data.id).Append(':').Append(p.Level);
            }

            return subFieldSb.ToString();
        }
    }
}
#endif
