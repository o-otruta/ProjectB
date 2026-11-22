using UnityEngine;
using ProjectB.Data.Enemies;
using ProjectB.Abilities;

namespace ProjectB.Core.Events
{
    /// <summary>
    /// Враг получил урон. Публикуется из EnemyBase.TakeDamage()
    /// </summary>
    public struct EnemyDamagedEvent
    {
        public Vector3 Position;
        public int Damage;
        public bool IsBossOrMiniBoss;
        public bool IsCrit;

        public EnemyDamagedEvent(Vector3 position, int damage, bool isBossOrMiniBoss, bool isCrit = false)
        {
            Position = position;
            Damage = damage;
            IsBossOrMiniBoss = isBossOrMiniBoss;
            IsCrit = isCrit;
        }
    }

    /// <summary>
    /// Кристалл XP подобран героем.
    /// </summary>
    public struct XpCollectedEvent
    {
        public int Amount;

        public XpCollectedEvent(int amount)
        {
            Amount = amount;
        }
    }

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
        public EliteArchetype EliteArchetype;

        public EnemyDiedEvent(
            Vector3 position,
            EnemyData enemyData,
            bool wasElite,
            bool wasMiniBoss = false,
            bool wasBoss = false,
            string bossId = null,
            EliteArchetype eliteArchetype = EliteArchetype.None)
        {
            Position = position;
            EnemyData = enemyData;
            WasElite = wasElite;
            WasMiniBoss = wasMiniBoss;
            WasBoss = wasBoss;
            BossId = bossId;
            EliteArchetype = eliteArchetype;
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

    /// <summary>
    /// Огненный шар способности Fireball взорвался в точке попадания.
    /// </summary>
    public struct FireballExplodedEvent
    {
        public Vector3 Position;
        public float Radius;

        public FireballExplodedEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Удар метеора ультимейта Мага в указанной точке.
    /// </summary>
    public struct MeteorImpactEvent
    {
        public Vector3 Position;
        public float Radius;

        public MeteorImpactEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Активирована синергия способностей.
    /// </summary>
    public struct SynergyActivatedEvent
    {
        public string SynergyId;
        public string DisplayName;

        public SynergyActivatedEvent(string synergyId, string displayName)
        {
            SynergyId = synergyId;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Сейсмическая волна способности Seismic Hammer.
    /// </summary>
    public struct SeismicSlamEvent
    {
        public Vector3 Position;
        public float Radius;

        public SeismicSlamEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Детонация врага-подрывника (ExploderEnemy).
    /// </summary>
    public struct EnemyExplodedEvent
    {
        public Vector3 Position;
        public float Radius;

        public EnemyExplodedEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Всплеск ледяной магии босса Frost Lich (ледяные шипы или старт бури).
    /// </summary>
    public struct FrostLichNovaEvent
    {
        public Vector3 Position;
        public float Radius;
        public bool IsBlizzard;

        public FrostLichNovaEvent(Vector3 position, float radius, bool isBlizzard)
        {
            Position = position;
            Radius = radius;
            IsBlizzard = isBlizzard;
        }
    }

    /// <summary>
    /// Волна стрел ультимейта Рейнджера (Arrow Rain).
    /// </summary>
    public struct ArrowRainWaveEvent
    {
        public Vector3 Position;
        public float Radius;
        public int WaveIndex;

        public ArrowRainWaveEvent(Vector3 position, float radius, int waveIndex)
        {
            Position = position;
            Radius = radius;
            WaveIndex = waveIndex;
        }
    }

    /// <summary>
    /// Выброс кольца спектральных черепов ультимейта Некроманта (Soul Nova).
    /// </summary>
    public struct SoulNovaCastEvent
    {
        public Vector3 Position;
        public float Range;
        public int HealAmount;

        public SoulNovaCastEvent(Vector3 position, float range, int healAmount)
        {
            Position = position;
            Range = range;
            HealAmount = healAmount;
        }
    }

    /// <summary>
    /// Взрыв воксельной бочки на арене (ExplosiveBarrel).
    /// </summary>
    public struct BarrelExplodedEvent
    {
        public Vector3 Position;
        public float Radius;
        public int EnemiesHit;

        public BarrelExplodedEvent(Vector3 position, float radius, int enemiesHit)
        {
            Position = position;
            Radius = radius;
            EnemiesHit = enemiesHit;
        }
    }

    /// <summary>
    /// Подбор кристалла-пылесоса (Vacuum Magnet), притянувшего все пикапы на арене.
    /// </summary>
    public struct VacuumMagnetCollectedEvent
    {
        public Vector3 Position;
        public int XpMagnetized;
        public int CoinsMagnetized;

        public VacuumMagnetCollectedEvent(Vector3 position, int xpMagnetized, int coinsMagnetized)
        {
            Position = position;
            XpMagnetized = xpMagnetized;
            CoinsMagnetized = coinsMagnetized;
        }
    }

    public enum ShrineEventType
    {
        Started = 0,
        Progress = 1,
        Completed = 2
    }

    /// <summary>
    /// Событие прогресса или завершения Алтаря Испытания (ChallengeShrine).
    /// </summary>
    public struct ChallengeShrineEvent
    {
        public ShrineEventType EventType;
        public Vector3 Position;
        public int RemainingElites;
        public int TotalElites;
        public int CoinReward;

        public ChallengeShrineEvent(
            ShrineEventType eventType,
            Vector3 position,
            int remainingElites,
            int totalElites,
            int coinReward)
        {
            EventType = eventType;
            Position = position;
            RemainingElites = remainingElites;
            TotalElites = totalElites;
            CoinReward = coinReward;
        }
    }
}

