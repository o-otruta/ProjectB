using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class DragonBurnZone : MonoBehaviour
    {
        private const float ZONE_RADIUS = 1.35f;
        private const float ZONE_DURATION = 2.0f;
        private const float TICK_INTERVAL = 0.4f;
        private static readonly Collider[] hitBuffer = new Collider[32];

        private float damage;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private RunStatistics runStatistics;
        private IObjectPool<DragonBurnZone> pool;
        private float expireTime;
        private float nextTickTime;
        private bool isReturned;
        private Transform visualRoot;

        public void PrewarmVisual(Material flameMaterial)
        {
            EnsureVisual(flameMaterial);
        }

        public void Initialize(
            Vector3 position,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            RunStatistics runStatistics,
            Material flameMaterial,
            IObjectPool<DragonBurnZone> pool)
        {
            EnsureVisual(flameMaterial);

            this.damage = damage;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.runStatistics = runStatistics;
            this.pool = pool;
            this.isReturned = false;

            transform.position = new Vector3(position.x, 0.05f, position.z);
            float now = Time.time;
            expireTime = now + ZONE_DURATION;
            ApplyTick();
            nextTickTime = now + TICK_INTERVAL;
        }

        private void EnsureVisual(Material flameMat)
        {
            if (visualRoot != null) return;

            var rootGo = new GameObject("DragonBurnVisual");
            rootGo.transform.SetParent(transform, false);
            visualRoot = rootGo.transform;

            CreateEmberTile(visualRoot, Vector3.zero, new Vector3(1.55f, 0.08f, 1.55f), flameMat);
            CreateEmberTile(visualRoot, new Vector3(0.45f, 0.04f, -0.35f), new Vector3(0.55f, 0.14f, 0.55f), flameMat);
            CreateEmberTile(visualRoot, new Vector3(-0.42f, 0.04f, 0.38f), new Vector3(0.5f, 0.16f, 0.5f), flameMat);
        }

        private static void CreateEmberTile(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            var col = cube.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var mr = cube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mat != null) mr.sharedMaterial = mat;
            }
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time >= expireTime)
            {
                ReturnToPool();
                return;
            }

            if (Time.time >= nextTickTime)
            {
                ApplyTick();
                nextTickTime = Time.time + TICK_INTERVAL;
            }
        }

        public void ApplyTick()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, ZONE_RADIUS, hitBuffer, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                bool isCrit = false;
                int finalDmg = runtimeStats != null
                    ? runtimeStats.RollDamage(damage, out isCrit)
                    : Mathf.Max(1, Mathf.RoundToInt(damage));

                if (damageable is EnemyBase enemy)
                {
                    enemy.TakeDamage(finalDmg, isCrit);
                    runStatistics?.RecordDamage(SynergyIds.DragonLord, finalDmg);
                }
                else
                {
                    damageable.TakeDamage(finalDmg);
                }
            }
        }

        public void ReturnToPool()
        {
            if (isReturned) return;
            isReturned = true;

            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
