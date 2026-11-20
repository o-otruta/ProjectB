using UnityEngine;
using ProjectB.Data.Combat;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "BossData", menuName = "ProjectB/Data/Enemies/BossData")]
    public class BossData : EnemyData
    {
        [Header("Boss Identity")]
        public string bossId = "golem";
        public string bossDisplayName = "Stone Golem";
        public bool isMiniBoss = false;
        [Range(0f, 1f)]
        [Tooltip("Multiplier applied to external pull forces like Black Hole (0 = immune, 1 = full pull)")]
        public float pullResistance = 0.15f;

        [Header("Summoning")]
        public EnemyData minionData;
        public int summonCount = 3;
        public float summonCooldown = 10f;

        [Header("Golem — Ground Slam AoE")]
        public float slamCooldown = 5.5f;
        public float slamTriggerRange = 6.5f;
        public float slamRadius = 4.5f;
        public float slamTelegraphDuration = 1.1f;
        public int slamDamage = 30;

        [Header("Necromancer — Teleport & Volley")]
        public float stopDistance = 9f;
        public float teleportCooldown = 7f;
        public float teleportMinDistance = 7f;
        public float teleportMaxDistance = 12f;
        public WeaponData volleyWeaponData;
        public int volleyProjectileCount = 5;
        public float volleySpreadAngle = 55f;
        public float volleyCooldown = 3.2f;
    }
}
