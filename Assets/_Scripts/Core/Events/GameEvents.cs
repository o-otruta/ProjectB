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

        public EnemyDiedEvent(Vector3 position, EnemyData enemyData, bool wasElite)
        {
            Position = position;
            EnemyData = enemyData;
            WasElite = wasElite;
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
}

