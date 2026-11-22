using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class VoxelTornadoAbility : ActiveAbility
    {
        private VoxelTornadoAbilityData TornadoData => Data as VoxelTornadoAbilityData;

        private float currentDamage;
        private float currentCooldown;
        private float currentRadius;
        private float currentDuration;
        private int currentCount;

        private float nextFireTime;
        private Transform tornadoContainer;
        private IObjectPool<TornadoProjectile> tornadoPool;
        private readonly Collider[] hitBuffer = new Collider[128];

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

            tornadoPool = new ObjectPool<TornadoProjectile>(
                createFunc: () =>
                {
                    var go = new GameObject("VoxelTornado");
                    go.transform.SetParent(tornadoContainer, false);
                    var proj = go.AddComponent<TornadoProjectile>();
                    proj.PrewarmVisual(TornadoData != null ? TornadoData.tornadoMaterial : null);
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

            if (Application.isPlaying)
            {
                PrewarmPools(4);
            }
        }

        public override void PrewarmPools(int count = 0)
        {
            if (tornadoPool == null) return;
            int targetCount = count > 0 ? count : 4;
            var temp = new TornadoProjectile[targetCount];
            for (int i = 0; i < targetCount; i++) temp[i] = tornadoPool.Get();
            for (int i = 0; i < targetCount; i++) if (temp[i] != null) tornadoPool.Release(temp[i]);
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
                    tornadoPool);
                spawned++;
            }

            return spawned;
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
                if (!col.TryGetComponent<IDamageable>(out var dmg) || dmg.IsDead) continue;

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
            if (tornadoContainer != null)
            {
                if (Application.isPlaying) Destroy(tornadoContainer.gameObject);
                else DestroyImmediate(tornadoContainer.gameObject);
            }
        }
    }
}
