using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    public class FireballProjectile : AbilityProjectile
    {
        private static readonly Collider[] explosionHits = new Collider[32];

        private float explosionRadius;
        private Vector3 lastKnownTargetPos;
        private GameEventBus eventBus;

        public void InitializeFireball(
            float damage,
            float speed,
            float explosionRadius,
            Transform target,
            LayerMask enemyLayer,
            UnityEngine.Pool.IObjectPool<AbilityProjectile> pool,
            GameEventBus eventBus = null)
        {
            base.Initialize(damage, speed, target, enemyLayer, pool);
            this.explosionRadius = explosionRadius;
            this.lastKnownTargetPos = target != null ? target.position : transform.position;
            this.eventBus = eventBus;
        }

        protected override void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            // Если цель жива — обновляем точку взрыва; если умерла в полёте — долетаем до последней позиции и взрываемся
            if (target != null && target.gameObject.activeInHierarchy && (targetDamageable == null || !targetDamageable.IsDead))
            {
                lastKnownTargetPos = target.position;
            }

            Vector3 toTarget = lastKnownTargetPos - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                transform.position = lastKnownTargetPos;
                HitTarget();
                return;
            }

            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * distanceThisFrame;
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        protected override void HitTarget()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, explosionHits, enemyLayer);
            int finalDamage = Mathf.RoundToInt(damage);
            for (int i = 0; i < hits; i++)
            {
                if (explosionHits[i] != null && explosionHits[i].TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    damageable.TakeDamage(finalDamage);
                }
            }

            eventBus?.Publish(new FireballExplodedEvent(transform.position, explosionRadius));
            ReturnToPool();
        }
    }
}
