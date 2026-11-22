using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class DragonFlameProjectile : MonoBehaviour
    {
        private static readonly Collider[] hitBuffer = new Collider[12];
        private const float MAX_LIFETIME = 1.6f;

        private Vector3 moveDirection;
        private float speed;
        private float damage;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private IObjectPool<DragonFlameProjectile> pool;
        private float expireTime;
        private bool isReturned;

        public void Initialize(
            Vector3 startPos,
            Vector3 direction,
            float speed,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            IObjectPool<DragonFlameProjectile> pool)
        {
            this.moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            this.speed = Mathf.Max(1f, speed);
            this.damage = damage;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.pool = pool;
            this.expireTime = Time.time + MAX_LIFETIME;
            this.isReturned = false;

            transform.position = startPos;
            transform.rotation = Quaternion.LookRotation(this.moveDirection);
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time >= expireTime)
            {
                ReturnToPool();
                return;
            }

            transform.position += moveDirection * (speed * Time.deltaTime);

            int hits = Physics.OverlapSphereNonAlloc(transform.position, 0.5f, hitBuffer, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                bool isCrit = false;
                int finalDmg = runtimeStats != null
                    ? runtimeStats.RollDamage(damage, out isCrit)
                    : Mathf.Max(1, Mathf.RoundToInt(damage));

                if (damageable is EnemyBase enemy)
                {
                    enemy.TakeDamage(finalDmg, isCrit);
                }
                else
                {
                    damageable.TakeDamage(finalDmg);
                }

                ReturnToPool();
                return;
            }
        }

        public void ReturnToPool()
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
