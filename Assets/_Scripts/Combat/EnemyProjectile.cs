using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.Combat
{
    public class EnemyProjectile : MonoBehaviour
    {
        private IObjectPool<EnemyProjectile> pool;
        private Transform target;
        private int damage;
        private float speed;
        private bool isReturned;
        private float spawnTime;
        private const float MAX_LIFETIME = 6f;

        public void Initialize(int damage, float speed, Transform target, 
            IObjectPool<EnemyProjectile> projectilePool)
        {
            this.damage = damage;
            this.speed = speed;
            this.target = target;
            this.pool = projectilePool;
            this.isReturned = false;
            this.spawnTime = Time.time;
        }

        private void Update()
        {
            if (isReturned) return;
            if (Time.time > spawnTime + MAX_LIFETIME) { ReturnToPool(); return; }
            if (target == null || !target.gameObject.activeInHierarchy) { ReturnToPool(); return; }

            Vector3 toTarget = target.position - transform.position;
            float step = speed * Time.deltaTime;
            if (toTarget.sqrMagnitude <= step * step) { HitTarget(); return; }

            transform.position += toTarget.normalized * step;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }
        }

        private void HitTarget()
        {
            if (target != null && target.TryGetComponent<IDamageable>(out var damageable))
            {
                if (!damageable.IsDead)
                    damageable.TakeDamage(damage);
            }
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (isReturned) return;
            isReturned = true;
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }
    }
}
