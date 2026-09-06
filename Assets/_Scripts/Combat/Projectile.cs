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
        private WeaponData data;
        private bool isReturned;
        private float damageMultiplier = 1f;
        private float spawnTime;

        public void Initialize(WeaponData weaponData, Transform targetTransform, IObjectPool<Projectile> projectilePool, float damageMultiplier = 1f)
        {
            data = weaponData;
            target = targetTransform;
            pool = projectilePool;
            isReturned = false;
            this.damageMultiplier = damageMultiplier;
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (isReturned) return;

            // Ограничение максимального времени жизни, чтобы избежать зависания и утечки пула
            if (Time.time >= spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            // Если цель уничтожена или выключена, возвращаем снаряд в пул
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                ReturnToPool();
                return;
            }

            IDamageable damageable = null;
            if (target.TryGetComponent(out damageable) && damageable.IsDead)
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
                HitTarget(damageable);
                return;
            }

            transform.position += toTarget.normalized * distanceThisFrame;
        }

        private void HitTarget(IDamageable damageable = null)
        {
            if (damageable == null && target != null)
            {
                target.TryGetComponent(out damageable);
            }

            if (damageable != null && !damageable.IsDead)
            {
                int finalDamage = Mathf.RoundToInt(data.damage * damageMultiplier);
                damageable.TakeDamage(finalDamage);
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
