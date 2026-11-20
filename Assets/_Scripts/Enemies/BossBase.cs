using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
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
            float difficultyMultiplier = 1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier);

            bossData = data;
            sharedProjectilePool = projectilePool;
            summonMinionCallback = onSummonMinion;
            getSafePositionNearTarget = safePositionProvider;
            activeBossId = data != null ? data.bossId : "boss";
            isMiniBoss = data != null && data.isMiniBoss;

            float summonCd = data != null && data.summonCooldown > 0f ? data.summonCooldown : 10f;
            nextSummonTime = Time.time + summonCd * 0.6f;

            string displayName = data != null && !string.IsNullOrEmpty(data.bossDisplayName)
                ? data.bossDisplayName
                : "Boss";

            eventBus?.Publish(new BossSpawnedEvent(BossId, displayName, currentHp, maxHp, isMiniBoss));
        }

        protected override void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            base.Update();

            if (!IsTargetDead())
            {
                UpdateSummoning();
            }
        }

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
            if (!isAlive) return;
            float mult = bossData != null ? bossData.pullResistance : 0.15f;
            if (mult <= 0f) return;
            transform.position += pullDelta * mult;
        }
    }
}
