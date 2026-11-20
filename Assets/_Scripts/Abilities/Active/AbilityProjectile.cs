using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class AbilityProjectile : MonoBehaviour
    {
        protected IObjectPool<AbilityProjectile> pool;
        protected Transform target;
        protected IDamageable targetDamageable;
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
            this.targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;
            this.enemyLayer = enemyLayer;
            this.pool = pool;
            this.isReturned = false;
            this.spawnTime = Time.time;
        }

        protected virtual void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            if (target == null || !target.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
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

            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * distanceThisFrame;
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        protected virtual void HitTarget()
        {
            if (targetDamageable != null && !targetDamageable.IsDead)
            {
                targetDamageable.TakeDamage(Mathf.RoundToInt(damage));
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
