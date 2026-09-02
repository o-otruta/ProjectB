using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Data.Combat;

namespace ProjectB.Enemies
{
    public class ShootingEnemy : EnemyBase
    {
        private ShootingEnemyData shootingData;
        private IObjectPool<EnemyProjectile> projectilePool;
        private Transform projectileContainer;
        private float nextFireTime;

        public override void Initialize(EnemyData data, Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool, float difficultyMultiplier = 1f,
            ProjectB.LevelUp.XpManager xpManager = null,
            ProjectB.LevelUp.CoinManager coinManager = null,
            ProjectB.Core.RunStatistics runStatistics = null)
        {
            base.Initialize(data, heroTarget, enemyPool, difficultyMultiplier, 
                xpManager, coinManager, runStatistics);
            
            shootingData = data as ShootingEnemyData;
            nextFireTime = Time.time + Random.Range(0f, 1f); // Рассинхронизация
            Debug.Log($"[ShootingEnemy] Initialized. Data is ShootingEnemyData: {shootingData != null}. WeaponData assigned: {shootingData?.weaponData != null}. nextFireTime: {nextFireTime}");

            if (projectilePool == null && shootingData != null && shootingData.weaponData != null)
            {
                Debug.Log($"[ShootingEnemy] Creating projectile pool.");
                InitializeProjectilePool();
            }
        }

        private void InitializeProjectilePool()
        {
            if (projectileContainer == null)
            {
                projectileContainer = new GameObject($"EnemyProjectiles_{gameObject.GetEntityId()}").transform;
            }

            projectilePool = new ObjectPool<EnemyProjectile>(
                createFunc: () =>
                {
                    GameObject go;
                    if (shootingData.weaponData.projectilePrefab != null)
                    {
                        go = Instantiate(shootingData.weaponData.projectilePrefab, projectileContainer);
                        Debug.Log($"[ShootingEnemy] Created projectile from prefab: {go.name}");
                    }
                    else
                    {
                        // Fallback: маленькая сфера
                        go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        go.transform.SetParent(projectileContainer);
                        go.transform.localScale = Vector3.one * 0.25f;
                        Debug.Log($"[ShootingEnemy] Created fallback sphere for projectile.");
                        
                        // Disable and destroy collider immediately to prevent physics explosions!
                        Collider col = go.GetComponent<Collider>();
                        if (col != null)
                        {
                            col.enabled = false;
                            Destroy(col);
                        }
                    }
                    var proj = go.GetComponent<EnemyProjectile>();
                    if (proj == null) proj = go.AddComponent<EnemyProjectile>();
                    return proj;
                },
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => Destroy(p.gameObject),
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 20
            );
        }

        protected override void UpdateMovement()
        {
            // Стрелок останавливается на stopDistance
            if (shootingData == null) { base.UpdateMovement(); return; }

            Vector3 direction = (target.position - transform.position);
            direction.y = 0;
            float distSqr = direction.sqrMagnitude;
            float stopDist = shootingData.stopDistance;

            if (distSqr > stopDist * stopDist)
            {
                // Далеко — идти к герою (используем базовую логику движения)
                base.UpdateMovement();
            }
            else if (direction.sqrMagnitude > 0.01f)
            {
                // В пределах дистанции — поворачиваемся лицом к герою, но не двигаемся
                transform.rotation = Quaternion.LookRotation(direction.normalized);
            }
        }

        protected override void UpdateAttack()
        {
            // Стрелок НЕ использует контактный урон — он стреляет
            if (shootingData == null || shootingData.weaponData == null) 
            { 
                base.UpdateAttack(); 
                return; 
            }

            Vector3 direction = (target.position - transform.position);
            direction.y = 0;
            float distSqr = direction.sqrMagnitude;
            float range = shootingData.weaponData.range;

            if (distSqr <= range * range)
            {
                if (Time.time >= nextFireTime)
                {
                    Debug.Log($"[ShootingEnemy] Firing! distSqr: {distSqr}, rangeSqr: {range*range}, Time: {Time.time}");
                    Fire();
                    nextFireTime = Time.time + shootingData.weaponData.attackCooldown;
                }
            }
        }

        private void Fire()
        {
            if (projectilePool == null || target == null) 
            {
                Debug.LogError($"[ShootingEnemy] Fire failed! projectilePool null: {projectilePool == null}, target null: {target == null}");
                return;
            }

            var proj = projectilePool.Get();
            if (proj == null) 
            {
                Debug.LogError($"[ShootingEnemy] Fire failed! Projectile from pool is null.");
                return;
            }

            // Спавним чуть впереди и выше, чтобы не задеть самого моба (на всякий случай)
            Vector3 spawnDirection = (target.position - transform.position).normalized;
            spawnDirection.y = 0;
            proj.transform.position = transform.position + Vector3.up * 0.5f + spawnDirection * 0.5f;
            
            int finalDamage = Mathf.RoundToInt(
                shootingData.weaponData.damage * 
                (1f + (currentDamage - shootingData.contactDamage) * 0.1f)); 
            
            Debug.Log($"[ShootingEnemy] Projectile initialized. Damage: {finalDamage}, Speed: {shootingData.weaponData.projectileSpeed}");
            proj.Initialize(finalDamage, shootingData.weaponData.projectileSpeed, 
                target, projectilePool);
        }
    }
}
