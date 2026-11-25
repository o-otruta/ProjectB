using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Visuals;

namespace ProjectB.Enemies
{
    /// <summary>
    /// VoidSummoner cultist that maintains ~8m standoff distance, summons 2 skeleton minions,
    /// and heals nearby allied enemies for 15% of their MaxHP on each pulse (bosses and mini-bosses
    /// receive only <see cref="SummonerEnemyData.bossHealMultiplier"/> of it). Glows green before each pulse.
    /// </summary>
    public class SummonerEnemy : EnemyBase
    {
        private static readonly Collider[] AllyHealBuffer = new Collider[32];

        [Header("Voxel Visuals")]
        [SerializeField] private VoxelMeshAnimator meshAnimator;
        [SerializeField] private MeshFilter bodyMeshFilter;
        [SerializeField] private Mesh castMesh;
        [SerializeField] private float castPoseDuration = 0.45f;

        private SummonerEnemyData summonerData;
        private Action<EnemyData, Vector3> summonCallback;
        private float nextPulseTime;
        private float castPoseEndTime;
        private bool isMoving;
        private bool telegraphStarted;

        // Healer marker: floating green pluses above the head (shared mesh + instanced material)
        private static Mesh sharedPlusMesh;
        private Transform healerMarkerRoot;
        private Material healerMarkerMaterialInUse;
        private float healerMarkerBaseHeight;
        private float markerBobPhase;
        private float markerVisibility;
        private float markerShowFrom;
        private float markerShowUntil;
        private bool markerScheduled;

        public void SetSummonCallback(Action<EnemyData, Vector3> callback)
        {
            summonCallback = callback;
        }

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);

            summonerData = data as SummonerEnemyData;
            float interval = summonerData != null ? Mathf.Max(1.5f, summonerData.summonInterval) : 6f;
            nextPulseTime = Time.time + interval * 0.65f;
            castPoseEndTime = 0f;
            isMoving = false;
            telegraphStarted = false;
            markerVisibility = 0f;
            markerShowFrom = 0f;
            markerShowUntil = 0f;
            markerScheduled = false;

