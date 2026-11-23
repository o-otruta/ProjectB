using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    /// <summary>
    /// Skyfall Spire (Epic): telegraphs and drops a pillar of fire onto the densest enemy cluster.
    /// Synergy "Solar Eclipse" (+ Black Hole): 0.3s before impact an eclipse pulse drags enemies into
    /// the strike point, the strike deals bonus damage and leaves survivors blinded (slowed).
    /// </summary>
    public class SkyfallSpireAbility : ActiveAbility
    {
        public const int MAX_CONCURRENT_STRIKES = 6;
        private const int MAX_CANDIDATES = 64;

        private struct StrikeState
        {
            public bool active;
            public Vector3 position;
            public float timer;
            public bool eclipseTriggered;
        }

        private SkyfallSpireAbilityData SpireData => Data as SkyfallSpireAbilityData;

        private readonly Collider[] hitBuffer = new Collider[128];
        private readonly Vector3[] candidatePositions = new Vector3[MAX_CANDIDATES];
        private readonly StrikeState[] strikes = new StrikeState[MAX_CONCURRENT_STRIKES];
        private readonly Transform[] spireVisuals = new Transform[MAX_CONCURRENT_STRIKES];
        private readonly Transform[] telegraphVisuals = new Transform[MAX_CONCURRENT_STRIKES];
        private readonly Vector3[] chosenBuffer = new Vector3[MAX_CONCURRENT_STRIKES];
        private Transform visualRoot;

        private float currentDamage;
        private float currentCooldown;
        private int currentStrikeCount;
        private float currentStrikeRadius;
        private float nextFireTime;

        public int ActiveStrikeCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < strikes.Length; i++) if (strikes[i].active) c++;
                return c;
            }
        }

        public float EffectiveStrikeRadius => ScaleArea(currentStrikeRadius);
        public int EclipsePulses { get; private set; }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            var d = SpireData;
            if (d != null)
            {
                currentDamage = d.baseDamage;
                currentCooldown = d.baseCooldown;
                currentStrikeCount = d.strikeCount;
                currentStrikeRadius = d.strikeRadius;
            }
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            visualRoot = new GameObject("SkyfallSpire_Visuals").transform;
            var d = SpireData;
            for (int i = 0; i < MAX_CONCURRENT_STRIKES; i++)
            {
                spireVisuals[i] = SpawnVisual(d != null ? d.spirePrefab : null, $"Spire_{i}");
                telegraphVisuals[i] = SpawnVisual(d != null ? d.telegraphPrefab : null, $"SpireTelegraph_{i}");
            }
        }

        private Transform SpawnVisual(GameObject prefab, string visualName)
        {
            GameObject go = prefab != null ? Instantiate(prefab, visualRoot) : new GameObject(visualName);
            go.name = visualName;
            go.transform.SetParent(visualRoot, false);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[r].receiveShadows = false;
            }
            go.SetActive(false);
            return go.transform;
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDamage += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(1.2f, currentCooldown - value); break;
                case ModifierType.Count: currentStrikeCount = Mathf.Min(MAX_CONCURRENT_STRIKES, currentStrikeCount + Mathf.Max(1, Mathf.RoundToInt(value))); break;
                case ModifierType.Radius:
                case ModifierType.Area: currentStrikeRadius += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1) currentStrikeRadius *= 1.2f;
            else if (tier == 2) currentStrikeCount = Mathf.Min(MAX_CONCURRENT_STRIKES, currentStrikeCount + 1);
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            StepStrikes(Time.deltaTime);

            if (Time.time >= nextFireTime)
            {
                int launched = Fire();
                nextFireTime = Time.time + (launched > 0 ? GetEffectiveCooldown(currentCooldown, 1.2f) : 0.3f);
            }
        }

        /// <summary>Picks the densest clusters and starts telegraphed strikes. Returns strikes launched.</summary>
        public int Fire()
        {
            var d = SpireData;
            if (d == null) return 0;

            int chosen = FindDensestClusters(transform.position, d.searchRadius, d.clusterRadius, currentStrikeCount, d.targetLayer, chosenBuffer);
            int launched = 0;
            for (int c = 0; c < chosen; c++)
            {
                if (StartStrike(chosenBuffer[c])) launched++;
            }
            return launched;
        }

        public bool StartStrike(Vector3 position)
        {
            var d = SpireData;
            if (d == null) return false;

            for (int i = 0; i < strikes.Length; i++)
            {
                if (strikes[i].active) continue;

                position.y = 0f;
                strikes[i].active = true;
                strikes[i].position = position;
                strikes[i].timer = d.telegraphDelay;
                strikes[i].eclipseTriggered = false;

                float r = EffectiveStrikeRadius;
                if (telegraphVisuals[i] != null)
                {
                    telegraphVisuals[i].gameObject.SetActive(true);
                    telegraphVisuals[i].position = position + Vector3.up * 0.05f;
                    telegraphVisuals[i].localScale = new Vector3(r, 1f, r);
                }
                if (spireVisuals[i] != null)
                {
                    spireVisuals[i].gameObject.SetActive(true);
                    spireVisuals[i].position = position + Vector3.up * d.spireDropHeight;
                    spireVisuals[i].localScale = Vector3.one * VisualScaleMultiplier;
                }

                eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.SkyfallTelegraph, position, r));
                return true;
            }
            return false;
        }

        /// <summary>Advances telegraphs/drops and resolves impacts (exposed for tests).</summary>
        public void StepStrikes(float dt)
        {
            var d = SpireData;
            if (d == null) return;

            for (int i = 0; i < strikes.Length; i++)
            {
                if (!strikes[i].active) continue;

                strikes[i].timer -= dt;

                if (!strikes[i].eclipseTriggered && strikes[i].timer <= d.eclipseLeadTime && HasSynergy(SynergyIds.SolarEclipse))
                {
                    strikes[i].eclipseTriggered = true;
                    TriggerEclipsePull(strikes[i].position);
                }

                if (spireVisuals[i] != null)
                {
                    float t = 1f - Mathf.Clamp01(strikes[i].timer / Mathf.Max(0.01f, d.telegraphDelay));
                    float height = Mathf.Lerp(d.spireDropHeight, 0f, t * t);
                    spireVisuals[i].position = strikes[i].position + Vector3.up * height;
                }

                if (strikes[i].timer <= 0f)
                {
                    ResolveImpact(strikes[i].position, strikes[i].eclipseTriggered);
                    strikes[i].active = false;
                    if (spireVisuals[i] != null) spireVisuals[i].gameObject.SetActive(false);
                    if (telegraphVisuals[i] != null) telegraphVisuals[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Solar Eclipse pulse: drags nearby enemies toward the impact point. Returns enemies pulled.</summary>
        public int TriggerEclipsePull(Vector3 point)
        {
            var d = SpireData;
            if (d == null) return 0;

            float pullRadius = EffectiveStrikeRadius * d.eclipsePullRadiusMultiplier;
            int found = Physics.OverlapSphereNonAlloc(point, pullRadius, hitBuffer, d.targetLayer);
            int pulled = 0;
            for (int i = 0; i < found; i++)
            {
                if (hitBuffer[i] == null || !hitBuffer[i].TryGetComponent<EnemyBase>(out var enemy) || enemy.IsDead) continue;
                Vector3 delta = point - enemy.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.04f) continue;
                enemy.ApplyPull(delta * d.eclipsePullStrength);
                pulled++;
            }

            EclipsePulses++;
            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.SolarEclipse, point, pullRadius));
            return pulled;
        }

        /// <summary>Deals the pillar damage. Returns enemies hit.</summary>
        public int ResolveImpact(Vector3 point, bool eclipsed)
        {
            var d = SpireData;
            if (d == null) return 0;

            float radius = EffectiveStrikeRadius;
            float raw = eclipsed ? currentDamage * d.eclipseDamageMultiplier : currentDamage;
            int found = Physics.OverlapSphereNonAlloc(point, radius, hitBuffer, d.targetLayer);
            int hitCount = 0;
            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                int dmg = RollScaledDamage(raw, out bool isCrit);
                DealDamage(damageable, dmg, isCrit, eclipsed ? SynergyIds.SolarEclipse : null, isAoE: true, sourcePosition: point);
                hitCount++;

                if (eclipsed && col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(d.eclipseBlindSlow, d.eclipseBlindDuration);
                }
            }

            eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.SkyfallStrike, point, radius));
            return hitCount;
        }

        /// <summary>
        /// Zero-GC density search: for each enemy within <paramref name="searchRadius"/> counts neighbours inside
        /// <paramref name="clusterRadius"/> and returns up to <paramref name="maxResults"/> non-overlapping cluster centres.
        /// </summary>
        public int FindDensestClusters(Vector3 origin, float searchRadius, float clusterRadius, int maxResults, LayerMask layer, Vector3[] results)
        {
            int found = Physics.OverlapSphereNonAlloc(origin, searchRadius, hitBuffer, layer);
            int candidateCount = 0;
            for (int i = 0; i < found && candidateCount < MAX_CANDIDATES; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var dmg) || dmg.IsDead) continue;
                if (!CombatTargetFilter.IsValidAutoAimTarget(col, dmg, layer)) continue;
                candidatePositions[candidateCount++] = col.transform.position;
            }

            return SelectDensestPoints(candidatePositions, candidateCount, clusterRadius, Mathf.Min(maxResults, results.Length), results);
        }

        /// <summary>Pure helper (testable without physics): greedy densest non-overlapping centres.</summary>
        public static int SelectDensestPoints(Vector3[] points, int count, float clusterRadius, int maxResults, Vector3[] results)
        {
            if (points == null || results == null || count <= 0 || maxResults <= 0) return 0;

            float clusterSqr = clusterRadius * clusterRadius;
            int chosenCount = 0;

            while (chosenCount < maxResults)
            {
                int bestIndex = -1;
                int bestScore = -1;
                for (int i = 0; i < count; i++)
                {
                    bool excluded = false;
                    for (int c = 0; c < chosenCount; c++)
                    {
                        Vector3 dc = points[i] - results[c];
                        dc.y = 0f;
                        if (dc.sqrMagnitude < clusterSqr) { excluded = true; break; }
                    }
                    if (excluded) continue;

                    int score = 0;
                    for (int j = 0; j < count; j++)
                    {
                        Vector3 dj = points[i] - points[j];
                        dj.y = 0f;
                        if (dj.sqrMagnitude <= clusterSqr) score++;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0) break;
                results[chosenCount++] = points[bestIndex];
            }

            return chosenCount;
        }

        private void OnDestroy()
        {
            if (visualRoot != null)
            {
                if (Application.isPlaying) Destroy(visualRoot.gameObject);
                else DestroyImmediate(visualRoot.gameObject);
            }
        }
    }
}
