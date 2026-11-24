using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "FrostWispEnemyData", menuName = "ProjectB/Data/Enemies/FrostWispEnemyData")]
    public class FrostWispEnemyData : EnemyData
    {
        [Header("Frost Wisp Flight & Death Zone")]
        [Min(0f)]
        [Tooltip("Visual hover elevation above the ground")]
        public float hoverHeight = 1.15f;

        [Min(0.5f)]
        [Tooltip("Radius of the frost slowing zone left behind when the wisp dies")]
        public float frostZoneRadius = 2.6f;

        [Min(0.25f)]
        [Tooltip("Duration in seconds of the death frost zone")]
        public float frostZoneDuration = 3.5f;

        [Range(0.25f, 0.95f)]
        [Tooltip("Hero speed multiplier while standing inside the frost zone (0.55 = 45% slow)")]
        public float heroSlowFactor = 0.55f;

        [Min(0.1f)]
        [Tooltip("Duration of slow applied on each frost zone tick")]
        public float slowRefreshDuration = 0.35f;

        [Min(0.05f)]
        [Tooltip("Tick interval in seconds for checking hero overlap inside the frost zone")]
        public float zoneTickInterval = 0.15f;

        [Tooltip("Optional custom visual prefab for the frost ground zone")]
        public GameObject frostZonePrefab;
    }
}
