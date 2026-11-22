using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "ExploderEnemyData", menuName = "ProjectB/Data/Enemies/ExploderEnemyData")]
    public class ExploderEnemyData : EnemyData
    {
        [Header("Explosion")]
        [Tooltip("Distance from hero at which the exploder stops and arms its fuse")]
        public float triggerDistance = 1.8f;

        [Tooltip("Telegraph fuse duration before detonation (seconds)")]
        public float fuseDuration = 0.75f;

        [Tooltip("AoE detonation radius (meters)")]
        public float explosionRadius = 2.5f;

        [Tooltip("Explosion damage dealt to the hero")]
        public int explosionDamage = 28;

        [Tooltip("Multiplier for friendly-fire explosion damage dealt to nearby enemies")]
        [Range(0f, 2f)]
        public float friendlyFireMultiplier = 1.0f;
    }
}
