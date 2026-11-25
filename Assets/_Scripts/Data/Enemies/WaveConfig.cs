using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [Serializable]
    public class EnemyWaveEntry
    {
        public EnemyData enemy;

        [Min(1)]
        [Tooltip("First wave on which this enemy can spawn (inclusive)")]
        public int minWave = 1;

        [Min(0)]
        [Tooltip("Last wave on which this enemy can spawn (0 = no upper limit)")]
        public int maxWave = 0;

        [Min(0.01f)]
        [Tooltip("Relative weighted spawn probability among eligible enemies")]
        public float spawnWeight = 1f;

        [Min(1)]
        [Tooltip("Number of enemies spawned together in one cluster")]
        public int packSize = 1;

        [Tooltip("First wave (minWave) acts as an intro wave with a guaranteed spawn share of this enemy")]
        public bool triggerIntroWave = true;

        [Range(0f, 1f)]
        [Tooltip("Fraction of enemies in the intro wave that will be this newly introduced enemy")]
        public float introSpawnShare = 0.5f;

        [Tooltip("Increase packSize on high waves using WaveConfig pack scaling")]
        public bool scalePackSizeWithWave = false;

        public bool IsEligibleForWave(int wave)
        {
            if (enemy == null || spawnWeight <= 0f) return false;
            int min = Mathf.Max(1, minWave);
            if (wave < min) return false;
            return maxWave <= 0 || wave <= maxWave;
        }
    }

    [Serializable]
    public class FtueWaveOverride
    {
        [Min(1)]
        [Tooltip("Number of enemies in this FTUE wave")]
        public int enemyCount = 4;

        [Min(0.05f)]
        [Tooltip("Pause between enemy spawns in this FTUE wave")]
        public float spawnDelay = 0.7f;

        [Min(0.1f)]
        [Tooltip("Enemy HP multiplier for this FTUE wave")]
        public float hpMultiplier = 0.75f;

        [Min(0.1f)]
        [Tooltip("Enemy damage multiplier for this FTUE wave")]
        public float damageMultiplier = 0.85f;
    }

    [CreateAssetMenu(fileName = "WaveConfig", menuName = "ProjectB/Data/Enemies/WaveConfig")]
    public class WaveConfig : ScriptableObject
    {
        [Tooltip("Количество врагов в 1-й волне")]
        public int baseEnemyCount = 10;
        
        [Tooltip("Множитель количества врагов с каждой волной")]
        public float enemiesPerWaveMultiplier = 1.2f;
        
        [Tooltip("Пауза между спавном каждого врага в волне")]
        public float spawnDelay = 0.5f; 
        
        [Tooltip("Пауза между волнами (секунды)")]
        public float waveDelay = 3f;
        
        [Tooltip("Мин. радиус спавна вокруг героя")]
        public float spawnRadiusMin = 15f;
        
        [Tooltip("Макс. радиус спавна вокруг героя")]
        public float spawnRadiusMax = 20f;
        
        [Tooltip("Максимальное количество врагов в одной волне (потолок)")]
        public int maxEnemiesPerWave = 50;

        [Header("Difficulty Scaling Curves")]
        [Min(0f)]
        [Tooltip("Линейный прирост HP врагов за волну (0.12 = +12%/волна)")]
        public float hpLinearPerWave = 0.12f;

        [Min(0f)]
        [Tooltip("Квадратичный прирост HP врагов на высоких волнах")]
        public float hpQuadraticPerWave = 0.002f;

        [Tooltip("Опциональный множитель кривой HP по номеру волны (если заданы ключи)")]
        public AnimationCurve hpCurveMultiplier = new AnimationCurve();

        [Min(0f)]
        [Tooltip("Линейный прирост урона врагов за волну (0.05 = +5%/волна)")]
        public float damageLinearPerWave = 0.05f;

        [Min(0f)]
        [Tooltip("Максимальный потолок множителя урона врагов (0 = без потолка)")]
        public float maxDamageMultiplier = 6.0f;

        [Tooltip("Опциональный множитель кривой урона по номеру волны (если заданы ключи)")]
        public AnimationCurve damageCurveMultiplier = new AnimationCurve();

        [Header("Coin Scaling (Sprint 12.1)")]
        [Min(0f)]
        [Tooltip("Линейный прирост монет с врагов и боссов за волну (0.015 = +1.5%/волна)")]
        public float coinLinearPerWave = 0f;

        [Min(0f)]
        [Tooltip("Максимальный потолок множителя монет по волне (0 = без потолка)")]
        public float maxCoinMultiplier = 2.5f;

        [Header("Phased Enemy Schedule")]
        [Tooltip("Фазовое расписание врагов: единый источник истины для WaveManager")]
        public List<EnemyWaveEntry> enemySchedule = new List<EnemyWaveEntry>();

        [Header("Elite Enemies")]
        [Tooltip("С какой волны начинают появляться элитные враги")]
        public int minWaveForElites = 10;

        [Tooltip("Шанс появления элиты за каждую волну после minWaveForElites (0.02 = 2%)")]
        public float eliteChancePerWave = 0.02f;

        [Tooltip("Максимальный шанс появления элиты")]
        public float maxEliteChance = 0.25f;

        [Tooltip("Множитель здоровья для элитных врагов (Berserker)")]
        public float eliteHpMultiplier = 3f;

        [Tooltip("Множитель урона для элитных врагов (Berserker)")]
        public float eliteDamageMultiplier = 2f;

        [Tooltip("Множитель скорости для элитного архетипа Berserker")]
        public float berserkerSpeedMultiplier = 1.25f;

        [Tooltip("Множитель здоровья для элитного архетипа Juggernaut")]
        public float juggernautHpMultiplier = 4.5f;

        [Tooltip("Множитель урона для элитного архетипа Juggernaut")]
        public float juggernautDamageMultiplier = 1.5f;

        [Tooltip("Множитель размера для элитного архетипа Juggernaut")]
        public float juggernautScaleMultiplier = 1.35f;

        [Range(0f, 1f)]
        [Tooltip("Множитель сопротивления притяжению (0 = иммунитет, 1 = полное притяжение) для Juggernaut")]
        public float juggernautPullResistance = 0.2f;

        [Tooltip("Множитель здоровья для элитного архетипа HasteAura")]
        public float hasteAuraHpMultiplier = 3.0f;

        [Tooltip("Множитель урона для элитного архетипа HasteAura")]
        public float hasteAuraDamageMultiplier = 1.5f;

        [Tooltip("Радиус ауры ускорения союзников для HasteAura")]
        public float hasteAuraRadius = 3.5f;

        [Tooltip("Бонус к скорости союзных врагов в ауре HasteAura (0.2 = +20%)")]
        public float hasteAuraSpeedBonus = 0.2f;

        [Header("Mini-Bosses (Every N Waves)")]
        [Tooltip("Интервал волн для появления мини-босса (каждые 5 волн, кроме босс-волн)")]
        public int miniBossInterval = 5;

        [Tooltip("Множитель HP мини-босса относительно обычного врага")]
        public float miniBossHpMultiplier = 5f;

        [Tooltip("Множитель урона мини-босса")]
        public float miniBossDamageMultiplier = 2f;

        [Tooltip("Масштаб модели мини-босса")]
        public float miniBossScale = 1.85f;

        [Tooltip("Количество XP-кристаллов, выпадающих с мини-босса")]
        public int miniBossXpBurstCount = 5;

        [Tooltip("Количество монет, выпадающих с мини-босса")]
        public int miniBossCoinBurstCount = 5;

        [Header("Bosses (Every N Waves)")]
        [Tooltip("Интервал волн для появления главного босса (каждые 25 волн)")]
        public int bossInterval = 25;

        [Tooltip("Список боссов, чередующихся каждые bossInterval волн (25 = [0], 50 = [1], ...)")]
        public List<BossData> bosses = new List<BossData>();

        [Tooltip("Количество XP-кристаллов, выпадающих с босса")]
        public int bossXpBurstCount = 12;

        [Tooltip("Количество монет, выпадающих с босса")]
        public int bossCoinBurstCount = 15;

        [Header("Vacuum Magnet Drops")]
        [Range(0f, 1f)]
        [Tooltip("Шанс выпадения кристалла-пылесоса (Vacuum Magnet) с мини-босса")]
        public float miniBossVacuumDropChance = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("Шанс выпадения кристалла-пылесоса (Vacuum Magnet) с босса")]
        public float bossVacuumDropChance = 1.0f;

        [Header("Boss Shards — Constellation currency (Sprint 11)")]
        [Min(0)] [Tooltip("Boss Shards granted for defeating a mini-boss")]
        public int miniBossShardReward = 1;
        [Min(0)] [Tooltip("Boss Shards granted for defeating a boss (Golem/Necromancer/Lich/Demon Lord)")]
        public int bossShardReward = 5;
        [Min(0)] [Tooltip("Boss Shards granted for clearing a Challenge Shrine")]
        public int shrineShardReward = 2;

        [Header("Safety & Wave Flow")]
        [Tooltip("Максимальная длительность волны в секундах. По истечении времени начинается следующая волна; оставшиеся враги не удаляются, а продолжают бой")]
        public float maxWaveDuration = 30f;

        [Tooltip("Максимальное расстояние от героя до врага. Дальше этого враг считается отставшим/застрявшим и телепортируется к герою")]
        public float leashDistance = 35f;

        [Tooltip("Интервал проверки отставших врагов в секундах")]
        public float leashCheckInterval = 2f;

        [Header("Anti-Kite: Forward Spawns & Trailing Relocation")]
        [Tooltip("Доля спавнов волны (и переносов отставших), которые ставятся в конус ПО ХОДУ движения героя. " +
                 "Бег по кругу упирается в свежую толпу. 0 = равномерно по кольцу")]
        [Range(0f, 1f)] public float forwardSpawnChance = 0.45f;

        [Tooltip("Полуугол конуса спавна по ходу движения, градусы")]
        [Range(10f, 180f)] public float forwardSpawnConeHalfAngle = 60f;

        [Tooltip("Минимальная скорость героя (м/с), при которой работает смещение спавна вперёд")]
        [Min(0f)] public float forwardSpawnMinHeroSpeed = 1.5f;

        [Tooltip("Переносить врагов, которые остались ЗА экраном позади бегущего героя, на кольцо спавна впереди. " +
                 "Убирает «змею» из мобов за спиной и держит плотность на экране")]
        public bool relocateTrailingEnemies = true;

        [Tooltip("Отступ (м) за радиусом внеэкранного спавна: дальше этого враг позади героя считается отставшим")]
        [Min(0f)] public float trailingRelocateMargin = 3f;

        [Tooltip("Насколько «позади» должен быть враг: dot(направление героя, направление на врага) ниже этого значения")]
        [Range(-1f, 0.5f)] public float trailingBehindDot = -0.2f;

        [Tooltip("Безопасный отступ от внешних стен арены при спавне")]
        public float arenaPadding = 3f;

        [Min(1)]
        [Tooltip("Глобальный лимит одновременно живых врагов на арене (волны, события, призывы).")]
        public int maxActiveEnemies = 300;

        [Header("Wave Events (Sprint 10)")]
        [Tooltip("Включить случайные события волн каждые 3-4 обычные волны")]
        public bool waveEventsEnabled = true;

        [Min(1)]
        [Tooltip("Минимальный номер волны, начиная с которого могут запускаться события волн")]
        public int firstWaveEventWave = 4;

        [Min(1)]
        [Tooltip("Минимальный интервал обычных волн между событиями")]
        public int minWavesBetweenEvents = 3;

        [Min(1)]
        [Tooltip("Максимальный интервал обычных волн между событиями")]
        public int maxWavesBetweenEvents = 4;

        [Tooltip("Враг, спавнящийся кольцом во время Swarm Rush (по умолчанию SkeletonData)")]
        public EnemyData swarmRushEnemyData;

        [Min(1)]
        public int swarmRushMinCount = 24;

        [Min(1)]
        public int swarmRushMaxCount = 32;

        public float swarmRushRadius = 12f;
        public float swarmRushDuration = 12f;

        [Tooltip("Данные убегающего лут-гоблина для события Gold Goblin")]
        public FleeingLootEnemyData goldGoblinData;

        public float goldGoblinDuration = 18f;

        public float bloodMoonDuration = 20f;
        public float bloodMoonEnemySpeedMultiplier = 1.2f;
        public float bloodMoonXpMultiplier = 2f;
        public float bloodMoonCoinMultiplier = 2f;

        public float eliteAmbushRadius = 12f;
        public int eliteAmbushCoinReward = 50;
        public int eliteAmbushCoinBurstCount = 10;

        [Header("FTUE First Run Waves (Sprint 12.2)")]
        [Tooltip("Optional lighter wave overrides applied on waves 1..N during a player's first run (totalRuns == 0)")]
        public List<FtueWaveOverride> ftueWaves = new List<FtueWaveOverride>();

        [Header("Density Scaling & Wave Overlap (Sprint 16.1)")]
        [Min(0.05f)]
        [Tooltip("Minimum pause between spawns once wave size saturates at maxEnemiesPerWave")]
        public float minSpawnDelay = 0.16f;

        [Min(0f)]
        [Tooltip("Reduction applied to spawnDelay per wave after reaching maxEnemiesPerWave")]
        public float spawnDelayReductionPerWave = 0.012f;

        [Tooltip("Start the next saturated wave early once alive enemies drop to <= waveOverlapThreshold of spawned count")]
        public bool enableWaveOverlap = true;

        [Range(0f, 0.75f)]
        [Tooltip("Fraction of current wave spawned enemies remaining that allows the next wave to start early")]
        public float waveOverlapThreshold = 0.30f;

        [Min(1)]
        [Tooltip("First wave on which packSize-scaled entries begin gaining bonus enemies per pack")]
        public int packSizeScalingStartWave = 20;

        [Min(1)]
        [Tooltip("Interval in waves per +1 bonus enemy in packSize-scaled entries")]
        public int packSizeBonusWaveInterval = 20;

        [Min(0)]
        [Tooltip("Maximum bonus enemies added to packSize on high waves")]
        public int maxPackSizeBonus = 2;

        [Header("Corridor Horde Event (Sprint 16.1)")]
        [Tooltip("Enable periodic one-sided Corridor Horde waves roughly every 10 waves")]
        public bool corridorHordeEnabled = true;

        [Min(1)]
        [Tooltip("First eligible wave for Corridor Horde (keeps early FTUE/intro runs W1-W12 untouched)")]
        public int firstCorridorHordeWave = 19;

        [Min(2)]
        [Tooltip("Target interval in waves between Corridor Horde events")]
        public int corridorHordeInterval = 10;

        [Min(3f)]
        [Tooltip("Duration in seconds of the directional Corridor Horde stream")]
        public float corridorHordeDuration = 12f;

        [Min(0.05f)]
        [Tooltip("Seconds between directional horde batches during Corridor Horde")]
        public float corridorHordeSpawnInterval = 0.30f;

        [Min(1)]
        [Tooltip("Number of weak mobs spawned per batch during Corridor Horde")]
        public int corridorHordeBatchSize = 2;

        [Range(0.2f, 1f)]
        [Tooltip("HP multiplier applied to Corridor Horde mobs so AoE builds mow them down")]
        public float corridorHordeHpMultiplier = 0.55f;

        [Min(1f)]
        [Tooltip("Half-width in meters of the directional spawn corridor")]
        public float corridorHordeCorridorHalfWidth = 3.5f;

        [Tooltip("Weak horde enemy spawned during Corridor Horde (defaults to SwarmRush / Skeleton)")]
        public EnemyData corridorHordeEnemyData;

        [Header("Wave Mutators (Sprint 16.3)")]
        [Tooltip("Enable periodic Wave Mutators on W30+ (Fog, Elite Surge, Glass Horde)")]
        public bool waveMutatorsEnabled = true;

        [Min(1)]
        [Tooltip("Minimum wave number on which Wave Mutators can trigger")]
        public int firstWaveMutatorWave = 30;

        [Min(1)]
        [Tooltip("Minimum waves between Wave Mutators")]
        public int minWavesBetweenMutators = 3;

        [Min(1)]
        [Tooltip("Maximum waves between Wave Mutators")]
        public int maxWavesBetweenMutators = 5;

        [Tooltip("Configured Wave Mutator SO assets")]
        public List<WaveMutatorData> waveMutators = new List<WaveMutatorData>();

        public WaveMutatorData FindMutatorByType(WaveMutatorType type)
        {
            if (type == WaveMutatorType.None || waveMutators == null) return null;
            for (int i = 0; i < waveMutators.Count; i++)
            {
                WaveMutatorData m = waveMutators[i];
                if (m != null && m.mutatorType == type)
                {
                    return m;
                }
            }
            return null;
        }

        public int FtueWaveCount => ftueWaves != null ? ftueWaves.Count : 0;

        public bool TryGetFtueWave(int wave, out FtueWaveOverride ftueWave)
        {
            if (wave >= 1 && ftueWaves != null && wave <= ftueWaves.Count)
            {
                ftueWave = ftueWaves[wave - 1];
                return ftueWave != null;
            }
            ftueWave = null;
            return false;
        }

        public bool IsFtueProtectedWave(int wave, bool isFtueRun)
        {
            return isFtueRun && TryGetFtueWave(wave, out _);
        }

        public int GetWaveEnemyCount(int wave, bool isFtueRun)
        {
            if (isFtueRun && TryGetFtueWave(wave, out FtueWaveOverride ftue))
            {
                int cap = maxEnemiesPerWave > 0 ? maxEnemiesPerWave : int.MaxValue;
                return Mathf.Clamp(ftue.enemyCount, 1, cap);
            }
            int w = Mathf.Max(0, wave - 1);
            int count = Mathf.RoundToInt(baseEnemyCount * Mathf.Pow(enemiesPerWaveMultiplier, w));
            return maxEnemiesPerWave > 0 ? Mathf.Min(count, maxEnemiesPerWave) : Mathf.Max(1, count);
        }

        public int GetDensitySaturationWave()
        {
            if (maxEnemiesPerWave <= 0) return int.MaxValue;
            if (baseEnemyCount >= maxEnemiesPerWave) return 1;
            if (enemiesPerWaveMultiplier <= 1.0001f) return int.MaxValue;

            for (int wave = 1; wave <= 200; wave++)
            {
                int w = wave - 1;
                int count = Mathf.RoundToInt(baseEnemyCount * Mathf.Pow(enemiesPerWaveMultiplier, w));
                if (count >= maxEnemiesPerWave)
                {
                    return wave;
                }
            }

            return int.MaxValue;
        }

        public bool IsWaveSaturated(int wave, bool isFtueRun)
        {
            if (isFtueRun || maxEnemiesPerWave <= 0 || wave <= 0) return false;
            return wave >= GetDensitySaturationWave();
        }

        public float GetWaveSpawnDelay(int wave, bool isFtueRun)
        {
            if (isFtueRun && TryGetFtueWave(wave, out FtueWaveOverride ftue))
            {
                return Mathf.Max(0.05f, ftue.spawnDelay);
            }

            float baseDelay = Mathf.Max(0.05f, spawnDelay);
            if (isFtueRun || spawnDelayReductionPerWave <= 0f)
            {
                return baseDelay;
            }

            int satWave = GetDensitySaturationWave();
            if (wave <= satWave)
            {
                return baseDelay;
            }

            float floorDelay = Mathf.Min(baseDelay, Mathf.Max(0.05f, minSpawnDelay));
            float reduced = baseDelay - (wave - satWave) * spawnDelayReductionPerWave;
            return Mathf.Max(floorDelay, reduced);
        }

        public int GetOverlapAliveThreshold(int wave, int spawnedInWave, bool isFtueRun, bool isBossWave)
        {
            if (!enableWaveOverlap || isBossWave || isFtueRun || waveOverlapThreshold <= 0f || spawnedInWave <= 0)
            {
                return 0;
            }

            if (!IsWaveSaturated(wave, isFtueRun))
            {
                return 0;
            }

            return Mathf.FloorToInt(spawnedInWave * Mathf.Clamp01(waveOverlapThreshold));
        }

        public int GetEffectivePackSize(EnemyWaveEntry entry, int wave)
        {
            if (entry == null) return 1;
            int basePack = Mathf.Max(1, entry.packSize);
            if (!entry.scalePackSizeWithWave || maxPackSizeBonus <= 0 || wave < packSizeScalingStartWave)
            {
                return basePack;
            }

            int interval = Mathf.Max(1, packSizeBonusWaveInterval);
            int bonus = Mathf.Min(maxPackSizeBonus, 1 + (wave - packSizeScalingStartWave) / interval);
            return basePack + bonus;
        }

        public float GetEliteChanceForWave(int wave)
        {
            if (wave < minWaveForElites || eliteChancePerWave <= 0f || maxEliteChance <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Min(maxEliteChance, (wave - minWaveForElites + 1) * eliteChancePerWave));
        }

        public float GetHpDifficultyMultiplier(int wave, bool isFtueRun)
        {
            float baseMult = GetHpDifficultyMultiplier(wave);
            if (isFtueRun && TryGetFtueWave(wave, out FtueWaveOverride ftue))
            {
                return Mathf.Max(0.1f, baseMult * Mathf.Max(0.1f, ftue.hpMultiplier));
            }
            return baseMult;
        }

        public float GetDamageDifficultyMultiplier(int wave, bool isFtueRun)
        {
            float baseMult = GetDamageDifficultyMultiplier(wave);
            if (isFtueRun && TryGetFtueWave(wave, out FtueWaveOverride ftue))
            {
                return Mathf.Max(0.1f, baseMult * Mathf.Max(0.1f, ftue.damageMultiplier));
            }
            return baseMult;
        }

        public float GetHpDifficultyMultiplier(int wave)
        {
            int w = Mathf.Max(0, wave - 1);
            float mult = 1f + w * hpLinearPerWave + w * w * hpQuadraticPerWave;
            if (hpCurveMultiplier != null && hpCurveMultiplier.length > 0)
            {
                mult *= Mathf.Max(0.1f, hpCurveMultiplier.Evaluate(wave));
            }
            return Mathf.Max(1f, mult);
        }

        public float GetDamageDifficultyMultiplier(int wave)
        {
            int w = Mathf.Max(0, wave - 1);
            float mult = 1f + w * damageLinearPerWave;
            if (damageCurveMultiplier != null && damageCurveMultiplier.length > 0)
            {
                mult *= Mathf.Max(0.1f, damageCurveMultiplier.Evaluate(wave));
            }
            if (maxDamageMultiplier > 0f)
            {
                mult = Mathf.Min(mult, maxDamageMultiplier);
            }
            return Mathf.Max(1f, mult);
        }

        public float GetCoinMultiplier(int wave)
        {
            int w = Mathf.Max(0, wave - 1);
            float mult = 1f + w * Mathf.Max(0f, coinLinearPerWave);
            if (maxCoinMultiplier > 0f)
            {
                mult = Mathf.Min(mult, maxCoinMultiplier);
            }
            return Mathf.Max(1f, mult);
        }

        public EnemyWaveEntry GetIntroEntryForWave(int wave)
        {
            if (enemySchedule == null || wave <= 0) return null;

            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry == null || !entry.triggerIntroWave || entry.introSpawnShare <= 0f) continue;
                if (Mathf.Max(1, entry.minWave) == wave && entry.IsEligibleForWave(wave))
                {
                    return entry;
                }
            }

            return null;
        }

        public EnemyWaveEntry SelectEnemyEntryForWave(int wave, float normalizedRoll)
        {
            return SelectEnemyEntryForWave(wave, normalizedRoll, excludeEntry: null);
        }

        public EnemyWaveEntry SelectEnemyEntryForWave(int wave, float normalizedRoll, EnemyWaveEntry excludeEntry)
        {
            if (enemySchedule == null || enemySchedule.Count == 0) return null;

            float totalWeight = 0f;
            EnemyWaveEntry firstFallback = null;
            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry == null || entry.enemy == null) continue;
                if (firstFallback == null) firstFallback = entry;
                if (excludeEntry != null && ReferenceEquals(entry, excludeEntry)) continue;
                if (entry.IsEligibleForWave(wave))
                {
                    totalWeight += entry.spawnWeight;
                }
            }

            if (totalWeight <= 0f)
            {
                return excludeEntry != null ? SelectEnemyEntryForWave(wave, normalizedRoll, null) : firstFallback;
            }

            float target = Mathf.Clamp01(normalizedRoll) * totalWeight;
            float accumulated = 0f;
            EnemyWaveEntry lastEligible = null;
            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry == null || !entry.IsEligibleForWave(wave)) continue;
                if (excludeEntry != null && ReferenceEquals(entry, excludeEntry)) continue;

                lastEligible = entry;
                accumulated += entry.spawnWeight;
                if (accumulated >= target)
                {
                    return entry;
                }
            }

            return lastEligible ?? firstFallback;
        }

        public EnemyData SelectMiniBossEnemyForWave(int wave, float normalizedRoll)
        {
            if (enemySchedule == null || enemySchedule.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry == null || !entry.IsEligibleForWave(wave)) continue;
                if (entry.enemy is ExploderEnemyData) continue;
                totalWeight += entry.spawnWeight;
            }

            if (totalWeight > 0f)
            {
                float target = Mathf.Clamp01(normalizedRoll) * totalWeight;
                float accumulated = 0f;
                EnemyData lastEligible = null;
                for (int i = 0; i < enemySchedule.Count; i++)
                {
                    EnemyWaveEntry entry = enemySchedule[i];
                    if (entry == null || !entry.IsEligibleForWave(wave)) continue;
                    if (entry.enemy is ExploderEnemyData) continue;

                    lastEligible = entry.enemy;
                    accumulated += entry.spawnWeight;
                    if (accumulated >= target)
                    {
                        return entry.enemy;
                    }
                }

                if (lastEligible != null) return lastEligible;
            }

            return SelectEnemyEntryForWave(wave, normalizedRoll)?.enemy;
        }

        public int GetEligibleEntriesForWave(int wave, List<EnemyWaveEntry> results)
        {
            if (results == null) return 0;
            results.Clear();
            if (enemySchedule == null) return 0;

            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry != null && entry.IsEligibleForWave(wave))
                {
                    results.Add(entry);
                }
            }

            return results.Count;
        }
    }
}
