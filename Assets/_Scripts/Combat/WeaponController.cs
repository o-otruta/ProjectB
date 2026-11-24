using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Combat;
using ProjectB.Enemies;
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
        private IObjectPool<MeleeSweepVfx> meleeVfxPool;
        private Transform facingTransform;
        private readonly IDamageable[] sweepVictims = new IDamageable[200];
        private float nextFireTime;
        private Collider[] overlapResults = new Collider[200]; // Буфер для поиска врагов
        private float damageMultiplier;

        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = value;
        }

        public const float MULTISHOT_LATERAL_SPACING = 0.35f;
        private const float MELEE_SWEEP_HALF_HEIGHT = 4f;

        /// <summary>Extra projectiles per volley from WeaponMod_Multishot cards.</summary>
        public int ExtraProjectiles { get; set; }

        /// <summary>Attack speed multiplier from WeaponMod_FireRate cards (cooldown / multiplier).</summary>
        public float FireRateMultiplier { get; set; } = 1f;

        /// <summary>Melee sweep radius multiplier from WeaponMod_Radius cards (Knight only).</summary>
        public float RadiusMultiplier { get; set; } = 1f;

        public bool IsMelee => weaponData != null && weaponData.attackMode == WeaponAttackMode.MeleeSweep;

        /// <summary>Final sweep radius: base x Radius cards x Global Area.</summary>
        public float EffectiveMeleeRadius
        {
            get
            {
                if (weaponData == null) return 0f;
                float r = weaponData.meleeRadius * Mathf.Max(0.1f, RadiusMultiplier);
                return runtimeStats != null ? runtimeStats.GetScaledArea(r) : r;
            }
        }

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
            RunStatistics runStatistics = null,
            Transform facingTransform = null)
        {
            this.weaponData = data;
            this.enemyLayer = enemyLayer;
            this.ownerTransform = ownerTransform;
            this.firePoint = firePoint;
            this.damageMultiplier = damageMultiplier;
            this.runtimeStats = runtimeStats;
            this.ownerHealth = ownerHealth;
            this.runStatistics = runStatistics;
            this.facingTransform = facingTransform;

            projectileContainer = new GameObject($"ProjectileContainer_{data.name}").transform;
            projectileContainer.SetParent(rootContainer);

            if (IsMelee) InitializeMeleeVfxPool();
            else InitializePool();
        }

        private void InitializeMeleeVfxPool()
        {
            if (weaponData.meleeVfxPrefab == null) return;

            meleeVfxPool = new ObjectPool<MeleeSweepVfx>(
                createFunc: () =>
                {
                    var obj = Object.Instantiate(weaponData.meleeVfxPrefab, projectileContainer);
                    var vfx = obj.GetComponent<MeleeSweepVfx>();
                    if (vfx == null) Debug.LogError("Melee VFX prefab is missing MeleeSweepVfx component!");
                    return vfx;
                },
                actionOnGet: v => { if (v != null) v.gameObject.SetActive(true); },
                actionOnRelease: v => { if (v != null) v.gameObject.SetActive(false); },
                actionOnDestroy: v =>
                {
                    if (v == null) return;
                    if (Application.isPlaying) Object.Destroy(v.gameObject);
                    else Object.DestroyImmediate(v.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: 4,
                maxSize: 12);

            if (Application.isPlaying)
            {
                MeleeSweepVfx a = meleeVfxPool.Get();
                MeleeSweepVfx b = meleeVfxPool.Get();
                if (a != null) meleeVfxPool.Release(a);
                if (b != null) meleeVfxPool.Release(b);
            }
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
            if (weaponData == null || Time.deltaTime <= 0f) return;

            if (IsMelee)
            {
                if (Time.time >= nextFireTime)
                {
                    nextFireTime = Time.time + (Sweep() > 0 ? EffectiveAttackCooldown : EMPTY_SCAN_COOLDOWN);
                }
                return;
            }

            if (projectilePool == null) return;

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

        /// <summary>Flat facing of the hero model (falls back to the owner forward).</summary>
        public Vector3 FacingDirection
        {
            get
            {
                Transform source = facingTransform != null ? facingTransform : ownerTransform;
                Vector3 f = source != null ? source.forward : Vector3.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>Sweep centre: shifted from the hero along the facing by radius x offset ratio.</summary>
        public Vector3 MeleeSweepCenter
        {
            get
            {
                Vector3 origin = ownerTransform != null ? ownerTransform.position : Vector3.zero;
                float offset = weaponData != null ? EffectiveMeleeRadius * weaponData.meleeForwardOffsetRatio : 0f;
                return origin + FacingDirection * offset;
            }
        }

        /// <summary>
        /// Performs one melee sweep: damages every target inside the offset circle. Does nothing (and plays no VFX)
        /// when no valid enemy is inside. Returns the number of damaged targets. Zero-GC.
        /// </summary>
        public int Sweep()
        {
            if (weaponData == null) return 0;

            float radius = EffectiveMeleeRadius;
            Vector3 center = MeleeSweepCenter;
            // Vertical cylinder (capsule) instead of a sphere: the sweep is a flat circle on the ground, so enemies
            // near the rim are hit regardless of collider height, and flyers (Bat, Frost Wisp) are not skipped.
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                center + Vector3.down * MELEE_SWEEP_HALF_HEIGHT,
                center + Vector3.up * MELEE_SWEEP_HALF_HEIGHT,
                radius, overlapResults, enemyLayer);

            bool hasEnemy = false;
            int victims = 0;
            for (int i = 0; i < hitCount && victims < sweepVictims.Length; i++)
            {
                Collider col = overlapResults[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var dmg) || dmg.IsDead) continue;

                bool duplicate = false;
                for (int v = 0; v < victims; v++)
                {
                    if (ReferenceEquals(sweepVictims[v], dmg)) { duplicate = true; break; }
                }
                if (duplicate) continue;

                if (!hasEnemy && CombatTargetFilter.IsValidAutoAimTarget(col, dmg, enemyLayer)) hasEnemy = true;
                sweepVictims[victims++] = dmg;
            }

            if (!hasEnemy)
            {
                for (int v = 0; v < victims; v++) sweepVictims[v] = null;
                LastVolleyCount = 0;
                return 0;
            }

            float rawDamage = weaponData.damage * damageMultiplier;
            Vector3 heroPos = ownerTransform != null ? ownerTransform.position : center;
            int damaged = 0;
            for (int v = 0; v < victims; v++)
            {
                IDamageable victim = sweepVictims[v];
                sweepVictims[v] = null;
                if (victim == null || victim.IsDead || rawDamage <= 0f) continue;

                bool isCrit = false;
                int finalDamage = runtimeStats != null
                    ? runtimeStats.RollDamage(rawDamage, out isCrit)
                    : Mathf.RoundToInt(rawDamage);
                if (finalDamage <= 0) continue;

                if (victim is EnemyBase enemy)
                {
                    int dealt = enemy.TakeDamageFrom(finalDamage, isCrit, heroPos);
                    runStatistics?.RecordDamage(DamageSourceIds.Weapon, dealt);
                }
                else
                {
                    victim.TakeDamage(finalDamage);
                }
                damaged++;
            }

            if (damaged > 0) TryApplyLifesteal();

            if (meleeVfxPool != null)
            {
                MeleeSweepVfx vfx = meleeVfxPool.Get();
                if (vfx != null) vfx.Play(center, FacingDirection, radius, meleeVfxPool, ownerTransform);
            }

            LastVolleyCount = damaged;
            return damaged;
        }

        private void TryApplyLifesteal()
        {
            if (weaponData.lifestealAmount <= 0 || weaponData.lifestealChance <= 0f) return;
            if (ownerHealth == null || ownerHealth.IsDead) return;
            if (weaponData.lifestealChance >= 1f || Random.value < weaponData.lifestealChance)
            {
                ownerHealth.Heal(weaponData.lifestealAmount);
            }
        }

        /// <summary>Fires one volley at the given primary target (exposed for tests).</summary>
        public int FireVolley(Transform target)
        {
            Fire(target);
            return LastVolleyCount;
        }

        private void Fire(Transform target)
        {
            if (projectilePool == null) { LastVolleyCount = 0; return; }
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
            if (IsMelee)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(MeleeSweepCenter, EffectiveMeleeRadius);
                return;
            }
            if (weaponData != null)
            {
                Gizmos.color = Color.red;
                Vector3 center = firePoint != null ? firePoint.position : (ownerTransform != null ? ownerTransform.position : Vector3.zero);
                Gizmos.DrawWireSphere(center, weaponData.range);
            }
        }
    }
}
