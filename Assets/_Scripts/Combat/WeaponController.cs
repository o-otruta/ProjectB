using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

namespace ProjectB.Combat
{
    public class WeaponController
    {
        private WeaponData weaponData;
        private LayerMask enemyLayer;
        private Transform ownerTransform;
        private Transform firePoint;
        private Transform projectileContainer;

        private IObjectPool<Projectile> projectilePool;
        private float nextFireTime;
        private Collider[] overlapResults = new Collider[200]; // Буфер для поиска врагов
        private float damageMultiplier;

        public WeaponController(WeaponData data, LayerMask enemyLayer, Transform ownerTransform, Transform firePoint, Transform rootContainer, float damageMultiplier = 1f)
        {
            this.weaponData = data;
            this.enemyLayer = enemyLayer;
            this.ownerTransform = ownerTransform;
            this.firePoint = firePoint;
            this.damageMultiplier = damageMultiplier;

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
                actionOnDestroy: p => { if (p != null) Object.Destroy(p.gameObject); },
                collectionCheck: true,
                defaultCapacity: 20,
                maxSize: 100
            );
        }

        public void UpdateController()
        {
            if (weaponData == null || projectilePool == null) return;

            if (Time.time >= nextFireTime)
            {
                Transform target = FindClosestEnemy();
                if (target != null)
                {
                    Fire(target);
                    nextFireTime = Time.time + weaponData.attackCooldown;
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
                Transform potentialTarget = overlapResults[i].transform;
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

        private void Fire(Transform target)
        {
            Projectile p = projectilePool.Get();
            if (p == null) return;
            
            p.transform.position = firePoint != null ? firePoint.position : (ownerTransform != null ? ownerTransform.position : Vector3.zero);
            p.Initialize(weaponData, target, projectilePool, damageMultiplier);
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
