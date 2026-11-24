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
                float waveCoinMult = _waveManager?.Config != null && _waveManager.CurrentWave > 0
                    ? _waveManager.Config.GetCoinMultiplier(_waveManager.CurrentWave)
                    : 1f;

                int xpAmount = evt.EnemyData.xpDrop > 0 ? Mathf.Max(1, Mathf.RoundToInt(evt.EnemyData.xpDrop * xpMult)) : 0;
                float exactCoinAmount = evt.EnemyData.coinDrop > 0 ? evt.EnemyData.coinDrop * coinMult * waveCoinMult : 0f;
                float coinChance = Mathf.Clamp01(evt.EnemyData.coinDropChance * coinMult);

                if (evt.WasBoss)
                {
                    int xpBursts = _waveManager?.Config != null ? _waveManager.Config.bossXpBurstCount : 12;
                    int coinBursts = _waveManager?.Config != null ? _waveManager.Config.bossCoinBurstCount : 15;
                    SpawnBurstDrops(evt.Position, xpAmount, xpBursts, exactCoinAmount, coinBursts, 2.2f);
                }
                else if (evt.WasMiniBoss)
                {
                    int xpBursts = _waveManager?.Config != null ? _waveManager.Config.miniBossXpBurstCount : 5;
                    int coinBursts = _waveManager?.Config != null ? _waveManager.Config.miniBossCoinBurstCount : 5;
                    SpawnBurstDrops(evt.Position, xpAmount, xpBursts, exactCoinAmount, coinBursts, 1.4f);
                }
                else
                {
                    bool dropXp = _xpManager != null && xpAmount > 0;
                    bool dropCoin = _coinManager != null && exactCoinAmount > 0f && UnityEngine.Random.value < coinChance;
                    int coinAmount = dropCoin ? RollProbabilisticAmount(exactCoinAmount) : 0;
                    dropCoin = dropCoin && coinAmount > 0;

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

        private static int RollProbabilisticAmount(float exactAmount)
        {
            if (exactAmount <= 0f) return 0;
            int floor = Mathf.FloorToInt(exactAmount);
            float frac = exactAmount - floor;
            if (frac > 0f && UnityEngine.Random.value < frac)
            {
                floor++;
            }
            return floor;
        }

        private void SpawnBurstDrops(Vector3 center, int xpAmountPerCrystal, int xpCount, float exactCoinAmountPerDrop, int coinCount, float spreadRadius)
        {
            if (_xpManager != null && xpAmountPerCrystal > 0)
            {
                for (int i = 0; i < xpCount; i++)
                {
                    Vector2 rand = UnityEngine.Random.insideUnitCircle * spreadRadius;
                    _xpManager.SpawnXp(center + new Vector3(rand.x, 0f, rand.y), xpAmountPerCrystal);
                }
            }

            if (_coinManager != null && exactCoinAmountPerDrop > 0f)
            {
                for (int i = 0; i < coinCount; i++)
                {
                    int coinAmount = RollProbabilisticAmount(exactCoinAmountPerDrop);
                    if (coinAmount <= 0) continue;
                    Vector2 rand = UnityEngine.Random.insideUnitCircle * spreadRadius;
                    _coinManager.SpawnCoin(center + new Vector3(rand.x, 0f, rand.y), coinAmount);
                }
            }
        }
    }
}
