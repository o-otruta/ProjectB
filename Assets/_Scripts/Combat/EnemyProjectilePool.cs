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
        private readonly List<EnemyProjectile> _activeProjectiles = new List<EnemyProjectile>(64);
        private Transform _rootContainer;

        public IReadOnlyList<EnemyProjectile> ActiveProjectiles => _activeProjectiles;

        public EnemyProjectile Spawn(
            WeaponData weaponData,
            Vector3 position,
            Vector3 direction,
            int damage,
            Transform heroTarget,
            IDamageable heroDamageable,
            string sourceId = "EnemyProjectile")
        {
            if (weaponData == null) return null;

            IObjectPool<EnemyProjectile> pool = GetOrCreatePool(weaponData);
            EnemyProjectile proj = pool.Get();
            if (proj == null) return null;

            proj.transform.position = position;
            proj.Initialize(damage, weaponData.projectileSpeed, direction, pool, heroTarget, heroDamageable, sourceId);
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
                actionOnGet: p =>
                {
                    if (p != null)
                    {
                        p.gameObject.SetActive(true);
                        _activeProjectiles.Add(p);
                    }
                },
                actionOnRelease: p =>
                {
                    if (p != null)
                    {
                        RemoveActiveProjectile(p);
                        p.gameObject.SetActive(false);
                    }
                },
                actionOnDestroy: p =>
                {
                    if (p != null)
                    {
                        RemoveActiveProjectile(p);
                        if (Application.isPlaying) UnityEngine.Object.Destroy(p.gameObject);
                        else UnityEngine.Object.DestroyImmediate(p.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 200
            );

            _pools[weaponData] = pool;
            return pool;
        }

        private void RemoveActiveProjectile(EnemyProjectile p)
        {
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_activeProjectiles[i], p))
                {
                    int last = _activeProjectiles.Count - 1;
                    _activeProjectiles[i] = _activeProjectiles[last];
                    _activeProjectiles.RemoveAt(last);
                    return;
                }
            }
        }

        public void Prewarm(WeaponData weaponData, int count = 24)
        {
            if (weaponData == null || count <= 0) return;
            var pool = GetOrCreatePool(weaponData);
            var temp = new EnemyProjectile[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = pool.Get();
            }
            for (int i = 0; i < count; i++)
            {
                if (temp[i] != null) pool.Release(temp[i]);
            }
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
                    if (Application.isPlaying) UnityEngine.Object.Destroy(col);
                    else UnityEngine.Object.DestroyImmediate(col);
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
            _activeProjectiles.Clear();
            foreach (var kvp in _pools)
            {
                kvp.Value?.Clear();
            }
            _pools.Clear();

            if (_rootContainer != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_rootContainer.gameObject);
                else UnityEngine.Object.DestroyImmediate(_rootContainer.gameObject);
                _rootContainer = null;
            }
        }
    }
}
