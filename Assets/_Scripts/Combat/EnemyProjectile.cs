using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Combat
{
    public class EnemyProjectile : MonoBehaviour
    {
        private const float MAX_LIFETIME = 6f;
        private const float PROJECTILE_RADIUS = 0.25f;
        private const float HERO_HIT_RADIUS = 0.75f;
        private const float HERO_HIT_RADIUS_SQ = HERO_HIT_RADIUS * HERO_HIT_RADIUS;

        private static int obstacleLayerMask = -1;

        private IObjectPool<EnemyProjectile> pool;
        private Transform heroTarget;
        private IDamageable heroDamageable;
        private Vector3 direction;
        private int damage;
        private float speed;
        private string sourceId;
        private bool isReturned;
        private float spawnTime;

        public Vector3 Direction => direction;
        public float Speed => speed;
        public bool IsActive => !isReturned && gameObject.activeInHierarchy;

        private static void EnsureLayerMasks()
        {
            if (obstacleLayerMask == -1)
            {
                int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
                obstacleLayerMask = obstaclesLayer != -1 ? (1 << obstaclesLayer) : 0;
            }
        }

        public void Initialize(
            int damage,
            float speed,
            Vector3 shootDirection,
            IObjectPool<EnemyProjectile> projectilePool,
            Transform targetHero = null,
            IDamageable targetHeroDamageable = null,
            string sourceId = "EnemyProjectile")
        {
            EnsureLayerMasks();
            this.damage = damage;
            this.speed = speed;
            this.pool = projectilePool;
            this.heroTarget = targetHero;
            this.heroDamageable = targetHeroDamageable != null
                ? targetHeroDamageable
                : (targetHero != null ? targetHero.GetComponent<IDamageable>() : null);
            this.sourceId = !string.IsNullOrEmpty(sourceId) ? sourceId : "EnemyProjectile";
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

        public void Initialize(int damage, float speed, Transform target, IObjectPool<EnemyProjectile> projectilePool)
        {
            Vector3 dir = transform.forward;
            if (target != null)
            {
                dir = target.position - transform.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude < 0.001f ? transform.forward : dir.normalized;
            }
            Initialize(damage, speed, dir, projectilePool, target, null, "EnemyProjectile");
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            Vector3 currentPos = transform.position;
            float step = speed * Time.deltaTime;

            // 1. Проверка попадания по герою по отрезку движения в XZ (0 вызовов PhysX)
            if (heroTarget != null && heroDamageable != null && !heroDamageable.IsDead)
            {
                if (IntersectsHeroSegmentXZ(currentPos, direction, step, heroTarget.position))
                {
                    if (heroDamageable is HeroHealth heroHealth)
                    {
                        heroHealth.TakeDamage(damage, sourceId);
                    }
                    else
                    {
                        heroDamageable.TakeDamage(damage);
                    }
                    ReturnToPool();
                    return;
                }
            }

            // 2. Проверка столкновения только со стенами/препятствиями
            if (obstacleLayerMask != 0 &&
                Physics.SphereCast(currentPos, PROJECTILE_RADIUS, direction, out RaycastHit hitInfo, step, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = currentPos + direction * hitInfo.distance;
                ReturnToPool();
                return;
            }

            // 3. Прямолинейный полет
            transform.position = currentPos + direction * step;
        }

        private static bool IntersectsHeroSegmentXZ(Vector3 start, Vector3 dir, float length, Vector3 heroPos)
        {
            float dx = dir.x * length;
            float dz = dir.z * length;
            float toHeroX = heroPos.x - start.x;
            float toHeroZ = heroPos.z - start.z;

            float segLenSq = dx * dx + dz * dz;
            float t = segLenSq > 0.00001f
                ? Mathf.Clamp01((toHeroX * dx + toHeroZ * dz) / segLenSq)
                : 0f;

            float closestX = start.x + dx * t;
            float closestZ = start.z + dz * t;
            float distX = heroPos.x - closestX;
            float distZ = heroPos.z - closestZ;
            return (distX * distX + distZ * distZ) <= HERO_HIT_RADIUS_SQ;
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
