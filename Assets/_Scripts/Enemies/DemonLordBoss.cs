using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Visuals;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Wave 100 Boss: Demon Lord — 2-phase encounter.
    /// Phase 1: Telegraphed Fire Charge + 8-fireball 360 ring.
    /// Phase 2 (&lt;50% HP ENRAGE): Crimson glow, +35% speed, 12-fireball ring, and 5 telegraphed meteor impact zones.
    /// </summary>
    public class DemonLordBoss : BossBase
    {
        public enum DemonLordState
        {
            Idle = 0,
            WindingUpCharge = 1,
            Charging = 2,
            RecoveringCharge = 3,
            CastingFireRing = 4,
            TelegraphingMeteors = 5,
            ShowingMeteors = 6
        }

        private const int MAX_METEOR_ZONES = 8;
        private static readonly Color Phase1RimColor = new Color(1.00f, 0.36f, 0.12f, 0.92f);
        private static readonly Color EnragedRimColor = new Color(1.00f, 0.08f, 0.08f, 1.00f);
        private static readonly Color EnragedBaseTint = new Color(1.00f, 0.82f, 0.80f, 1.00f);

        [Header("Voxel Visuals")]
        [SerializeField] private VoxelMeshAnimator meshAnimator;
        [SerializeField] private MeshFilter bodyMeshFilter;
        [SerializeField] private Mesh chargePoseMesh;
        [SerializeField] private Mesh castMesh;

        [Header("Demon Attack Telegraph Materials")]
        [SerializeField] private Material chargeTelegraphMaterial;
        [SerializeField] private Material meteorTelegraphMaterial;

        private DemonLordState state;
        private float stateTimer;
        private float chargeCooldownRemaining;
        private float fireballCooldownRemaining;
        private float meteorCooldownRemaining;

        private Vector3 chargeDirection;
        private float plannedChargeDistance;
        private float chargeDistanceTravelled;
        private bool hitHeroThisCharge;

        private LineRenderer chargeTelegraphLine;
        private Transform meteorTelegraphRoot;
        private Transform[] meteorTelegraphDiscs;
        private readonly Vector3[] meteorZonePositions = new Vector3[MAX_METEOR_ZONES];
        private int activeMeteorZoneCount;
        private bool isMoving;

        public DemonLordState CurrentState => state;
        public int CurrentPhase { get; private set; } = 1;
        public bool IsEnraged { get; private set; }
        public Vector3 LockedChargeDirection => chargeDirection;
        public int ActiveMeteorZoneCount => activeMeteorZoneCount;
        public int LastFireballRingCount { get; private set; }

        public Vector3 GetMeteorZonePosition(int index)
        {
            if (index < 0 || index >= activeMeteorZoneCount) return Vector3.zero;
            return meteorZonePositions[index];
        }

        protected override float GetAdditionalSpeedMultiplier()
        {
            if (IsEnraged && bossData != null)
            {
                return Mathf.Max(1f, bossData.demonEnrageSpeedMultiplier);
            }
            return 1f;
        }

        protected override Color GetBossRimColor() => IsEnraged ? EnragedRimColor : Phase1RimColor;
        protected override Color GetBossBaseTint() => IsEnraged ? EnragedBaseTint : Color.white;

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

            CurrentPhase = 1;
            IsEnraged = false;
            activeScaleMultiplier = 1f;
            transform.localScale = baseLocalScale;

            state = DemonLordState.Idle;
            stateTimer = 0f;
            chargeDirection = transform.forward;
            plannedChargeDistance = 0f;
            chargeDistanceTravelled = 0f;
            hitHeroThisCharge = false;
            activeMeteorZoneCount = 0;
            LastFireballRingCount = 0;
            isMoving = false;

            chargeCooldownRemaining = data != null ? Mathf.Max(1.5f, data.demonChargeCooldown * 0.45f) : 2.5f;
            fireballCooldownRemaining = data != null ? Mathf.Max(1.5f, data.demonFireballCooldown * 0.65f) : 3.0f;
            meteorCooldownRemaining = data != null ? Mathf.Max(2.0f, data.demonMeteorCooldown * 0.5f) : 3.5f;

            meshAnimator?.Stop();
            EnsureVisuals();
            HideAllTelegraphs();
            ApplyVisualState(0f);
        }

        protected override void OnDamageApplied(int finalAmount, bool isCrit, bool wasBlocked)
        {
            base.OnDamageApplied(finalAmount, isCrit, wasBlocked);
            CheckEnrageThreshold();
        }

        public bool CheckEnrageThreshold()
        {
            if (IsEnraged || !isAlive || maxHp <= 0) return false;
            float threshold = bossData != null ? Mathf.Clamp(bossData.demonEnrageHpThreshold, 0.1f, 0.9f) : 0.5f;
            if (currentHp > 0 && currentHp <= Mathf.CeilToInt(maxHp * threshold))
            {
                EnterEnrage();
                return true;
            }
            return false;
        }

        private void EnterEnrage()
        {
            if (IsEnraged) return;
            IsEnraged = true;
            CurrentPhase = 2;

            CaptureBaseScale();
            activeScaleMultiplier = 1.12f;
            transform.localScale = baseLocalScale * activeScaleMultiplier;
            meteorCooldownRemaining = Mathf.Min(meteorCooldownRemaining, 1.2f);
            ApplyVisualState(hitFlashTimerFallback());
            eventBus?.Publish(new DemonLordEnragedEvent(transform.position));
        }

        private float hitFlashTimerFallback() => IsHitFlashing ? 1f : 0f;

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
            if (state != DemonLordState.Idle) return;
            isMoving = true;
            base.UpdateMovement();
        }

        protected override void UpdateAttack()
        {
            if (state != DemonLordState.Charging)
            {
                base.UpdateAttack();
            }

            if (bossData == null) return;
            TickBossAbilities(Time.deltaTime);
        }

        public void TickBossAbilities(float deltaTime)
        {
            if (!isAlive || bossData == null || deltaTime < 0f) return;

            CheckEnrageThreshold();

            switch (state)
            {
                case DemonLordState.Idle:
                    if (chargeCooldownRemaining > 0f) chargeCooldownRemaining -= deltaTime;
                    if (fireballCooldownRemaining > 0f) fireballCooldownRemaining -= deltaTime;
                    if (IsEnraged && meteorCooldownRemaining > 0f) meteorCooldownRemaining -= deltaTime;

                    if (target != null && !IsTargetDead())
                    {
                        if (IsEnraged && meteorCooldownRemaining <= 0f)
                        {
                            BeginMeteorBarrageTelegraph();
                        }
                        else if (fireballCooldownRemaining <= 0f)
                        {
                            ExecuteFireballRing();
                        }
                        else if (chargeCooldownRemaining <= 0f)
                        {
                            Vector3 diff = target.position - transform.position;
                            diff.y = 0f;
                            float triggerRange = Mathf.Max(2f, bossData.demonChargeTriggerRange);
                            if (diff.sqrMagnitude <= triggerRange * triggerRange)
                            {
                                BeginFireChargeWindup();
                            }
                        }
                    }
                    break;

                case DemonLordState.WindingUpCharge:
                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        BeginFireCharge();
                    }
                    break;

                case DemonLordState.Charging:
                    StepFireCharge(deltaTime);
                    break;

                case DemonLordState.RecoveringCharge:
                case DemonLordState.CastingFireRing:
                case DemonLordState.ShowingMeteors:
                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        state = DemonLordState.Idle;
                    }
                    break;

                case DemonLordState.TelegraphingMeteors:
                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        ExecuteMeteorBarrage();
                    }
                    break;
            }
        }

        public void BeginFireChargeWindup()
        {
            if (!isAlive || bossData == null) return;

            state = DemonLordState.WindingUpCharge;
            stateTimer = Mathf.Max(0.2f, bossData.demonChargeWindupDuration);

            Vector3 dir = target != null ? target.position - transform.position : transform.forward;
            dir.y = 0f;
            chargeDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(chargeDirection);

            float maxDistance = Mathf.Max(2f, bossData.demonChargeDistance);
            plannedChargeDistance = maxDistance;
            if (ObstacleMask != 0 && Physics.SphereCast(
                transform.position + Vector3.up * 0.6f,
                0.55f,
                chargeDirection,
                out RaycastHit hit,
                maxDistance,
                ObstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                plannedChargeDistance = Mathf.Max(0.5f, hit.distance - 0.35f);
            }

            SetPoseMesh(chargePoseMesh);
            ShowChargeTelegraph(transform.position, transform.position + chargeDirection * plannedChargeDistance);
        }

        public void BeginFireCharge()
        {
            if (!isAlive) return;
            state = DemonLordState.Charging;
            chargeDistanceTravelled = 0f;
            hitHeroThisCharge = false;
            SetChargeTelegraphActive(false);
        }

        private void StepFireCharge(float deltaTime)
        {
            float baseSpeed = bossData != null ? bossData.demonChargeSpeed : 13f;
            float speed = baseSpeed * slowFactor * externalHasteMultiplier * GlobalEventSpeedMultiplier * GetAdditionalSpeedMultiplier();
            float requestedStep = Mathf.Max(0f, speed * deltaTime);
            float remaining = Mathf.Max(0f, plannedChargeDistance - chargeDistanceTravelled);
            float step = Mathf.Min(requestedStep, remaining);

            bool hitObstacle = false;
            if (step > 0.0001f && ObstacleMask != 0 && Physics.SphereCast(
                transform.position + Vector3.up * 0.6f,
                0.55f,
                chargeDirection,
                out RaycastHit obsHit,
                step + 0.45f,
                ObstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                step = Mathf.Min(step, Mathf.Max(0f, obsHit.distance - 0.35f));
                hitObstacle = true;
            }

            if (step > 0.0001f)
            {
                transform.position += chargeDirection * step;
                chargeDistanceTravelled += step;
            }

            TryDealChargeHit();

            if (hitObstacle || chargeDistanceTravelled >= plannedChargeDistance - 0.001f)
            {
                SetChargeTelegraphActive(false);
                state = DemonLordState.RecoveringCharge;
                stateTimer = 0.4f;
                chargeCooldownRemaining = bossData != null ? Mathf.Max(2f, bossData.demonChargeCooldown) : 5.5f;
            }
        }

        private void TryDealChargeHit()
        {
            if (hitHeroThisCharge || bossData == null || target == null || targetDamageable == null || targetDamageable.IsDead) return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float hitRadius = Mathf.Max(0.5f, bossData.demonChargeHitRadius);
            if (diff.sqrMagnitude <= hitRadius * hitRadius)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(bossData.demonChargeDamage * currentDamageMultiplier));
                targetDamageable.TakeDamage(dmg);
                hitHeroThisCharge = true;
            }
        }

        public int ExecuteFireballRing()
        {
            if (!isAlive || bossData == null) return 0;

            int count = IsEnraged
                ? Mathf.Max(1, bossData.demonPhase2FireballCount)
                : Mathf.Max(1, bossData.demonPhase1FireballCount);
            LastFireballRingCount = count;

            if (sharedProjectilePool != null && bossData.demonFireballWeaponData != null)
            {
                float step = (Mathf.PI * 2f) / count;
                float shootHeight = target != null ? target.position.y : 0.7f;
                int finalDmg = Mathf.Max(1, Mathf.RoundToInt(bossData.demonFireballWeaponData.damage * currentDamageMultiplier));

                for (int i = 0; i < count; i++)
                {
                    float angle = step * i;
                    Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 spawnPos = new Vector3(transform.position.x, shootHeight, transform.position.z) + dir * 0.9f;
                    sharedProjectilePool.Spawn(
                        bossData.demonFireballWeaponData,
                        spawnPos,
                        dir,
                        finalDmg,
                        target,
                        targetDamageable);
                }
            }

            SetPoseMesh(castMesh);
            state = DemonLordState.CastingFireRing;
            stateTimer = 0.35f;
            fireballCooldownRemaining = Mathf.Max(1.5f, bossData.demonFireballCooldown);
            return count;
        }

        public int BeginMeteorBarrageTelegraph()
        {
            if (!isAlive || bossData == null) return 0;

            EnsureVisuals();
            int count = Mathf.Clamp(bossData.demonMeteorCount, 1, MAX_METEOR_ZONES);
            activeMeteorZoneCount = count;

            Vector3 center = target != null ? target.position : transform.position;
            center.y = 0f;

            meteorZonePositions[0] = center;
            float angleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float ringStep = count > 1 ? (Mathf.PI * 2f) / (count - 1) : 0f;

            for (int i = 1; i < count; i++)
            {
                float angle = angleOffset + ringStep * (i - 1) + UnityEngine.Random.Range(-0.22f, 0.22f);
                float radiusDist = UnityEngine.Random.Range(3.6f, 7.2f);
                Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radiusDist;
                pos.y = 0f;
                meteorZonePositions[i] = pos;
            }

            UpdateMeteorTelegraphVisuals(true);
            SetPoseMesh(castMesh);
            state = DemonLordState.TelegraphingMeteors;
            stateTimer = Mathf.Max(0.3f, bossData.demonMeteorTelegraphDuration);
            return count;
        }

        public bool ExecuteMeteorBarrage()
        {
            if (!isAlive || bossData == null) return false;

            float radius = Mathf.Max(1f, bossData.demonMeteorRadius);
            float radiusSq = radius * radius;
            bool hitHero = false;

            for (int i = 0; i < activeMeteorZoneCount; i++)
            {
                Vector3 zonePos = meteorZonePositions[i];
                eventBus?.Publish(new MeteorImpactEvent(zonePos, radius));

                if (!hitHero && target != null && !IsTargetDead())
                {
                    float dx = target.position.x - zonePos.x;
                    float dz = target.position.z - zonePos.z;
                    if (dx * dx + dz * dz <= radiusSq)
                    {
                        if (targetDamageable == null) targetDamageable = target.GetComponent<IDamageable>();
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(bossData.demonMeteorDamage * currentDamageMultiplier));
                        targetDamageable?.TakeDamage(dmg);
                        hitHero = true;
                    }
                }
            }

            UpdateMeteorTelegraphVisuals(false);
            activeMeteorZoneCount = 0;
            state = DemonLordState.ShowingMeteors;
            stateTimer = 0.25f;
            meteorCooldownRemaining = Mathf.Max(2.5f, bossData.demonMeteorCooldown);
            return hitHero;
        }

        private void EnsureVisuals()
        {
            if (chargeTelegraphLine == null)
            {
                var lineGo = new GameObject("DemonChargeTelegraph");
                lineGo.transform.SetParent(transform, false);
                lineGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                lineGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                chargeTelegraphLine = lineGo.AddComponent<LineRenderer>();
                chargeTelegraphLine.useWorldSpace = true;
                chargeTelegraphLine.alignment = LineAlignment.TransformZ;
                chargeTelegraphLine.positionCount = 2;
                chargeTelegraphLine.startWidth = 1.45f;
                chargeTelegraphLine.endWidth = 1.45f;
                chargeTelegraphLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                chargeTelegraphLine.receiveShadows = false;
                chargeTelegraphLine.startColor = new Color(1f, 0.34f, 0.08f, 0.65f);
                chargeTelegraphLine.endColor = new Color(1f, 0.14f, 0.08f, 0.45f);
                if (chargeTelegraphMaterial != null) chargeTelegraphLine.sharedMaterial = chargeTelegraphMaterial;
                chargeTelegraphLine.enabled = false;
            }

            if (meteorTelegraphRoot == null)
            {
                var rootGo = new GameObject("DemonMeteorTelegraphs");
                rootGo.transform.SetParent(transform, false);
                meteorTelegraphRoot = rootGo.transform;

                meteorTelegraphDiscs = new Transform[MAX_METEOR_ZONES];
                for (int i = 0; i < MAX_METEOR_ZONES; i++)
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    disc.name = $"DemonMeteorTelegraph_{i}";
                    disc.transform.SetParent(meteorTelegraphRoot, false);
                    var col = disc.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                    var mr = disc.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        if (meteorTelegraphMaterial != null) mr.sharedMaterial = meteorTelegraphMaterial;
                    }
                    disc.SetActive(false);
                    meteorTelegraphDiscs[i] = disc.transform;
                }
            }
        }

        private void ShowChargeTelegraph(Vector3 start, Vector3 end)
        {
            EnsureVisuals();
            if (chargeTelegraphLine == null) return;
            start.y = 0.12f;
            end.y = 0.12f;
            chargeTelegraphLine.positionCount = 2;
            chargeTelegraphLine.SetPosition(0, start);
            chargeTelegraphLine.SetPosition(1, end);
            chargeTelegraphLine.enabled = true;
        }

        private void SetChargeTelegraphActive(bool active)
        {
            if (chargeTelegraphLine != null)
            {
                chargeTelegraphLine.enabled = active;
            }
        }

        private void UpdateMeteorTelegraphVisuals(bool active)
        {
            if (meteorTelegraphDiscs == null) return;

            float radius = bossData != null ? Mathf.Max(1f, bossData.demonMeteorRadius) : 2.6f;
            float invX = transform.localScale.x > 0.001f ? 1f / transform.localScale.x : 1f;
            float invZ = transform.localScale.z > 0.001f ? 1f / transform.localScale.z : 1f;
            float diameter = radius * 2f;

            for (int i = 0; i < meteorTelegraphDiscs.Length; i++)
            {
                Transform disc = meteorTelegraphDiscs[i];
                if (disc == null) continue;

                bool show = active && i < activeMeteorZoneCount;
                disc.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 worldPos = meteorZonePositions[i];
                    disc.position = new Vector3(worldPos.x, 0.06f, worldPos.z);
                    disc.rotation = Quaternion.identity;
                    disc.localScale = new Vector3(diameter * invX, 0.04f, diameter * invZ);
                }
            }
        }

        private void HideAllTelegraphs()
        {
            SetChargeTelegraphActive(false);
            UpdateMeteorTelegraphVisuals(false);
        }

        private void SetPoseMesh(Mesh poseMesh)
        {
            if (meshAnimator != null && meshAnimator.IsPlaying)
            {
                meshAnimator.Pause();
            }
            if (bodyMeshFilter != null && poseMesh != null)
            {
                bodyMeshFilter.sharedMesh = poseMesh;
            }
        }

        private void UpdateAnimationState()
        {
            if (meshAnimator == null) return;

            if (state == DemonLordState.WindingUpCharge || state == DemonLordState.Charging)
            {
                SetPoseMesh(chargePoseMesh);
                return;
            }
            if (state == DemonLordState.CastingFireRing || state == DemonLordState.TelegraphingMeteors)
            {
                SetPoseMesh(castMesh);
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

        public override void Teleport(Vector3 newPosition)
        {
            state = DemonLordState.Idle;
            stateTimer = 0f;
            HideAllTelegraphs();
            base.Teleport(newPosition);
        }

        protected override void Die()
        {
            state = DemonLordState.Idle;
            isMoving = false;
            HideAllTelegraphs();
            meshAnimator?.Stop();
            base.Die();
        }

        public override void Despawn()
        {
            state = DemonLordState.Idle;
            isMoving = false;
            HideAllTelegraphs();
            meshAnimator?.Stop();
            base.Despawn();
        }
    }
}
