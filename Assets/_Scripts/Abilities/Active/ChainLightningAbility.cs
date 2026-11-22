using System.Collections.Generic;
using UnityEngine;
using ProjectB.Combat;

namespace ProjectB.Abilities
{
    public class ChainLightningAbility : ActiveAbility
    {
        private ChainLightningAbilityData LightningData => Data as ChainLightningAbilityData;

        private float currentDamage;
        private float currentCooldown;
        private int currentBounceCount;
        private float currentSearchRadius;
        private float currentBounceRadius;

        private float nextFireTime;
        private float boltHideTime;
        private LineRenderer lineRenderer;

        private readonly Collider[] hitBuffer = new Collider[128];
        private readonly List<Transform> chainTargets = new List<Transform>(16);
        private Vector3[] boltPoints = new Vector3[64];

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
            if (lineRenderer != null) return;

            var lrGo = new GameObject("ChainLightning_Bolt");
            lrGo.transform.SetParent(transform, false);
            lineRenderer = lrGo.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.22f;
            lineRenderer.endWidth = 0.12f;
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

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            if (boltHideTime > 0f && Time.time >= boltHideTime)
            {
                boltHideTime = 0f;
                if (lineRenderer != null) lineRenderer.enabled = false;
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
            LayerMask mask = LightningData.targetLayer;

            Transform firstTarget = FindClosestTarget(transform.position, currentSearchRadius, mask, excludeSelected: false, out IDamageable firstDamageable);
            if (firstTarget == null) return 0;

            chainTargets.Add(firstTarget);
            int firstDmg = RollScaledDamage(currentDamage, out bool firstCrit);
            DealDamage(firstDamageable, firstDmg, firstCrit);

            Transform lastTarget = firstTarget;
            for (int b = 0; b < currentBounceCount; b++)
            {
                Transform nextTarget = FindClosestTarget(lastTarget.position, currentBounceRadius, mask, excludeSelected: true, out IDamageable nextDamageable);
                if (nextTarget == null) break;

                chainTargets.Add(nextTarget);
                int bounceDmg = RollScaledDamage(currentDamage, out bool bounceCrit);
                DealDamage(nextDamageable, bounceDmg, bounceCrit);
                lastTarget = nextTarget;
            }

            DrawBoltVisual();
            return chainTargets.Count;
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
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

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
    }
}
