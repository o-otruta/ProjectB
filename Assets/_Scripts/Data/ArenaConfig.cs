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

        [Header("Decorations")]
        [Tooltip("List of prefabs to spawn as decorations (e.g. grass)")]
        public GameObject[] DecorationPrefabs;

        [Tooltip("Minimum number of decorations to spawn")]
        public int MinDecorations = 50;

        [Tooltip("Maximum number of decorations to spawn")]
        public int MaxDecorations = 100;

        [Header("Interactive Arena Objects")]
        [Tooltip("Explosive voxel barrel prefab spawned around the arena")]
        public GameObject ExplosiveBarrelPrefab;

        [Min(0)]
        [Tooltip("Minimum number of explosive barrels per arena")]
        public int MinExplosiveBarrels = 8;

        [Min(0)]
        [Tooltip("Maximum number of explosive barrels per arena")]
        public int MaxExplosiveBarrels = 12;

        [Min(0f)]
        [Tooltip("Minimum distance from arena center for explosive barrels")]
        public float BarrelMinDistanceFromCenter = 12f;

        [Min(0f)]
        [Tooltip("Maximum distance from arena center for explosive barrels")]
        public float BarrelMaxDistanceFromCenter = 65f;

        [Tooltip("Challenge shrine prefab spawned once per arena")]
        public GameObject ChallengeShrinePrefab;

        [Min(0f)]
        [Tooltip("Minimum distance from arena center for the Challenge Shrine")]
        public float ShrineMinDistanceFromCenter = 18f;

        [Min(0f)]
        [Tooltip("Maximum distance from arena center for the Challenge Shrine")]
        public float ShrineMaxDistanceFromCenter = 48f;
    }
}
