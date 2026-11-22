using UnityEngine;

namespace ProjectB.Abilities
{
    public class ShooterAbility : ProjectileAbilityBase<ShooterAbilityData>
    {
        protected override void LoadBaseStats()
        {
            currentDamage = ProjectileData.baseDamage;
            currentCooldown = ProjectileData.baseCooldown;
            currentCount = ProjectileData.projectileCount;
            currentSpeed = ProjectileData.projectileSpeed;
            currentRange = ProjectileData.searchRadius;
        }

        protected override AbilityProjectile CreateProjectilePrefab()
        {
            GameObject obj;
            if (ProjectileData.projectilePrefab != null)
            {
                obj = Instantiate(ProjectileData.projectilePrefab, projectileContainer);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.SetParent(projectileContainer);
                obj.transform.localScale = Vector3.one * 0.3f;
                Destroy(obj.GetComponent<SphereCollider>());
            }
            var p = obj.GetComponent<AbilityProjectile>();
            if (p == null) p = obj.AddComponent<AbilityProjectile>();
            return p;
        }

        private readonly System.Collections.Generic.List<Transform> targetBuffer = new System.Collections.Generic.List<Transform>(8);

        protected override void Fire()
        {
            int foundCount = FindClosestTargets(currentRange, ProjectileData.targetLayer, currentCount, targetBuffer);
            if (foundCount == 0) return;

            for (int i = 0; i < currentCount; i++)
            {
                var p = projectilePool.Get();
                if (p == null) continue;

                Transform chosenTarget = targetBuffer[i % foundCount];
                Vector3 offset = Random.insideUnitSphere * 0.2f;
                offset.y = 0;
                p.transform.position = transform.position + offset;
                
                p.Initialize(GetScaledDamage(currentDamage), currentSpeed, chosenTarget, ProjectileData.targetLayer, projectilePool, runtimeStats);
            }
        }
    }
}
