using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Data.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Enemies
{
    public class ShootingEnemy : EnemyBase
    {
        private ShootingEnemyData shootingData;
        private EnemyProjectilePool sharedProjectilePool;
        private float nextFireTime;

        public void SetProjectilePool(EnemyProjectilePool pool)
        {
            sharedProjectilePool = pool;
        }

        public override void Initialize(EnemyData data, Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool, GameEventBus bus, float difficultyMultiplier = 1f, float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);
            
            shootingData = data as ShootingEnemyData;
            nextFireTime = Time.time + Random.Range(0f, 1f); // Рассинхронизация
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
                    Fire();
                    nextFireTime = Time.time + shootingData.weaponData.attackCooldown;
                }
            }
        }

        private void Fire()
        {
            if (sharedProjectilePool == null || target == null || shootingData == null || shootingData.weaponData == null)
            {
                return;
            }

            // Направление выстрела в сторону текущей позиции героя в плоскости XZ
            Vector3 shootDirection = target.position - transform.position;
            shootDirection.y = 0;
            if (shootDirection.sqrMagnitude > 0.001f)
            {
                shootDirection.Normalize();
            }
            else
            {
                shootDirection = transform.forward;
            }

            // Спавним на высоте цели (Y игрока), чуть впереди моба
            float shootHeight = target.position.y;
            Vector3 spawnPos = new Vector3(transform.position.x, shootHeight, transform.position.z) + shootDirection * 0.5f;

            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(shootingData.weaponData.damage * currentDamageMultiplier));

            sharedProjectilePool.Spawn(
                shootingData.weaponData,
                spawnPos,
                shootDirection,
                finalDamage,
                target,
                targetDamageable);
        }
    }
}
