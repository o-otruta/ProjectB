using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

namespace ProjectB.Combat
{
    public class Projectile : MonoBehaviour
    {
        private const float MAX_LIFETIME = 5f;

        private IObjectPool<Projectile> pool;
        private Transform target;
        private IDamageable targetDamageable;
        private WeaponData data;
        private bool isReturned;
        private float damageMultiplier = 1f;
        private float spawnTime;

        public void Initialize(WeaponData weaponData, Transform targetTransform, IObjectPool<Projectile> projectilePool, float damageMultiplier = 1f)
        {
            data = weaponData;
            target = targetTransform;
            targetDamageable = targetTransform != null ? targetTransform.GetComponent<IDamageable>() : null;
            pool = projectilePool;
            isReturned = false;
            this.damageMultiplier = damageMultiplier;
            spawnTime = Time.time;
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

            transform.position += toTarget.normalized * distanceThisFrame;
        }

        private void HitTarget()
        {
            if (targetDamageable != null && !targetDamageable.IsDead)
            {
                int finalDamage = Mathf.RoundToInt(data.damage * damageMultiplier);
                targetDamageable.TakeDamage(finalDamage);
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
