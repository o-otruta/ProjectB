using UnityEngine;
using ProjectB.Combat;
using ProjectB.Enemies;
using ProjectB.Player;
using ProjectB.Visuals;
using UnityEngine.Pool;

namespace ProjectB.Abilities
{
    public class LaserTurret : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Transform firePoint;
        [SerializeField] private VoxelMeshAnimator meshAnimator;

        [Header("Aiming Settings")]
        [SerializeField] private float rotationSpeed = 720f;

        private float searchRadius;
        private float dps;
        private float lifetime;
        private LayerMask enemyLayer;
        private IObjectPool<LaserTurret> pool;
        private HeroRuntimeStats runtimeStats;
        private bool isReturned;

        private const float TARGET_SCAN_INTERVAL = 0.2f;

        private float spawnTime;
        private float nextScanTime;
        private Transform currentTarget;
        private IDamageable currentTargetDamageable;
        private Collider[] hitBuffer = new Collider[128];

        public void Initialize(
            float searchRadius,
            float dps,
            float lifetime,
            LayerMask enemyLayer,
            IObjectPool<LaserTurret> pool,
            HeroRuntimeStats runtimeStats = null)
        {
            this.searchRadius = searchRadius;
            this.dps = dps;
            this.lifetime = lifetime;
            this.enemyLayer = enemyLayer;
            this.pool = pool;
            this.runtimeStats = runtimeStats;
            this.isReturned = false;

            spawnTime = Time.time;
            nextScanTime = 0f;
            currentTarget = null;
            currentTargetDamageable = null;
            accumulatedDamage = 0f;

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                    lineRenderer.startWidth = 0.1f;
                    lineRenderer.endWidth = 0.1f;
                    lineRenderer.startColor = Color.cyan;
                    lineRenderer.endColor = Color.cyan;
                }
            }
            lineRenderer.enabled = false;

            if (meshAnimator == null)
            {
                meshAnimator = GetComponentInChildren<VoxelMeshAnimator>();
            }
            meshAnimator?.Stop();
        }

        private void OnDisable()
        {
            currentTarget = null;
            currentTargetDamageable = null;
            accumulatedDamage = 0f;
            if (lineRenderer != null) lineRenderer.enabled = false;
            meshAnimator?.Stop();
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time >= spawnTime + lifetime)
            {
                ReturnToPool();
                return;
            }

            FindTarget();

            if (currentTarget != null)
            {
                Vector3 targetPos = currentTarget.position;
                Vector3 targetDir = targetPos - transform.position;
                targetDir.y = 0;
                if (targetDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }

                lineRenderer.enabled = true;
                Vector3 origin = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 0.5f);
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, targetPos + Vector3.up * 0.5f);

                meshAnimator?.Play();

                if (currentTargetDamageable != null && !currentTargetDamageable.IsDead)
                {
                    float effectiveDps = runtimeStats != null ? dps * runtimeStats.AbilityDamageMultiplier : dps;
                    accumulatedDamage += effectiveDps * Time.deltaTime;
                    if (accumulatedDamage >= 1f)
                    {
                        int baseDmg = Mathf.FloorToInt(accumulatedDamage);
                        accumulatedDamage -= baseDmg;
                        bool isCrit = false;
                        int finalDmg = runtimeStats != null
                            ? runtimeStats.RollDamage(baseDmg, out isCrit)
                            : baseDmg;

                        if (currentTargetDamageable is EnemyBase enemy)
                        {
                            enemy.TakeDamage(finalDmg, isCrit);
                        }
                        else
                        {
                            currentTargetDamageable.TakeDamage(finalDmg);
                        }
                    }
                }
                else
                {
                    currentTarget = null;
                    currentTargetDamageable = null;
                }
            }
            else
            {
                lineRenderer.enabled = false;
                accumulatedDamage = 0f;
                meshAnimator?.Stop();
            }
        }

        private float accumulatedDamage = 0f;

        private void FindTarget()
        {
            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            {
                if ((currentTarget.position - transform.position).sqrMagnitude <= searchRadius * searchRadius &&
                    currentTargetDamageable != null && !currentTargetDamageable.IsDead)
                {
                    return;
                }
            }

            currentTarget = null;
            currentTargetDamageable = null;

            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + TARGET_SCAN_INTERVAL;

            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, hitBuffer, enemyLayer);
            if (targetsFound == 0) return;

            float minSqrDist = float.MaxValue;

            for (int i = 0; i < targetsFound; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) ||
                    !CombatTargetFilter.IsValidAutoAimTarget(col, damageable, enemyLayer))
                {
                    continue;
                }

                var target = col.transform;
                float sqrDist = (target.position - transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    currentTarget = target;
                    currentTargetDamageable = damageable;
                }
            }
        }

        public void Despawn()
        {
            ReturnToPool();
        }

        public void ReturnToPool()
        {
            if (isReturned) return;
            isReturned = true;

            currentTarget = null;
            currentTargetDamageable = null;
            accumulatedDamage = 0f;
            if (lineRenderer != null) lineRenderer.enabled = false;
            meshAnimator?.Stop();

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
