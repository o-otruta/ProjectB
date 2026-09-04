using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Enemies;

namespace ProjectB.Combat
{
    public class EnemyProjectile : MonoBehaviour
    {
        private IObjectPool<EnemyProjectile> pool;
        private Vector3 direction;
        private int damage;
        private float speed;
        private bool isReturned;
        private float spawnTime;
        private const float MAX_LIFETIME = 6f;
        private const float PROJECTILE_RADIUS = 0.25f;

        private static readonly Collider[] overlapBuffer = new Collider[8];
        private static int hitLayerMask = -1;
        private static int obstacleLayerMask = -1;

        private static void EnsureLayerMasks()
        {
            if (hitLayerMask == -1)
            {
                int heroLayer = LayerMask.NameToLayer("Hero");
                int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
                hitLayerMask = 0;
                obstacleLayerMask = 0;
                if (heroLayer != -1) hitLayerMask |= (1 << heroLayer);
                if (obstaclesLayer != -1)
                {
                    hitLayerMask |= (1 << obstaclesLayer);
                    obstacleLayerMask |= (1 << obstaclesLayer);
                }
                if (hitLayerMask == 0)
                {
                    // Fallback если слои не настроены
                    hitLayerMask = ~0;
                }
            }
        }

        public void Initialize(int damage, float speed, Vector3 shootDirection, 
            IObjectPool<EnemyProjectile> projectilePool)
        {
            EnsureLayerMasks();
            this.damage = damage;
            this.speed = speed;
            this.pool = projectilePool;
            this.isReturned = false;
            this.spawnTime = Time.time;

            if (shootDirection.sqrMagnitude > 0.001f)
            {
                this.direction = shootDirection.normalized;
            }
            else
            {
                this.direction = transform.forward;
            }

            transform.rotation = Quaternion.LookRotation(this.direction);
        }

        public void Initialize(int damage, float speed, Transform target, 
            IObjectPool<EnemyProjectile> projectilePool)
        {
            Vector3 dir = transform.forward;
            if (target != null)
            {
                dir = target.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = transform.forward;
                }
                else
                {
                    dir.Normalize();
                }
            }
            Initialize(damage, speed, dir, projectilePool);
        }

        private void Update()
        {
            if (isReturned) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            float step = speed * Time.deltaTime;

            // 1. Проверяем попадание на текущей позиции
            int hits = Physics.OverlapSphereNonAlloc(transform.position, PROJECTILE_RADIUS, overlapBuffer, hitLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider col = overlapBuffer[i];
                if (col != null)
                {
                    if (TryDamage(col))
                    {
                        ReturnToPool();
                        return;
                    }
                    else if (IsObstacle(col))
                    {
                        ReturnToPool();
                        return;
                    }
                }
            }

            // 2. Проверяем траекторию движения за этот кадр (SphereCast)
            if (Physics.SphereCast(transform.position, PROJECTILE_RADIUS, direction, out RaycastHit hitInfo, step, hitLayerMask, QueryTriggerInteraction.Ignore))
            {
                transform.position += direction * hitInfo.distance;
                if (hitInfo.collider != null)
                {
                    TryDamage(hitInfo.collider);
                }
                ReturnToPool();
                return;
            }

            // 3. Прямолинейный полет в заданном направлении (снаряд НЕ наводится за игроком)
            transform.position += direction * step;
        }

        private bool TryDamage(Collider col)
        {
            // Не наносим урон другим врагам
            if (col.GetComponentInParent<EnemyBase>() != null)
            {
                return false;
            }

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage);
                return true;
            }
            return false;
        }

        private bool IsObstacle(Collider col)
        {
            if (obstacleLayerMask != 0)
            {
                return ((1 << col.gameObject.layer) & obstacleLayerMask) != 0;
            }
            return false;
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
