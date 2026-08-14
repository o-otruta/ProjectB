using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class AbilityProjectile : MonoBehaviour
    {
        protected IObjectPool<AbilityProjectile> pool;
        protected Transform target;
        protected float damage;
        protected float speed;
        protected LayerMask enemyLayer;
        protected bool isReturned;
        protected float spawnTime;
        protected const float MAX_LIFETIME = 8f;

        public virtual void Initialize(float damage, float speed, Transform target, LayerMask enemyLayer, IObjectPool<AbilityProjectile> pool)
        {
            this.damage = damage;
            this.speed = speed;
            this.target = target;
            this.enemyLayer = enemyLayer;
            this.pool = pool;
            this.isReturned = false;
            this.spawnTime = Time.time;
        }

        protected virtual void Update()
        {
            if (isReturned) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                ReturnToPool();
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;
            
            if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                HitTarget();
                return;
            }

            transform.position += toTarget.normalized * distanceThisFrame;
            transform.rotation = Quaternion.LookRotation(toTarget.normalized);
        }

        protected virtual void HitTarget()
        {
            if (target != null && target.TryGetComponent<IDamageable>(out var damageable))
            {
                if (!damageable.IsDead)
                {
                    damageable.TakeDamage((int)damage);
                }
            }
            ReturnToPool();
        }

        public virtual void ReturnToPool()
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
