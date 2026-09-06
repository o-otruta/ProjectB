using UnityEngine;

namespace ProjectB.Data.Enemies
{
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

        [Header("Elite Enemies")]
        [Tooltip("С какой волны начинают появляться элитные враги")]
        public int minWaveForElites = 10;

        [Tooltip("Шанс появления элиты за каждую волну после minWaveForElites (0.02 = 2%)")]
        public float eliteChancePerWave = 0.02f;

        [Tooltip("Максимальный шанс появления элиты")]
        public float maxEliteChance = 0.25f;

        [Tooltip("Множитель здоровья для элитных врагов")]
        public float eliteHpMultiplier = 3f;

        [Tooltip("Множитель урона для элитных врагов")]
        public float eliteDamageMultiplier = 2f;

        [Header("Safety & Wave Flow")]
        [Tooltip("Максимальная длительность волны в секундах (таймаут). По истечении волна принудительно завершается (0 = без таймаута)")]
        public float maxWaveDuration = 60f;

        [Tooltip("Максимальное расстояние от героя до врага. Дальше этого враг считается отставшим/застрявшим и телепортируется к герою")]
        public float leashDistance = 35f;

        [Tooltip("Интервал проверки отставших врагов в секундах")]
        public float leashCheckInterval = 2f;

        [Tooltip("Безопасный отступ от внешних стен арены при спавне")]
        public float arenaPadding = 3f;
    }
}
