using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "ShieldedEnemyData", menuName = "ProjectB/Data/Enemies/ShieldedEnemyData")]
    public class ShieldedEnemyData : EnemyData
    {
        [Header("Frontal Shield")]
        [Range(10f, 120f)]
        [Tooltip("Half-angle in degrees of the frontal arc protected by the shield (+/- 60 deg = 120 deg cone)")]
        public float frontalBlockHalfAngle = 60f;

        [Range(0f, 0.95f)]
        [Tooltip("Fraction of non-AoE frontal damage blocked by the shield (0.65 = 65% reduction)")]
        public float frontalDamageReduction = 0.65f;
    }
}
