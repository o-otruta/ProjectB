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
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        mat.color = Color.cyan;
                        obj.GetComponent<Renderer>().material = mat;
                        Destroy(obj.GetComponent<CapsuleCollider>());
                    }
                    var t = obj.GetComponent<LaserTurret>();
                    if (t == null) t = obj.AddComponent<LaserTurret>();
                    return t;
                },
                actionOnGet: t => { if (t != null) t.gameObject.SetActive(true); },
                actionOnRelease: t => { if (t != null) t.gameObject.SetActive(false); },
                actionOnDestroy: t => { if (t != null) Destroy(t.gameObject); },
                collectionCheck: true,
                defaultCapacity: 3,
                maxSize: 10
            );
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

        private void Update()
        {
            if (Data == null || turretPool == null) return;

            if (Time.time >= nextFireTime)
            {
                SpawnTurret();
                nextFireTime = Time.time + currentCooldown;
            }
        }

        private void SpawnTurret()
        {
            var t = turretPool.Get();
            if (t == null) return;

            t.transform.position = transform.position; 
            t.transform.rotation = Quaternion.identity;
            t.Initialize(currentSearchRadius, currentDps, currentLifetime, TurretData.targetLayer, turretPool);
        }

        private void OnDestroy()
        {
            turretPool?.Clear();
            if (turretContainer != null)
            {
                Destroy(turretContainer.gameObject);
            }
        }
    }
}
