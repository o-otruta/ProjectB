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

        public bool IsEligibleForWave(int wave)
        {
            if (enemy == null || spawnWeight <= 0f) return false;
            int min = Mathf.Max(1, minWave);
            if (wave < min) return false;
            return maxWave <= 0 || wave <= maxWave;
        }
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

        [Tooltip("Множитель увеличения здоровья и урона врагов с каждой волной (0.2 = +20% за волну)")]
        public float difficultyPerWave = 0.01f;

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

        [Tooltip("Общий материал кольца ауры элитных врагов")]
        public Material eliteAuraMaterial;

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

        [Header("Safety & Wave Flow")]
        [Tooltip("Максимальная длительность волны в секундах. По истечении времени начинается следующая волна; оставшиеся враги не удаляются, а продолжают бой")]
        public float maxWaveDuration = 30f;

        [Tooltip("Максимальное расстояние от героя до врага. Дальше этого враг считается отставшим/застрявшим и телепортируется к герою")]
        public float leashDistance = 35f;

        [Tooltip("Интервал проверки отставших врагов в секундах")]
        public float leashCheckInterval = 2f;

        [Tooltip("Безопасный отступ от внешних стен арены при спавне")]
        public float arenaPadding = 3f;

        public EnemyWaveEntry SelectEnemyEntryForWave(int wave, float normalizedRoll)
        {
            if (enemySchedule == null || enemySchedule.Count == 0) return null;

            float totalWeight = 0f;
            EnemyWaveEntry firstFallback = null;
            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry == null || entry.enemy == null) continue;
                if (firstFallback == null) firstFallback = entry;
                if (entry.IsEligibleForWave(wave))
                {
                    totalWeight += entry.spawnWeight;
                }
            }

            if (totalWeight <= 0f) return firstFallback;

            float target = Mathf.Clamp01(normalizedRoll) * totalWeight;
            float accumulated = 0f;
            EnemyWaveEntry lastEligible = null;
            for (int i = 0; i < enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = enemySchedule[i];
                if (entry == null || !entry.IsEligibleForWave(wave)) continue;

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
