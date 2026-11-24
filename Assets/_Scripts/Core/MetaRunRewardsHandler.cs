using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Enemies;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Core
{
    /// <summary>
    /// Sprint 11 run-scoped bridge between combat events and meta-progression:
    /// awards Boss Shards (mini-boss / boss / shrine), applies Constellation run effects
    /// (Boss Damage, Keystone "Collector's Greed") and feeds the new shrine/combo achievements.
    /// </summary>
    public sealed class MetaRunRewardsHandler : IStartable, IDisposable
    {
        private readonly GameEventBus eventBus;
        private readonly RunStatistics runStatistics;
        private readonly WaveManager waveManager;
        private readonly HeroRuntimeStats runtimeStats;
        private readonly HeroEconomy heroEconomy;
        private readonly AchievementManager achievementManager;
        private readonly EnemyRuntimeModifiers enemyRuntimeModifiers;
        private readonly RunRulesConfig runRulesConfig;

        private bool started;
        private int crystalsCollected;

        public int CrystalsCollected => crystalsCollected;

        [Inject]
        public MetaRunRewardsHandler(
            GameEventBus eventBus,
            RunStatistics runStatistics,
            WaveManager waveManager = null,
            HeroRuntimeStats runtimeStats = null,
            HeroEconomy heroEconomy = null,
            AchievementManager achievementManager = null,
            EnemyRuntimeModifiers enemyRuntimeModifiers = null,
            RunRulesConfig runRulesConfig = null)
        {
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            this.waveManager = waveManager;
            this.runtimeStats = runtimeStats;
            this.heroEconomy = heroEconomy;
            this.achievementManager = achievementManager;
            this.enemyRuntimeModifiers = enemyRuntimeModifiers ?? waveManager?.RuntimeModifiers;
            this.runRulesConfig = runRulesConfig;
        }

        private int CrystalsPerGreedBonus => runRulesConfig != null && runRulesConfig.crystalsPerGreedBonus > 0
            ? runRulesConfig.crystalsPerGreedBonus
            : RunRulesConfig.DEFAULT_CRYSTALS_PER_GREED_BONUS;

        public void Start()
        {
            if (started) return;
            started = true;

            if (enemyRuntimeModifiers != null)
            {
                enemyRuntimeModifiers.BossDamageTakenMultiplier = runtimeStats != null ? Mathf.Max(0.1f, runtimeStats.BossDamageMultiplier) : 1f;
            }

            if (eventBus == null) return;
            eventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            eventBus.Subscribe<ChallengeShrineEvent>(OnChallengeShrine);
            eventBus.Subscribe<ComboMilestoneEvent>(OnComboMilestone);
            eventBus.Subscribe<XpCollectedEvent>(OnXpCollected);
        }

        public void Dispose()
        {
            if (enemyRuntimeModifiers != null)
            {
                enemyRuntimeModifiers.BossDamageTakenMultiplier = 1f;
            }
            if (!started || eventBus == null) return;
            eventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
            eventBus.Unsubscribe<ChallengeShrineEvent>(OnChallengeShrine);
            eventBus.Unsubscribe<ComboMilestoneEvent>(OnComboMilestone);
            eventBus.Unsubscribe<XpCollectedEvent>(OnXpCollected);
            started = false;
        }

        private int MiniBossShards => waveManager != null && waveManager.Config != null ? waveManager.Config.miniBossShardReward : 1;
        private int BossShards => waveManager != null && waveManager.Config != null ? waveManager.Config.bossShardReward : 5;
        private int ShrineShards => waveManager != null && waveManager.Config != null ? waveManager.Config.shrineShardReward : 2;

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            if (evt.WasBoss)
            {
                GrantShards(BossShards, evt.Position);
            }
            else if (evt.WasMiniBoss)
            {
                GrantShards(MiniBossShards, evt.Position);
            }
        }

        private void OnChallengeShrine(ChallengeShrineEvent evt)
        {
            if (evt.EventType != ShrineEventType.Completed) return;
            runStatistics?.AddShrineCleared();
            achievementManager?.OnShrineCleared();
            GrantShards(ShrineShards, evt.Position);
        }

        private void OnComboMilestone(ComboMilestoneEvent evt)
        {
            achievementManager?.OnComboReached(evt.ComboCount);
        }

        private void OnXpCollected(XpCollectedEvent evt)
        {
            int bonus = runtimeStats != null ? runtimeStats.CrystalCoinBonus : 0;
            if (bonus <= 0) return;

            crystalsCollected++;
            if (crystalsCollected % CrystalsPerGreedBonus == 0)
            {
                heroEconomy?.AddCoin(bonus);
            }
        }

        /// <summary>Adds shards to the run tally (written to SaveData at run end / abandon).</summary>
        public void GrantShards(int amount, Vector3 position)
        {
            if (amount <= 0 || runStatistics == null) return;
            runStatistics.AddBossShards(amount);
            eventBus?.Publish(new BossShardsGainedEvent(position, amount, runStatistics.BossShardsEarned));
        }
    }
}
