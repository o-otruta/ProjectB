using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class LaserTurretAbility : ActiveAbility
    {
        private const int MAX_FENCE_LINKS = 12;
        private const float FENCE_MAX_RANGE = 12f;
        private const float FENCE_TICK_INTERVAL = 0.25f;
        private const float FENCE_CAPSULE_RADIUS = 0.85f;

        private LaserTurretAbilityData TurretData => Data as LaserTurretAbilityData;

        private float currentDps;
        private float currentCooldown;
        private float currentSearchRadius;
        private float currentLifetime;

        private float nextFireTime;
        private float nextFenceTickTime;
        private IObjectPool<LaserTurret> turretPool;
        private Transform turretContainer;

        private readonly LineRenderer[] fenceRenderers = new LineRenderer[MAX_FENCE_LINKS];
        private readonly Collider[] fenceHitBuffer = new Collider[48];
        private readonly HashSet<EntityId> fenceTickHits = new HashSet<EntityId>(64);
        private bool fenceRenderersCreated;

        public int ActiveFenceLinkCount { get; private set; }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            currentDps = TurretData.dps;
            currentCooldown = TurretData.baseCooldown;
            currentSearchRadius = TurretData.searchRadius;
            currentLifetime = TurretData.turretLifetime;

            turretContainer = new GameObject($"Turrets_{Data.id}").transform;
            turretContainer.SetParent(null);

            InitializePool();
            PrewarmFenceRenderers();
        }

        private void InitializePool()
        {
            turretPool = new ObjectPool<LaserTurret>(
                createFunc: () => {
                    GameObject obj;
                    if (TurretData.turretPrefab != null)
                    {
                        obj = Instantiate(TurretData.turretPrefab, turretContainer);
                    }
                    else
                    {
                        obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        obj.transform.SetParent(turretContainer);
                        obj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                        var col = obj.GetComponent<CapsuleCollider>();
                        if (col != null)
                        {
                            if (Application.isPlaying) Destroy(col);
                            else DestroyImmediate(col);
                        }
                    }
                    var t = obj.GetComponent<LaserTurret>();
                    if (t == null) t = obj.AddComponent<LaserTurret>();
                    return t;
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
                defaultCapacity: 3,
                maxSize: 10
            );

            if (Application.isPlaying)
            {
                PrewarmPools(3);
            }
        }

        private void PrewarmFenceRenderers()
        {
            if (fenceRenderersCreated || turretContainer == null) return;
            fenceRenderersCreated = true;

            for (int i = 0; i < MAX_FENCE_LINKS; i++)
            {
                var lrGo = new GameObject($"ElectricFence_{i}");
                lrGo.transform.SetParent(turretContainer, false);
                var lr = lrGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.startWidth = 0.22f;
                lr.endWidth = 0.22f;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startColor = new Color(0.25f, 0.92f, 1f, 0.95f);
                lr.endColor = new Color(0.55f, 0.98f, 1f, 0.95f);
                lr.enabled = false;
                fenceRenderers[i] = lr;
            }
        }

        public override void PrewarmPools(int count = 0)
        {
            PrewarmFenceRenderers();
            if (turretPool == null) return;
            int targetCount = count > 0 ? count : 3;
            var temp = new LaserTurret[targetCount];
            for (int i = 0; i < targetCount; i++) temp[i] = turretPool.Get();
            for (int i = 0; i < targetCount; i++) if (temp[i] != null) turretPool.Release(temp[i]);
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDps += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(1f, currentCooldown - value); break;
                case ModifierType.Radius: currentSearchRadius += value; break;
                case ModifierType.Duration: currentLifetime += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentSearchRadius *= 1.2f;
                currentLifetime *= 1.2f;
            }
            else if (tier == 2)
            {
                currentCooldown = Mathf.Max(0.5f, currentCooldown * 0.8f);
                currentSearchRadius *= 1.15f;
            }
        }

        private static int cachedGroundMask = -1;

        private static int GroundMask
        {
            get
            {
                if (cachedGroundMask == -1)
                {
                    cachedGroundMask = LayerMask.GetMask("Default", "Obstacles");
                    if (cachedGroundMask == 0) cachedGroundMask = ~LayerMask.GetMask("Hero");
                }
                return cachedGroundMask;
            }
        }

        private void Update()
        {
            if (Data == null || turretPool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                SpawnTurret();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.5f);
            }

            if (HasSynergy(SynergyIds.OverchargedGrid))
            {
                TickOverchargedGrid(forceDamageTick: false);
            }
            else if (ActiveFenceLinkCount > 0)
            {
                HideAllFenceRenderers();
            }
        }

        private void SpawnTurret()
        {
            Vector3 spawnPos = transform.position;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 10f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y;
            }
            else
            {
                spawnPos.y = 0f;
            }

            SpawnTurretAt(spawnPos);
        }

        public LaserTurret SpawnTurretAt(Vector3 worldPos)
        {
            if (turretPool == null || TurretData == null) return null;
            var t = turretPool.Get();
            if (t == null) return null;

            t.transform.position = worldPos;
            t.transform.rotation = Quaternion.identity;
            t.Initialize(
                currentSearchRadius,
                currentDps,
                currentLifetime,
                TurretData.targetLayer,
                turretPool,
                runtimeStats,
                runStatistics,
                cachedAbilityId,
                VisualScaleMultiplier);
            return t;
        }

        public int TickOverchargedGrid(bool forceDamageTick = false)
        {
            if (TurretData == null || !HasSynergy(SynergyIds.OverchargedGrid))
            {
                HideAllFenceRenderers();
                return 0;
            }

            PrewarmFenceRenderers();

            IReadOnlyList<LaserTurret> turrets = LaserTurret.ActiveTurrets;
            int count = turrets.Count;
            if (count < 2)
            {
                HideAllFenceRenderers();
                return 0;
            }

            bool applyDamage = forceDamageTick || Time.time >= nextFenceTickTime;
            if (applyDamage)
            {
                nextFenceTickTime = Time.time + GetEffectiveCooldown(FENCE_TICK_INTERVAL, 0.1f);
                fenceTickHits.Clear();
            }

            int linkIdx = 0;
            int enemiesHit = 0;
            float maxRangeSqr = FENCE_MAX_RANGE * FENCE_MAX_RANGE;
            float tickDamage = currentDps * FENCE_TICK_INTERVAL * 1.25f;

            for (int i = 0; i < count && linkIdx < MAX_FENCE_LINKS; i++)
            {
                LaserTurret a = turrets[i];
                if (a == null || !a.gameObject.activeInHierarchy) continue;

                for (int j = i + 1; j < count && linkIdx < MAX_FENCE_LINKS; j++)
                {
                    LaserTurret b = turrets[j];
                    if (b == null || !b.gameObject.activeInHierarchy) continue;

                    Vector3 delta = b.transform.position - a.transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > maxRangeSqr) continue;

                    Vector3 p0 = a.transform.position + Vector3.up * 0.5f;
                    Vector3 p1 = b.transform.position + Vector3.up * 0.5f;

                    LineRenderer lr = fenceRenderers[linkIdx];
                    if (lr != null)
                    {
                        lr.SetPosition(0, p0);
                        lr.SetPosition(1, p1);
                        lr.enabled = true;
                    }
                    linkIdx++;

                    if (applyDamage)
                    {
                        int hits = Physics.OverlapCapsuleNonAlloc(p0, p1, FENCE_CAPSULE_RADIUS, fenceHitBuffer, TurretData.targetLayer);
                        for (int h = 0; h < hits; h++)
                        {
                            Collider col = fenceHitBuffer[h];
                            if (col == null) continue;
                            if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                            EntityId id = col.gameObject.GetEntityId();
                            if (!fenceTickHits.Add(id)) continue;

                            int finalDmg = RollScaledDamage(tickDamage, out bool isCrit);
                            DealDamage(damageable, finalDmg, isCrit, SynergyIds.OverchargedGrid);
                            enemiesHit++;
                        }
                    }
                }
            }

            for (int i = linkIdx; i < MAX_FENCE_LINKS; i++)
            {
                if (fenceRenderers[i] != null)
                {
                    fenceRenderers[i].enabled = false;
                }
            }

            ActiveFenceLinkCount = linkIdx;
            return enemiesHit;
        }

        private void HideAllFenceRenderers()
        {
            for (int i = 0; i < MAX_FENCE_LINKS; i++)
            {
                if (fenceRenderers[i] != null && fenceRenderers[i].enabled)
                {
                    fenceRenderers[i].enabled = false;
                }
            }
            ActiveFenceLinkCount = 0;
        }

        private void OnDestroy()
        {
            turretPool?.Clear();
            if (turretContainer != null)
            {
                if (Application.isPlaying) Destroy(turretContainer.gameObject);
                else DestroyImmediate(turretContainer.gameObject);
            }
        }
    }
}
