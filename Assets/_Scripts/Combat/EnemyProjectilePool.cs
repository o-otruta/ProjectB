using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Combat;

namespace ProjectB.Combat
{
    /// <summary>
    /// Centralized pool for enemy projectiles shared across all ShootingEnemy instances.
    /// Avoids creating a separate ObjectPool and hierarchy container per enemy.
    /// </summary>
    public class EnemyProjectilePool : IDisposable
    {
        private readonly Dictionary<WeaponData, IObjectPool<EnemyProjectile>> _pools = new Dictionary<WeaponData, IObjectPool<EnemyProjectile>>();
        private Transform _rootContainer;

        public EnemyProjectile Spawn(
            WeaponData weaponData,
            Vector3 position,
            Vector3 direction,
            int damage,
            Transform heroTarget,
            IDamageable heroDamageable)
        {
            if (weaponData == null) return null;

            IObjectPool<EnemyProjectile> pool = GetOrCreatePool(weaponData);
            EnemyProjectile proj = pool.Get();
            if (proj == null) return null;

            proj.transform.position = position;
            proj.Initialize(damage, weaponData.projectileSpeed, direction, pool, heroTarget, heroDamageable);
            return proj;
        }

        private IObjectPool<EnemyProjectile> GetOrCreatePool(WeaponData weaponData)
        {
            if (_pools.TryGetValue(weaponData, out var existing))
            {
                return existing;
            }

            if (_rootContainer == null)
            {
                _rootContainer = new GameObject("EnemyProjectilesContainer").transform;
            }

            IObjectPool<EnemyProjectile> pool = new ObjectPool<EnemyProjectile>(
                createFunc: () => CreateProjectile(weaponData),
                actionOnGet: p => { if (p != null) p.gameObject.SetActive(true); },
                actionOnRelease: p => { if (p != null) p.gameObject.SetActive(false); },
                actionOnDestroy: p => { if (p != null) UnityEngine.Object.Destroy(p.gameObject); },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 200
            );

            _pools[weaponData] = pool;
            return pool;
        }

        private EnemyProjectile CreateProjectile(WeaponData weaponData)
        {
            GameObject go;
            if (weaponData.projectilePrefab != null)
            {
                go = UnityEngine.Object.Instantiate(weaponData.projectilePrefab, _rootContainer);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(_rootContainer);
                go.transform.localScale = Vector3.one * 0.25f;

                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    UnityEngine.Object.Destroy(col);
                }
            }

            EnemyProjectile proj = go.GetComponent<EnemyProjectile>();
            if (proj == null)
            {
                proj = go.AddComponent<EnemyProjectile>();
            }
            return proj;
        }

        public void Dispose()
        {
            foreach (var kvp in _pools)
            {
                kvp.Value?.Clear();
            }
            _pools.Clear();

            if (_rootContainer != null)
            {
                UnityEngine.Object.Destroy(_rootContainer.gameObject);
                _rootContainer = null;
            }
        }
    }
}
