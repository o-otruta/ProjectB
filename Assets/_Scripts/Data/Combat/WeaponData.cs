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

        [Tooltip("Optional AoE splash radius on impact (0 = single target)")]
        public float splashRadius = 0f;

        [Header("On-Hit Effects")]
        [Range(0f, 1f)]
        [Tooltip("Chance (0..1) to heal the hero on projectile impact")]
        public float lifestealChance = 0f;

        [Min(0)]
        [Tooltip("HP restored when lifesteal procs")]
        public int lifestealAmount = 0;
        
        [Header("Visuals")]
        [Tooltip("Префаб снаряда (с компонентом Projectile)")]
        public GameObject projectilePrefab;
    }
}
