using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.LevelUp;
using ProjectB.Meta;

namespace ProjectB.Enemies
{
    public class EnemyDeathHandler : IStartable, IDisposable
    {
        private readonly XpManager _xpManager;
        private readonly CoinManager _coinManager;
        private readonly RunStatistics _runStatistics;
        private readonly AchievementManager _achievementManager;
        private readonly GameEventBus _eventBus;

        [Inject]
        public EnemyDeathHandler(
            XpManager xpManager,
            CoinManager coinManager,
            RunStatistics runStatistics,
            AchievementManager achievementManager,
            GameEventBus eventBus)
        {
            _xpManager = xpManager;
            _coinManager = coinManager;
            _runStatistics = runStatistics;
            _achievementManager = achievementManager;
            _eventBus = eventBus;
        }

        public void Start()
        {
            _eventBus?.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        public void Dispose()
        {
            _eventBus?.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            Vector2 rand = UnityEngine.Random.insideUnitCircle * 0.5f;
            Vector3 dropPos = evt.Position + new Vector3(rand.x, 0f, rand.y);

            if (evt.EnemyData != null)
            {
                if (_xpManager != null && evt.EnemyData.xpDrop > 0)
                {
                    _xpManager.SpawnXp(dropPos, evt.EnemyData.xpDrop);
                }

                if (_coinManager != null && evt.EnemyData.coinDrop > 0 && UnityEngine.Random.value < evt.EnemyData.coinDropChance)
                {
                    _coinManager.SpawnCoin(dropPos, evt.EnemyData.coinDrop);
                }
            }

            if (_runStatistics != null)
            {
                _runStatistics.AddKill();
            }

            if (_achievementManager != null)
            {
                _achievementManager.OnEnemyKilled();
            }
        }
    }
}

