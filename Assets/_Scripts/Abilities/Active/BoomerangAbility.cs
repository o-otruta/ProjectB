using System;
using UnityEngine;

namespace ProjectB.Abilities
{
    public class BoomerangAbility : ProjectileAbilityBase<BoomerangAbilityData>
    {
        protected override int DefaultPoolCapacity => 5;
        protected override int MaxPoolCapacity => 20;

        private int bladeStormHitCounter;
        private Action cachedEnemyHitCallback;

        public override void Initialize(AbilityData data)
        {
            cachedEnemyHitCallback = HandleBoomerangEnemyHit;
            bladeStormHitCounter = 0;
            base.Initialize(data);
        }

        protected override void LoadBaseStats()
        {
            currentDamage = ProjectileData.baseDamage;
            currentCooldown = ProjectileData.baseCooldown;
            currentSpeed = ProjectileData.travelSpeed;
            currentRange = ProjectileData.throwRange;
            currentCount = ProjectileData.projectileCount;
            currentPierceCount = ProjectileData.pierceCount;
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentSpeed *= 1.2f;
                currentRange *= 1.2f;
            }
            else if (tier == 2)
            {
                currentCount += 1;
                currentPierceCount += 2;
            }
        }

        private void HandleBoomerangEnemyHit()
        {
            if (!HasSynergy(SynergyIds.BladeStorm)) return;

            bladeStormHitCounter++;
            if (bladeStormHitCounter >= 3)
            {
                bladeStormHitCounter = 0;
                heroAbilities?.GetActiveAbility<OrbitalCompanionAbility>()?.FireBladeStormVolley();
            }
        }

        protected override AbilityProjectile CreateProjectilePrefab()
        {
            GameObject obj;
            if (ProjectileData.boomerangPrefab != null)
            {
                obj = Instantiate(ProjectileData.boomerangPrefab, projectileContainer);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.SetParent(projectileContainer);
                var col = obj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
            var p = obj.GetComponent<BoomerangProjectile>();
            if (p == null) p = obj.AddComponent<BoomerangProjectile>();
            return p;
        }

        protected override void Fire()
        {
            Transform closestTarget = FindClosestTarget(currentRange, ProjectileData.targetLayer);
            if (closestTarget == null) return;

            for (int i = 0; i < currentCount; i++)
            {
                var p = projectilePool.Get() as BoomerangProjectile;
                if (p == null) continue;

                Vector3 dir = (closestTarget.position - transform.position).normalized;
                dir.y = 0;

                if (currentCount > 1)
                {
                    float angleOffset = -15f * (currentCount - 1) / 2f + (15f * i);
                    dir = Quaternion.Euler(0, angleOffset, 0) * dir;
                }

                p.transform.position = transform.position;
                p.InitializeBoomerang(
                    GetScaledDamage(currentDamage),
                    currentSpeed,
                    currentRange,
                    currentPierceCount,
                    dir,
                    transform,
                    ProjectileData.targetLayer,
                    projectilePool,
                    runtimeStats,
                    cachedEnemyHitCallback,
                    runStatistics,
                    cachedAbilityId,
                    VisualScaleMultiplier);
            }
        }
    }
}
