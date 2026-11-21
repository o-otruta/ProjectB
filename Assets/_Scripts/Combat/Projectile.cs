using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

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
        private bool isReturned;
        private float damageMultiplier = 1f;
        private float spawnTime;

        public void Initialize(
            WeaponData weaponData,
            Transform targetTransform,
            IObjectPool<Projectile> projectilePool,
            float damageMultiplier = 1f,
            LayerMask enemyLayer = default)
        {
            data = weaponData;
            target = targetTransform;
            targetDamageable = targetTransform != null ? targetTransform.GetComponent<IDamageable>() : null;
            pool = projectilePool;
            this.enemyLayer = enemyLayer;
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
            int finalDamage = data != null ? Mathf.RoundToInt(data.damage * damageMultiplier) : 0;

            if (targetDamageable != null && !targetDamageable.IsDead && finalDamage > 0)
            {
                targetDamageable.TakeDamage(finalDamage);
            }

            if (data != null && data.splashRadius > 0f && finalDamage > 0 && enemyLayer.value != 0)
            {
                int hits = Physics.OverlapSphereNonAlloc(transform.position, data.splashRadius, splashHits, enemyLayer);
                for (int i = 0; i < hits; i++)
                {
                    if (splashHits[i] != null &&
                        splashHits[i].TryGetComponent<IDamageable>(out var splashTarget) &&
                        splashTarget != targetDamageable &&
                        !splashTarget.IsDead)
                    {
                        splashTarget.TakeDamage(finalDamage);
                    }
                }
            }

            ReturnToPool();
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
