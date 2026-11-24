using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// W46 Frost Wisp: flying crystalline enemy that glides over ground obstacles
    /// and leaves behind a slowing frost zone when killed.
    /// </summary>
    public class FrostWispEnemy : EnemyBase
    {
        private FrostWispEnemyData wispData;
        private Action<Vector3, FrostWispEnemyData> frostZoneCallback;

        public void SetFrostZoneCallback(Action<Vector3, FrostWispEnemyData> callback)
        {
            frostZoneCallback = callback;
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
            wispData = data as FrostWispEnemyData;
        }

        protected override void UpdateMovement()
        {
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f) return;

            direction.Normalize();
            transform.position += direction * (EffectiveMoveSpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(direction);
        }

        protected override void OnBeforeDeathBroadcast()
        {
            if (wispData == null) return;
            frostZoneCallback?.Invoke(transform.position, wispData);
            eventBus?.Publish(new FrostWispZoneSpawnedEvent(transform.position, wispData.frostZoneRadius));
        }
    }
}
