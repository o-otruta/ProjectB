using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

namespace ProjectB.Combat
{
    public class HeroCombat : MonoBehaviour
    {
        [SerializeField] private System.Collections.Generic.List<WeaponData> startingWeapons = new System.Collections.Generic.List<WeaponData>();
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private Transform firePoint;

        private System.Collections.Generic.List<WeaponController> activeWeapons = new System.Collections.Generic.List<WeaponController>();
        private ProjectB.Player.HeroHealth hp;
        private Transform projectilesRoot;

        private void Start()
        {
            hp = GetComponent<ProjectB.Player.HeroHealth>();
            projectilesRoot = new GameObject("ProjectilesRoot").transform;
            
            // Инициализация стартового оружия
            foreach (var weapon in startingWeapons)
            {
                AddWeapon(weapon);
            }
        }

        public void AddWeapon(WeaponData data)
        {
            if (data == null) return;
            var controller = new WeaponController(data, enemyLayer, transform, firePoint, projectilesRoot);
            activeWeapons.Add(controller);
        }

        private void Update()
        {
            if (hp != null && hp.IsDead) return;

            foreach (var weapon in activeWeapons)
            {
                weapon.UpdateController();
            }
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var weapon in activeWeapons)
            {
                weapon.DrawGizmos();
            }
        }
    }
}
