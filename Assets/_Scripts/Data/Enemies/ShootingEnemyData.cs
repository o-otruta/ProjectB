using UnityEngine;
using ProjectB.Data.Combat;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "ShootingEnemyData", menuName = "ProjectB/Data/Enemies/ShootingEnemyData")]
    public class ShootingEnemyData : EnemyData
    {
        [Header("Shooting")]
        [Tooltip("Weapon data for ranged attack")]
        public WeaponData weaponData;

        [Tooltip("Distance at which enemy stops moving and starts shooting")]
        public float stopDistance = 5f;
    }
}

