using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class PoisonPuddle : MonoBehaviour
    {
        private static readonly Collider[] hitBuffer = new Collider[128];
        private static readonly List<PoisonPuddle> activePuddles = new List<PoisonPuddle>(32);
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color NapalmColor = new Color(1f, 0.48f, 0.08f, 1f);
        private static readonly Color NapalmEmission = new Color(1.2f, 0.42f, 0.06f, 1f);

        public static IReadOnlyList<PoisonPuddle> ActivePuddles => activePuddles;

        private float radius;
        private float tickInterval;
        private float damage;
        private float slowFactor;
        private LayerMask enemyLayer;
        private HeroRuntimeStats runtimeStats;
        private RunStatistics runStatistics;
        private string damageSourceId;
        private IObjectPool<PoisonPuddle> pool;
        private float expireTime;
        private float nextTickTime;
        private bool isReturned;
        private Transform visualRoot;
        private MeshRenderer[] tileRenderers;
        private MaterialPropertyBlock napalmPropBlock;

        public bool IsIgnited { get; private set; }
        public float Radius => radius;
        public float CurrentDamage => damage;

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
            IObjectPool<PoisonPuddle> pool,
            RunStatistics runStatistics = null,
            string damageSourceId = "PoisonAlchemist",
            GameObject puddleVisualPrefab = null)
        {
            EnsureVisual(puddleMaterial, puddleVisualPrefab);

            this.radius = Mathf.Max(0.5f, radius);
            this.tickInterval = Mathf.Max(0.15f, tickInterval);
            this.damage = damage;
            this.slowFactor = Mathf.Clamp(slowFactor, 0.2f, 1f);
            this.enemyLayer = enemyLayer;
            this.runtimeStats = runtimeStats;
            this.runStatistics = runStatistics;
            this.damageSourceId = !string.IsNullOrEmpty(damageSourceId) ? damageSourceId : "PoisonAlchemist";
            this.pool = pool;
            this.isReturned = false;
            this.IsIgnited = false;

            ClearNapalmTint();

            if (!activePuddles.Contains(this))
            {
                activePuddles.Add(this);
            }

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

        public void PrewarmVisual(Material puddleMaterial, GameObject puddleVisualPrefab = null)
        {
            EnsureVisual(puddleMaterial, puddleVisualPrefab);
        }

        private void OnDisable()
        {
            activePuddles.Remove(this);
        }

        private void OnDestroy()
        {
            activePuddles.Remove(this);
        }

        private void EnsureVisual(Material puddleMaterial, GameObject puddleVisualPrefab = null)
        {
            if (visualRoot != null) return;

            napalmPropBlock = new MaterialPropertyBlock();

            if (puddleVisualPrefab != null)
            {
                var inst = Instantiate(puddleVisualPrefab, transform, false);
                inst.name = "VoxelPuddleVisual";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                visualRoot = inst.transform;
                tileRenderers = inst.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < tileRenderers.Length; i++)
                {
                    if (tileRenderers[i] != null)
                    {
                        tileRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        tileRenderers[i].receiveShadows = false;
                    }
                }
                return;
            }

            var rootGo = new GameObject("VoxelPuddleVisual");
            rootGo.transform.SetParent(transform, false);
            visualRoot = rootGo.transform;

            tileRenderers = new MeshRenderer[5];

            tileRenderers[0] = CreateVoxelTile(visualRoot, new Vector3(0f, 0f, 0f), new Vector3(1.4f, 0.06f, 1.4f), puddleMaterial);
            tileRenderers[1] = CreateVoxelTile(visualRoot, new Vector3(0.55f, 0.01f, 0.35f), new Vector3(0.65f, 0.08f, 0.65f), puddleMaterial);
            tileRenderers[2] = CreateVoxelTile(visualRoot, new Vector3(-0.52f, 0.01f, -0.4f), new Vector3(0.7f, 0.07f, 0.6f), puddleMaterial);
            tileRenderers[3] = CreateVoxelTile(visualRoot, new Vector3(-0.48f, 0.01f, 0.48f), new Vector3(0.55f, 0.09f, 0.55f), puddleMaterial);
            tileRenderers[4] = CreateVoxelTile(visualRoot, new Vector3(0.45f, 0.01f, -0.52f), new Vector3(0.58f, 0.07f, 0.52f), puddleMaterial);
        }

        private static MeshRenderer CreateVoxelTile(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
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
            return mr;
        }

        public int Ignite(float detonationDamage, GameEventBus bus = null, RunStatistics overrideStats = null)
        {
            if (IsIgnited || isReturned) return 0;

            IsIgnited = true;
            damage *= 2f;
            ApplyNapalmTint();

            RunStatistics stats = overrideStats ?? runStatistics;
            float detonationRadius = radius * 1.15f;
            int found = Physics.OverlapSphereNonAlloc(transform.position, detonationRadius, hitBuffer, enemyLayer);
            int hitCount = 0;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                bool isCrit = false;
                int finalDmg = runtimeStats != null
                    ? runtimeStats.RollDamage(detonationDamage, out isCrit)
                    : Mathf.Max(1, Mathf.RoundToInt(detonationDamage));

                if (damageable is EnemyBase enemy)
                {
                    enemy.TakeDamage(finalDmg, isCrit);
                    stats?.RecordDamage(SynergyIds.ToxicInferno, finalDmg);
                }
                else
                {
                    damageable.TakeDamage(finalDmg);
                }
                hitCount++;
            }

            bus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.ToxicInfernoIgnite, transform.position, detonationRadius));
            return hitCount;
        }

        private void ApplyNapalmTint()
        {
            if (tileRenderers == null) return;
            if (napalmPropBlock == null) napalmPropBlock = new MaterialPropertyBlock();

            napalmPropBlock.SetColor(BaseColorPropertyId, NapalmColor);
            napalmPropBlock.SetColor(ColorPropertyId, NapalmColor);
            napalmPropBlock.SetColor(EmissionColorPropertyId, NapalmEmission);

            for (int i = 0; i < tileRenderers.Length; i++)
            {
                if (tileRenderers[i] != null)
                {
                    tileRenderers[i].SetPropertyBlock(napalmPropBlock);
                }
            }
        }

        private void ClearNapalmTint()
        {
            if (tileRenderers == null) return;
            for (int i = 0; i < tileRenderers.Length; i++)
            {
                if (tileRenderers[i] != null)
                {
                    tileRenderers[i].SetPropertyBlock(null);
                }
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
                        if (IsIgnited && finalDmg > 1)
                        {
                            int bonusHalf = finalDmg / 2;
                            runStatistics?.RecordDamage(damageSourceId, finalDmg - bonusHalf);
                            runStatistics?.RecordDamage(SynergyIds.ToxicInferno, bonusHalf);
                        }
                        else
                        {
                            runStatistics?.RecordDamage(damageSourceId, finalDmg);
                        }
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
            activePuddles.Remove(this);

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
