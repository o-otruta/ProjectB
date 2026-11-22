using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    public class OrbitalCompanionAbility : ActiveAbility
    {
        private const float SCAN_INTERVAL = 0.08f;
        private const float BLADE_STORM_INTERVAL = 2.8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private OrbitalCompanionAbilityData OrbitalData => Data as OrbitalCompanionAbilityData;
        private readonly List<Transform> instances = new List<Transform>();
        private float currentAngle = 0f;

        private float tickRate = 0.5f;
        private readonly Dictionary<EntityId, float> enemyDamageCooldowns = new Dictionary<EntityId, float>(128);
        private float nextCleanupTime = 0f;
        private float nextScanTime = 0f;
        private float nextBladeStormTime = 0f;
        private readonly List<EntityId> keysToRemove = new List<EntityId>(128);

        private Transform bladeStormContainer;
        private IObjectPool<PhantomBladeProjectile> phantomBladePool;
        private Material phantomBladeMaterial;
        private MaterialPropertyBlock overdrivePropBlock;

        // Modifiable stats
        private int currentCount;
        private float currentDamage;
        private float currentSpeed;
        private float currentRadius;
        private float currentArea;

        private readonly Collider[] hitBuffer = new Collider[64];

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            currentCount = OrbitalData.count;
            currentDamage = OrbitalData.baseDamage;
            currentSpeed = OrbitalData.orbitSpeed;
            currentRadius = OrbitalData.orbitDistance;
            currentArea = OrbitalData.baseArea;

            overdrivePropBlock = new MaterialPropertyBlock();

            bladeStormContainer = new GameObject($"BladeStorm_{Data.id}").transform;
            bladeStormContainer.SetParent(null);
            InitializePhantomBladePool();

            SpawnInstances();
        }

        private void InitializePhantomBladePool()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                phantomBladeMaterial = new Material(shader);
                if (phantomBladeMaterial.HasProperty(BaseColorId))
                {
                    phantomBladeMaterial.SetColor(BaseColorId, new Color(0.45f, 0.95f, 1f, 0.95f));
                }
                phantomBladeMaterial.color = new Color(0.45f, 0.95f, 1f, 0.95f);
                phantomBladeMaterial.enableInstancing = true;
            }

            phantomBladePool = new ObjectPool<PhantomBladeProjectile>(
                createFunc: CreatePhantomBladeInstance,
                actionOnGet: b => { if (b != null) b.gameObject.SetActive(true); },
                actionOnRelease: b => { if (b != null) b.gameObject.SetActive(false); },
                actionOnDestroy: b =>
                {
                    if (b != null)
                    {
                        if (Application.isPlaying) Destroy(b.gameObject);
                        else DestroyImmediate(b.gameObject);
                    }
                },
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 32);

            if (Application.isPlaying)
            {
                PrewarmPools(8);
            }
        }

        private PhantomBladeProjectile CreatePhantomBladeInstance()
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "PhantomBlade";
            obj.transform.SetParent(bladeStormContainer, false);
            obj.transform.localScale = new Vector3(0.22f, 0.12f, 0.72f);

            var col = obj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var mr = obj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (phantomBladeMaterial != null)
                {
                    mr.sharedMaterial = phantomBladeMaterial;
                }
            }

            return obj.AddComponent<PhantomBladeProjectile>();
        }

        public override void PrewarmPools(int count = 0)
        {
            if (phantomBladePool == null) return;
            int targetCount = count > 0 ? count : 8;
            var temp = new PhantomBladeProjectile[targetCount];
            for (int i = 0; i < targetCount; i++) temp[i] = phantomBladePool.Get();
            for (int i = 0; i < targetCount; i++) if (temp[i] != null) phantomBladePool.Release(temp[i]);
        }

        public override void OnSynergyActivated(string synergyId)
        {
            if (synergyId == SynergyIds.BladeStorm)
            {
                nextBladeStormTime = Time.time + GetEffectiveCooldown(BLADE_STORM_INTERVAL, 0.8f);
            }
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Count:
                    currentCount += (int)value;
                    SpawnInstances();
                    break;
                case ModifierType.Speed:
                    currentSpeed += value;
                    break;
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Radius:
                    currentRadius += value;
                    break;
                case ModifierType.Area:
                    currentArea += value;
                    UpdateInstanceScales();
                    break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentSpeed *= 1.2f;
                currentRadius *= 1.15f;
                UpdateInstanceScales();
                ApplyOverdriveGlow();
            }
            else if (tier == 2)
            {
                currentCount += 1;
                SpawnInstances();
                UpdateInstanceScales();
                ApplyOverdriveGlow();
            }
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private void SpawnInstances()
        {
            while (instances.Count > currentCount)
            {
                var inst = instances[instances.Count - 1];
                if (inst != null)
                {
                    if (Application.isPlaying) Destroy(inst.gameObject);
                    else DestroyImmediate(inst.gameObject);
                }
                instances.RemoveAt(instances.Count - 1);
            }

            for (int i = instances.Count; i < currentCount; i++)
            {
                GameObject obj;
                if (OrbitalData.prefab != null)
                {
                    obj = Instantiate(OrbitalData.prefab, transform);
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    obj.transform.SetParent(transform);
                    var col = obj.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                }
                obj.name = $"Orbital_{i}";
                obj.transform.localScale = Vector3.one * (currentArea * VisualScaleMultiplier);

                instances.Add(obj.transform);
            }
            RecalculatePositions();
            if (OverdriveTier > 0) ApplyOverdriveGlow();
        }

        private void UpdateInstanceScales()
        {
            Vector3 scale = Vector3.one * (currentArea * VisualScaleMultiplier);
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null)
                {
                    instances[i].localScale = scale;
                }
            }
        }

        private void ApplyOverdriveGlow()
        {
            if (OverdriveTier <= 0) return;
            if (overdrivePropBlock == null) overdrivePropBlock = new MaterialPropertyBlock();

            Color emission = OverdriveTier >= 2
                ? new Color(0.28f, 0.85f, 1.0f, 1f)
                : new Color(1.0f, 0.72f, 0.16f, 1f);
            overdrivePropBlock.SetColor(EmissionColorId, emission);

            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i] == null) continue;
                var renderers = instances[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] != null)
                    {
                        renderers[r].SetPropertyBlock(overdrivePropBlock);
                    }
                }
            }
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            currentAngle += currentSpeed * Time.deltaTime;
            currentAngle %= 360f;
            RecalculatePositions();

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + SCAN_INTERVAL;
                ApplyDamageTick();
            }

            if (HasSynergy(SynergyIds.BladeStorm) && Time.time >= nextBladeStormTime)
            {
                FireBladeStormVolley();
                nextBladeStormTime = Time.time + GetEffectiveCooldown(BLADE_STORM_INTERVAL, 0.8f);
            }

            CleanupCooldowns();
        }

        public int FireBladeStormVolley()
        {
            if (phantomBladePool == null || instances.Count == 0 || OrbitalData == null) return 0;

            float bladeDamage = GetScaledDamage(currentDamage * 1.15f);
            int launched = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                Transform inst = instances[i];
                if (inst == null) continue;

                Vector3 outDir = inst.position - transform.position;
                outDir.y = 0f;
                if (outDir.sqrMagnitude < 0.0001f) outDir = inst.forward;
                outDir.Normalize();

                PhantomBladeProjectile blade = phantomBladePool.Get();
                if (blade == null) continue;

                Vector3 spawnPos = new Vector3(inst.position.x, 0.65f, inst.position.z);
                blade.Initialize(
                    spawnPos,
                    outDir,
                    bladeDamage,
                    OrbitalData.targetLayer,
                    runtimeStats,
                    runStatistics,
                    phantomBladePool,
                    VisualScaleMultiplier);
                launched++;
            }

            if (launched > 0)
            {
                eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.BladeStormVolley, transform.position, currentRadius));
            }

            return launched;
        }

        public bool TryGetClosestInstancePosition(Vector3 worldPos, out Vector3 closestPos)
        {
            closestPos = transform.position;
            if (instances.Count == 0) return false;

            float bestSqr = float.MaxValue;
            bool found = false;

            for (int i = 0; i < instances.Count; i++)
            {
                Transform inst = instances[i];
                if (inst == null) continue;

                float sqr = (inst.position - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    closestPos = inst.position;
                    found = true;
                }
            }

            return found;
        }

        private void RecalculatePositions()
        {
            if (instances.Count == 0) return;
            float angleStep = 360f / instances.Count;
            for (int i = 0; i < instances.Count; i++)
            {
                float angle = currentAngle + (i * angleStep);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad)) * currentRadius;
                instances[i].position = transform.position + offset;
                if (offset != Vector3.zero)
                {
                    instances[i].rotation = Quaternion.LookRotation(offset);
                }
            }
        }

        private void ApplyDamageTick()
        {
            float hitRadius = (currentArea * VisualScaleMultiplier) / 2f;
            float currentTime = Time.time;
            float effectiveTick = GetEffectiveCooldown(tickRate, 0.15f);

            foreach (var inst in instances)
            {
                int count = Physics.OverlapSphereNonAlloc(inst.position, hitRadius, hitBuffer, ActiveData.targetLayer);
                for (int i = 0; i < count; i++)
                {
                    var col = hitBuffer[i];
                    if (col == null || !col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                    EntityId enemyId = col.gameObject.GetEntityId();

                    if (!enemyDamageCooldowns.TryGetValue(enemyId, out float nextDamageTime) || currentTime >= nextDamageTime)
                    {
                        int scaledDamage = RollScaledDamage(currentDamage, out bool isCrit);
                        DealDamage(damageable, scaledDamage, isCrit);
                        enemyDamageCooldowns[enemyId] = currentTime + effectiveTick;
                    }
                }
            }
        }

        private void CleanupCooldowns()
        {
            if (Time.time < nextCleanupTime) return;
            nextCleanupTime = Time.time + 5f;

            keysToRemove.Clear();
            float currentTime = Time.time;
            foreach (var kvp in enemyDamageCooldowns)
            {
                if (currentTime >= kvp.Value + 1f)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                enemyDamageCooldowns.Remove(key);
            }
        }

        private void OnDestroy()
        {
            phantomBladePool?.Clear();
            if (bladeStormContainer != null)
            {
                if (Application.isPlaying) Destroy(bladeStormContainer.gameObject);
                else DestroyImmediate(bladeStormContainer.gameObject);
            }
            if (phantomBladeMaterial != null)
            {
                if (Application.isPlaying) Destroy(phantomBladeMaterial);
                else DestroyImmediate(phantomBladeMaterial);
            }
        }
    }
}
