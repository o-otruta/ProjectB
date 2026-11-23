using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
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
        private RunStatistics runStatistics;
        private string sourceId;
        private int remainingPierces;
        private int hitsDealt;
        private Action<Vector3, float> onFuryHit;
        private IObjectPool<DragonFlameProjectile> pool;
        private float expireTime;
        private bool isReturned;
        private Vector3 baseScale = new Vector3(0.28f, 0.28f, 0.42f);
        private bool baseScaleCaptured;
        private readonly HashSet<EntityId> hitIds = new HashSet<EntityId>(8);

        public int RemainingPierces => remainingPierces;

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
            float speed,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            IObjectPool<DragonFlameProjectile> pool,
            int pierceCount = 1,
            Action<Vector3, float> onFuryHit = null,
            RunStatistics runStatistics = null,
            string sourceId = "MiniDragon",
            float visualScale = 1f)
        {
            CaptureBaseScale();
            float furyScale = pierceCount > 1 ? 1.35f : 1f;
            transform.localScale = baseScale * (furyScale * Mathf.Max(0.5f, visualScale));

            this.moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            this.speed = Mathf.Max(1f, speed);
            this.damage = damage;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.runStatistics = runStatistics;
            this.sourceId = !string.IsNullOrEmpty(sourceId) ? sourceId : "MiniDragon";
            this.remainingPierces = Mathf.Max(1, pierceCount);
            this.hitsDealt = 0;
            this.onFuryHit = onFuryHit;
            this.pool = pool;
            this.expireTime = Time.time + MAX_LIFETIME;
            this.isReturned = false;
            this.hitIds.Clear();

            transform.position = startPos;
            transform.rotation = Quaternion.LookRotation(this.moveDirection);
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float dt)
        {
            if (isReturned || dt <= 0f) return;

            if (Time.time >= expireTime)
            {
                ReturnToPool();
                return;
            }

            transform.position += moveDirection * (speed * dt);

            int hits = Physics.OverlapSphereNonAlloc(transform.position, 0.5f, hitBuffer, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                if (remainingPierces <= 0) break;

                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                EntityId id = col.gameObject.GetEntityId();
                if (!hitIds.Add(id)) continue;

                bool isCrit = false;
                int finalDmg = runtimeStats != null
                    ? runtimeStats.RollDamage(damage, out isCrit)
                    : Mathf.Max(1, Mathf.RoundToInt(damage));

                hitsDealt++;
                if (damageable is EnemyBase enemy)
                {
                    int dealt = enemy.TakeDamageFrom(finalDmg, isCrit, transform.position - moveDirection * 2f, isAoE: false);
                    string attrSource = hitsDealt > 1 ? SynergyIds.DragonLord : sourceId;
                    if (dealt > 0) runStatistics?.RecordDamage(attrSource, dealt);
                }
                else
                {
                    damageable.TakeDamage(finalDmg);
                }

                onFuryHit?.Invoke(col.transform.position, damage * 0.45f);

                remainingPierces--;
                if (remainingPierces <= 0)
                {
                    ReturnToPool();
                    return;
                }
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
