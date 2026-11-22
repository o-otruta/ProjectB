using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public abstract class ProjectileAbilityBase<TData> : ActiveAbility 
        where TData : ActiveAbilityData
    {
        protected TData ProjectileData => Data as TData;

        protected float currentDamage;
        protected float currentCooldown;
        protected int currentCount;
        protected int currentPierceCount;
        protected float currentSpeed;
        protected float currentRange;

        protected float nextFireTime;
        protected Collider[] hitBuffer = new Collider[128];
        protected IObjectPool<AbilityProjectile> projectilePool;
        protected Transform projectileContainer;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            
            LoadBaseStats();

            projectileContainer = new GameObject($"Projectiles_{Data.id}").transform;
            projectileContainer.SetParent(null);

            InitializePool();
        }

        protected abstract void LoadBaseStats();

        private void InitializePool()
        {
            projectilePool = new ObjectPool<AbilityProjectile>(
                createFunc: CreateProjectilePrefab,
                actionOnGet: p => { if (p != null) p.gameObject.SetActive(true); },
                actionOnRelease: p => { if (p != null) p.gameObject.SetActive(false); },
                actionOnDestroy: p =>
                {
                    if (p != null)
                    {
                        if (Application.isPlaying) Destroy(p.gameObject);
                        else DestroyImmediate(p.gameObject);
                    }
                },
                collectionCheck: true,
                defaultCapacity: DefaultPoolCapacity,
                maxSize: MaxPoolCapacity
            );

            if (Application.isPlaying)
            {
                PrewarmPools(DefaultPoolCapacity);
            }
        }

        public override void PrewarmPools(int count = 0)
        {
            if (projectilePool == null) return;
            int targetCount = count > 0 ? count : DefaultPoolCapacity;
            var temp = new AbilityProjectile[targetCount];
            for (int i = 0; i < targetCount; i++)
            {
                temp[i] = projectilePool.Get();
            }
            for (int i = 0; i < targetCount; i++)
            {
                if (temp[i] != null) projectilePool.Release(temp[i]);
            }
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        protected virtual int DefaultPoolCapacity => 10;
        protected virtual int MaxPoolCapacity => 100;

        protected abstract AbilityProjectile CreateProjectilePrefab();

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDamage += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(0.1f, currentCooldown - value); break;
                case ModifierType.Speed: currentSpeed += value; break;
                case ModifierType.Radius: currentRange += value; break;
                case ModifierType.Count: 
                    currentCount += (int)value; break;
                case ModifierType.PierceCount: 
                    currentPierceCount += (int)value; break;
                default:
                    ApplySpecificModifier(type, value);
                    break;
            }
        }

        protected virtual void ApplySpecificModifier(ModifierType type, float value) { }

        protected virtual void Update()
        {
            if (Data == null || projectilePool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                Fire();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown);
            }
        }

        protected abstract void Fire();

        protected Transform FindClosestTarget(float searchRadius, LayerMask targetLayer)
        {
            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, hitBuffer, targetLayer);
            
            if (targetsFound == 0) return null;

            Transform closestTarget = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < targetsFound; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (col.TryGetComponent<IDamageable>(out var dmg) &&
                    !CombatTargetFilter.IsValidAutoAimTarget(col, dmg, targetLayer))
                {
                    continue;
                }

                var target = col.transform;
                float sqrDist = (target.position - transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closestTarget = target;
                }
            }

            return closestTarget;
        }

        protected int FindClosestTargets(float searchRadius, LayerMask targetLayer, int maxTargets, List<Transform> results)
        {
            results.Clear();
            if (maxTargets <= 0) return 0;

            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, hitBuffer, targetLayer);
            if (targetsFound == 0) return 0;

            Vector3 origin = transform.position;
            int needed = Mathf.Min(maxTargets, targetsFound);

            for (int pick = 0; pick < needed; pick++)
            {
                Transform best = null;
                float bestSqrDist = float.MaxValue;

                for (int i = 0; i < targetsFound; i++)
                {
                    Collider col = hitBuffer[i];
                    if (col == null) continue;
                    if (col.TryGetComponent<IDamageable>(out var dmg) &&
                        !CombatTargetFilter.IsValidAutoAimTarget(col, dmg, targetLayer))
                    {
                        continue;
                    }

                    Transform candidate = col.transform;
                    if (results.Contains(candidate)) continue;

                    float sqrDist = (candidate.position - origin).sqrMagnitude;
                    if (sqrDist < bestSqrDist)
                    {
                        bestSqrDist = sqrDist;
                        best = candidate;
                    }
                }

                if (best == null) break;
                results.Add(best);
            }

            return results.Count;
        }

        protected virtual void OnDestroy()
        {
            projectilePool?.Clear();
            if (projectileContainer != null)
            {
                if (Application.isPlaying) Destroy(projectileContainer.gameObject);
                else DestroyImmediate(projectileContainer.gameObject);
            }
        }
    }
}
