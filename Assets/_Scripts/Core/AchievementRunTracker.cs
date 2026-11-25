using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Abilities;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Core
{
    /// <summary>
    /// Run-scoped bridge from the gameplay event bus to <see cref="AchievementManager"/>.
    /// Keeps per-run state (kills, rerolls, distinct synergies, boss enrage) and builds the
    /// <see cref="AchievementRunContext"/> used by constrained "reach wave" achievements.
    /// Zero allocations per event: all buffers are created once per run.
    /// </summary>
    public sealed class AchievementRunTracker : IStartable, IDisposable
    {
        private const int KILL_REPORT_STEP = 10;

        private readonly GameEventBus eventBus;
        private readonly AchievementManager achievementManager;
        private readonly RunStatistics runStatistics;
        private readonly SaveManager saveManager;
        private readonly HeroAbilities heroAbilities;
        private readonly HeroHealth heroHealth;

        private readonly HashSet<string> synergiesThisRun = new HashSet<string>(StringComparer.Ordinal);
        private bool started;
        private int killsThisRun;
        private bool usedReroll;
        private string enragedBossId;

        public int KillsThisRun => killsThisRun;
        public bool UsedReroll => usedReroll;
        public int DistinctSynergiesThisRun => synergiesThisRun.Count;

        [Inject]
        public AchievementRunTracker(
            GameEventBus eventBus,
            AchievementManager achievementManager,
            RunStatistics runStatistics = null,
            SaveManager saveManager = null,
            HeroAbilities heroAbilities = null,
            HeroHealth heroHealth = null)
        {
            this.eventBus = eventBus;
            this.achievementManager = achievementManager;
            this.runStatistics = runStatistics;
            this.saveManager = saveManager;
            this.heroAbilities = heroAbilities;
            this.heroHealth = heroHealth;
        }

        public void Start()
        {
            if (started || eventBus == null || achievementManager == null) return;
            started = true;

            eventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            eventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            eventBus.Subscribe<BossEnragedEvent>(OnBossEnraged);
            eventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            eventBus.Subscribe<CardOfferShownEvent>(OnCardOfferShown);
            eventBus.Subscribe<BarrelExplodedEvent>(OnBarrelExploded);
            eventBus.Subscribe<SiegeShellBrokenEvent>(OnSiegeShellBroken);
            eventBus.Subscribe<VacuumMagnetCollectedEvent>(OnVacuumMagnet);
            eventBus.Subscribe<SynergyActivatedEvent>(OnSynergyActivated);
            eventBus.Subscribe<UltimateActivatedEvent>(OnUltimateActivated);
            eventBus.Subscribe<WaveEventEndedEvent>(OnWaveEventEnded);
            eventBus.Subscribe<WaveMutatorEndedEvent>(OnMutatorEnded);
            eventBus.Subscribe<SecondWindTriggeredEvent>(OnSecondWind);
            eventBus.Subscribe<GameOverEvent>(OnGameOver);
        }

        public void Dispose()
        {
            if (!started) return;
            started = false;

            eventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
            eventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            eventBus.Unsubscribe<BossEnragedEvent>(OnBossEnraged);
            eventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            eventBus.Unsubscribe<CardOfferShownEvent>(OnCardOfferShown);
            eventBus.Unsubscribe<BarrelExplodedEvent>(OnBarrelExploded);
            eventBus.Unsubscribe<SiegeShellBrokenEvent>(OnSiegeShellBroken);
            eventBus.Unsubscribe<VacuumMagnetCollectedEvent>(OnVacuumMagnet);
            eventBus.Unsubscribe<SynergyActivatedEvent>(OnSynergyActivated);
            eventBus.Unsubscribe<UltimateActivatedEvent>(OnUltimateActivated);
            eventBus.Unsubscribe<WaveEventEndedEvent>(OnWaveEventEnded);
            eventBus.Unsubscribe<WaveMutatorEndedEvent>(OnMutatorEnded);
            eventBus.Unsubscribe<SecondWindTriggeredEvent>(OnSecondWind);
            eventBus.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        /// <summary>Snapshot of the current run for constrained achievements.</summary>
        public AchievementRunContext BuildContext()
        {
            string heroId = saveManager?.Data != null ? saveManager.Data.selectedHeroId : null;
            int active = heroAbilities != null ? heroAbilities.ActiveAbilities.Count : 0;
            int passive = heroAbilities != null ? heroAbilities.PassiveAbilities.Count : 0;
            float seconds = runStatistics != null ? runStatistics.PlayTime : 0f;
            return new AchievementRunContext(heroId, active, passive, usedReroll, seconds);
        }

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            killsThisRun++;
            if (killsThisRun % KILL_REPORT_STEP == 0)
            {
                achievementManager.OnKillsInRun(killsThisRun);
            }

            if (evt.WasElite) achievementManager.OnEliteKilled();
            if (evt.WasMiniBoss) achievementManager.OnMiniBossDefeated();

            if (evt.WasBoss)
            {
                bool beforeEnrage = string.IsNullOrEmpty(enragedBossId) ||
                                    !string.Equals(enragedBossId, evt.BossId, StringComparison.Ordinal);
                float hpPercent = heroHealth != null && heroHealth.MaxHp > 0
                    ? 100f * heroHealth.CurrentHp / heroHealth.MaxHp
                    : 0f;
                achievementManager.OnBigBossKilled(beforeEnrage, hpPercent);
                enragedBossId = null;
            }
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            if (!evt.IsMiniBoss) enragedBossId = null;
        }

        private void OnBossEnraged(BossEnragedEvent evt)
        {
            enragedBossId = evt.BossId;
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            achievementManager.OnWaveReached(evt.NextWave, BuildContext());
            ReportDuration();
        }

        private void OnCardOfferShown(CardOfferShownEvent evt)
        {
            if (evt.IsReroll) usedReroll = true;
        }

        private void OnBarrelExploded(BarrelExplodedEvent evt)
        {
            achievementManager.OnBarrelExploded(evt.EnemiesHit);
        }

        private void OnSiegeShellBroken(SiegeShellBrokenEvent evt)
        {
            achievementManager.OnSiegeShellBroken();
        }

        private void OnVacuumMagnet(VacuumMagnetCollectedEvent evt)
        {
            achievementManager.OnVacuumMagnetCollected(evt.XpMagnetized);
        }

        private void OnSynergyActivated(SynergyActivatedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.SynergyId)) return;
            synergiesThisRun.Add(evt.SynergyId);
            achievementManager.OnSynergyActivated(evt.SynergyId, synergiesThisRun.Count);
        }

        private void OnUltimateActivated(UltimateActivatedEvent evt)
        {
            achievementManager.OnUltimateUsed();
        }

        private void OnWaveEventEnded(WaveEventEndedEvent evt)
        {
            if (evt.Completed) achievementManager.OnWaveEventCompleted(evt.EventType);
        }

        private void OnMutatorEnded(WaveMutatorEndedEvent evt)
        {
            if (evt.Completed) achievementManager.OnMutatorSurvived(evt.MutatorType);
        }

        private void OnSecondWind(SecondWindTriggeredEvent evt)
        {
            achievementManager.OnSecondWindTriggered();
        }

        private void OnGameOver(GameOverEvent evt)
        {
            achievementManager.OnKillsInRun(killsThisRun);
            ReportDuration();
        }

        /// <summary>Run length and best combo (combo milestones stop at x100, so higher targets read the run best).</summary>
        private void ReportDuration()
        {
            if (runStatistics == null) return;
            achievementManager.OnRunDuration(Mathf.FloorToInt(runStatistics.PlayTime / 60f));
            achievementManager.OnComboReached(runStatistics.BestCombo);
        }
    }
}
