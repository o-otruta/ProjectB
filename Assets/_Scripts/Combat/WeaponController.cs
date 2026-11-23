using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core;
using ProjectB.Data.Combat;
using ProjectB.Player;

namespace ProjectB.Combat
{
    public class WeaponController
    {
        private WeaponData weaponData;
        private LayerMask enemyLayer;
        private Transform ownerTransform;
        private Transform firePoint;
        private Transform projectileContainer;
        private HeroRuntimeStats runtimeStats;
        private HeroHealth ownerHealth;
        private RunStatistics runStatistics;

        private IObjectPool<Projectile> projectilePool;
        private float nextFireTime;
        private Collider[] overlapResults = new Collider[200]; // Буфер для поиска врагов
        private float damageMultiplier;

        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = value;
        }

        public const float MULTISHOT_LATERAL_SPACING = 0.35f;

        /// <summary>Extra projectiles per volley from WeaponMod_Multishot cards.</summary>
        public int ExtraProjectiles { get; set; }

        /// <summary>Attack speed multiplier from WeaponMod_FireRate cards (cooldown / multiplier).</summary>
        public float FireRateMultiplier { get; set; } = 1f;

        public int ProjectilesPerVolley => 1 + Mathf.Max(0, ExtraProjectiles);
        public WeaponData Data => weaponData;
        public int LastVolleyCount { get; private set; }

        private readonly Transform[] volleyTargets = new Transform[16];

        public float EffectiveAttackCooldown
        {
            get
            {
                if (weaponData == null) return 0f;
                float baseCooldown = weaponData.attackCooldown / Mathf.Max(0.1f, FireRateMultiplier);
                return runtimeStats != null
                    ? runtimeStats.GetEffectiveCooldown(baseCooldown, 0.08f)
                    : Mathf.Max(0.08f, baseCooldown);
            }
        }

        public WeaponController(
            WeaponData data,
            LayerMask enemyLayer,
            Transform ownerTransform,
            Transform firePoint,
            Transform rootContainer,
            float damageMultiplier = 1f,
            HeroRuntimeStats runtimeStats = null,
            HeroHealth ownerHealth = null,
            RunStatistics runStatistics = null)
        {
            this.weaponData = data;
            this.enemyLayer = enemyLayer;
            this.ownerTransform = ownerTransform;
            this.firePoint = firePoint;
            this.damageMultiplier = damageMultiplier;
            this.runtimeStats = runtimeStats;
            this.ownerHealth = ownerHealth;
            this.runStatistics = runStatistics;

            projectileContainer = new GameObject($"ProjectileContainer_{data.name}").transform;
            projectileContainer.SetParent(rootContainer);

            InitializePool();
        }

        private void InitializePool()
        {
            projectilePool = new ObjectPool<Projectile>(
                createFunc: () => {
                    if (weaponData.projectilePrefab == null) return null;
                    var obj = Object.Instantiate(weaponData.projectilePrefab, projectileContainer);
                    var p = obj.GetComponent<Projectile>();
                    if (p == null) {
                        Debug.LogError("Projectile prefab is missing Projectile component!");
                    }
                    return p;
                },
                actionOnGet: p => { if (p != null) p.gameObject.SetActive(true); },
                actionOnRelease: p => { if (p != null) p.gameObject.SetActive(false); },
                actionOnDestroy: p =>
                {
                    if (p != null)
                    {
                        if (Application.isPlaying) Object.Destroy(p.gameObject);
                        else Object.DestroyImmediate(p.gameObject);
                    }
                },
                collectionCheck: true,
                defaultCapacity: 20,
                maxSize: 100
            );

            if (Application.isPlaying)
            {
                PrewarmPool(12);
            }
        }

        public void PrewarmPool(int count = 20)
        {
            if (projectilePool == null || weaponData == null || weaponData.projectilePrefab == null || count <= 0) return;

            var temp = new Projectile[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = projectilePool.Get();
            }
            for (int i = 0; i < count; i++)
            {
                if (temp[i] != null) projectilePool.Release(temp[i]);
            }
        }

        private const float EMPTY_SCAN_COOLDOWN = 0.15f;

        public void UpdateController()
        {
            if (weaponData == null || projectilePool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                Transform target = FindClosestEnemy();
                if (target != null)
                {
                    Fire(target);
                    nextFireTime = Time.time + EffectiveAttackCooldown;
                }
                else
                {
                    nextFireTime = Time.time + EMPTY_SCAN_COOLDOWN;
                }
            }
        }

