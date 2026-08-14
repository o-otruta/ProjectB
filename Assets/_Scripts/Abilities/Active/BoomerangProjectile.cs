using UnityEngine;
using System.Collections.Generic;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class BoomerangProjectile : AbilityProjectile
    {
        private float throwRange;
        private int pierceCount;
        private int currentPierces;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private bool returning;
        private Transform returnTarget;

        private HashSet<EntityId> damagedEnemies = new HashSet<EntityId>();
        private Collider[] overlapHits = new Collider[20];
        private float hitRadius = 1f;

        public void InitializeBoomerang(float damage, float speed, float throwRange, int pierceCount, Vector3 direction, Transform returnTarget, LayerMask enemyLayer, UnityEngine.Pool.IObjectPool<AbilityProjectile> pool)
        {
            this.damage = damage;
            this.speed = speed;
            this.throwRange = throwRange;
            this.pierceCount = pierceCount;
            this.returnTarget = returnTarget;
            this.enemyLayer = enemyLayer;
            this.pool = pool;
            this.isReturned = false;

            startPosition = transform.position;
            targetPosition = startPosition + direction.normalized * throwRange;
            returning = false;
            currentPierces = 0;
            damagedEnemies.Clear();
            
            transform.localScale = new Vector3(1.5f, 0.2f, 1.5f);
        }

        protected override void Update()
        {
            if (isReturned) return;

            transform.Rotate(Vector3.up * 720f * Time.deltaTime);

            Vector3 destination = returning ? (returnTarget != null ? returnTarget.position : startPosition) : targetPosition;
            Vector3 toDestination = destination - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (toDestination.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                if (returning)
                {
                    ReturnToPool();
                }
                else
                {
                    returning = true;
                    damagedEnemies.Clear(); 
                }
            }
            else
            {
                transform.position += toDestination.normalized * distanceThisFrame;
            }

            CheckCollisions();
        }

        private void CheckCollisions()
        {
            if (currentPierces >= pierceCount) return;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, hitRadius, overlapHits, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                if (currentPierces >= pierceCount) break;

                var hitCol = overlapHits[i];
                if (hitCol.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    EntityId id = hitCol.gameObject.GetEntityId();
                    if (!damagedEnemies.Contains(id))
                    {
                        damageable.TakeDamage((int)damage);
                        damagedEnemies.Add(id);
                        currentPierces++;
                    }
                }
            }
        }

        protected override void HitTarget()
        {
            // Do not use default hit target, handle in CheckCollisions
        }
    }
}
