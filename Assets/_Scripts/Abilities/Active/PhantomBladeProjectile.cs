using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class PhantomBladeProjectile : MonoBehaviour
    {
        private const float MAX_DISTANCE = 7.5f;
        private const float MOVE_SPEED = 12f;
        private const float SPIRAL_TURN_DEG_PER_SEC = 45f;
        private const float HIT_RADIUS = 0.65f;
        private static readonly Collider[] hitBuffer = new Collider[24];

        private Vector3 moveDirection;
        private float traveledDistance;
        private float damage;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private RunStatistics runStatistics;
        private IObjectPool<PhantomBladeProjectile> pool;
        private bool isReturned;
        private Vector3 baseScale = new Vector3(0.22f, 0.12f, 0.72f);
        private bool baseScaleCaptured;
        private readonly HashSet<EntityId> hitIds = new HashSet<EntityId>(32);

        private void Awake()
        {
            CaptureBaseScale();
        }

        private void CaptureBaseScale()
        {
            if (!baseScaleCaptured)
            {
                baseScale = transform.localScale;
                baseScaleCaptured = true;
            }
        }

        public void Initialize(
            Vector3 startPos,
            Vector3 direction,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            RunStatistics runStatistics,
            IObjectPool<PhantomBladeProjectile> pool,
            float visualScale = 1f)
        {
            CaptureBaseScale();
            transform.localScale = baseScale * Mathf.Max(0.5f, visualScale);

            moveDirection = direction;
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude < 0.0001f) moveDirection = Vector3.forward;
            moveDirection.Normalize();

            this.damage = damage;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.runStatistics = runStatistics;
            this.pool = pool;
            this.traveledDistance = 0f;
            this.isReturned = false;
            this.hitIds.Clear();

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

            moveDirection = Quaternion.Euler(0f, SPIRAL_TURN_DEG_PER_SEC * dt, 0f) * moveDirection;
            float step = MOVE_SPEED * dt;
            transform.position += moveDirection * step;
            transform.rotation = Quaternion.LookRotation(moveDirection);
            traveledDistance += step;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, HIT_RADIUS, hitBuffer, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                EntityId id = col.gameObject.GetEntityId();
                if (!hitIds.Add(id)) continue;

                bool isCrit = false;
                int finalDmg = runtimeStats != null
                    ? runtimeStats.RollDamage(damage, out isCrit)
                    : Mathf.Max(1, Mathf.RoundToInt(damage));

                if (damageable is EnemyBase enemy)
                {
                    enemy.TakeDamage(finalDmg, isCrit);
                    runStatistics?.RecordDamage(SynergyIds.BladeStorm, finalDmg);
                }
                else
                {
                    damageable.TakeDamage(finalDmg);
                }
            }

            if (traveledDistance >= MAX_DISTANCE)
            {
                ReturnToPool();
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
