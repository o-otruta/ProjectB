using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Arena;
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
        private readonly WaveManager _waveManager;
        private readonly VacuumMagnetSpawner _vacuumMagnetSpawner;
        private readonly RunComboTracker _comboTracker;
        private readonly WaveEventManager _waveEventManager;

        [Inject]
        public EnemyDeathHandler(
            XpManager xpManager,
            CoinManager coinManager,
            RunStatistics runStatistics,
            AchievementManager achievementManager,
            GameEventBus eventBus,
            WaveManager waveManager = null,
            VacuumMagnetSpawner vacuumMagnetSpawner = null,
            RunComboTracker comboTracker = null,
            WaveEventManager waveEventManager = null)
        {
            _xpManager = xpManager;
            _coinManager = coinManager;
            _runStatistics = runStatistics;
            _achievementManager = achievementManager;
            _eventBus = eventBus;
            _waveManager = waveManager;
            _vacuumMagnetSpawner = vacuumMagnetSpawner;
            _comboTracker = comboTracker;
            _waveEventManager = waveEventManager;
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
            if (evt.EnemyData != null)
            {
                bool isBloodMoon = _waveEventManager != null && _waveEventManager.IsBloodMoonActive;
                float xpMult = isBloodMoon ? _waveEventManager.BloodMoonXpMultiplier : 1f;
                float coinMult = isBloodMoon ? _waveEventManager.BloodMoonCoinMultiplier : 1f;

                int xpAmount = evt.EnemyData.xpDrop > 0 ? Mathf.Max(1, Mathf.RoundToInt(evt.EnemyData.xpDrop * xpMult)) : 0;
                int coinAmount = evt.EnemyData.coinDrop > 0 ? Mathf.Max(1, Mathf.RoundToInt(evt.EnemyData.coinDrop * coinMult)) : 0;
                float coinChance = Mathf.Clamp01(evt.EnemyData.coinDropChance * coinMult);

                if (evt.WasBoss)
                {
                    int xpBursts = _waveManager?.Config != null ? _waveManager.Config.bossXpBurstCount : 12;
                    int coinBursts = _waveManager?.Config != null ? _waveManager.Config.bossCoinBurstCount : 15;
                    SpawnBurstDrops(evt.Position, xpAmount, xpBursts, coinAmount, coinBursts, 2.2f);
                }
                else if (evt.WasMiniBoss)
                {
                    int xpBursts = _waveManager?.Config != null ? _waveManager.Config.miniBossXpBurstCount : 5;
                    int coinBursts = _waveManager?.Config != null ? _waveManager.Config.miniBossCoinBurstCount : 5;
                    SpawnBurstDrops(evt.Position, xpAmount, xpBursts, coinAmount, coinBursts, 1.4f);
                }
                else
                {
                    bool dropXp = _xpManager != null && xpAmount > 0;
                    bool dropCoin = _coinManager != null && coinAmount > 0 && UnityEngine.Random.value < coinChance;

                    if (dropXp && dropCoin)
                    {
                        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                        float radius = UnityEngine.Random.Range(0.4f, 0.55f);
                        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                        _xpManager.SpawnXp(evt.Position + offset, xpAmount);
                        _coinManager.SpawnCoin(evt.Position - offset, coinAmount);
                    }
                    else if (dropXp)
                    {
                        Vector2 rand = UnityEngine.Random.insideUnitCircle * 0.4f;
                        _xpManager.SpawnXp(evt.Position + new Vector3(rand.x, 0f, rand.y), xpAmount);
                    }
                    else if (dropCoin)
                    {
                        Vector2 rand = UnityEngine.Random.insideUnitCircle * 0.4f;
                        _coinManager.SpawnCoin(evt.Position + new Vector3(rand.x, 0f, rand.y), coinAmount);
                    }
                }
            }

            if (evt.WasBoss || evt.WasMiniBoss)
            {
                _vacuumMagnetSpawner?.TryDropFromEnemyDeath(
                    evt.Position,
                    evt.WasBoss,
                    evt.WasMiniBoss,
                    UnityEngine.Random.value);
            }

            if (_runStatistics != null)
            {
                _runStatistics.AddKill();
            }

            _comboTracker?.RegisterKill();

            if (_achievementManager != null)
            {
                _achievementManager.OnEnemyKilled();
                if (evt.WasBoss && !string.IsNullOrEmpty(evt.BossId))
                {
                    _achievementManager.OnBossDefeated(evt.BossId);
                }
            }
        }

        private void SpawnBurstDrops(Vector3 center, int xpAmountPerCrystal, int xpCount, int coinAmountPerDrop, int coinCount, float spreadRadius)
        {
            if (_xpManager != null && xpAmountPerCrystal > 0)
            {
                for (int i = 0; i < xpCount; i++)
                {
                    Vector2 rand = UnityEngine.Random.insideUnitCircle * spreadRadius;
                    _xpManager.SpawnXp(center + new Vector3(rand.x, 0f, rand.y), xpAmountPerCrystal);
                }
            }

            if (_coinManager != null && coinAmountPerDrop > 0)
            {
                for (int i = 0; i < coinCount; i++)
                {
                    Vector2 rand = UnityEngine.Random.insideUnitCircle * spreadRadius;
                    _coinManager.SpawnCoin(center + new Vector3(rand.x, 0f, rand.y), coinAmountPerDrop);
                }
            }
        }
    }
}
