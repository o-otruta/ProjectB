using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class GravityShardProjectile : MonoBehaviour
    {
        private const float DEFAULT_MOVE_SPEED = 13f;
        private const float DEFAULT_LIFETIME = 1.2f;
        private const float DEFAULT_AOE_RADIUS = 1.8f;
        private static readonly Collider[] hitBuffer = new Collider[32];

        private Vector3 moveDirection;
        private float damage;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private RunStatistics runStatistics;
        private IObjectPool<GravityShardProjectile> pool;
        private float expireTime;
        private bool isReturned;
        private float moveSpeed = DEFAULT_MOVE_SPEED;
        private float aoeRadius = DEFAULT_AOE_RADIUS;
        private string damageSourceId = SynergyIds.CataclysmVortex;
        private bool spinVisual = true;

        /// <summary>
        /// Optional overrides for reuse by other reactions (e.g. Phantom Legion fireballs). Call after Initialize.
        /// </summary>
        public void Configure(string sourceId, float speed, float lifetime, float radius, bool spin)
        {
            damageSourceId = !string.IsNullOrEmpty(sourceId) ? sourceId : SynergyIds.CataclysmVortex;
            moveSpeed = Mathf.Max(0.5f, speed);
            aoeRadius = Mathf.Max(0.2f, radius);
            spinVisual = spin;
            expireTime = Time.time + Mathf.Max(0.1f, lifetime);
        }

        public void Initialize(
            Vector3 startPos,
            Vector3 direction,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            RunStatistics runStatistics,
            IObjectPool<GravityShardProjectile> pool)
        {
            moveDirection = direction;
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude < 0.0001f) moveDirection = Vector3.forward;
            moveDirection.Normalize();

            this.damage = damage;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.runStatistics = runStatistics;
            this.pool = pool;
            this.expireTime = Time.time + DEFAULT_LIFETIME;
            this.isReturned = false;
            this.moveSpeed = DEFAULT_MOVE_SPEED;
            this.aoeRadius = DEFAULT_AOE_RADIUS;
            this.damageSourceId = SynergyIds.CataclysmVortex;
            this.spinVisual = true;

            transform.position = startPos;
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float dt)
        {
            if (isReturned || dt <= 0f) return;

            transform.position += moveDirection * (moveSpeed * dt);
            if (spinVisual) transform.Rotate(360f * dt, 240f * dt, 0f);

            int contactHits = Physics.OverlapSphereNonAlloc(transform.position, 0.55f, hitBuffer, enemyLayer);
            bool hitLiving = false;
            for (int i = 0; i < contactHits; i++)
            {
                if (hitBuffer[i] != null &&
                    hitBuffer[i].TryGetComponent<IDamageable>(out var dmg) &&
                    !dmg.IsDead)
                {
                    hitLiving = true;
                    break;
                }
            }

            if (hitLiving || Time.time >= expireTime)
            {
                Detonate();
            }
        }

        public void Detonate()
        {
            if (isReturned) return;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, aoeRadius, hitBuffer, enemyLayer);
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
                    int dealt = enemy.TakeDamageFrom(finalDmg, isCrit, transform.position, isAoE: true);
                    if (dealt > 0) runStatistics?.RecordDamage(damageSourceId, dealt);
                }
                else
                {
                    damageable.TakeDamage(finalDmg);
                }
            }

            ReturnToPool();
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
