using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class TornadoProjectile : MonoBehaviour
    {
        private static readonly Collider[] hitBuffer = new Collider[128];
        private static int cachedObstacleMask = -1;

        private Vector3 originPos;
        private Vector3 forwardDir;
        private Vector3 sideDir;
        private float moveSpeed;
        private float waveAmplitude;
        private float waveFrequency;
        private float vortexRadius;
        private float pullForce;
        private float duration;
        private float tickInterval;
        private float damage;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private IObjectPool<TornadoProjectile> pool;

        private float elapsed;
        private float nextTickTime;
        private bool isReturned;
        private Transform visualSpiral;

        private static int ObstacleMask
        {
            get
            {
                if (cachedObstacleMask == -1)
                {
                    cachedObstacleMask = LayerMask.GetMask("Obstacles");
                }
                return cachedObstacleMask;
            }
        }

        public void Initialize(
            Vector3 startPos,
            Vector3 direction,
            float moveSpeed,
            float waveAmplitude,
            float waveFrequency,
            float vortexRadius,
            float pullForce,
            float duration,
            float tickInterval,
            float damage,
            LayerMask enemyLayer,
            HeroRuntimeStats runtimeStats,
            Material tornadoMaterial,
            IObjectPool<TornadoProjectile> pool)
        {
            EnsureVisual(tornadoMaterial);

            originPos = new Vector3(startPos.x, 0f, startPos.z);
            forwardDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            forwardDir.y = 0f;
            if (forwardDir.sqrMagnitude < 0.0001f) forwardDir = Vector3.forward;
            forwardDir.Normalize();
            sideDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

            this.moveSpeed = Mathf.Max(0.5f, moveSpeed);
            this.waveAmplitude = waveAmplitude;
            this.waveFrequency = waveFrequency;
            this.vortexRadius = Mathf.Max(0.6f, vortexRadius);
            this.pullForce = Mathf.Max(0f, pullForce);
            this.duration = Mathf.Max(0.4f, duration);
            this.tickInterval = Mathf.Max(0.1f, tickInterval);
            this.damage = damage;
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.pool = pool;

            elapsed = 0f;
            isReturned = false;
            transform.position = originPos;

            if (visualSpiral != null)
            {
                float radiusScale = Mathf.Max(0.6f, this.vortexRadius * 0.45f);
                visualSpiral.localScale = new Vector3(radiusScale, 1f, radiusScale);
            }

            nextTickTime = Time.time;
            ApplyVortexStep(dt: 0f, applyDamage: true);
            nextTickTime = Time.time + this.tickInterval;
        }

        public void PrewarmVisual(Material tornadoMat)
        {
            EnsureVisual(tornadoMat);
        }

        private void EnsureVisual(Material tornadoMat)
        {
            if (visualSpiral != null) return;

            var spiralGo = new GameObject("VoxelSpiral");
            spiralGo.transform.SetParent(transform, false);
            visualSpiral = spiralGo.transform;

            const int ringCount = 5;
            for (int i = 0; i < ringCount; i++)
            {
                float t = (float)i / (ringCount - 1);
                float y = 0.25f + i * 0.48f;
                float ringRadius = Mathf.Lerp(0.28f, 0.95f, t);
                float angle = i * 58f * Mathf.Deg2Rad;
                Vector3 localPos = new Vector3(Mathf.Cos(angle) * ringRadius * 0.45f, y, Mathf.Sin(angle) * ringRadius * 0.45f);
                float cubeSize = Mathf.Lerp(0.35f, 0.68f, t);

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Swirl_{i}";
                cube.transform.SetParent(visualSpiral, false);
                cube.transform.localPosition = localPos;
                cube.transform.localScale = new Vector3(cubeSize, 0.36f, cubeSize);
                cube.transform.localRotation = Quaternion.Euler(0f, i * 24f, 0f);
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
                    if (tornadoMat != null) mr.sharedMaterial = tornadoMat;
                }
            }
        }

        private void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            float dt = Time.deltaTime;
            elapsed += dt;
            if (elapsed >= duration)
            {
                ReturnToPool();
                return;
            }

            float lateral = Mathf.Sin(elapsed * waveFrequency) * waveAmplitude;
            transform.position = originPos + forwardDir * (moveSpeed * elapsed) + sideDir * lateral;

            if (visualSpiral != null)
            {
                visualSpiral.Rotate(Vector3.up, 540f * dt);
            }

            bool applyDamage = Time.time >= nextTickTime;
            ApplyVortexStep(dt, applyDamage);
            if (applyDamage)
            {
                nextTickTime = Time.time + tickInterval;
            }
        }

        public void ApplyVortexStep(float dt, bool applyDamage)
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, vortexRadius, hitBuffer, enemyLayer);
            Vector3 center = transform.position;
            int obstacleMask = ObstacleMask;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;

                if (dt > 0f && pullForce > 0f && col.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead)
                {
                    Vector3 enemyPos = enemy.transform.position;
                    Vector3 toCenter = center - enemyPos;
                    toCenter.y = 0f;
                    float dist = toCenter.magnitude;
                    if (dist > 0.35f)
                    {
                        Vector3 pullDir = toCenter / dist;
                        float step = Mathf.Min(pullForce * dt, dist - 0.35f);
                        if (step > 0.0001f &&
                            (obstacleMask == 0 || !Physics.SphereCast(enemyPos + Vector3.up * 0.5f, 0.35f, pullDir, out _, step + 0.2f, obstacleMask, QueryTriggerInteraction.Ignore)))
                        {
                            enemy.ApplyPull(pullDir * step);
                        }
                    }
                }

                if (applyDamage && col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    bool isCrit = false;
                    int finalDmg = runtimeStats != null
                        ? runtimeStats.RollDamage(damage, out isCrit)
                        : Mathf.Max(1, Mathf.RoundToInt(damage));

                    if (damageable is EnemyBase dmgEnemy)
                    {
                        dmgEnemy.TakeDamage(finalDmg, isCrit);
                    }
                    else
                    {
                        damageable.TakeDamage(finalDmg);
                    }
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
