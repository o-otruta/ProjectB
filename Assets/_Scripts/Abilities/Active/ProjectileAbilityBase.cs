using UnityEngine;
using UnityEngine.Pool;

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
        protected Collider[] hitBuffer = new Collider[50];
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
                actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                collectionCheck: true,
                defaultCapacity: DefaultPoolCapacity,
                maxSize: MaxPoolCapacity
            );
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
            if (Data == null || projectilePool == null) return;

            if (Time.time >= nextFireTime)
            {
                Fire();
                nextFireTime = Time.time + currentCooldown;
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
                var target = hitBuffer[i].transform;
                float sqrDist = (target.position - transform.position).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closestTarget = target;
                }
            }

            return closestTarget;
        }

        protected virtual void OnDestroy()
        {
            projectilePool?.Clear();
            if (projectileContainer != null)
                Destroy(projectileContainer.gameObject);
        }
    }
}
