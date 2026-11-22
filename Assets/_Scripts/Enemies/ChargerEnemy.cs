using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Visuals;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Heavy charger enemy: chases the hero, locks a straight line with a visible ground telegraph,
    /// then dashes rapidly forward while dealing single-hit charge damage and stopping on obstacles.
    /// </summary>
    public class ChargerEnemy : EnemyBase
    {
        public enum ChargeState
        {
            Chasing = 0,
            WindingUp = 1,
            Charging = 2,
            Recovering = 3
        }

        [Header("Charge Telegraph & Visuals")]
        [SerializeField] private LineRenderer chargeTelegraphLine;
        [SerializeField] private Material chargeTelegraphMaterial;
        [SerializeField] private VoxelMeshAnimator meshAnimator;
        [SerializeField] private MeshFilter bodyMeshFilter;
        [SerializeField] private Mesh chargePoseMesh;

        private ChargerEnemyData chargerData;
        private ChargeState state;
        private float stateTimer;
        private float cooldownRemaining;
        private Vector3 chargeDirection;
        private float plannedChargeDistance;
        private float chargeDistanceTravelled;
        private bool hitHeroThisCharge;

        public ChargeState CurrentState => state;
        public Vector3 LockedChargeDirection => chargeDirection;

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);

            chargerData = data as ChargerEnemyData;
            state = ChargeState.Chasing;
            stateTimer = 0f;
            cooldownRemaining = chargerData != null ? chargerData.chargeCooldown * 0.35f : 1.5f;
            chargeDirection = transform.forward;
            plannedChargeDistance = 0f;
            chargeDistanceTravelled = 0f;
            hitHeroThisCharge = false;

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
                Transform visuals = transform.Find("Visuals");
                if (visuals != null) bodyMeshFilter = visuals.GetComponent<MeshFilter>();
            }

            if (meshAnimator != null)
            {
                meshAnimator.Play();
            }

            EnsureTelegraphLine();
            SetTelegraphActive(false);
        }

        protected override void UpdateMovement()
        {
            if (state != ChargeState.Chasing) return;
            base.UpdateMovement();
        }

        protected override void UpdateAttack()
        {
            if (chargerData == null)
            {
                base.UpdateAttack();
                return;
            }

            if (state == ChargeState.Chasing)
            {
                base.UpdateAttack();
            }

            TickCharge(Time.deltaTime);
        }

        public void TickCharge(float deltaTime)
        {
            if (!isAlive || chargerData == null || deltaTime < 0f) return;

            switch (state)
            {
                case ChargeState.Chasing:
                    if (cooldownRemaining > 0f)
                    {
                        cooldownRemaining -= deltaTime;
                    }

                    if (cooldownRemaining <= 0f && target != null && !IsTargetDead())
                    {
                        Vector3 diff = target.position - transform.position;
                        diff.y = 0f;
                        float triggerRange = Mathf.Max(1f, chargerData.chargeTriggerRange);
                        if (diff.sqrMagnitude <= triggerRange * triggerRange)
                        {
                            BeginWindup();
                        }
                    }
                    break;

                case ChargeState.WindingUp:
                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        BeginCharge();
                    }
                    break;

                case ChargeState.Charging:
                    StepCharge(deltaTime);
                    break;

                case ChargeState.Recovering:
                    stateTimer -= deltaTime;
                    if (stateTimer <= 0f)
                    {
                        state = ChargeState.Chasing;
                        cooldownRemaining = Mathf.Max(0.5f, chargerData.chargeCooldown);
                    }
                    break;
            }
        }

        public void BeginWindup()
        {
            if (!isAlive || chargerData == null) return;

            state = ChargeState.WindingUp;
            stateTimer = Mathf.Max(0.1f, chargerData.chargeWindupDuration);

            Vector3 dir = target != null ? target.position - transform.position : transform.forward;
            dir.y = 0f;
            chargeDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(chargeDirection);

            float maxDistance = Mathf.Max(1f, chargerData.chargeDistance);
            plannedChargeDistance = maxDistance;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.45f, chargeDirection, out RaycastHit hit, maxDistance, ObstacleMask))
            {
                plannedChargeDistance = Mathf.Max(0.35f, hit.distance - 0.3f);
            }

            if (meshAnimator != null)
            {
                meshAnimator.Pause();
            }
            if (bodyMeshFilter != null && chargePoseMesh != null)
            {
                bodyMeshFilter.sharedMesh = chargePoseMesh;
            }

            ShowTelegraphLine(transform.position, transform.position + chargeDirection * plannedChargeDistance);
        }

        public void BeginCharge()
        {
            if (!isAlive) return;
            state = ChargeState.Charging;
            chargeDistanceTravelled = 0f;
            hitHeroThisCharge = false;
            SetTelegraphActive(false);
        }

        private void StepCharge(float deltaTime)
        {
            float chargeSpeed = chargerData.chargeSpeed * slowFactor * eliteSpeedMultiplier * externalHasteMultiplier;
            float requestedStep = Mathf.Max(0f, chargeSpeed * deltaTime);
            float remaining = Mathf.Max(0f, plannedChargeDistance - chargeDistanceTravelled);
            float step = Mathf.Min(requestedStep, remaining);

            bool hitObstacle = false;
            if (step > 0.0001f && Physics.SphereCast(
                transform.position + Vector3.up * 0.5f,
                0.45f,
                chargeDirection,
                out RaycastHit obsHit,
                step + 0.4f,
                ObstacleMask))
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
                EnterRecovery();
            }
        }

        private void TryDealChargeHit()
        {
            if (hitHeroThisCharge || target == null || targetDamageable == null || targetDamageable.IsDead) return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float hitRadius = Mathf.Max(0.4f, chargerData.chargeHitRadius);
            if (diff.sqrMagnitude <= hitRadius * hitRadius)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(chargerData.chargeDamage * currentDamageMultiplier));
                targetDamageable.TakeDamage(dmg);
                hitHeroThisCharge = true;
            }
        }

        private void EnterRecovery()
        {
            state = ChargeState.Recovering;
            stateTimer = Mathf.Max(0.05f, chargerData.recoveryDuration);
            SetTelegraphActive(false);
            if (meshAnimator != null)
            {
                meshAnimator.Play();
            }
        }

        private void EnsureTelegraphLine()
        {
            if (chargeTelegraphLine != null) return;

            var child = new GameObject("ChargeTelegraph");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            chargeTelegraphLine = child.AddComponent<LineRenderer>();
            chargeTelegraphLine.useWorldSpace = true;
            chargeTelegraphLine.alignment = LineAlignment.TransformZ;
            chargeTelegraphLine.positionCount = 2;
            chargeTelegraphLine.startWidth = 0.9f;
            chargeTelegraphLine.endWidth = 0.9f;
            chargeTelegraphLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chargeTelegraphLine.receiveShadows = false;
            chargeTelegraphLine.startColor = new Color(1f, 0.2f, 0.12f, 0.55f);
            chargeTelegraphLine.endColor = new Color(1f, 0.35f, 0.12f, 0.45f);
            if (chargeTelegraphMaterial != null)
            {
                chargeTelegraphLine.sharedMaterial = chargeTelegraphMaterial;
            }
            chargeTelegraphLine.enabled = false;
        }

        private void ShowTelegraphLine(Vector3 start, Vector3 end)
        {
            EnsureTelegraphLine();
            if (chargeTelegraphLine == null) return;

            start.y = 0.12f;
            end.y = 0.12f;
            chargeTelegraphLine.positionCount = 2;
            chargeTelegraphLine.SetPosition(0, start);
            chargeTelegraphLine.SetPosition(1, end);
            chargeTelegraphLine.enabled = true;
        }

        private void SetTelegraphActive(bool active)
        {
            if (chargeTelegraphLine != null)
            {
                chargeTelegraphLine.enabled = active;
            }
        }

        public override void Teleport(Vector3 newPosition)
        {
            state = ChargeState.Chasing;
            stateTimer = 0f;
            SetTelegraphActive(false);
            if (meshAnimator != null) meshAnimator.Play();
            base.Teleport(newPosition);
        }

        protected override void Die()
        {
            state = ChargeState.Chasing;
            SetTelegraphActive(false);
            if (meshAnimator != null) meshAnimator.Stop();
            base.Die();
        }

        public override void Despawn()
        {
            state = ChargeState.Chasing;
            SetTelegraphActive(false);
            if (meshAnimator != null) meshAnimator.Stop();
            base.Despawn();
        }
    }
}
