using UnityEngine;

namespace ProjectB.Abilities
{
    public class FireballAbility : ProjectileAbilityBase<FireballAbilityData>
    {
        private float currentExplosionRadius;

        protected override void LoadBaseStats()
        {
            currentDamage = ProjectileData.baseDamage;
            currentCooldown = ProjectileData.baseCooldown;
            currentCount = ProjectileData.projectileCount;
            currentSpeed = ProjectileData.projectileSpeed;
            currentRange = ProjectileData.searchRadius;
            currentExplosionRadius = ProjectileData.explosionRadius;
        }

        protected override void ApplySpecificModifier(ModifierType type, float value)
        {
            if (type == ModifierType.Area)
            {
                currentExplosionRadius += value;
            }
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
                obj.transform.localScale = Vector3.one * 0.5f;
                // TODO(Post-MVP): Убрать Shader.Find после добавления префабов
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = Color.red;
                obj.GetComponent<Renderer>().material = mat;
                Destroy(obj.GetComponent<SphereCollider>());
            }
            var p = obj.GetComponent<FireballProjectile>();
            if (p == null) p = obj.AddComponent<FireballProjectile>();
            return p;
        }

        protected override void Fire()
        {
            Transform closestTarget = FindClosestTarget(currentRange, ProjectileData.targetLayer);
            if (closestTarget == null) return;

            for (int i = 0; i < currentCount; i++)
            {
                var p = projectilePool.Get() as FireballProjectile;
                if (p == null) continue;

                Vector3 offset = Random.insideUnitSphere * 0.2f;
                offset.y = 0;
                p.transform.position = transform.position + offset;
                
                p.InitializeFireball(currentDamage, currentSpeed, currentExplosionRadius, closestTarget, ProjectileData.targetLayer, projectilePool);
            }
        }
    }
}
