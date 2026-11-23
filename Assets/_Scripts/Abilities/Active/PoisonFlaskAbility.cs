using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data;

namespace ProjectB.Abilities
{
    public class PoisonFlaskAbility : ActiveAbility
    {
        private PoisonFlaskAbilityData FlaskData => Data as PoisonFlaskAbilityData;

        private float currentDamage;
        private float currentCooldown;
        private float currentPuddleRadius;
        private float currentPuddleDuration;
        private int currentFlaskCount;
        private float currentThrowRange;

        private float nextFireTime;
        private Transform zoneContainer;
        private IObjectPool<PoisonFlaskProjectile> flaskPool;
        private IObjectPool<PoisonPuddle> puddlePool;
        private System.Action<Vector3> cachedLandCallback;

        private readonly Collider[] hitBuffer = new Collider[128];
        private readonly List<Transform> targetBuffer = new List<Transform>(8);

        public override void SimulateFrameTick()
        {
            Update();
        }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            if (FlaskData != null)
            {
                currentDamage = FlaskData.baseDamage;
                currentCooldown = FlaskData.baseCooldown;
                currentPuddleRadius = FlaskData.puddleRadius;
                currentPuddleDuration = FlaskData.puddleDuration;
                currentFlaskCount = Mathf.Max(1, FlaskData.flaskCount);
                currentThrowRange = FlaskData.throwRange;
            }

            zoneContainer = new GameObject($"PoisonZones_{Data.id}").transform;
            zoneContainer.SetParent(null);
            cachedLandCallback = OnFlaskLanded;

            InitializePools();
        }

        private void InitializePools()
        {
            flaskPool = new ObjectPool<PoisonFlaskProjectile>(
                createFunc: CreateFlaskInstance,
                actionOnGet: f => { if (f != null) f.gameObject.SetActive(true); },
                actionOnRelease: f => { if (f != null) f.gameObject.SetActive(false); },
                actionOnDestroy: f => { if (f != null) SafeDestroy(f.gameObject); },
                collectionCheck: true,
                defaultCapacity: 4,
                maxSize: 24);

            puddlePool = new ObjectPool<PoisonPuddle>(
                createFunc: CreatePuddleInstance,
                actionOnGet: p => { if (p != null) p.gameObject.SetActive(true); },
                actionOnRelease: p => { if (p != null) p.gameObject.SetActive(false); },
                actionOnDestroy: p => { if (p != null) SafeDestroy(p.gameObject); },
                collectionCheck: true,
                defaultCapacity: 4,
                maxSize: 24);

            if (Application.isPlaying)
            {
                PrewarmPools(4);
            }
        }

        public override void PrewarmPools(int count = 0)
        {
            int targetCount = count > 0 ? count : 4;
            if (flaskPool != null)
            {
                var tempFlasks = new PoisonFlaskProjectile[targetCount];
                for (int i = 0; i < targetCount; i++) tempFlasks[i] = flaskPool.Get();
                for (int i = 0; i < targetCount; i++) if (tempFlasks[i] != null) flaskPool.Release(tempFlasks[i]);
            }
            if (puddlePool != null)
            {
                var tempPuddles = new PoisonPuddle[targetCount];
                for (int i = 0; i < targetCount; i++) tempPuddles[i] = puddlePool.Get();
                for (int i = 0; i < targetCount; i++) if (tempPuddles[i] != null) puddlePool.Release(tempPuddles[i]);
            }
        }

        private PoisonFlaskProjectile CreateFlaskInstance()
        {
            GameObject obj;
            if (FlaskData != null && FlaskData.flaskPrefab != null)
            {
                obj = Instantiate(FlaskData.flaskPrefab, zoneContainer);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "PoisonFlask";
                obj.transform.SetParent(zoneContainer, false);
                obj.transform.localScale = new Vector3(0.32f, 0.4f, 0.32f);
                var col = obj.GetComponent<Collider>();
                if (col != null) SafeDestroy(col);
                var mr = obj.GetComponent<MeshRenderer>();
                if (mr != null && FlaskData != null && FlaskData.puddleMaterial != null)
                {
                    mr.sharedMaterial = FlaskData.puddleMaterial;
                }
            }

            var proj = obj.GetComponent<PoisonFlaskProjectile>();
            if (proj == null) proj = obj.AddComponent<PoisonFlaskProjectile>();
            return proj;
        }

