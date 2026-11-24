using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "SplitterEnemyData", menuName = "ProjectB/Data/Enemies/SplitterEnemyData")]
    public class SplitterEnemyData : EnemyData
    {
        [Header("Splitter Mechanics")]
        [Min(1)]
        [Tooltip("Minimum number of smaller slimes spawned on death")]
        public int splitCountMin = 2;

        [Min(1)]
        [Tooltip("Maximum number of smaller slimes spawned on death")]
        public int splitCountMax = 3;

        [Tooltip("EnemyData used for spawned split children (must not itself be a SplitterEnemyData)")]
        public EnemyData splitChildData;

        [Range(0.1f, 1.5f)]
        [Tooltip("HP multiplier applied to split children relative to the current wave scaling")]
        public float childHpMultiplier = 0.42f;

        [Range(0.1f, 1.5f)]
        [Tooltip("Damage multiplier applied to split children relative to the current wave scaling")]
        public float childDamageMultiplier = 0.75f;

        [Min(0.2f)]
        [Tooltip("Scatter radius around the dying splitter where children appear")]
        public float splitScatterRadius = 1.1f;
    }
}
