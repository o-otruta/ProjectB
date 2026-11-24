using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "SiegeGolemiteEnemyData", menuName = "ProjectB/Data/Enemies/SiegeGolemiteEnemyData")]
    public class SiegeGolemiteEnemyData : EnemyData
    {
        [Header("Stone Shell Phase 1")]
        [Min(1)]
        [Tooltip("Base durability of the stone shell (scaled with wave HP multiplier)")]
        public int shellHp = 130;

        [Min(0)]
        [Tooltip("Flat damage subtracted from every incoming hit while the shell is intact")]
        public int shellFlatArmor = 18;

        [Range(0.05f, 0.75f)]
        [Tooltip("Minimum fraction of incoming hit damage that still bypasses flat armor (protects DoTs/auras)")]
        public float shellMinDamagePercent = 0.15f;

        [Header("Exposed Core Phase 2")]
        [Min(1f)]
        [Tooltip("Movement speed multiplier once the stone shell shatters and exposes the core")]
        public float coreSpeedMultiplier = 2.0f;
    }
}