        private PoisonPuddle CreatePuddleInstance()
        {
            var go = new GameObject("PoisonPuddle");
            go.transform.SetParent(zoneContainer, false);
            var puddle = go.AddComponent<PoisonPuddle>();
            puddle.PrewarmVisual(
                FlaskData != null ? FlaskData.puddleMaterial : null,
                FlaskData != null ? FlaskData.puddleVisualPrefab : null);
            return puddle;
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Count:
                    currentFlaskCount = Mathf.Max(1, currentFlaskCount + Mathf.RoundToInt(value));
                    break;
                case ModifierType.Area:
                case ModifierType.Radius:
                    currentPuddleRadius += value;
                    break;
                case ModifierType.Duration:
                    currentPuddleDuration += value;
                    break;
                case ModifierType.Cooldown:
                    currentCooldown = Mathf.Max(0.35f, currentCooldown - value);
                    break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentPuddleRadius *= 1.2f;
                currentThrowRange *= 1.15f;
            }
            else if (tier == 2)
            {
                currentFlaskCount += 1;
            }
        }

        private void Update()
        {
            if (Data == null || flaskPool == null || Time.deltaTime <= 0f) return;

            if (Time.time >= nextFireTime)
            {
                ThrowFlasks();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.35f);
            }
        }

        public int ThrowFlasks()
        {
            if (FlaskData == null || flaskPool == null) return 0;

            int found = FindClosestTargets(currentThrowRange, FlaskData.targetLayer, currentFlaskCount, targetBuffer);
            if (found == 0) return 0;

            Vector3 origin = transform.position + Vector3.up * 0.8f;
            float flight = FlaskData.flightDuration;

            for (int i = 0; i < currentFlaskCount; i++)
            {
                Transform target = targetBuffer[i % found];
                if (target == null) continue;

                PoisonFlaskProjectile flask = flaskPool.Get();
                if (flask == null) continue;

                Vector3 landingPos = target.position;
                if (i >= found)
                {
                    Vector2 jitter = Random.insideUnitCircle * 1.2f;
                    landingPos += new Vector3(jitter.x, 0f, jitter.y);
                }

                flask.transform.localScale = Vector3.one * VisualScaleMultiplier;
                flask.Initialize(origin, landingPos, flight, cachedLandCallback, flaskPool);
            }

            return currentFlaskCount;
        }

        private void OnFlaskLanded(Vector3 position)
        {
            SpawnPuddleAt(position);
        }

        public PoisonPuddle SpawnPuddleAt(Vector3 position)
        {
            if (puddlePool == null || FlaskData == null) return null;

            PoisonPuddle puddle = puddlePool.Get();
            if (puddle == null) return null;

            puddle.Initialize(
                position,
                ScaleArea(currentPuddleRadius),
                currentPuddleDuration,
                FlaskData.tickInterval,
                GetScaledDamage(currentDamage),
                FlaskData.slowFactor,
                FlaskData.targetLayer,
                runtimeStats,
                FlaskData.puddleMaterial,
                puddlePool,
                runStatistics,
                cachedAbilityId,
                FlaskData.puddleVisualPrefab);
            eventBus?.Publish(new AbilitySfxEvent(SfxType.PoisonSplash, 0.65f));
            return puddle;
        }

        private int FindClosestTargets(float searchRadius, LayerMask targetLayer, int maxTargets, List<Transform> results)
        {
            results.Clear();
            int targetsFound = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, hitBuffer, targetLayer);
            if (targetsFound == 0) return 0;

            Vector3 origin = transform.position;
            int needed = Mathf.Min(maxTargets, targetsFound);

            for (int pick = 0; pick < needed; pick++)
            {
                Transform best = null;
                float bestSqr = float.MaxValue;

                for (int i = 0; i < targetsFound; i++)
                {
                    Collider col = hitBuffer[i];
                    if (col == null) continue;
                    if (!col.TryGetComponent<IDamageable>(out var dmg) ||
                        !CombatTargetFilter.IsValidAutoAimTarget(col, dmg, targetLayer))
                    {
                        continue;
                    }

                    Transform cand = col.transform;
                    if (results.Contains(cand)) continue;

                    float sqr = (cand.position - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = cand;
                    }
                }

                if (best == null) break;
                results.Add(best);
            }

            return results.Count;
        }

        private void OnDestroy()
        {
            flaskPool?.Clear();
            puddlePool?.Clear();
            if (zoneContainer != null)
            {
                SafeDestroy(zoneContainer.gameObject);
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