        private Transform FindClosestEnemy()
        {
            Vector3 center = firePoint != null ? firePoint.position : (ownerTransform != null ? ownerTransform.position : Vector3.zero);
            int hitCount = Physics.OverlapSphereNonAlloc(center, weaponData.range, overlapResults, enemyLayer);
            
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = overlapResults[i];
                if (col == null) continue;
                if (col.TryGetComponent<IDamageable>(out var damageable) &&
                    !CombatTargetFilter.IsValidAutoAimTarget(col, damageable, enemyLayer))
                {
                    continue;
                }

                Transform potentialTarget = col.transform;
                Vector3 directionToTarget = potentialTarget.position - center;
                float dSqrToTarget = directionToTarget.sqrMagnitude;

                if (dSqrToTarget < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToTarget;
                    closestTarget = potentialTarget;
                }
            }

            return closestTarget;
        }

        /// <summary>Fires one volley at the given primary target (exposed for tests).</summary>
        public int FireVolley(Transform target)
        {
            Fire(target);
            return LastVolleyCount;
        }

        private void Fire(Transform target)
        {
            int volley = Mathf.Min(ProjectilesPerVolley, volleyTargets.Length);
            int targetCount = 1;
            volleyTargets[0] = target;
            if (volley > 1)
            {
                targetCount = CollectExtraTargets(target, volley);
            }

            Vector3 origin = firePoint != null ? firePoint.position : (ownerTransform != null ? ownerTransform.position : Vector3.zero);
            Vector3 toTarget = target != null ? target.position - origin : Vector3.forward;
            toTarget.y = 0f;
            Vector3 lateral = toTarget.sqrMagnitude > 0.0001f
                ? Vector3.Cross(Vector3.up, toTarget.normalized)
                : Vector3.right;

            int fired = 0;
            for (int i = 0; i < volley; i++)
            {
                Projectile p = projectilePool.Get();
                if (p == null) continue;

                // Fan layout: 0, +1, -1, +2, -2 ... lateral offsets around the muzzle.
                int side = (i + 1) / 2;
                float sign = (i % 2 == 1) ? 1f : -1f;
                Vector3 offset = i == 0 ? Vector3.zero : lateral * (side * sign * MULTISHOT_LATERAL_SPACING);

                p.transform.position = origin + offset;
                Transform chosen = volleyTargets[i % targetCount];
                p.Initialize(weaponData, chosen, projectilePool, damageMultiplier, enemyLayer, runtimeStats, ownerHealth, runStatistics);
                fired++;
            }

            for (int i = 0; i < targetCount; i++) volleyTargets[i] = null;
            LastVolleyCount = fired;
        }

        /// <summary>Collects up to <paramref name="maxTargets"/> distinct closest valid targets (primary first). Zero-GC.</summary>
        private int CollectExtraTargets(Transform primary, int maxTargets)
        {
            Vector3 center = firePoint != null ? firePoint.position : (ownerTransform != null ? ownerTransform.position : Vector3.zero);
            int hitCount = Physics.OverlapSphereNonAlloc(center, weaponData.range, overlapResults, enemyLayer);
            int count = 1;

            while (count < maxTargets)
            {
                Transform best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < hitCount; i++)
                {
                    Collider col = overlapResults[i];
                    if (col == null) continue;
                    Transform t = col.transform;
                    bool used = false;
                    for (int u = 0; u < count; u++)
                    {
                        if (volleyTargets[u] == t) { used = true; break; }
                    }
                    if (used) continue;
                    if (col.TryGetComponent<IDamageable>(out var dmg) &&
                        !CombatTargetFilter.IsValidAutoAimTarget(col, dmg, enemyLayer))
                    {
                        continue;
                    }

                    float d = (t.position - center).sqrMagnitude;
                    if (d < bestSqr)
                    {
                        bestSqr = d;
                        best = t;
                    }
                }

                if (best == null) break;
                volleyTargets[count++] = best;
            }

            return count;
        }

        public void DrawGizmos()
        {
            if (weaponData != null)
            {
                Gizmos.color = Color.red;
                Vector3 center = firePoint != null ? firePoint.position : (ownerTransform != null ? ownerTransform.position : Vector3.zero);
                Gizmos.DrawWireSphere(center, weaponData.range);
            }
        }
    }
}
