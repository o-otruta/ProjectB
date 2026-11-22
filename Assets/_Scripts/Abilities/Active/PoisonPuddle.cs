using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class PoisonPuddle : MonoBehaviour
    {
        private static readonly Collider[] hitBuffer = new Collider[48];

        private float radius;
        private float tickInterval;
        private float damage;
        private float slowFactor;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private IObjectPool<PoisonPuddle> pool;
        private float expireTime;
        private float nextTickTime;
        private bool isReturned;
        private Transform visualRoot;

        public void Initialize(
            Vector3 position,
            float radius,
            float duration,
            float tickInterval,
            float damage,
            float slowFactor,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            Material puddleMaterial,
            IObjectPool<PoisonPuddle> pool)
        {
            EnsureVisual(puddleMaterial);

            this.radius = Mathf.Max(0.5f, radius);
            this.tickInterval = Mathf.Max(0.15f, tickInterval);
            this.damage = damage;
            this.slowFactor = Mathf.Clamp(slowFactor, 0.2f, 1f);
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.pool = pool;
            this.isReturned = false;

            transform.position = new Vector3(position.x, 0.04f, position.z);
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(this.radius, 1f, this.radius);
            }

            float now = Time.time;
            expireTime = now + Mathf.Max(0.25f, duration);
            nextTickTime = now;
            ApplyTick();
            nextTickTime = now + this.tickInterval;
        }

        private void EnsureVisual(Material puddleMaterial)
        {
            if (visualRoot != null) return;

            var rootGo = new GameObject("VoxelPuddleVisual");
            rootGo.transform.SetParent(transform, false);
            visualRoot = rootGo.transform;

            // Voxel cluster tiles so the puddle matches the cubic world aesthetic
            CreateVoxelTile(visualRoot, new Vector3(0f, 0f, 0f), new Vector3(1.4f, 0.06f, 1.4f), puddleMaterial);
            CreateVoxelTile(visualRoot, new Vector3(0.55f, 0.01f, 0.35f), new Vector3(0.65f, 0.08f, 0.65f), puddleMaterial);
            CreateVoxelTile(visualRoot, new Vector3(-0.52f, 0.01f, -0.4f), new Vector3(0.7f, 0.07f, 0.6f), puddleMaterial);
            CreateVoxelTile(visualRoot, new Vector3(-0.48f, 0.01f, 0.48f), new Vector3(0.55f, 0.09f, 0.55f), puddleMaterial);
            CreateVoxelTile(visualRoot, new Vector3(0.45f, 0.01f, -0.52f), new Vector3(0.58f, 0.07f, 0.52f), puddleMaterial);
        }

        private static void CreateVoxelTile(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
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
                nextTickTime = Time.time + tickInterval;
            }
        }

        public void ApplyTick()
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, radius, hitBuffer, enemyLayer);
            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    bool isCrit = false;
                    int finalDmg = runtimeStats != null
                        ? runtimeStats.RollDamage(damage, out isCrit)
                        : Mathf.Max(1, Mathf.RoundToInt(damage));

                    if (damageable is EnemyBase enemy)
                    {
                        enemy.TakeDamage(finalDmg, isCrit);
                    }
                    else
                    {
                        damageable.TakeDamage(finalDmg);
                    }
                }

                if (col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(slowFactor, tickInterval + 0.15f);
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
