using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class AbilityProjectile : MonoBehaviour
    {
        private const float TURRET_OVERCHARGE_RADIUS = 2.4f;
        private static readonly Collider[] pierceBuffer = new Collider[16];

        protected IObjectPool<AbilityProjectile> pool;
        protected Transform target;
        protected IDamageable targetDamageable;
        protected float damage;
        protected float speed;
        protected LayerMask enemyLayer;
        protected HeroRuntimeStats runtimeStats;
        protected RunStatistics runStatistics;
        protected string sourceId;
        protected bool isReturned;
        protected float spawnTime;
        protected const float MAX_LIFETIME = 8f;

        protected bool canOverchargeFromTurrets;
        protected bool isOvercharged;
        protected int remainingPierces;
        protected bool piercingLinearFlight;
        protected Vector3 lastMoveDir = Vector3.forward;
        protected Vector3 baseScale = Vector3.one;
        protected bool baseScaleCaptured;
        private readonly HashSet<EntityId> piercedIds = new HashSet<EntityId>(8);

        public bool IsOvercharged => isOvercharged;
        public int RemainingPierces => remainingPierces;

        protected virtual void Awake()
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

        public virtual void Initialize(
            float damage,
            float speed,
            Transform target,
            LayerMask enemyLayer,
            IObjectPool<AbilityProjectile> pool,
            HeroRuntimeStats runtimeStats = null)
        {
            CaptureBaseScale();
            transform.localScale = baseScale;

            this.damage = damage;
            this.speed = speed;
            this.target = target;
            this.targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;
            this.enemyLayer = enemyLayer;
            this.pool = pool;
            this.runtimeStats = runtimeStats;
            this.isReturned = false;
            this.spawnTime = Time.time;

            this.runStatistics = null;
            this.sourceId = null;
            this.canOverchargeFromTurrets = false;
            this.isOvercharged = false;
            this.remainingPierces = 0;
            this.piercingLinearFlight = false;
            this.lastMoveDir = target != null
                ? (target.position - transform.position).normalized
                : transform.forward;
            if (this.lastMoveDir.sqrMagnitude < 0.0001f) this.lastMoveDir = Vector3.forward;
            this.piercedIds.Clear();
        }

        public void ConfigureCombatContext(
            RunStatistics runStatistics,
            string sourceId,
            bool canOverchargeFromTurrets = false,
            float visualScale = 1f)
        {
            CaptureBaseScale();
            this.runStatistics = runStatistics;
            this.sourceId = sourceId;
            this.canOverchargeFromTurrets = canOverchargeFromTurrets;
            transform.localScale = baseScale * Mathf.Max(0.5f, visualScale);
        }

        protected virtual void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float dt)
        {
            if (isReturned || dt <= 0f) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            if (canOverchargeFromTurrets && !isOvercharged)
            {
                TryOverchargeNearTurret();
            }

            if (piercingLinearFlight)
            {
                StepPiercingFlight(dt);
                return;
            }

            if (target == null || !target.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
            {
                ReturnToPool();
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            float distanceThisFrame = speed * dt;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                lastMoveDir = toTarget.normalized;
            }

            if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                transform.position = target.position;
                HitTarget();
                return;
            }

            transform.position += lastMoveDir * distanceThisFrame;
            transform.rotation = Quaternion.LookRotation(lastMoveDir);
        }

        private void TryOverchargeNearTurret()
        {
            IReadOnlyList<LaserTurret> turrets = LaserTurret.ActiveTurrets;
            if (turrets == null || turrets.Count == 0) return;

            float maxSqr = TURRET_OVERCHARGE_RADIUS * TURRET_OVERCHARGE_RADIUS;
            Vector3 pos = transform.position;

            for (int i = 0; i < turrets.Count; i++)
            {
                LaserTurret turret = turrets[i];
                if (turret == null || !turret.gameObject.activeInHierarchy) continue;

                Vector3 diff = turret.transform.position - pos;
                diff.y = 0f;
                if (diff.sqrMagnitude <= maxSqr)
                {
                    isOvercharged = true;
                    remainingPierces += 2;
                    speed *= 1.25f;
                    transform.localScale *= 1.25f;
                    return;
                }
            }
        }

        private void StepPiercingFlight(float dt)
        {
            transform.position += lastMoveDir * (speed * dt);
            transform.rotation = Quaternion.LookRotation(lastMoveDir);

            int hits = Physics.OverlapSphereNonAlloc(transform.position, 0.7f, pierceBuffer, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                if (remainingPierces <= 0) break;

                Collider col = pierceBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                EntityId id = col.gameObject.GetEntityId();
                if (!piercedIds.Add(id)) continue;

                int finalDamage = RollHitDamage(damage, out bool isCrit);
                DealDamage(damageable, finalDamage, isCrit);
                if (damageable is EnemyBase)
                {
                    runStatistics?.RecordDamage(SynergyIds.OverchargedGrid, finalDamage);
                }

                remainingPierces--;
                if (remainingPierces <= 0)
                {
                    ReturnToPool();
                    return;
                }
            }
        }

        protected virtual void HitTarget()
        {
            if (targetDamageable != null && !targetDamageable.IsDead)
            {
                int finalDamage = RollHitDamage(damage, out bool isCrit);
                DealDamage(targetDamageable, finalDamage, isCrit);
                if (targetDamageable is EnemyBase && !string.IsNullOrEmpty(sourceId))
                {
                    runStatistics?.RecordDamage(sourceId, finalDamage);
                }
            }

            if (isOvercharged && remainingPierces > 0)
            {
                if (target != null)
                {
                    piercedIds.Add(target.gameObject.GetEntityId());
                }
                piercingLinearFlight = true;
                return;
            }

            ReturnToPool();
        }

        protected int RollHitDamage(float rawDamage, out bool isCrit)
        {
            if (runtimeStats != null)
            {
                return runtimeStats.RollDamage(rawDamage, out isCrit);
            }
            isCrit = false;
            return Mathf.RoundToInt(rawDamage);
        }

        protected static void DealDamage(IDamageable victim, int finalDamage, bool isCrit)
        {
            if (victim == null || victim.IsDead || finalDamage <= 0) return;
            if (victim is EnemyBase enemy)
            {
                enemy.TakeDamage(finalDamage, isCrit);
            }
            else
            {
                victim.TakeDamage(finalDamage);
            }
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
