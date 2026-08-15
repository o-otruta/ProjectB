using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "ProjectB/Arena Config")]
    public class ArenaConfig : ScriptableObject
    {
        [Header("Arena Settings")]
        [Tooltip("Size of the square arena (width and length)")]
        public float ArenaSize = 500f;

        [Tooltip("Seed for procedural generation. 0 means random seed.")]
        public int Seed = 0;

        [Header("Obstacles")]
        [Tooltip("List of prefabs to spawn as obstacles")]
        public GameObject[] ObstaclePrefabs;

        [Tooltip("Minimum number of obstacles to spawn")]
        public int MinObstacles = 10;

        [Tooltip("Maximum number of obstacles to spawn")]
        public int MaxObstacles = 30;

        [Tooltip("Radius around the center (0,0) where no obstacles will spawn")]
        public float SafeZoneRadius = 5f;
    }
}
