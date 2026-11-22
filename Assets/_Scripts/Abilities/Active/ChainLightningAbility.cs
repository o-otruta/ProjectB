using System.Collections.Generic;
using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    public class ChainLightningAbility : ActiveAbility
    {
        private const float THUNDERSTORM_DEEP_SLOW_FACTOR = 0.10f;
        private const float THUNDERSTORM_DEEP_SLOW_DURATION = 0.65f;
        private const float THUNDERSTORM_ARC_RADIUS = 4.2f;
        private const int THUNDERSTORM_EXTRA_ARCS = 2;

        private ChainLightningAbilityData LightningData => Data as ChainLightningAbilityData;

        private float currentDamage;
        private float currentCooldown;
        private int currentBounceCount;
        private float currentSearchRadius;
        private float currentBounceRadius;

        private float nextFireTime;
        private float boltHideTime;
        private float frostHideTime;
        private LineRenderer lineRenderer;
        private LineRenderer frostArcLineRenderer;

        private readonly Collider[] hitBuffer = new Collider[128];
        private readonly Collider[] frostHitBuffer = new Collider[48];
        private readonly List<Transform> chainTargets = new List<Transform>(16);
        private readonly Transform[] frostPicked = new Transform[THUNDERSTORM_EXTRA_ARCS];
        private readonly IDamageable[] frostPickedDamageables = new IDamageable[THUNDERSTORM_EXTRA_ARCS];
        private Vector3[] boltPoints = new Vector3[64];
        private Vector3[] frostArcPoints = new Vector3[48];
        private int frostArcPointCount;

        public override void SimulateFrameTick()
        {
            Update();
        }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            if (LightningData != null)
            {
                currentDamage = LightningData.baseDamage;
                currentCooldown = LightningData.baseCooldown;
                currentBounceCount = Mathf.Max(0, LightningData.bounceCount);
                currentSearchRadius = LightningData.searchRadius;
                currentBounceRadius = LightningData.bounceRadius;
            }

            EnsureLineRenderer();
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                var lrGo = new GameObject("ChainLightning_Bolt");
                lrGo.transform.SetParent(transform, false);
                lineRenderer = lrGo.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.startWidth = 0.22f * VisualScaleMultiplier;
                lineRenderer.endWidth = 0.12f * VisualScaleMultiplier;
                lineRenderer.numCornerVertices = 2;
                lineRenderer.numCapVertices = 2;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.startColor = new Color(0.75f, 0.96f, 1f, 1f);
                lineRenderer.endColor = new Color(0.28f, 0.72f, 1f, 0.95f);

                if (LightningData != null && LightningData.lineMaterial != null)
                {
                    lineRenderer.sharedMaterial = LightningData.lineMaterial;
                }

                lineRenderer.enabled = false;
            }

            if (frostArcLineRenderer == null)
            {
                var frostGo = new GameObject("ChainLightning_FrostArc");
                frostGo.transform.SetParent(transform, false);
                frostArcLineRenderer = frostGo.AddComponent<LineRenderer>();
                frostArcLineRenderer.useWorldSpace = true;
                frostArcLineRenderer.startWidth = 0.18f * VisualScaleMultiplier;
                frostArcLineRenderer.endWidth = 0.08f * VisualScaleMultiplier;
                frostArcLineRenderer.numCornerVertices = 1;
                frostArcLineRenderer.numCapVertices = 1;
                frostArcLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                frostArcLineRenderer.receiveShadows = false;
                frostArcLineRenderer.startColor = new Color(0.72f, 0.98f, 1f, 1f);
                frostArcLineRenderer.endColor = new Color(0.45f, 0.88f, 1f, 0.9f);

                if (LightningData != null && LightningData.lineMaterial != null)
                {
                    frostArcLineRenderer.sharedMaterial = LightningData.lineMaterial;
                }

                frostArcLineRenderer.enabled = false;
            }
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Count:
                    currentBounceCount = Mathf.Max(0, currentBounceCount + Mathf.RoundToInt(value));
                    break;
                case ModifierType.Cooldown:
                    currentCooldown = Mathf.Max(0.25f, currentCooldown - value);
                    break;
                case ModifierType.Radius:
                    currentSearchRadius += value;
                    currentBounceRadius += value * 0.5f;
                    break;
                case ModifierType.Area:
                    currentBounceRadius += value;
                    break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentSearchRadius *= 1.2f;
                currentBounceRadius *= 1.2f;
                UpdateBoltWidths();
            }
            else if (tier == 2)
            {
                currentBounceCount += 2;
                UpdateBoltWidths();
            }
        }

        private void UpdateBoltWidths()
        {
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = 0.22f * VisualScaleMultiplier;
                lineRenderer.endWidth = 0.12f * VisualScaleMultiplier;
            }
            if (frostArcLineRenderer != null)
            {
                frostArcLineRenderer.startWidth = 0.18f * VisualScaleMultiplier;
                frostArcLineRenderer.endWidth = 0.08f * VisualScaleMultiplier;
            }
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            if (boltHideTime > 0f && Time.time >= boltHideTime)
            {
                boltHideTime = 0f;
                if (lineRenderer != null) lineRenderer.enabled = false;
            }

            if (frostHideTime > 0f && Time.time >= frostHideTime)
            {
                frostHideTime = 0f;
                if (frostArcLineRenderer != null) frostArcLineRenderer.enabled = false;
            }

            if (Time.time >= nextFireTime)
            {
                TriggerChainCast();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.25f);
            }
        }

        public int TriggerChainCast()
        {
            if (LightningData == null) return 0;

            chainTargets.Clear();
            frostArcPointCount = 0;
            LayerMask mask = LightningData.targetLayer;
            bool hasThunderstorm = HasSynergy(SynergyIds.Thunderstorm);

            Transform firstTarget = FindClosestTarget(transform.position, currentSearchRadius, mask, excludeSelected: false, out IDamageable firstDamageable);
            if (firstTarget == null) return 0;

            chainTargets.Add(firstTarget);
            bool firstWasSlowed = firstDamageable is EnemyBase firstEnemy && firstEnemy.IsSlowed;
            int firstDmg = RollScaledDamage(currentDamage, out bool firstCrit);
            DealDamage(firstDamageable, firstDmg, firstCrit);
            if (hasThunderstorm && firstWasSlowed)
            {
                TriggerThunderstormOverload(firstTarget, firstDamageable as EnemyBase, mask);
            }

            Transform lastTarget = firstTarget;
            for (int b = 0; b < currentBounceCount; b++)
            {
                Transform nextTarget = FindClosestTarget(lastTarget.position, currentBounceRadius, mask, excludeSelected: true, out IDamageable nextDamageable);
                if (nextTarget == null) break;

                chainTargets.Add(nextTarget);
                bool nextWasSlowed = nextDamageable is EnemyBase nextEnemy && nextEnemy.IsSlowed;
                int bounceDmg = RollScaledDamage(currentDamage, out bool bounceCrit);
                DealDamage(nextDamageable, bounceDmg, bounceCrit);
                if (hasThunderstorm && nextWasSlowed)
                {
                    TriggerThunderstormOverload(nextTarget, nextDamageable as EnemyBase, mask);
                }
                lastTarget = nextTarget;
            }

            DrawBoltVisual();
            DrawFrostArcVisual();
            return chainTargets.Count;
        }

        private void TriggerThunderstormOverload(Transform sourceTarget, EnemyBase sourceEnemy, LayerMask mask)
        {
            if (sourceTarget == null) return;

            Vector3 origin = sourceTarget.position;
            if (sourceEnemy != null && !sourceEnemy.IsDead)
            {
                sourceEnemy.ApplySlow(THUNDERSTORM_DEEP_SLOW_FACTOR, THUNDERSTORM_DEEP_SLOW_DURATION);
            }

            for (int i = 0; i < THUNDERSTORM_EXTRA_ARCS; i++)
            {
                frostPicked[i] = null;
                frostPickedDamageables[i] = null;
            }

            int found = Physics.OverlapSphereNonAlloc(origin, THUNDERSTORM_ARC_RADIUS, frostHitBuffer, mask);
            int pickedCount = 0;

            for (int pick = 0; pick < THUNDERSTORM_EXTRA_ARCS; pick++)
            {
                Transform best = null;
                IDamageable bestDmg = null;
                float bestSqr = float.MaxValue;

                for (int i = 0; i < found; i++)
                {
                    Collider col = frostHitBuffer[i];
                    if (col == null) continue;
                    Transform cand = col.transform;
                    if (cand == sourceTarget) continue;

                    bool alreadyPicked = false;
                    for (int p = 0; p < pickedCount; p++)
                    {
                        if (frostPicked[p] == cand)
                        {
                            alreadyPicked = true;
                            break;
                        }
                    }
                    if (alreadyPicked) continue;

                    if (!col.TryGetComponent<IDamageable>(out var damageable) ||
                        !CombatTargetFilter.IsValidAutoAimTarget(col, damageable, mask))
                    {
                        continue;
                    }

                    float sqr = (cand.position - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = cand;
                        bestDmg = damageable;
                    }
                }

                if (best == null) break;
                frostPicked[pickedCount] = best;
                frostPickedDamageables[pickedCount] = bestDmg;
                pickedCount++;
            }

            Vector3 originVisual = origin + Vector3.up * 0.7f;
            for (int i = 0; i < pickedCount; i++)
            {
                Transform neighbor = frostPicked[i];
                IDamageable neighborDmg = frostPickedDamageables[i];
                if (neighbor == null || neighborDmg == null) continue;

                int arcDmg = RollScaledDamage(currentDamage * 0.75f, out bool arcCrit);
                DealDamage(neighborDmg, arcDmg, arcCrit, SynergyIds.Thunderstorm);

                if (neighborDmg is EnemyBase neighborEnemy && !neighborEnemy.IsDead)
                {
                    neighborEnemy.ApplySlow(0.35f, THUNDERSTORM_DEEP_SLOW_DURATION);
                }

                if (frostArcPointCount + 3 <= frostArcPoints.Length)
                {
                    frostArcPoints[frostArcPointCount++] = originVisual;
                    frostArcPoints[frostArcPointCount++] = neighbor.position + Vector3.up * 0.7f;
                    frostArcPoints[frostArcPointCount++] = originVisual;
                }
            }

            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.ThunderstormShatter, origin, THUNDERSTORM_ARC_RADIUS));
        }

        private Transform FindClosestTarget(
            Vector3 origin,
            float radius,
            LayerMask mask,
            bool excludeSelected,
            out IDamageable bestDamageable)
        {
            bestDamageable = null;
            int found = Physics.OverlapSphereNonAlloc(origin, radius, hitBuffer, mask);
            if (found == 0) return null;

            Transform best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) ||
                    !CombatTargetFilter.IsValidAutoAimTarget(col, damageable, mask))
                {
                    continue;
                }

                Transform candidate = col.transform;
                if (excludeSelected && ContainsTransform(chainTargets, candidate)) continue;

                float sqr = (candidate.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                    bestDamageable = damageable;
                }
            }

            return best;
        }

        private static bool ContainsTransform(List<Transform> list, Transform target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return true;
            }
            return false;
        }

        private void DrawBoltVisual()
        {
            EnsureLineRenderer();
            if (lineRenderer == null || chainTargets.Count == 0) return;

            int totalPoints = 1 + chainTargets.Count * 3;
            if (boltPoints.Length < totalPoints)
            {
                boltPoints = new Vector3[totalPoints];
            }

            Vector3 prev = transform.position + Vector3.up * 0.7f;
            int idx = 0;
            boltPoints[idx++] = prev;

            for (int i = 0; i < chainTargets.Count; i++)
            {
                Vector3 end = chainTargets[i].position + Vector3.up * 0.7f;
                Vector3 delta = end - prev;
                Vector3 side = new Vector3(-delta.z, 0f, delta.x);
                if (side.sqrMagnitude > 0.0001f) side.Normalize();

                float jitterAmp = Mathf.Clamp(delta.magnitude * 0.14f, 0.15f, 0.55f);
                boltPoints[idx++] = Vector3.Lerp(prev, end, 0.33f) + side * Random.Range(-jitterAmp, jitterAmp) + Vector3.up * Random.Range(0.05f, 0.3f);
                boltPoints[idx++] = Vector3.Lerp(prev, end, 0.67f) + side * Random.Range(-jitterAmp, jitterAmp) + Vector3.up * Random.Range(-0.1f, 0.2f);
                boltPoints[idx++] = end;

                prev = end;
            }

            lineRenderer.positionCount = idx;
            for (int i = 0; i < idx; i++)
            {
                lineRenderer.SetPosition(i, boltPoints[i]);
            }

            lineRenderer.enabled = true;
            float duration = LightningData != null ? LightningData.boltVisualDuration : 0.16f;
            boltHideTime = Time.time + Mathf.Max(0.05f, duration);
        }

        private void DrawFrostArcVisual()
        {
            if (frostArcLineRenderer == null) return;
            if (frostArcPointCount <= 0)
            {
                frostArcLineRenderer.enabled = false;
                return;
            }

            frostArcLineRenderer.positionCount = frostArcPointCount;
            for (int i = 0; i < frostArcPointCount; i++)
            {
                frostArcLineRenderer.SetPosition(i, frostArcPoints[i]);
            }

            frostArcLineRenderer.enabled = true;
            float duration = LightningData != null ? LightningData.boltVisualDuration : 0.16f;
            frostHideTime = Time.time + Mathf.Max(0.08f, duration * 1.25f);
        }
    }
}
