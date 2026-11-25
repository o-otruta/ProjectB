using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    public abstract class BossBase : EnemyBase
    {
        protected BossData bossData;
        protected EnemyProjectilePool sharedProjectilePool;
        protected Action<EnemyData, Vector3> summonMinionCallback;
        protected Func<Vector3, float, float, Vector3> getSafePositionNearTarget;

        private float nextSummonTime;

        // Ярость по таймеру боя (анти-кайт)
        private float bossFightStartTime;
        private float baseDamageMultiplier = 1f;
        private float timedEnrageSpeedMultiplier = 1f;
        private int timedEnrageLevel;
        private System.Action<Vector3, float, float, float> heroSlowZoneSpawner;

        public int TimedEnrageLevel => timedEnrageLevel;
        public bool IsTimedEnraged => timedEnrageLevel > 0;
        protected override bool HasEnrageRim => timedEnrageLevel > 0;

        /// <summary>Хук от WaveManager: зона замедления героя (позиция, радиус, длительность, slowFactor).</summary>
        public void SetHeroSlowZoneSpawner(System.Action<Vector3, float, float, float> spawner) => heroSlowZoneSpawner = spawner;

        protected void SpawnHeroSlowZone(Vector3 position, float radius, float duration, float slowFactor)
        {
            if (radius <= 0f || duration <= 0f) return;
            heroSlowZoneSpawner?.Invoke(position, radius, duration, slowFactor);
        }

        public override bool IsBoss => true;
        public override string BossId => bossData != null ? bossData.bossId : base.BossId;
        public BossData BossData => bossData;

        public virtual void InitializeBoss(
            BossData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            EnemyProjectilePool projectilePool,
            Action<EnemyData, Vector3> onSummonMinion,
            Func<Vector3, float, float, Vector3> safePositionProvider,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);

            bossData = data;
            sharedProjectilePool = projectilePool;
            summonMinionCallback = onSummonMinion;
            getSafePositionNearTarget = safePositionProvider;
            activeBossId = data != null ? data.bossId : "boss";
            isMiniBoss = data != null && data.isMiniBoss;
            hasBossRim = true;
            pullResistance = data != null ? Mathf.Clamp01(data.pullResistance) : 0.15f;
            ApplyVisualState(0f);

            float summonCd = data != null && data.summonCooldown > 0f ? data.summonCooldown : 10f;
            nextSummonTime = Time.time + summonCd * 0.6f;

            bossFightStartTime = Time.time;
            baseDamageMultiplier = currentDamageMultiplier;
            timedEnrageSpeedMultiplier = 1f;
            timedEnrageLevel = 0;

            string displayName = data != null ? Loc.Name(data) : string.Empty;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = Loc.Get("ui.hud.boss", "Boss");
            }

            eventBus?.Publish(new BossSpawnedEvent(BossId, displayName, currentHp, maxHp, isMiniBoss));
        }

        protected override void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            base.Update();

            if (!IsTargetDead())
            {
                UpdateSummoning();
                UpdateTimedEnrage();
            }
        }

        protected override float GetAdditionalSpeedMultiplier() => timedEnrageSpeedMultiplier;

        public static int ComputeTimedEnrageLevel(float elapsed, float enrageAfter, float rampInterval)
        {
            if (enrageAfter <= 0f || elapsed < enrageAfter) return 0;
            return 1 + Mathf.FloorToInt((elapsed - enrageAfter) / Mathf.Max(5f, rampInterval));
        }

        private void UpdateTimedEnrage()
        {
            if (bossData == null || isMiniBoss || bossData.enrageAfterSeconds <= 0f) return;

            int level = ComputeTimedEnrageLevel(Time.time - bossFightStartTime, bossData.enrageAfterSeconds, bossData.enrageRampInterval);
            if (level <= timedEnrageLevel) return;

            timedEnrageLevel = level;
            int steps = level - 1;
            timedEnrageSpeedMultiplier = Mathf.Min(
                Mathf.Max(1f, bossData.enrageMaxSpeedMultiplier),
                bossData.enrageSpeedMultiplier + steps * bossData.enrageRampSpeedStep);
            float dmgMult = Mathf.Min(
                Mathf.Max(1f, bossData.enrageMaxDamageMultiplier),
                bossData.enrageDamageMultiplier + steps * bossData.enrageRampDamageStep);
            currentDamageMultiplier = baseDamageMultiplier * dmgMult;
            OnTimedEnrageLevelChanged(level);
            ApplyVisualState(0f);
            eventBus?.Publish(new BossEnragedEvent(transform.position, BossId, level));
        }

        /// <summary>Хук для боссов: например, Golem прыгает чаще в ярости.</summary>
        protected virtual void OnTimedEnrageLevelChanged(int level) { }

        protected void UpdateSummoning()
        {
            if (bossData == null || bossData.minionData == null || bossData.summonCount <= 0) return;
            if (summonMinionCallback == null) return;

            if (Time.time >= nextSummonTime)
            {
                SummonMinions();
                nextSummonTime = Time.time + Mathf.Max(2f, bossData.summonCooldown);
            }
        }

        protected void SummonMinions()
        {
            if (bossData == null || bossData.minionData == null || summonMinionCallback == null) return;

            int count = bossData.summonCount;
            float radius = 2.5f;
            float angleStep = (Mathf.PI * 2f) / Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                float angle = angleStep * i + UnityEngine.Random.Range(-0.25f, 0.25f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 spawnPos = transform.position + offset;
                spawnPos.y = 0f;
                summonMinionCallback.Invoke(bossData.minionData, spawnPos);
            }
        }

        public override void ApplyPull(Vector3 pullDelta)
        {
            base.ApplyPull(pullDelta);
        }
    }
}
