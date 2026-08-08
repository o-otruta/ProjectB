using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

namespace ProjectB.Combat
{
    public class HeroCombat : MonoBehaviour
    {
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private Transform firePoint;

        private IObjectPool<Projectile> projectilePool;
        private float nextFireTime;
        private Collider[] overlapResults = new Collider[200]; // Увеличенный буфер для bullet heaven

        private Transform projectileContainer;

        private void Start()
        {
            if (weaponData == null)
            {
                Debug.LogWarning("WeaponData not set on HeroCombat!");
                return;
            }

            projectileContainer = new GameObject("ProjectileContainer").transform;

            projectilePool = new ObjectPool<Projectile>(
                createFunc: () => {
                    var p = Instantiate(weaponData.projectilePrefab, projectileContainer).GetComponent<Projectile>();
                    return p;
                },
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => Destroy(p.gameObject),
                collectionCheck: true,
                defaultCapacity: 20,
                maxSize: 100
            );
        }

        private void Update()
        {
            if (weaponData == null) return;

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
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, weaponData.range, overlapResults, enemyLayer);
            
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;
            Vector3 currentPosition = transform.position;

            for (int i = 0; i < hitCount; i++)
            {
                Transform potentialTarget = overlapResults[i].transform;
                Vector3 directionToTarget = potentialTarget.position - currentPosition;
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
            p.transform.position = firePoint != null ? firePoint.position : transform.position;
            p.Initialize(weaponData, target, projectilePool);
        }

        private void OnDrawGizmosSelected()
        {
            if (weaponData != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, weaponData.range);
            }
        }
    }
}
