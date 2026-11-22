using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.Abilities
{
    public class LaserTurretAbility : ActiveAbility
    {
        private LaserTurretAbilityData TurretData => Data as LaserTurretAbilityData;

        private float currentDps;
        private float currentCooldown;
        private float currentSearchRadius;
        private float currentLifetime;
        
        private float nextFireTime;
        private IObjectPool<LaserTurret> turretPool;
        private Transform turretContainer;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            
            currentDps = TurretData.dps;
            currentCooldown = TurretData.baseCooldown;
            currentSearchRadius = TurretData.searchRadius;
            currentLifetime = TurretData.turretLifetime;

            turretContainer = new GameObject($"Turrets_{Data.id}").transform;
            turretContainer.SetParent(null); 

            InitializePool();
        }

        private void InitializePool()
        {
            turretPool = new ObjectPool<LaserTurret>(
                createFunc: () => {
                    GameObject obj;
                    if (TurretData.turretPrefab != null)
                    {
                        obj = Instantiate(TurretData.turretPrefab, turretContainer);
                    }
                    else
                    {
                        obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        obj.transform.SetParent(turretContainer);
                        obj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                        var col = obj.GetComponent<CapsuleCollider>();
                        if (col != null)
                        {
                            if (Application.isPlaying) Destroy(col);
                            else DestroyImmediate(col);
                        }
                    }
                    var t = obj.GetComponent<LaserTurret>();
                    if (t == null) t = obj.AddComponent<LaserTurret>();
                    return t;
                },
                actionOnGet: t => { if (t != null) t.gameObject.SetActive(true); },
                actionOnRelease: t => { if (t != null) t.gameObject.SetActive(false); },
                actionOnDestroy: t =>
                {
                    if (t != null)
                    {
                        if (Application.isPlaying) Destroy(t.gameObject);
                        else DestroyImmediate(t.gameObject);
                    }
                },
                collectionCheck: true,
                defaultCapacity: 3,
                maxSize: 10
            );

            if (Application.isPlaying)
            {
                PrewarmPools(3);
            }
        }

        public override void PrewarmPools(int count = 0)
        {
            if (turretPool == null) return;
            int targetCount = count > 0 ? count : 3;
            var temp = new LaserTurret[targetCount];
            for (int i = 0; i < targetCount; i++) temp[i] = turretPool.Get();
            for (int i = 0; i < targetCount; i++) if (temp[i] != null) turretPool.Release(temp[i]);
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDps += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(1f, currentCooldown - value); break;
                case ModifierType.Radius: currentSearchRadius += value; break;
                case ModifierType.Duration: currentLifetime += value; break;
            }
        }

        private static int cachedGroundMask = -1;

        private static int GroundMask
        {
            get
            {
                if (cachedGroundMask == -1)
                {
                    cachedGroundMask = LayerMask.GetMask("Default", "Obstacles");
                    if (cachedGroundMask == 0) cachedGroundMask = ~LayerMask.GetMask("Hero");
                }
                return cachedGroundMask;
            }
        }

        private void Update()
        {
            if (Data == null || turretPool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                SpawnTurret();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.5f);
            }
        }

        private void SpawnTurret()
        {
            var t = turretPool.Get();
            if (t == null) return;

            Vector3 spawnPos = transform.position;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 10f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y;
            }
            else
            {
                spawnPos.y = 0f;
            }

            t.transform.position = spawnPos; 
            t.transform.rotation = Quaternion.identity;
            t.Initialize(currentSearchRadius, currentDps, currentLifetime, TurretData.targetLayer, turretPool, runtimeStats);
        }

        private void OnDestroy()
        {
            turretPool?.Clear();
            if (turretContainer != null)
            {
                if (Application.isPlaying) Destroy(turretContainer.gameObject);
                else DestroyImmediate(turretContainer.gameObject);
            }
        }
    }
}