            if (meshAnimator == null)
            {
                meshAnimator = GetComponentInChildren<VoxelMeshAnimator>(true);
            }
            if (bodyMeshFilter == null && meshAnimator != null)
            {
                bodyMeshFilter = meshAnimator.TargetMeshFilter;
            }
            if (bodyMeshFilter == null)
            {
                bodyMeshFilter = GetComponentInChildren<MeshFilter>(true);
            }
            meshAnimator?.Stop();
            EnsureHealerMarker();
        }

        protected override void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            isMoving = false;
            base.Update();

            if (IsTargetDead())
            {
                isMoving = false;
            }

            UpdateAnimationState();
            UpdateHealerMarker();
        }

        private void EnsureHealerMarker()
        {
            Material mat = summonerData != null ? summonerData.healerMarkerMaterial : null;
            int count = summonerData != null ? summonerData.healerMarkerCount : 0;
            if (mat == null || count <= 0)
            {
                if (healerMarkerRoot != null) healerMarkerRoot.gameObject.SetActive(false);
                return;
            }

            if (healerMarkerRoot != null && healerMarkerMaterialInUse == mat)
            {
                // Hidden until the next heal telegraph.
                healerMarkerRoot.gameObject.SetActive(false);
                return;
            }

            if (healerMarkerRoot != null)
            {
                Destroy(healerMarkerRoot.gameObject);
                healerMarkerRoot = null;
            }

            if (sharedPlusMesh == null) sharedPlusMesh = BuildPlusMesh();

            float size = Mathf.Max(0.05f, summonerData.healerMarkerSize);
            var rootGo = new GameObject("HealerMarker");
            healerMarkerRoot = rootGo.transform;
            healerMarkerRoot.SetParent(transform, false);
            healerMarkerBaseHeight = ComputeModelTopLocal() + summonerData.healerMarkerHeight + size * 0.5f;
            healerMarkerRoot.localPosition = new Vector3(0f, healerMarkerBaseHeight, 0f);
            markerBobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            healerMarkerMaterialInUse = mat;

            // Loose upward zig-zag (reads as a small triangle), with per-instance jitter and tilt
            // so the pluses look scattered rather than perfectly aligned.
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                var plusGo = new GameObject("Plus", typeof(MeshFilter), typeof(MeshRenderer));
                plusGo.transform.SetParent(healerMarkerRoot, false);
                float plusSize = size * Mathf.Lerp(1f, 0.7f, count > 1 ? i / (float)(count - 1) : 0f);
                float side = (i % 2 == 0 ? -1f : 1f) * (i == 0 ? 0.35f : 0.55f);
                float jitterX = UnityEngine.Random.Range(-0.15f, 0.15f) * size;
                float jitterY = UnityEngine.Random.Range(-0.12f, 0.12f) * size;
                plusGo.transform.localPosition = new Vector3(side * size + jitterX, y + jitterY, 0f);
                plusGo.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-14f, 14f));
                plusGo.transform.localScale = Vector3.one * plusSize;
                y += plusSize * 1.15f;
                plusGo.GetComponent<MeshFilter>().sharedMesh = sharedPlusMesh;
                var mr = plusGo.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            healerMarkerRoot.localScale = Vector3.zero;
            rootGo.SetActive(false);
        }

        private void UpdateHealerMarker()
        {
            if (healerMarkerRoot == null || summonerData == null) return;

            // Pop in right before the heal, then float up and shrink away right after it.
            bool wantVisible = Time.time >= markerShowFrom && Time.time < markerShowUntil;
            float fade = Mathf.Max(0.01f, wantVisible ? summonerData.healerMarkerFadeTime : summonerData.healerMarkerFadeOutTime);
            markerVisibility = Mathf.MoveTowards(markerVisibility, wantVisible ? 1f : 0f, Time.deltaTime / fade);

            GameObject markerGo = healerMarkerRoot.gameObject;
            if (markerVisibility <= 0f)
            {
                if (markerGo.activeSelf) markerGo.SetActive(false);
                return;
            }
            if (!markerGo.activeSelf) markerGo.SetActive(true);

            // Face the camera so the pluses always read as "+" from the top-down view.
            Camera cam = Camera.main;
            if (cam != null) healerMarkerRoot.rotation = cam.transform.rotation;

            float t = markerVisibility;
            float scale;
            float rise = 0f;
            if (wantVisible)
            {
                // Grow in: ease-out-back (slight overshoot).
                scale = Mathf.Max(0f, 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f));
            }
            else
            {
                // Disappear: shrink smoothly while drifting upward.
                scale = t * t * (3f - 2f * t);
                float riseWorld = summonerData.healerMarkerRiseDistance;
                rise = (1f - t) * riseWorld / Mathf.Max(0.01f, transform.lossyScale.y);
            }
            healerMarkerRoot.localScale = new Vector3(scale, scale, scale);
            Vector3 p = healerMarkerRoot.localPosition;
            p.y = healerMarkerBaseHeight + rise + Mathf.Sin(Time.time * 3.2f + markerBobPhase) * 0.05f;
            healerMarkerRoot.localPosition = p;
        }

        private float ComputeModelTopLocal()
        {
            float topWorld = transform.position.y + 1.8f;
            bool found = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (healerMarkerRoot != null && r.transform.IsChildOf(healerMarkerRoot)) continue;
                float maxY = r.bounds.max.y;
                if (!found || maxY > topWorld) topWorld = maxY;
                found = true;
            }
            float scaleY = Mathf.Max(0.01f, transform.lossyScale.y);
            return (topWorld - transform.position.y) / scaleY;
        }

        private static Mesh BuildPlusMesh()
        {
            var vertices = new System.Collections.Generic.List<Vector3>(48);
            var normals = new System.Collections.Generic.List<Vector3>(48);
            var triangles = new System.Collections.Generic.List<int>(72);
            const float thick = 0.34f;
            AddBox(vertices, normals, triangles, new Vector3(1f, thick, thick));  // horizontal bar
            AddBox(vertices, normals, triangles, new Vector3(thick, 1f, thick));  // vertical bar

            var mesh = new Mesh { name = "HealerPlus" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(
            System.Collections.Generic.List<Vector3> v,
            System.Collections.Generic.List<Vector3> n,
            System.Collections.Generic.List<int> t,
            Vector3 size)
        {
            Vector3 h = size * 0.5f;
            AddFace(v, n, t, Vector3.forward, new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z));
            AddFace(v, n, t, Vector3.back, new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z));
            AddFace(v, n, t, Vector3.right, new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z));
            AddFace(v, n, t, Vector3.left, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z));
            AddFace(v, n, t, Vector3.up, new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z));
            AddFace(v, n, t, Vector3.down, new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z));
        }

        private static void AddFace(
            System.Collections.Generic.List<Vector3> v,
            System.Collections.Generic.List<Vector3> n,
            System.Collections.Generic.List<int> t,
            Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            // Unity renders a triangle's front side where Cross(b - a, c - a) points; wind to match the normal.
            bool abcFacesNormal = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f;
            if (abcFacesNormal)
            {
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
                t.Add(start); t.Add(start + 2); t.Add(start + 3);
            }
            else
            {
                t.Add(start); t.Add(start + 2); t.Add(start + 1);
                t.Add(start); t.Add(start + 3); t.Add(start + 2);
            }
        }

        protected override void UpdateMovement()
        {
            if (summonerData == null)
            {
                isMoving = true;
                base.UpdateMovement();
                return;
            }

            if (castPoseEndTime > 0f && Time.time < castPoseEndTime)
            {
                isMoving = false;
                return;
            }

            Vector3 toHero = target.position - transform.position;
            toHero.y = 0f;
            float distSq = toHero.sqrMagnitude;
            float preferred = Mathf.Max(2f, summonerData.preferredDistance);
            float retreat = Mathf.Max(1f, summonerData.retreatDistance);

            if (distSq > (preferred + 0.5f) * (preferred + 0.5f))
            {
                isMoving = true;
                base.UpdateMovement();
            }
            else if (distSq < retreat * retreat && distSq > 0.001f)
            {
                Vector3 awayDir = -toHero.normalized;
                if (ObstacleMask == 0 || !Physics.SphereCast(
                    transform.position + Vector3.up * 0.5f,
                    0.4f,
                    awayDir,
                    out _,
                    0.85f,
                    ObstacleMask,
                    QueryTriggerInteraction.Ignore))
                {
                    isMoving = true;
                    transform.position += awayDir * (EffectiveMoveSpeed * 0.85f * Time.deltaTime);
                }
                transform.rotation = Quaternion.LookRotation(toHero.normalized);
            }
            else if (distSq > 0.001f)
            {
                isMoving = false;
                transform.rotation = Quaternion.LookRotation(toHero.normalized);
            }
        }

        protected override void UpdateAttack()
        {
            base.UpdateAttack();
            if (summonerData == null) return;

            // Telegraph: the healer glows green (ramping up) right before each pulse.
            float telegraph = Mathf.Max(0f, summonerData.healTelegraphDuration);
            if (!telegraphStarted && telegraph > 0f && summonerData.healPercent > 0f && Time.time >= nextPulseTime - telegraph)
            {
                telegraphStarted = true;
                PlayHealGlow(Mathf.Max(0.05f, nextPulseTime - Time.time), rampUp: true);
            }

            // Healer pluses: appear right before the heal, linger briefly after it.
            float lead = Mathf.Max(0f, summonerData.healerMarkerLeadTime);
            if (!markerScheduled && summonerData.healPercent > 0f && Time.time >= nextPulseTime - lead)
            {
                markerScheduled = true;
                markerShowFrom = nextPulseTime - lead;
                markerShowUntil = nextPulseTime + Mathf.Max(0f, summonerData.healerMarkerHoldAfterPulse);
            }

            if (Time.time >= nextPulseTime)
            {
                ExecuteSummonAndHealPulse();
                nextPulseTime = Time.time + Mathf.Max(2f, summonerData.summonInterval);
                telegraphStarted = false;
                markerScheduled = false;
            }
        }

        public int ExecuteSummonAndHealPulse()
        {
            if (!isAlive || summonerData == null) return 0;

            int summoned = 0;
            int count = Mathf.Max(1, summonerData.summonCount);
            if (summonCallback != null)
            {
                float radius = 2.0f;
                float step = (Mathf.PI * 2f) / count;
                for (int i = 0; i < count; i++)
                {
                    float angle = step * i + UnityEngine.Random.Range(-0.25f, 0.25f);
                    Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    pos.y = 0f;
                    summonCallback.Invoke(summonerData.minionData, pos);
                    summoned++;
                }
            }

            int healedAllies = 0;
            float healRadius = Mathf.Max(1f, summonerData.healRadius);
            float healPct = Mathf.Clamp01(summonerData.healPercent);
            if (healPct > 0f)
            {
                int found = Physics.OverlapSphereNonAlloc(transform.position, healRadius, AllyHealBuffer, EnemyMask);
                for (int i = 0; i < found; i++)
                {
                    Collider col = AllyHealBuffer[i];
                    if (col == null) continue;

                    if (!col.TryGetComponent<EnemyBase>(out var ally))
                    {
                        ally = col.GetComponentInParent<EnemyBase>();
                    }
                    if (ally == null || ally == this || ally.IsDead) continue;

                    bool isBossTarget = ally.IsBoss || ally.IsMiniBoss;
                    float pct = isBossTarget ? healPct * Mathf.Clamp01(summonerData.bossHealMultiplier) : healPct;
                    if (pct <= 0f) continue;

                    int healAmount = Mathf.Max(1, Mathf.RoundToInt(ally.MaxHp * pct));
                    int healed = ally.Heal(healAmount);
                    if (healed > 0)
                    {
                        healedAllies++;
                        eventBus?.Publish(new EnemyHealedEvent(transform.position, ally.transform.position, healed, isBossTarget));
                    }
                }
            }

            castPoseEndTime = Time.time + castPoseDuration;
            if (meshAnimator != null && meshAnimator.IsPlaying)
            {
                meshAnimator.Pause();
            }
            if (bodyMeshFilter != null && castMesh != null)
            {
                bodyMeshFilter.sharedMesh = castMesh;
            }

            eventBus?.Publish(new EnemySummonHealEvent(transform.position, healRadius, healedAllies));
            return summoned + healedAllies;
        }

        private void UpdateAnimationState()
        {
            if (meshAnimator == null) return;

            if (castPoseEndTime > 0f)
            {
                if (Time.time < castPoseEndTime)
                {
                    if (meshAnimator.IsPlaying) meshAnimator.Pause();
                    if (bodyMeshFilter != null && castMesh != null && bodyMeshFilter.sharedMesh != castMesh)
                    {
                        bodyMeshFilter.sharedMesh = castMesh;
                    }
                    return;
                }
                castPoseEndTime = 0f;
            }

            if (isMoving)
            {
                meshAnimator.Play();
            }
            else if (meshAnimator.IsPlaying || (meshAnimator.IdleMesh != null && bodyMeshFilter != null && bodyMeshFilter.sharedMesh != meshAnimator.IdleMesh))
            {
                meshAnimator.Stop();
            }
        }

        protected override void Die()
        {
            castPoseEndTime = 0f;
            isMoving = false;
            meshAnimator?.Stop();
            base.Die();
        }

        public override void Despawn()
        {
            castPoseEndTime = 0f;
            isMoving = false;
            meshAnimator?.Stop();
            base.Despawn();
        }
    }
}
