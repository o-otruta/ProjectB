using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Visuals;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Wave 75 Boss: Frost Lich — alternates between telegraphed Cross/Star Ice Spikes
    /// and a Blizzard Ring with a safe 3m inner eye around the boss.
    /// </summary>
    public class FrostLichBoss : BossBase
    {
        public enum FrostLichState
        {
            Idle = 0,
            TelegraphingSpikes = 1,
            ShowingSpikes = 2,
            TelegraphingBlizzard = 3,
            ActiveBlizzard = 4
        }

        [Header("Voxel Visuals")]
        [SerializeField] private VoxelMeshAnimator meshAnimator;
        [SerializeField] private MeshFilter bodyMeshFilter;
        [SerializeField] private Mesh castMesh;

        [Header("Frost Effect Materials")]
        [SerializeField] private Material frostTelegraphMaterial;
        [SerializeField] private Material frostSpikeMaterial;
        [SerializeField] private Material blizzardRingMaterial;

        private const int MAX_SPIKE_LINES = 12;
        private const int PILLARS_PER_RAY = 3;
        private const int BLIZZARD_SWIRL_CUBES = 16;
        private const float SPIKE_SHOW_DURATION = 0.45f;

        private FrostLichState state;
        private float stateTimer;
        private float stateDuration;
        private float nextSpikeCooldown;
        private float nextBlizzardCooldown;
        private float blizzardTickTimer;
        private Vector3 spikeOrigin;
        private readonly Vector3[] spikeRayDirections = new Vector3[MAX_SPIKE_LINES];
        private int activeSpikeRayCount;
        private ISlowable targetSlowable;
        private bool isMoving;

        private LineRenderer[] spikeTelegraphLines;
        private Transform spikeBurstRoot;
        private Transform[] spikePillars;
        private Transform blizzardRoot;
        private MeshFilter blizzardDonutMeshFilter;
        private MeshRenderer blizzardDonutMeshRenderer;
        private Mesh blizzardDonutMesh;
        private LineRenderer safeEyeBorderLine;
        private Transform blizzardSwirlRoot;
        private float builtInnerRadius = -1f;
        private float builtOuterRadius = -1f;

        public FrostLichState CurrentState => state;
        public int ActiveSpikeRayCount => activeSpikeRayCount;
        public Vector3[] SpikeRayDirections => spikeRayDirections;
        public Vector3 SpikeOrigin => spikeOrigin;

        public override void InitializeBoss(
            BossData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            EnemyProjectilePool projectilePool,
            System.Action<EnemyData, Vector3> onSummonMinion,
            System.Func<Vector3, float, float, Vector3> safePositionProvider,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.InitializeBoss(data, heroTarget, enemyPool, bus, projectilePool, onSummonMinion, safePositionProvider, difficultyMultiplier, damageDifficultyMultiplier);

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

            targetSlowable = heroTarget != null ? heroTarget.GetComponent<ISlowable>() : null;
            state = FrostLichState.Idle;
            stateTimer = 0f;
            stateDuration = 0f;
            activeSpikeRayCount = 0;
            blizzardTickTimer = 0f;
            isMoving = false;
            nextSpikeCooldown = data != null ? Mathf.Max(1.5f, data.spikeCooldown * 0.45f) : 2.5f;
            nextBlizzardCooldown = data != null ? Mathf.Max(3f, data.blizzardCooldown * 0.65f) : 6.0f;

            meshAnimator?.Stop();
            EnsureSpikeVisuals();
            EnsureBlizzardVisuals();
            HideAllFrostVisuals();
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
        }

        protected override void UpdateMovement()
        {
            if (state != FrostLichState.Idle || bossData == null)
            {
                if (state == FrostLichState.Idle)
                {
                    isMoving = true;
                    base.UpdateMovement();
                }
                return;
            }

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            float stopDist = Mathf.Max(2f, bossData.frostStopDistance);

            if (direction.sqrMagnitude > stopDist * stopDist)
            {
                isMoving = true;
                base.UpdateMovement();
            }
            else
            {
                isMoving = false;
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized);
                }
            }
        }

        protected override void UpdateAttack()
        {
            base.UpdateAttack();
            if (bossData == null) return;
            TickBossAbilities(Time.deltaTime);
        }

        public void TickBossAbilities(float deltaTime)
        {
            if (!isAlive || bossData == null || deltaTime < 0f) return;

            switch (state)
            {
                case FrostLichState.Idle:
                    if (nextSpikeCooldown > 0f) nextSpikeCooldown -= deltaTime;
                    if (nextBlizzardCooldown > 0f) nextBlizzardCooldown -= deltaTime;

                    if (target != null && !IsTargetDead())
                    {
                        if (nextSpikeCooldown <= 0f)
                        {
                            BeginIceSpikesTelegraph();
                        }
                        else if (nextBlizzardCooldown <= 0f)
                        {
                            BeginBlizzardTelegraph();
                        }
                    }
                    break;

                case FrostLichState.TelegraphingSpikes:
                    stateTimer -= deltaTime;
                    UpdateSpikeTelegraphVisuals();
                    if (stateTimer <= 0f)
                    {
                        ExecuteIceSpikes();
                    }
                    break;

                case FrostLichState.ShowingSpikes:
                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        SetSpikeBurstActive(false);
                        state = FrostLichState.Idle;
                        nextSpikeCooldown = Mathf.Max(2f, bossData.spikeCooldown);
                    }
                    break;

                case FrostLichState.TelegraphingBlizzard:
                    stateTimer -= deltaTime;
                    UpdateBlizzardVisualPosition();
                    if (stateTimer <= 0f)
                    {
                        BeginActiveBlizzard();
                    }
                    break;

                case FrostLichState.ActiveBlizzard:
                    UpdateBlizzardVisualPosition();
                    if (blizzardSwirlRoot != null)
                    {
                        blizzardSwirlRoot.Rotate(Vector3.up, 150f * deltaTime, Space.Self);
                    }

                    blizzardTickTimer -= deltaTime;
                    if (blizzardTickTimer <= 0f)
                    {
                        TickBlizzardDamage();
                        blizzardTickTimer = Mathf.Max(0.15f, bossData.blizzardTickInterval);
                    }

                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        SetBlizzardActive(false, swirlActive: false);
                        state = FrostLichState.Idle;
                        nextBlizzardCooldown = Mathf.Max(3f, bossData.blizzardCooldown);
                    }
                    break;
            }
        }

        public void BeginIceSpikesTelegraph()
        {
            if (!isAlive || bossData == null) return;

            state = FrostLichState.TelegraphingSpikes;
            stateDuration = Mathf.Max(0.25f, bossData.spikeTelegraphDuration);
            stateTimer = stateDuration;

            spikeOrigin = transform.position;
            spikeOrigin.y = 0f;

            Vector3 forwardDir = target != null ? target.position - spikeOrigin : transform.forward;
            forwardDir.y = 0f;
            float baseAngle = forwardDir.sqrMagnitude > 0.001f
                ? Mathf.Atan2(forwardDir.z, forwardDir.x)
                : 0f;

            if (forwardDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(forwardDir.normalized);
            }

            activeSpikeRayCount = Mathf.Clamp(bossData.spikeLineCount, 4, MAX_SPIKE_LINES);
            float step = (Mathf.PI * 2f) / activeSpikeRayCount;
            for (int i = 0; i < activeSpikeRayCount; i++)
            {
                float angle = baseAngle + step * i;
                spikeRayDirections[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            EnsureSpikeVisuals();
            UpdateSpikeTelegraphVisuals();
        }

        public bool ExecuteIceSpikes()
        {
            if (!isAlive || bossData == null) return false;

            SetSpikeTelegraphsActive(false);
            ShowSpikeBurstPillars();

            float rayLength = Mathf.Max(2f, bossData.spikeLineLength);
            float halfWidth = Mathf.Max(0.25f, bossData.spikeLineHalfWidth);
            eventBus?.Publish(new FrostLichNovaEvent(spikeOrigin, rayLength, isBlizzard: false));

            bool hitHero = false;
            if (target != null && !IsTargetDead())
            {
                if (IsPointHitBySpikeRays(spikeOrigin, target.position, spikeRayDirections, activeSpikeRayCount, rayLength, halfWidth))
                {
                    if (targetDamageable == null) targetDamageable = target.GetComponent<IDamageable>();
                    if (targetSlowable == null) targetSlowable = target.GetComponent<ISlowable>();

                    int dmg = Mathf.Max(1, Mathf.RoundToInt(bossData.spikeDamage * currentDamageMultiplier));
                    targetDamageable?.TakeDamage(dmg);
                    targetSlowable?.ApplySlow(bossData.spikeSlowFactor, bossData.spikeSlowDuration);
                    hitHero = true;
                }
            }

            state = FrostLichState.ShowingSpikes;
            stateTimer = SPIKE_SHOW_DURATION;
            return hitHero;
        }

        public void BeginBlizzardTelegraph()
        {
            if (!isAlive || bossData == null) return;

            state = FrostLichState.TelegraphingBlizzard;
            stateDuration = Mathf.Max(0.3f, bossData.blizzardTelegraphDuration);
            stateTimer = stateDuration;

            EnsureBlizzardVisuals();
            SetBlizzardActive(true, swirlActive: false);
            UpdateBlizzardVisualPosition();
        }

        private void BeginActiveBlizzard()
        {
            if (!isAlive || bossData == null) return;

            state = FrostLichState.ActiveBlizzard;
            stateDuration = Mathf.Max(0.5f, bossData.blizzardActiveDuration);
            stateTimer = stateDuration;
            blizzardTickTimer = 0f;

            EnsureBlizzardVisuals();
            SetBlizzardActive(true, swirlActive: true);
            UpdateBlizzardVisualPosition();
            eventBus?.Publish(new FrostLichNovaEvent(transform.position, bossData.blizzardOuterRadius, isBlizzard: true));
        }

        public bool TickBlizzardDamage()
        {
            if (!isAlive || bossData == null || target == null || IsTargetDead()) return false;

            float inner = Mathf.Max(0.5f, bossData.blizzardSafeInnerRadius);
            float outer = Mathf.Max(inner + 0.5f, bossData.blizzardOuterRadius);
            if (!IsPointInBlizzardRing(transform.position, target.position, inner, outer))
            {
                return false;
            }

            if (targetDamageable == null) targetDamageable = target.GetComponent<IDamageable>();
            if (targetSlowable == null) targetSlowable = target.GetComponent<ISlowable>();

            int dmg = Mathf.Max(1, Mathf.RoundToInt(bossData.blizzardTickDamage * currentDamageMultiplier));
            targetDamageable?.TakeDamage(dmg);
            targetSlowable?.ApplySlow(bossData.blizzardSlowFactor, Mathf.Max(0.6f, bossData.blizzardTickInterval * 1.4f));
            return true;
        }

        public static bool IsPointHitBySpikeRays(
            Vector3 origin,
            Vector3 targetPos,
            Vector3[] rayDirs,
            int rayCount,
            float rayLength,
            float halfWidth)
        {
            if (rayDirs == null || rayCount <= 0 || rayLength <= 0f || halfWidth <= 0f) return false;

            Vector2 toTarget = new Vector2(targetPos.x - origin.x, targetPos.z - origin.z);
            float halfWidthSq = halfWidth * halfWidth;

            for (int i = 0; i < rayCount && i < rayDirs.Length; i++)
            {
                Vector2 dir = new Vector2(rayDirs[i].x, rayDirs[i].z);
                float lenSq = dir.sqrMagnitude;
                if (lenSq <= 0.00001f) continue;
                dir /= Mathf.Sqrt(lenSq);

                float projection = Vector2.Dot(toTarget, dir);
                if (projection < 0f || projection > rayLength) continue;

                Vector2 closest = dir * projection;
                if ((toTarget - closest).sqrMagnitude <= halfWidthSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsPointInBlizzardRing(Vector3 center, Vector3 targetPos, float innerRadius, float outerRadius)
        {
            float dx = targetPos.x - center.x;
            float dz = targetPos.z - center.z;
            float distSq = dx * dx + dz * dz;
            return distSq >= innerRadius * innerRadius && distSq <= outerRadius * outerRadius;
        }

        private void EnsureSpikeVisuals()
        {
            if (spikeTelegraphLines == null)
            {
                spikeTelegraphLines = new LineRenderer[MAX_SPIKE_LINES];
                for (int i = 0; i < MAX_SPIKE_LINES; i++)
                {
                    var lineGo = new GameObject($"IceSpikeTelegraph_{i}");
                    lineGo.transform.SetParent(transform, false);
                    lineGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    lineGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    var lr = lineGo.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.alignment = LineAlignment.TransformZ;
                    lr.positionCount = 2;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.startColor = new Color(0.45f, 0.92f, 1f, 0.65f);
                    lr.endColor = new Color(0.75f, 0.98f, 1f, 0.35f);
                    if (frostTelegraphMaterial != null) lr.sharedMaterial = frostTelegraphMaterial;
                    lr.enabled = false;
                    spikeTelegraphLines[i] = lr;
                }
            }

            if (spikeBurstRoot == null)
            {
                var burstGo = new GameObject("IceSpikeBurstVisual");
                burstGo.transform.SetParent(transform, false);
                spikeBurstRoot = burstGo.transform;

                int totalPillars = MAX_SPIKE_LINES * PILLARS_PER_RAY;
                spikePillars = new Transform[totalPillars];
                for (int i = 0; i < totalPillars; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"IceSpike_{i}";
                    cube.transform.SetParent(spikeBurstRoot, false);
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
                        if (frostSpikeMaterial != null) mr.sharedMaterial = frostSpikeMaterial;
                    }
                    spikePillars[i] = cube.transform;
                }

                spikeBurstRoot.gameObject.SetActive(false);
            }
        }

        private void UpdateSpikeTelegraphVisuals()
        {
            if (spikeTelegraphLines == null || bossData == null) return;

            float progress = stateDuration > 0.001f ? Mathf.Clamp01(1f - (stateTimer / stateDuration)) : 1f;
            float targetWidth = Mathf.Max(0.4f, bossData.spikeLineHalfWidth * 2f);
            float width = Mathf.Lerp(0.3f, targetWidth, progress);
            float rayLength = Mathf.Max(2f, bossData.spikeLineLength);

            for (int i = 0; i < spikeTelegraphLines.Length; i++)
            {
                LineRenderer lr = spikeTelegraphLines[i];
                if (lr == null) continue;

                bool active = i < activeSpikeRayCount;
                lr.enabled = active;
                if (!active) continue;

                Vector3 start = new Vector3(spikeOrigin.x, 0.12f, spikeOrigin.z);
                Vector3 end = start + spikeRayDirections[i] * rayLength;
                lr.startWidth = width;
                lr.endWidth = width * 0.85f;
                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
            }
        }

        private void ShowSpikeBurstPillars()
        {
            EnsureSpikeVisuals();
            if (spikeBurstRoot == null || spikePillars == null || bossData == null) return;

            float rayLength = Mathf.Max(2f, bossData.spikeLineLength);
            spikeBurstRoot.position = Vector3.zero;
            spikeBurstRoot.gameObject.SetActive(true);

            int pillarIndex = 0;
            for (int r = 0; r < MAX_SPIKE_LINES; r++)
            {
                bool activeRay = r < activeSpikeRayCount;
                Vector3 dir = spikeRayDirections[r];
                Quaternion rot = activeRay && dir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(dir)
                    : Quaternion.identity;

                for (int p = 0; p < PILLARS_PER_RAY; p++)
                {
                    if (pillarIndex >= spikePillars.Length) break;
                    Transform pillar = spikePillars[pillarIndex++];
                    if (pillar == null) continue;

                    pillar.gameObject.SetActive(activeRay);
                    if (!activeRay) continue;

                    float distFrac = (p + 1f) / (PILLARS_PER_RAY + 0.35f);
                    float dist = rayLength * distFrac;
                    float height = Mathf.Lerp(1.8f, 1.1f, (float)p / PILLARS_PER_RAY);
                    pillar.position = spikeOrigin + dir * dist + Vector3.up * (height * 0.5f);
                    pillar.rotation = rot;
                    pillar.localScale = new Vector3(0.45f, height, 0.65f);
                }
            }
        }

        private void EnsureBlizzardVisuals()
        {
            if (bossData == null) return;
            float inner = Mathf.Max(0.5f, bossData.blizzardSafeInnerRadius);
            float outer = Mathf.Max(inner + 0.5f, bossData.blizzardOuterRadius);

            if (blizzardRoot == null)
            {
                var rootGo = new GameObject("BlizzardRingVisual");
                rootGo.transform.SetParent(transform, false);
                blizzardRoot = rootGo.transform;

                var donutGo = new GameObject("BlizzardDonutMesh", typeof(MeshFilter), typeof(MeshRenderer));
                donutGo.transform.SetParent(blizzardRoot, false);
                donutGo.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                blizzardDonutMeshFilter = donutGo.GetComponent<MeshFilter>();
                blizzardDonutMeshRenderer = donutGo.GetComponent<MeshRenderer>();
                blizzardDonutMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                blizzardDonutMeshRenderer.receiveShadows = false;
                if (blizzardRingMaterial != null)
                {
                    blizzardDonutMeshRenderer.sharedMaterial = blizzardRingMaterial;
                }

                var borderGo = new GameObject("SafeEyeBorder");
                borderGo.transform.SetParent(blizzardRoot, false);
                borderGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                borderGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                safeEyeBorderLine = borderGo.AddComponent<LineRenderer>();
                safeEyeBorderLine.useWorldSpace = false;
                safeEyeBorderLine.loop = true;
                safeEyeBorderLine.alignment = LineAlignment.TransformZ;
                safeEyeBorderLine.startWidth = 0.2f;
                safeEyeBorderLine.endWidth = 0.2f;
                safeEyeBorderLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                safeEyeBorderLine.receiveShadows = false;
                safeEyeBorderLine.startColor = new Color(0.85f, 0.98f, 1f, 0.9f);
                safeEyeBorderLine.endColor = new Color(0.85f, 0.98f, 1f, 0.9f);
                if (frostTelegraphMaterial != null)
                {
                    safeEyeBorderLine.sharedMaterial = frostTelegraphMaterial;
                }

                var swirlGo = new GameObject("BlizzardSwirlCubes");
                swirlGo.transform.SetParent(blizzardRoot, false);
                blizzardSwirlRoot = swirlGo.transform;
                for (int i = 0; i < BLIZZARD_SWIRL_CUBES; i++)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = $"FrostShard_{i}";
                    cube.transform.SetParent(blizzardSwirlRoot, false);
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
                        if (frostSpikeMaterial != null) mr.sharedMaterial = frostSpikeMaterial;
                    }
                }

                blizzardRoot.gameObject.SetActive(false);
            }

            if (Mathf.Abs(builtInnerRadius - inner) > 0.01f || Mathf.Abs(builtOuterRadius - outer) > 0.01f)
            {
                RebuildBlizzardGeometry(inner, outer);
            }
        }

        private void RebuildBlizzardGeometry(float inner, float outer)
        {
            builtInnerRadius = inner;
            builtOuterRadius = outer;

            const int segments = 32;
            if (blizzardDonutMesh == null)
            {
                blizzardDonutMesh = new Mesh { name = "BlizzardDonut" };
            }
            blizzardDonutMesh.Clear();

            var verts = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f / segments) * i;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts[i * 2] = new Vector3(cos * inner, 0f, sin * inner);
                verts[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
                float u = (float)i / segments;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);

                int next = (i + 1) % segments;
                int triIdx = i * 6;
                int v0 = i * 2;
                int v1 = i * 2 + 1;
                int v2 = next * 2;
                int v3 = next * 2 + 1;

                // Clockwise in XZ looking from +Y down so top-face normals point +Y
                tris[triIdx] = v0;
                tris[triIdx + 1] = v2;
                tris[triIdx + 2] = v3;
                tris[triIdx + 3] = v0;
                tris[triIdx + 4] = v3;
                tris[triIdx + 5] = v1;
            }

            blizzardDonutMesh.vertices = verts;
            blizzardDonutMesh.uv = uvs;
            blizzardDonutMesh.triangles = tris;
            blizzardDonutMesh.RecalculateNormals();
            if (blizzardDonutMeshFilter != null)
            {
                blizzardDonutMeshFilter.sharedMesh = blizzardDonutMesh;
            }

            if (safeEyeBorderLine != null)
            {
                safeEyeBorderLine.positionCount = segments;
                for (int i = 0; i < segments; i++)
                {
                    float angle = (Mathf.PI * 2f / segments) * i;
                    safeEyeBorderLine.SetPosition(i, new Vector3(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner, 0f));
                }
            }

            if (blizzardSwirlRoot != null)
            {
                int childCount = blizzardSwirlRoot.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    float angle = (Mathf.PI * 2f / Mathf.Max(1, childCount)) * i;
                    float frac = (i % 3) / 2f;
                    float r = Mathf.Lerp(inner + 0.6f, outer - 0.5f, frac);
                    Transform shard = blizzardSwirlRoot.GetChild(i);
                    shard.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.55f + (i % 2) * 0.35f, Mathf.Sin(angle) * r);
                    shard.localRotation = Quaternion.Euler(20f * (i % 3), -angle * Mathf.Rad2Deg, 15f);
                    shard.localScale = new Vector3(0.32f, 0.55f, 0.32f);
                }
            }
        }

        private void UpdateBlizzardVisualPosition()
        {
            if (blizzardRoot == null) return;
            blizzardRoot.position = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        private void SetSpikeTelegraphsActive(bool active)
        {
            if (spikeTelegraphLines == null) return;
            for (int i = 0; i < spikeTelegraphLines.Length; i++)
            {
                if (spikeTelegraphLines[i] != null)
                {
                    spikeTelegraphLines[i].enabled = active;
                }
            }
        }

        private void SetSpikeBurstActive(bool active)
        {
            if (spikeBurstRoot != null)
            {
                spikeBurstRoot.gameObject.SetActive(active);
            }
        }

        private void SetBlizzardActive(bool active, bool swirlActive)
        {
            if (blizzardRoot != null)
            {
                blizzardRoot.gameObject.SetActive(active);
            }
            if (blizzardSwirlRoot != null)
            {
                blizzardSwirlRoot.gameObject.SetActive(active && swirlActive);
            }
        }

        private void HideAllFrostVisuals()
        {
            SetSpikeTelegraphsActive(false);
            SetSpikeBurstActive(false);
            SetBlizzardActive(false, swirlActive: false);
        }

        private void UpdateAnimationState()
        {
            if (meshAnimator == null) return;

            bool isCasting = state == FrostLichState.TelegraphingSpikes
                || state == FrostLichState.ShowingSpikes
                || state == FrostLichState.TelegraphingBlizzard
                || state == FrostLichState.ActiveBlizzard;

            if (isCasting)
            {
                if (meshAnimator.IsPlaying)
                {
                    meshAnimator.Pause();
                }
                if (bodyMeshFilter != null && castMesh != null && bodyMeshFilter.sharedMesh != castMesh)
                {
                    bodyMeshFilter.sharedMesh = castMesh;
                }
                return;
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
            state = FrostLichState.Idle;
            isMoving = false;
            HideAllFrostVisuals();
            meshAnimator?.Stop();
            base.Die();
        }

        public override void Despawn()
        {
            state = FrostLichState.Idle;
            isMoving = false;
            HideAllFrostVisuals();
            meshAnimator?.Stop();
            base.Despawn();
        }
    }
}
