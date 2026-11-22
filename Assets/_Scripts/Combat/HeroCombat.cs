using UnityEngine;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Meta;
using ProjectB.Player;
using VContainer;

namespace ProjectB.Combat
{
    public class HeroCombat : MonoBehaviour
    {
        [SerializeField] private System.Collections.Generic.List<WeaponData> startingWeapons = new System.Collections.Generic.List<WeaponData>();
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private Transform firePoint;

        private System.Collections.Generic.List<WeaponController> activeWeapons = new System.Collections.Generic.List<WeaponController>();
        private HeroHealth hp;
        private Transform projectilesRoot;
        private HeroRuntimeStats runtimeStats;
        private SelectedHeroService selectedHeroService;
        private RunStatistics runStatistics;

        [Inject]
        public void Construct(
            HeroHealth heroHealth,
            HeroRuntimeStats runtimeStats,
            SelectedHeroService selectedHeroService = null,
            RunStatistics runStatistics = null)
        {
            hp = heroHealth;
            this.runtimeStats = runtimeStats;
            this.selectedHeroService = selectedHeroService;
            this.runStatistics = runStatistics;
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (projectilesRoot != null) return;

            projectilesRoot = new GameObject("ProjectilesRoot").transform;

            if (runtimeStats != null)
            {
                runtimeStats.OnStatsChanged += RefreshWeaponMultipliers;
            }

            if (activeWeapons.Count == 0)
            {
                HeroData selectedHero = selectedHeroService?.GetSelectedHero();
                if (selectedHero != null && selectedHero.startingWeapon != null)
                {
                    AddWeapon(selectedHero.startingWeapon);
                }
                else
                {
                    // Инициализация стартового оружия из префаба (fallback)
                    foreach (var weapon in startingWeapons)
                    {
                        AddWeapon(weapon);
                    }
                }
            }
        }

        public void PrewarmWeapons(int countPerWeapon = 20)
        {
            EnsureInitialized();
            for (int i = 0; i < activeWeapons.Count; i++)
            {
                activeWeapons[i]?.PrewarmPool(countPerWeapon);
            }
        }

        public void SimulateFrameTick()
        {
            Update();
        }

        private void OnDestroy()
        {
            if (runtimeStats != null)
            {
                runtimeStats.OnStatsChanged -= RefreshWeaponMultipliers;
            }
            if (projectilesRoot != null)
            {
                if (Application.isPlaying) Destroy(projectilesRoot.gameObject);
                else DestroyImmediate(projectilesRoot.gameObject);
                projectilesRoot = null;
            }
        }

        public void AddWeapon(WeaponData data)
        {
            if (data == null) return;
            var controller = new WeaponController(data, enemyLayer, transform, firePoint, projectilesRoot, HeroDamageMultiplier, runtimeStats, hp, runStatistics);
            activeWeapons.Add(controller);
        }

        public float HeroDamageMultiplier => runtimeStats != null ? runtimeStats.WeaponDamageMultiplier : 1f;

        public void IncreaseDamageMultiplier(float bonus)
        {
            runtimeStats?.AddGlobalDamagePercent(bonus * 100f);
        }

        private void RefreshWeaponMultipliers()
        {
            float mult = HeroDamageMultiplier;
            for (int i = 0; i < activeWeapons.Count; i++)
            {
                activeWeapons[i].DamageMultiplier = mult;
            }
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
