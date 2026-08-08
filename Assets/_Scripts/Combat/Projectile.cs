using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

namespace ProjectB.Combat
{
    public class Projectile : MonoBehaviour
    {
        private IObjectPool<Projectile> pool;
        private Transform target;
        private WeaponData data;

        public void Initialize(WeaponData weaponData, Transform targetTransform, IObjectPool<Projectile> projectilePool)
        {
            data = weaponData;
            target = targetTransform;
            pool = projectilePool;
        }

        private void Update()
        {
            // Если цель уничтожена или выключена, возвращаем снаряд в пул
            if (target == null || !target.gameObject.activeInHierarchy)
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
            if (target != null && target.TryGetComponent<IDamageable>(out var damageable))
            {
                if (!damageable.IsDead)
                {
                    damageable.TakeDamage(data.damage);
                }
            }
            ReturnToPool();
        }

        private void ReturnToPool()
        {
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
