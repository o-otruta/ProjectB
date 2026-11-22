using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "ChargerEnemyData", menuName = "ProjectB/Data/Enemies/ChargerEnemyData")]
    public class ChargerEnemyData : EnemyData
    {
        [Header("Charge Attack")]
        [Tooltip("Distance to hero at which the charger starts winding up a dash")]
        public float chargeTriggerRange = 7f;

        [Tooltip("Telegraph windup duration before dashing (seconds)")]
        public float chargeWindupDuration = 0.8f;

        [Tooltip("Movement speed during the straight-line dash")]
        public float chargeSpeed = 11f;

        [Tooltip("Maximum dash travel distance (meters)")]
        public float chargeDistance = 10f;

        [Tooltip("Damage dealt when colliding with the hero during a dash")]
        public int chargeDamage = 18;

        [Tooltip("Radius checked around the charger to hit the hero mid-dash")]
        public float chargeHitRadius = 1.15f;

        [Tooltip("Cooldown after finishing a charge before another can begin (seconds)")]
        public float chargeCooldown = 4.0f;

        [Tooltip("Brief pause after a charge finishes or hits an obstacle (seconds)")]
        public float recoveryDuration = 0.45f;
    }
}
