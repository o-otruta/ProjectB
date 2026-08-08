using UnityEngine;

namespace ProjectB.Data.Combat
{
    [CreateAssetMenu(fileName = "WeaponData", menuName = "ProjectB/Data/Combat/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Weapon Stats")]
        [Tooltip("Урон за одно попадание")]
        public int damage = 10;
        
        [Tooltip("Задержка между выстрелами в секундах")]
        public float attackCooldown = 0.5f;
        
        [Tooltip("Радиус поиска цели (в юнитах)")]
        public float range = 5f;
        
        [Tooltip("Скорость полета снаряда")]
        public float projectileSpeed = 10f;
        
        [Header("Visuals")]
        [Tooltip("Префаб снаряда (с компонентом Projectile)")]
        public GameObject projectilePrefab;
    }
}
