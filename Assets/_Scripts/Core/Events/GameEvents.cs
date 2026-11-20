using UnityEngine;
using ProjectB.Data.Enemies;
using ProjectB.Abilities;

namespace ProjectB.Core.Events
{
    /// <summary>
    /// Враг погиб. Публикуется из EnemyBase.Die()
    /// </summary>
    public struct EnemyDiedEvent
    {
        public Vector3 Position;
        public EnemyData EnemyData;
        public bool WasElite;
        public bool WasMiniBoss;
        public bool WasBoss;
        public string BossId;

        public EnemyDiedEvent(
            Vector3 position,
            EnemyData enemyData,
            bool wasElite,
            bool wasMiniBoss = false,
            bool wasBoss = false,
            string bossId = null)
        {
            Position = position;
            EnemyData = enemyData;
            WasElite = wasElite;
            WasMiniBoss = wasMiniBoss;
            WasBoss = wasBoss;
            BossId = bossId;
        }
    }

    /// <summary>
    /// Появился босс или мини-босс.
    /// </summary>
    public struct BossSpawnedEvent
    {
        public string BossId;
        public string DisplayName;
        public int CurrentHp;
        public int MaxHp;
        public bool IsMiniBoss;

        public BossSpawnedEvent(string bossId, string displayName, int currentHp, int maxHp, bool isMiniBoss)
        {
            BossId = bossId;
            DisplayName = displayName;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            IsMiniBoss = isMiniBoss;
        }
    }

    /// <summary>
    /// Изменилось здоровье активного босса или мини-босса.
    /// </summary>
    public struct BossHealthChangedEvent
    {
        public string BossId;
        public int CurrentHp;
        public int MaxHp;

        public BossHealthChangedEvent(string bossId, int currentHp, int maxHp)
        {
            BossId = bossId;
            CurrentHp = currentHp;
            MaxHp = maxHp;
        }
    }

    /// <summary>
    /// Босс или мини-босс побеждён.
    /// </summary>
    public struct BossDefeatedEvent
    {
        public string BossId;
        public bool IsMiniBoss;

        public BossDefeatedEvent(string bossId, bool isMiniBoss)
        {
            BossId = bossId;
            IsMiniBoss = isMiniBoss;
        }
    }

    /// <summary>
    /// Волна врагов завершена. Публикуется из WaveManager.CheckWaveEnd()
    /// </summary>
    public struct WaveCompletedEvent
    {
        public int CompletedWave;
        public int NextWave;

        public WaveCompletedEvent(int completedWave, int nextWave)
        {
            CompletedWave = completedWave;
            NextWave = nextWave;
        }
    }

    /// <summary>
    /// Герой получил новый уровень. Публикуется из HeroExperience.LevelUp()
    /// </summary>
    public struct HeroLeveledUpEvent
    {
        public int NewLevel;

        public HeroLeveledUpEvent(int newLevel)
        {
            NewLevel = newLevel;
        }
    }

    /// <summary>
    /// Новая способность добавлена герою. Публикуется из HeroAbilities.AddAbility()
    /// </summary>
    public struct AbilityUnlockedEvent
    {
        public string AbilityId;
        public AbilityType AbilityType;

        public AbilityUnlockedEvent(string abilityId, AbilityType abilityType)
        {
            AbilityId = abilityId;
            AbilityType = abilityType;
        }
    }

    /// <summary>
    /// Герой погиб. Публикуется из HeroHealth.Die()
    /// </summary>
    public struct HeroDiedEvent
    {
    }

    /// <summary>
    /// Монета подобрана. Публикуется при зачислении монет.
    /// </summary>
    public struct CoinCollectedEvent
    {
        public int Amount;

        public CoinCollectedEvent(int amount)
        {
            Amount = amount;
        }
    }

    /// <summary>
    /// Завершение игры (победа или поражение). Публикуется из GameManager.HandleHeroDeath()
    /// </summary>
    public struct GameOverEvent
    {
        public int WaveReached;
        public int CoinsEarned;

        public GameOverEvent(int waveReached, int coinsEarned)
        {
            WaveReached = waveReached;
            CoinsEarned = coinsEarned;
        }
    }

    /// <summary>
    /// Секретный предмет на арене подобран героем.
    /// </summary>
    public struct SecretItemCollectedEvent
    {
        public string ItemId;
        public string DisplayName;
        public string RewardSummary;

        public SecretItemCollectedEvent(string itemId, string displayName, string rewardSummary)
        {
            ItemId = itemId;
            DisplayName = displayName;
            RewardSummary = rewardSummary;
        }
    }
}

