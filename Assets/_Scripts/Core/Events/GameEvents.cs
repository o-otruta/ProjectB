using UnityEngine;
using ProjectB.Data;
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
        public bool IsBlocked;

        public EnemyDamagedEvent(Vector3 position, int damage, bool isBossOrMiniBoss, bool isCrit = false, bool isBlocked = false)
        {
            Position = position;
            Damage = damage;
            IsBossOrMiniBoss = isBossOrMiniBoss;
            IsCrit = isCrit;
            IsBlocked = isBlocked;
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
        public int ShardMultiplier;

        public EnemyDiedEvent(
            Vector3 position,
            EnemyData enemyData,
            bool wasElite,
            bool wasMiniBoss = false,
            bool wasBoss = false,
            string bossId = null,
            EliteArchetype eliteArchetype = EliteArchetype.None,
            int shardMultiplier = 1)
        {
            Position = position;
            EnemyData = enemyData;
            WasElite = wasElite;
            WasMiniBoss = wasMiniBoss;
            WasBoss = wasBoss;
            BossId = bossId;
            EliteArchetype = eliteArchetype;
            ShardMultiplier = shardMultiplier > 1 ? shardMultiplier : 1;
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

    /// <summary>
    /// Ударная волна ультимейта Рыцаря (Aegis Shield) при активации.
    /// </summary>
    public struct ShieldShockwaveEvent
    {
        public Vector3 Position;
        public float Radius;

        public ShieldShockwaveEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    public enum SynergyReactionVfxType
    {
        ToxicInfernoIgnite = 0,
        SteamBlastNova = 1,
        ThunderstormShatter = 2,
        AegisPhalanxWave = 3,
        CataclysmShards = 4,
        DragonBurn = 5,
        BladeStormVolley = 6,
        ShatterNova = 7,
        RicochetBarrage = 8,
        SolarEclipse = 9,
        PhantomLegion = 10
    }

    /// <summary>
    /// Визуальный и аудио-отклик сработавшей реактивной синергии.
    /// </summary>
    public struct SynergyReactionVfxEvent
    {
        public SynergyReactionVfxType ReactionType;
        public Vector3 Position;
        public float Radius;

        public SynergyReactionVfxEvent(SynergyReactionVfxType reactionType, Vector3 position, float radius)
        {
            ReactionType = reactionType;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Способность достигла порога Overdrive (каждые 10 уровней: Lv 10, Lv 20, Lv 30...).
    /// </summary>
    public struct AbilityOverdriveEvent
    {
        public string AbilityId;
        public string AbilityName;
        public int Level;
        public int Tier;
        public string TierLabel;

        public AbilityOverdriveEvent(string abilityId, string abilityName, int level, int tier, string tierLabel)
        {
            AbilityId = abilityId;
            AbilityName = abilityName;
            Level = level;
            Tier = tier;
            TierLabel = tierLabel;
        }
    }

    /// <summary>
    /// Достигнут рубеж комбо-убийств (x15, x30, x50, x100).
    /// </summary>
    public struct ComboMilestoneEvent
    {
        public int ComboCount;
        public int MilestoneTier;
        public float RewardBonusPercent;

        public ComboMilestoneEvent(int comboCount, int milestoneTier, float rewardBonusPercent)
        {
            ComboCount = comboCount;
            MilestoneTier = milestoneTier;
            RewardBonusPercent = rewardBonusPercent;
        }
    }

    /// <summary>
    /// Универсальный запрос на проигрывание SFX от способностей и реакций без прямой зависимости от AudioManager.
    /// </summary>
    public struct AbilitySfxEvent
    {
        public SfxType SfxType;
        public float VolumeScale;

        public AbilitySfxEvent(SfxType sfxType, float volumeScale = 1f)
        {
            SfxType = sfxType;
            VolumeScale = volumeScale;
        }
    }

    /// <summary>
    /// Началось динамическое событие волны (Swarm Rush, Gold Goblin, Blood Moon, Elite Ambush).
    /// </summary>
    public struct WaveEventStartedEvent
    {
        public WaveEventType EventType;
        public string Title;
        public string Subtitle;
        public float Duration;

        public WaveEventStartedEvent(WaveEventType eventType, string title, string subtitle, float duration)
        {
            EventType = eventType;
            Title = title;
            Subtitle = subtitle;
            Duration = duration;
        }
    }

    /// <summary>
    /// Динамическое событие волны завершилось (по таймеру или после победы над целью).
    /// </summary>
    public struct WaveEventEndedEvent
    {
        public WaveEventType EventType;
        public bool Completed;
        public Vector3 RewardPosition;

        public WaveEventEndedEvent(WaveEventType eventType, bool completed, Vector3 rewardPosition)
        {
            EventType = eventType;
            Completed = completed;
            RewardPosition = rewardPosition;
        }
    }

    /// <summary>
    /// Призыватель (SummonerEnemy) призвал миньонов и исцелил ближайших союзников.
    /// </summary>
    public struct EnemySummonHealEvent
    {
        public Vector3 Position;
        public float Radius;
        public int HealedCount;

        public EnemySummonHealEvent(Vector3 position, float radius, int healedCount)
        {
            Position = position;
            Radius = radius;
            HealedCount = healedCount;
        }
    }

    /// <summary>
    /// Босс Demon Lord перешёл во 2-ю фазу ярости (< 50% HP).
    /// </summary>
    public struct DemonLordEnragedEvent
    {
        public Vector3 Position;

        public DemonLordEnragedEvent(Vector3 position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Sprint 11: Boss Shards awarded during a run (mini-boss +1, boss +5, shrine +2).
    /// </summary>
    public struct BossShardsGainedEvent
    {
        public Vector3 Position;
        public int Amount;
        public int RunTotal;

        public BossShardsGainedEvent(Vector3 position, int amount, int runTotal)
        {
            Position = position;
            Amount = amount;
            RunTotal = runTotal;
        }
    }

    /// <summary>
    /// Sprint 11: Keystone "Second Wind" saved the hero from a lethal hit.
    /// </summary>
    public struct SecondWindTriggeredEvent
    {
        public Vector3 Position;
        public float Radius;
        public int PushedEnemies;

        public SecondWindTriggeredEvent(Vector3 position, float radius, int pushedEnemies)
        {
            Position = position;
            Radius = radius;
            PushedEnemies = pushedEnemies;
        }
    }

    public enum AbilityVfxType
    {
        SkyfallTelegraph = 0,
        SkyfallStrike = 1,
        GlacialFreeze = 2,
        GlacialShatter = 3,
        DecoyDeploy = 4,
        DecoyDetonate = 5,
        ChakramRicochet = 6,
        ConsecrationCast = 7,
        ConsecrationPulse = 8,
        TurretDrop = 9,
        VampiricHeal = 10
    }

    /// <summary>
    /// Sprint 11: generic visual/audio feedback for the new abilities and ultimates.
    /// </summary>
    public struct AbilityVfxEvent
    {
        public AbilityVfxType VfxType;
        public Vector3 Position;
        public float Radius;

        public AbilityVfxEvent(AbilityVfxType vfxType, Vector3 position, float radius)
        {
            VfxType = vfxType;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Sprint 16.2: Splitter Slime split into smaller slimelets upon death.
    /// </summary>
    public struct EnemySplitEvent
    {
        public Vector3 Position;
        public int SpawnedCount;

        public EnemySplitEvent(Vector3 position, int spawnedCount)
        {
            Position = position;
            SpawnedCount = spawnedCount;
        }
    }

    /// <summary>
    /// Sprint 16.2: Frost Wisp spawned a slowing frost zone on death.
    /// </summary>
    public struct FrostWispZoneSpawnedEvent
    {
        public Vector3 Position;
        public float Radius;

        public FrostWispZoneSpawnedEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Sprint 16.2: Siege Golemite's stone shell shattered into Phase 2 (exposed fast core).
    /// </summary>
    public struct SiegeShellBrokenEvent
    {
        public Vector3 Position;
        public float Scale;

        public SiegeShellBrokenEvent(Vector3 position, float scale)
        {
            Position = position;
            Scale = scale;
        }
    }

    /// <summary>
    /// Sprint 16.3: A wave mutator (Fog, Elite Surge, Glass Horde) started for N waves.
    /// </summary>
    public struct WaveMutatorStartedEvent
    {
        public WaveMutatorType MutatorType;
        public string Title;
        public string Subtitle;
        public int DurationWaves;
        public int StartWave;

        public WaveMutatorStartedEvent(
            WaveMutatorType mutatorType,
            string title,
            string subtitle,
            int durationWaves,
            int startWave)
        {
            MutatorType = mutatorType;
            Title = title;
            Subtitle = subtitle;
            DurationWaves = durationWaves;
            StartWave = startWave;
        }
    }

    /// <summary>
    /// Sprint 16.3: Active wave mutator ended cleanly.
    /// </summary>
    public struct WaveMutatorEndedEvent
    {
        public WaveMutatorType MutatorType;
        public bool Completed;

        public WaveMutatorEndedEvent(WaveMutatorType mutatorType, bool completed)
        {
            MutatorType = mutatorType;
            Completed = completed;
        }
    }
}
