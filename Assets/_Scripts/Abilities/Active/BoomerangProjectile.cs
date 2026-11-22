using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.Player;

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
        private Action onHitEnemyCallback;

        private readonly HashSet<EntityId> damagedEnemies = new HashSet<EntityId>(32);
        private readonly Collider[] overlapHits = new Collider[64];
        private float hitRadius = 1f;

        public void InitializeBoomerang(
            float damage,
            float speed,
            float throwRange,
            int pierceCount,
            Vector3 direction,
            Transform returnTarget,
            LayerMask enemyLayer,
            UnityEngine.Pool.IObjectPool<AbilityProjectile> pool,
            HeroRuntimeStats runtimeStats = null,
            Action onHitEnemyCallback = null,
            RunStatistics runStatistics = null,
            string sourceId = "Boomerang",
            float visualScale = 1f)
        {
            this.damage = damage;
            this.speed = speed;
            this.throwRange = throwRange;
            this.pierceCount = pierceCount;
            this.returnTarget = returnTarget;
            this.enemyLayer = enemyLayer;
            this.pool = pool;
            this.runtimeStats = runtimeStats;
            this.onHitEnemyCallback = onHitEnemyCallback;
            this.runStatistics = runStatistics;
            this.sourceId = !string.IsNullOrEmpty(sourceId) ? sourceId : "Boomerang";
            this.isReturned = false;
            this.spawnTime = Time.time;

            startPosition = transform.position;
            targetPosition = startPosition + direction.normalized * throwRange;
            returning = false;
            currentPierces = 0;
            damagedEnemies.Clear();

            transform.localScale = Vector3.one * Mathf.Max(0.5f, visualScale);
        }

        protected override void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time >= spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            transform.Rotate(Vector3.up * 720f * Time.deltaTime);

            Vector3 destination = returning ? (returnTarget != null ? returnTarget.position : startPosition) : targetPosition;
            Vector3 toDestination = destination - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (toDestination.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                if (returning)
                {
                    ReturnToPool();
                    return;
                }
                else
                {
                    returning = true;
                    currentPierces = 0;
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
                if (hitCol != null && hitCol.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    EntityId id = hitCol.gameObject.GetEntityId();
                    if (!damagedEnemies.Contains(id))
                    {
                        int finalDamage = RollHitDamage(damage, out bool isCrit);
                        DealDamage(damageable, finalDamage, isCrit);
                        if (damageable is EnemyBase)
                        {
                            runStatistics?.RecordDamage(sourceId, finalDamage);
                            onHitEnemyCallback?.Invoke();
                        }
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
