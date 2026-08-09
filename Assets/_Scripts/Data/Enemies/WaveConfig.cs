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
    }
}
