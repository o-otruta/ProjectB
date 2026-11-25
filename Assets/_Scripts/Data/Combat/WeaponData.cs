using UnityEngine;

namespace ProjectB.Data.Combat
{
    public enum WeaponAttackMode
    {
        /// <summary>Auto-aimed projectile at the closest enemy within <c>range</c>.</summary>
        Projectile = 0,
        /// <summary>Instant circular sweep in front of the hero (Knight). Upgraded by Radius cards instead of Multishot.</summary>
        MeleeSweep = 1
    }

    [CreateAssetMenu(fileName = "WeaponData", menuName = "ProjectB/Data/Combat/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Attack Mode")]
        public WeaponAttackMode attackMode = WeaponAttackMode.Projectile;

        [Header("Weapon Stats")]
        [Tooltip("Урон за одно попадание")]
        public int damage = 10;
        
        [Tooltip("Задержка между выстрелами в секундах")]
        public float attackCooldown = 0.5f;
        
        [Tooltip("Радиус поиска цели (в юнитах)")]
        public float range = 5f;
        
        [Tooltip("Скорость полета снаряда")]
        public float projectileSpeed = 10f;

        [Tooltip("Упреждение для вражеских стрелков: 0 = в текущую точку цели, 1 = точно в точку перехвата при постоянной скорости. " +
                 "0.5–0.7 наказывает бег по кругу, но резкая смена направления по-прежнему спасает")]
        [Range(0f, 1f)] public float leadFactor = 0f;

        [Tooltip("Optional AoE splash radius on impact (0 = single target)")]
        public float splashRadius = 0f;

        [Tooltip("Extra enemies the projectile retargets to after the first hit (0 = single target). Holy Hammer = 2")]
        [Min(0)] public int pierceCount = 0;

        [Tooltip("Search radius used to find the next pierce target")]
        [Min(0.5f)] public float pierceRetargetRadius = 4.5f;

        [Header("Melee Sweep (attackMode = MeleeSweep)")]
        [Tooltip("Base radius of the sweep circle before Radius cards and Global Area")]
        [Min(0.5f)] public float meleeRadius = 2.6f;

        [Tooltip("Sweep centre is shifted along the hero facing by radius * ratio (0 = centred on the hero)")]
        [Range(0f, 1f)] public float meleeForwardOffsetRatio = 0.5f;

        [Tooltip("Pooled wind-slash visual (MeleeSweepVfx) played on every swing")]
        public GameObject meleeVfxPrefab;

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
