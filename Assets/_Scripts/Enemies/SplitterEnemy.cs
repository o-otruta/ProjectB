using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// W32 Splitter Slime: divides into 2-3 smaller slimelets upon real death (never on Despawn),
    /// respecting the global active enemy cap via WaveManager's split spawn callback.
    /// </summary>
    public class SplitterEnemy : EnemyBase
    {
        private SplitterEnemyData splitterData;
        private Action<EnemyData, Vector3, float, float> splitSpawnCallback;

        public void SetSplitSpawnCallback(Action<EnemyData, Vector3, float, float> callback)
        {
            splitSpawnCallback = callback;
        }

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);
            splitterData = data as SplitterEnemyData;
        }

        protected override void OnBeforeDeathBroadcast()
        {
            if (splitterData == null || splitterData.splitChildData == null || splitSpawnCallback == null)
            {
                return;
            }

            // Guard against accidental recursive splitting if someone assigns a SplitterEnemyData as child.
            if (splitterData.splitChildData is SplitterEnemyData)
            {
                return;
            }

            int minCount = Mathf.Max(1, splitterData.splitCountMin);
            int maxCount = Mathf.Max(minCount, splitterData.splitCountMax);
            int count = UnityEngine.Random.Range(minCount, maxCount + 1);
            float scatter = Mathf.Max(0.25f, splitterData.splitScatterRadius);
            float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float step = (Mathf.PI * 2f) / count;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + step * i;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * scatter;
                Vector3 spawnPos = transform.position + offset;
                spawnPos.y = 0f;

                splitSpawnCallback.Invoke(
                    splitterData.splitChildData,
                    spawnPos,
                    splitterData.childHpMultiplier,
                    splitterData.childDamageMultiplier);
            }

            eventBus?.Publish(new EnemySplitEvent(transform.position, count));
        }
    }
}
