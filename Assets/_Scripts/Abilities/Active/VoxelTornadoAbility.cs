using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    public class VoxelTornadoAbility : ActiveAbility
    {
        private const int CATACLYSM_SHARD_COUNT = 6;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private VoxelTornadoAbilityData TornadoData => Data as VoxelTornadoAbilityData;

        private float currentDamage;
        private float currentCooldown;
        private float currentRadius;
        private float currentDuration;
        private int currentCount;

        private float nextFireTime;
        private Transform tornadoContainer;
        private IObjectPool<TornadoProjectile> tornadoPool;
        private IObjectPool<GravityShardProjectile> shardPool;
        private Material shardMaterial;
        private Action<Vector3, float> cachedDestabilizeCallback;
        private TornadoProjectile lastSpawnedTornado;
        private readonly Collider[] hitBuffer = new Collider[128];

        public TornadoProjectile LastSpawnedTornado => lastSpawnedTornado;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            if (TornadoData != null)
            {
                currentDamage = TornadoData.baseDamage;
                currentCooldown = TornadoData.baseCooldown;
                currentRadius = TornadoData.vortexRadius;
                currentDuration = TornadoData.duration;
                currentCount = Mathf.Max(1, TornadoData.tornadoCount);
            }

            tornadoContainer = new GameObject($"Tornadoes_{Data.id}").transform;
            tornadoContainer.SetParent(null);
            cachedDestabilizeCallback = SpawnGravityShards;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                shardMaterial = new Material(shader);
                if (shardMaterial.HasProperty(BaseColorId))
                {
                    shardMaterial.SetColor(BaseColorId, new Color(0.75f, 0.32f, 1f, 1f));
                }
                shardMaterial.color = new Color(0.75f, 0.32f, 1f, 1f);
                shardMaterial.enableInstancing = true;
            }

            tornadoPool = new ObjectPool<TornadoProjectile>(
                createFunc: () =>
                {
                    var go = new GameObject("VoxelTornado");
                    go.transform.SetParent(tornadoContainer, false);
                    var proj = go.AddComponent<TornadoProjectile>();
                    proj.PrewarmVisual(
                        TornadoData != null ? TornadoData.tornadoMaterial : null,
                        TornadoData != null ? TornadoData.tornadoVisualPrefab : null);
                    return proj;
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
                defaultCapacity: 4,
                maxSize: 20);

            shardPool = new ObjectPool<GravityShardProjectile>(
                createFunc: CreateGravityShardInstance,
                actionOnGet: s => { if (s != null) s.gameObject.SetActive(true); },
                actionOnRelease: s => { if (s != null) s.gameObject.SetActive(false); },
                actionOnDestroy: s =>
                {
                    if (s != null)
                    {
                        if (Application.isPlaying) Destroy(s.gameObject);
                        else DestroyImmediate(s.gameObject);
                    }
                },
                collectionCheck: true,
                defaultCapacity: 12,
                maxSize: 48);

            if (Application.isPlaying)
            {
                PrewarmPools(4);
            }
        }

        private GravityShardProjectile CreateGravityShardInstance()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GravityShard";
            cube.transform.SetParent(tornadoContainer, false);
            cube.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
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
                if (shardMaterial != null) mr.sharedMaterial = shardMaterial;
            }

            return cube.AddComponent<GravityShardProjectile>();
        }

        public override void PrewarmPools(int count = 0)
        {
            int targetCount = count > 0 ? count : 4;
            if (tornadoPool != null)
            {
                var temp = new TornadoProjectile[targetCount];
                for (int i = 0; i < targetCount; i++) temp[i] = tornadoPool.Get();
                for (int i = 0; i < targetCount; i++) if (temp[i] != null) tornadoPool.Release(temp[i]);
            }

            if (shardPool != null)
            {
                int shardCount = Mathf.Max(6, targetCount * 3);
                var tempShards = new GravityShardProjectile[shardCount];
                for (int i = 0; i < shardCount; i++) tempShards[i] = shardPool.Get();
                for (int i = 0; i < shardCount; i++) if (tempShards[i] != null) shardPool.Release(tempShards[i]);
            }
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Count:
                    currentCount = Mathf.Max(1, currentCount + Mathf.RoundToInt(value));
                    break;
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Duration:
                    currentDuration += value;
                    break;
                case ModifierType.Radius:
                case ModifierType.Area:
                    currentRadius += value;
                    break;
                case ModifierType.Cooldown:
                    currentCooldown = Mathf.Max(0.4f, currentCooldown - value);
                    break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentRadius *= 1.2f;
                currentDuration *= 1.2f;
            }
            else if (tier == 2)
            {
                currentCount += 1;
            }
        }

        private void Update()
        {
            if (Data == null || tornadoPool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                SpawnTornadoes();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.4f);
            }
        }

        public int SpawnTornadoes()
        {
            if (TornadoData == null || tornadoPool == null) return 0;

            float searchRange = Mathf.Max(14f, currentRadius * 4f);
            Transform target = FindClosestTarget(searchRange, TornadoData.targetLayer);
            if (target == null) return 0;

            Vector3 baseDir = target.position - transform.position;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = transform.forward;
            baseDir.Normalize();

            int spawned = 0;
            for (int i = 0; i < currentCount; i++)
            {
                TornadoProjectile tornado = tornadoPool.Get();
                if (tornado == null) continue;

                Vector3 dir = baseDir;
                if (currentCount > 1)
                {
                    float spread = 24f;
                    float angle = -spread * (currentCount - 1) * 0.5f + spread * i;
                    dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                }

                tornado.Initialize(
                    transform.position,
                    dir,
                    TornadoData.moveSpeed,
                    TornadoData.waveAmplitude,
                    TornadoData.waveFrequency,
                    currentRadius,
                    TornadoData.pullForce,
                    currentDuration,
                    TornadoData.tickInterval,
                    GetScaledDamage(currentDamage),
                    TornadoData.targetLayer,
                    runtimeStats,
                    TornadoData.tornadoMaterial,
                    tornadoPool,
                    heroAbilities,
                    runStatistics,
                    cachedAbilityId,
                    cachedDestabilizeCallback,
                    VisualScaleMultiplier,
                    TornadoData.tornadoVisualPrefab);
                lastSpawnedTornado = tornado;
                spawned++;
            }

            return spawned;
        }

        private void SpawnGravityShards(Vector3 center, float shardBaseDamage)
        {
            if (shardPool == null || TornadoData == null) return;

            float shardDamage = shardBaseDamage * 1.8f;
            float angleStep = 360f / CATACLYSM_SHARD_COUNT;

            for (int i = 0; i < CATACLYSM_SHARD_COUNT; i++)
            {
                GravityShardProjectile shard = shardPool.Get();
                if (shard == null) continue;

                Vector3 dir = Quaternion.Euler(0f, angleStep * i, 0f) * Vector3.forward;
                shard.Initialize(
                    new Vector3(center.x, 0.65f, center.z),
                    dir,
                    shardDamage,
                    TornadoData.targetLayer,
                    runtimeStats,
                    runStatistics,
                    shardPool);
            }

            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.CataclysmShards, center, 3.0f));
        }

        private Transform FindClosestTarget(float searchRadius, LayerMask layer)
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, hitBuffer, layer);
            if (found == 0) return null;

            Transform best = null;
            float bestSqr = float.MaxValue;
            Vector3 origin = transform.position;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var dmg) ||
                    !CombatTargetFilter.IsValidAutoAimTarget(col, dmg, layer))
                {
                    continue;
                }

                float sqr = (col.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = col.transform;
                }
            }

            return best;
        }

        private void OnDestroy()
        {
            tornadoPool?.Clear();
            shardPool?.Clear();
            if (tornadoContainer != null)
            {
                if (Application.isPlaying) Destroy(tornadoContainer.gameObject);
                else DestroyImmediate(tornadoContainer.gameObject);
            }
            if (shardMaterial != null)
            {
                if (Application.isPlaying) Destroy(shardMaterial);
                else DestroyImmediate(shardMaterial);
            }
        }
    }
}
