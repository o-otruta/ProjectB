using UnityEngine;

namespace ProjectB.Abilities
{
    public class BoomerangAbility : ProjectileAbilityBase<BoomerangAbilityData>
    {
        protected override int DefaultPoolCapacity => 5;
        protected override int MaxPoolCapacity => 20;

        protected override void LoadBaseStats()
        {
            currentDamage = ProjectileData.baseDamage;
            currentCooldown = ProjectileData.baseCooldown;
            currentSpeed = ProjectileData.travelSpeed;
            currentRange = ProjectileData.throwRange;
            currentCount = ProjectileData.projectileCount;
            currentPierceCount = ProjectileData.pierceCount;
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
                // TODO(Post-MVP): Убрать Shader.Find после добавления префабов
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = Color.green;
                obj.GetComponent<Renderer>().material = mat;
                Destroy(obj.GetComponent<BoxCollider>());
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

                // Если выстреливаем несколько бумерангов, добавляем небольшое смещение угла
                Vector3 dir = (closestTarget.position - transform.position).normalized;
                dir.y = 0;
                
                if (currentCount > 1)
                {
                    float angleOffset = -15f * (currentCount - 1) / 2f + (15f * i);
                    dir = Quaternion.Euler(0, angleOffset, 0) * dir;
                }

                p.transform.position = transform.position;
                p.InitializeBoomerang(currentDamage, currentSpeed, currentRange, currentPierceCount, dir, transform, ProjectileData.targetLayer, projectilePool);
            }
        }
    }
}
