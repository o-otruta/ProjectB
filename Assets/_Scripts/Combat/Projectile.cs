using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Combat
{
    public class Projectile : MonoBehaviour
    {
        private const float MAX_LIFETIME = 5f;
        private static readonly Collider[] splashHits = new Collider[24];

        private IObjectPool<Projectile> pool;
        private Transform target;
        private IDamageable targetDamageable;
        private WeaponData data;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private HeroHealth ownerHealth;
        private RunStatistics runStatistics;
        private bool isReturned;
        private float damageMultiplier = 1f;
        private float spawnTime;

        public void Initialize(
            WeaponData weaponData,
            Transform targetTransform,
            IObjectPool<Projectile> projectilePool,
            float damageMultiplier = 1f,
            LayerMask enemyLayer = default,
            HeroRuntimeStats runtimeStats = null,
            HeroHealth ownerHealth = null,
            RunStatistics runStatistics = null)
        {
            data = weaponData;
            target = targetTransform;
            targetDamageable = targetTransform != null ? targetTransform.GetComponent<IDamageable>() : null;
            pool = projectilePool;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.ownerHealth = ownerHealth;
            this.runStatistics = runStatistics;
            isReturned = false;
            this.damageMultiplier = damageMultiplier;
            spawnTime = Time.time;

            if (target != null)
            {
                Vector3 initDir = target.position - transform.position;
                if (initDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(initDir.normalized);
                }
            }
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            // Ограничение максимального времени жизни, чтобы избежать зависания и утечки пула
            if (Time.time >= spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            // Если цель уничтожена или выключена, возвращаем снаряд в пул
            if (target == null || !target.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
            {
                ReturnToPool();
                return;
            }

            if (data == null || data.projectileSpeed <= 0f)
            {
                ReturnToPool();
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            float distanceThisFrame = data.projectileSpeed * Time.deltaTime;
            
            // Если шаг за этот кадр >= оставшаяся дистанция — считаем попадание
            if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                HitTarget();
                return;
            }

            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * distanceThisFrame;
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDir);
            }
        }

        private void HitTarget()
        {
            float rawDamage = data != null ? data.damage * damageMultiplier : 0f;

            if (targetDamageable != null && !targetDamageable.IsDead && rawDamage > 0f)
            {
                int finalDamage = RollHitDamage(rawDamage, out bool isCrit);
                DealDamage(targetDamageable, finalDamage, isCrit);
                TryApplyLifesteal();
            }

            if (data != null && data.splashRadius > 0f && rawDamage > 0f && enemyLayer.value != 0)
            {
                int hits = Physics.OverlapSphereNonAlloc(transform.position, data.splashRadius, splashHits, enemyLayer);
                for (int i = 0; i < hits; i++)
                {
                    if (splashHits[i] != null &&
                        splashHits[i].TryGetComponent<IDamageable>(out var splashTarget) &&
                        splashTarget != targetDamageable &&
                        !splashTarget.IsDead)
                    {
                        int splashDamage = RollHitDamage(rawDamage, out bool splashCrit);
                        DealDamage(splashTarget, splashDamage, splashCrit);
                    }
                }
            }

            ReturnToPool();
        }

        private void TryApplyLifesteal()
        {
            if (data == null || data.lifestealAmount <= 0 || data.lifestealChance <= 0f) return;
            if (ownerHealth == null || ownerHealth.IsDead) return;

            if (data.lifestealChance >= 1f || Random.value < data.lifestealChance)
            {
                ownerHealth.Heal(data.lifestealAmount);
            }
        }

        private int RollHitDamage(float rawDamage, out bool isCrit)
        {
            if (runtimeStats != null)
            {
                return runtimeStats.RollDamage(rawDamage, out isCrit);
            }
            isCrit = false;
            return Mathf.RoundToInt(rawDamage);
        }

        private void DealDamage(IDamageable victim, int damage, bool isCrit)
        {
            if (victim == null || victim.IsDead || damage <= 0) return;
            if (victim is EnemyBase enemy)
            {
                enemy.TakeDamage(damage, isCrit);
                runStatistics?.RecordDamage(DamageSourceIds.Weapon, damage);
            }
            else
            {
                victim.TakeDamage(damage);
            }
        }

        private void ReturnToPool()
        {
            if (isReturned) return;
            isReturned = true;
            
            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
