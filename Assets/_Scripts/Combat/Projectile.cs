using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Combat
{
    public class Projectile : MonoBehaviour
    {
        private const float MAX_LIFETIME = 5f;
        private static readonly Collider[] splashHits = new Collider[24];

        private IObjectPool<Projectile> pool;
        private Transform target;
        private IDamageable targetDamageable;
        private WeaponData data;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private HeroHealth ownerHealth;
        private RunStatistics runStatistics;
        private bool isReturned;
        private float damageMultiplier = 1f;
        private float spawnTime;
        private int remainingPierces;
        private readonly EntityId[] hitHistory = new EntityId[8];
        private int hitHistoryCount;

        public int RemainingPierces => remainingPierces;
        public Transform CurrentTarget => target;

        public void Initialize(
            WeaponData weaponData,
            Transform targetTransform,
            IObjectPool<Projectile> projectilePool,
            float damageMultiplier = 1f,
            LayerMask enemyLayer = default,
            HeroRuntimeStats runtimeStats = null,
            HeroHealth ownerHealth = null,
            RunStatistics runStatistics = null)
        {
            data = weaponData;
            target = targetTransform;
            targetDamageable = targetTransform != null ? targetTransform.GetComponent<IDamageable>() : null;
            pool = projectilePool;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.ownerHealth = ownerHealth;
            this.runStatistics = runStatistics;
            isReturned = false;
            this.damageMultiplier = damageMultiplier;
            spawnTime = Time.time;
            remainingPierces = weaponData != null ? Mathf.Max(0, weaponData.pierceCount) : 0;
            hitHistoryCount = 0;

            if (target != null)
            {
                Vector3 initDir = target.position - transform.position;
                if (initDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(initDir.normalized);
                }
            }
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            // Ограничение максимального времени жизни, чтобы избежать зависания и утечки пула
            if (Time.time >= spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            // Если цель уничтожена или выключена, возвращаем снаряд в пул
            if (target == null || !target.gameObject.activeInHierarchy || (targetDamageable != null && targetDamageable.IsDead))
            {
                ReturnToPool();
                return;
            }

            if (data == null || data.projectileSpeed <= 0f)
            {
                ReturnToPool();
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            float distanceThisFrame = data.projectileSpeed * Time.deltaTime;
            
            // Если шаг за этот кадр >= оставшаяся дистанция — считаем попадание
            if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                HitTarget();
                return;
            }

            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * distanceThisFrame;
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDir);
            }
        }

        /// <summary>Forces an immediate hit on the current target (used by tests).</summary>
        public void SimulateHit()
        {
            if (!isReturned) HitTarget();
        }

        private void HitTarget()
        {
            float rawDamage = data != null ? data.damage * damageMultiplier : 0f;
            if (target != null && hitHistoryCount < hitHistory.Length)
            {
                hitHistory[hitHistoryCount++] = target.gameObject.GetEntityId();
            }

            if (targetDamageable != null && !targetDamageable.IsDead && rawDamage > 0f)
            {
                int finalDamage = RollHitDamage(rawDamage, out bool isCrit);
                Vector3 hitOrigin = transform.position - transform.forward * 2f;
                DealDamage(targetDamageable, finalDamage, isCrit, hitOrigin, isAoE: false);
                TryApplyLifesteal();
            }

            if (data != null && data.splashRadius > 0f && rawDamage > 0f && enemyLayer.value != 0)
            {
                float splash = runtimeStats != null ? runtimeStats.GetScaledArea(data.splashRadius) : data.splashRadius;
                int hits = Physics.OverlapSphereNonAlloc(transform.position, splash, splashHits, enemyLayer);
                for (int i = 0; i < hits; i++)
                {
                    if (splashHits[i] != null &&
                        splashHits[i].TryGetComponent<IDamageable>(out var splashTarget) &&
                        splashTarget != targetDamageable &&
                        !splashTarget.IsDead)
                    {
                        int splashDamage = RollHitDamage(rawDamage, out bool splashCrit);
                        DealDamage(splashTarget, splashDamage, splashCrit, transform.position, isAoE: true);
                    }
                }
            }

            if (remainingPierces > 0 && TryRetargetForPierce())
            {
                remainingPierces--;
                return;
            }

            ReturnToPool();
        }

        private bool TryRetargetForPierce()
        {
            if (data == null || enemyLayer.value == 0) return false;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, data.pierceRetargetRadius, splashHits, enemyLayer);
            Transform best = null;
            IDamageable bestDamageable = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = splashHits[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var dmg) || dmg.IsDead) continue;
                if (!CombatTargetFilter.IsValidAutoAimTarget(col, dmg, enemyLayer)) continue;

                EntityId id = col.gameObject.GetEntityId();
                bool alreadyHit = false;
                for (int h = 0; h < hitHistoryCount; h++)
                {
                    if (hitHistory[h] == id) { alreadyHit = true; break; }
                }
                if (alreadyHit) continue;

                float d = (col.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = col.transform;
                    bestDamageable = dmg;
                }
            }

            if (best == null) return false;
            target = best;
            targetDamageable = bestDamageable;
            spawnTime = Time.time;
            return true;
        }

        private void TryApplyLifesteal()
        {
            if (data == null || data.lifestealAmount <= 0 || data.lifestealChance <= 0f) return;
            if (ownerHealth == null || ownerHealth.IsDead) return;

            if (data.lifestealChance >= 1f || Random.value < data.lifestealChance)
            {
                ownerHealth.Heal(data.lifestealAmount);
            }
        }

        private int RollHitDamage(float rawDamage, out bool isCrit)
        {
            if (runtimeStats != null)
            {
                return runtimeStats.RollDamage(rawDamage, out isCrit);
            }
            isCrit = false;
            return Mathf.RoundToInt(rawDamage);
        }

        private void DealDamage(IDamageable victim, int damage, bool isCrit, Vector3 sourcePosition, bool isAoE)
        {
            if (victim == null || victim.IsDead || damage <= 0) return;
            if (victim is EnemyBase enemy)
            {
                int dealt = enemy.TakeDamageFrom(damage, isCrit, sourcePosition, isAoE);
                runStatistics?.RecordDamage(DamageSourceIds.Weapon, dealt);
            }
            else
            {
                victim.TakeDamage(damage);
            }
        }

        private void ReturnToPool()
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
