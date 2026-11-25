using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Boss 1: Stone Golem — slow juggernaut with telegraphed AoE ground slam and slime summons.
    /// </summary>
    public class GolemBoss : BossBase
    {
        [SerializeField] private Transform slamTelegraphVisual;

        [Header("Voxel Visuals")]
        [SerializeField] private MeshFilter bodyMeshFilter;
        [SerializeField] private Mesh idleMesh;
        [SerializeField] private Mesh slamWindupMesh;
        [SerializeField] private Mesh slamImpactMesh;
        [SerializeField] private float slamImpactDuration = 0.3f;

        private bool isTelegraphingSlam;
        private float slamExecuteTime;
        private float slamStartTime;
        private float nextSlamTime;
        private float slamImpactResetTime;

        // Прыжок к герою (анти-кайт)
        private enum LeapPhase { None, Windup, Air }
        private LeapPhase leapPhase = LeapPhase.None;
        private float leapPhaseEndTime;
        private float nextLeapTime;
        private Vector3 leapStartPos;
        private Vector3 leapLandingPos;

        public bool IsTelegraphingSlam => isTelegraphingSlam;
        public bool IsLeaping => leapPhase != LeapPhase.None;
        public bool IsLeapAirborne => leapPhase == LeapPhase.Air;
        public Vector3 LeapLandingPosition => leapLandingPos;
        public float LeapImpactRadius => bossData != null ? bossData.leapImpactRadius : 0f;
        public float SlamRadius => bossData != null ? bossData.slamRadius : 0f;

        protected override Color GetBossRimColor() => new Color(1.0f, 0.68f, 0.16f, 0.9f);

        private void SetBodyMesh(Mesh mesh)
        {
            if (bodyMeshFilter != null && mesh != null)
            {
                bodyMeshFilter.sharedMesh = mesh;
            }
        }

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

            isTelegraphingSlam = false;
            slamImpactResetTime = 0f;
            nextSlamTime = Time.time + (data != null ? data.slamCooldown * 0.5f : 3f);
            leapPhase = LeapPhase.None;
            nextLeapTime = Time.time + (data != null ? Mathf.Max(2f, data.leapCooldown * 0.5f) : 3.5f);
            SetBodyMesh(idleMesh);

            if (slamTelegraphVisual != null)
            {
                slamTelegraphVisual.gameObject.SetActive(false);
            }
        }

        protected override void UpdateMovement()
        {
            // Pause movement while winding up the AoE slam so the player can dodge out of the telegraph zone
            if (isTelegraphingSlam) return;
            if (leapPhase == LeapPhase.Windup) return;
            if (leapPhase == LeapPhase.Air)
            {
                float air = Mathf.Max(0.05f, bossData != null ? bossData.leapAirDuration : 0.6f);
                float t = Mathf.Clamp01(1f - (leapPhaseEndTime - Time.time) / air);
                transform.position = Vector3.Lerp(leapStartPos, leapLandingPos, t);
                return;
            }
            base.UpdateMovement();
        }

        protected override void UpdateAttack()
        {
            base.UpdateAttack();

            if (!isTelegraphingSlam && slamImpactResetTime > 0f && Time.time >= slamImpactResetTime)
            {
                slamImpactResetTime = 0f;
                SetBodyMesh(idleMesh);
            }

            if (bossData == null || target == null) return;

            if (isTelegraphingSlam)
            {
                UpdateSlamTelegraph();
                return;
            }

            if (leapPhase != LeapPhase.None)
            {
                UpdateLeap();
                return;
            }

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float distSq = diff.sqrMagnitude;

            // Прыжок, если герой убежал дальше leapMinDistance
            if (bossData.leapCooldown > 0f && Time.time >= nextLeapTime
                && distSq >= bossData.leapMinDistance * bossData.leapMinDistance)
            {
                BeginLeap();
                return;
            }

            if (Time.time < nextSlamTime) return;

            float triggerRange = bossData.slamTriggerRange;
            if (distSq <= triggerRange * triggerRange)
            {
                BeginSlamTelegraph();
            }
        }

        private void BeginLeap()
        {
            leapPhase = LeapPhase.Windup;
            leapPhaseEndTime = Time.time + Mathf.Max(0.15f, bossData.leapWindupDuration);
            SetBodyMesh(slamWindupMesh);
            if (slamTelegraphVisual != null)
            {
                slamTelegraphVisual.gameObject.SetActive(true);
                UpdateTelegraphScaleForRadius(bossData.leapImpactRadius, 0.35f);
            }
        }

        private void UpdateLeap()
        {
            if (leapPhase == LeapPhase.Windup)
            {
                float windup = Mathf.Max(0.15f, bossData.leapWindupDuration);
                float progress = Mathf.Clamp01(1f - (leapPhaseEndTime - Time.time) / windup);
                UpdateTelegraphScaleForRadius(bossData.leapImpactRadius, Mathf.Lerp(0.35f, 0.7f, progress));
                if (Time.time < leapPhaseEndTime) return;

                // Точка приземления: позиция героя с упреждением на время полёта, не дальше leapMaxDistance
                float air = Mathf.Max(0.05f, bossData.leapAirDuration);
                Vector3 predicted = target.position + TargetVelocityXZ * (air * bossData.leapLeadFactor);
                Vector3 offset = predicted - transform.position;
                offset.y = 0f;
                float maxDist = Mathf.Max(1f, bossData.leapMaxDistance);
                if (offset.sqrMagnitude > maxDist * maxDist) offset = offset.normalized * maxDist;
                Vector3 landing = transform.position + offset;
                if (getSafePositionNearTarget != null) landing = getSafePositionNearTarget(landing, 0f, 1.5f);
                landing.y = transform.position.y;

                leapStartPos = transform.position;
                leapLandingPos = landing;
                leapPhase = LeapPhase.Air;
                leapPhaseEndTime = Time.time + air;
                return;
            }

            if (leapPhase == LeapPhase.Air)
            {
                float air = Mathf.Max(0.05f, bossData.leapAirDuration);
                float progress = Mathf.Clamp01(1f - (leapPhaseEndTime - Time.time) / air);
                UpdateTelegraphScaleForRadius(bossData.leapImpactRadius, Mathf.Lerp(0.7f, 1f, progress));
                if (Time.time < leapPhaseEndTime) return;
                ExecuteLeapLanding();
            }
        }

        private void ExecuteLeapLanding()
        {
            transform.position = leapLandingPos;
            leapPhase = LeapPhase.None;
            float cdMult = IsTimedEnraged ? 0.6f : 1f;
            nextLeapTime = Time.time + Mathf.Max(2f, bossData.leapCooldown * cdMult);
            // После приземления — пауза перед обычным ударом, чтобы не было двойного урона подряд
            nextSlamTime = Mathf.Max(nextSlamTime, Time.time + 1.2f);
            SetBodyMesh(slamImpactMesh);
            slamImpactResetTime = Time.time + slamImpactDuration;
            if (slamTelegraphVisual != null) slamTelegraphVisual.gameObject.SetActive(false);

            SpawnHeroSlowZone(leapLandingPos, bossData.rubbleZoneRadius, bossData.rubbleZoneDuration, bossData.rubbleSlowFactor);
            DealAreaDamageToHero(leapLandingPos, bossData.leapImpactRadius, bossData.leapDamage, "StoneGolem_Leap");
        }

        private void DealAreaDamageToHero(Vector3 center, float radius, int baseDamage, string sourceId)
        {
            if (target == null || targetDamageable == null || targetDamageable.IsDead) return;
            Vector3 diff = target.position - center;
            diff.y = 0f;
            if (diff.sqrMagnitude > radius * radius) return;

            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * currentDamageMultiplier));
            if (targetDamageable is ProjectB.Player.HeroHealth heroHealth)
            {
                heroHealth.TakeDamage(finalDamage, sourceId);
            }
            else
            {
                targetDamageable.TakeDamage(finalDamage);
            }
        }

        private void UpdateTelegraphScaleForRadius(float radius, float normalized)
        {
            if (slamTelegraphVisual == null) return;
            float diameter = radius * 2f * normalized;
            float invX = transform.localScale.x > 0.001f ? 1f / transform.localScale.x : 1f;
            float invZ = transform.localScale.z > 0.001f ? 1f / transform.localScale.z : 1f;
            slamTelegraphVisual.localScale = new Vector3(diameter * invX, 0.05f, diameter * invZ);
        }

        private void BeginSlamTelegraph()
        {
            isTelegraphingSlam = true;
            slamImpactResetTime = 0f;
            slamStartTime = Time.time;
            slamExecuteTime = Time.time + Mathf.Max(0.25f, bossData.slamTelegraphDuration);
            SetBodyMesh(slamWindupMesh);

            if (slamTelegraphVisual != null)
            {
                slamTelegraphVisual.gameObject.SetActive(true);
                UpdateTelegraphScale(0.25f);
            }
        }

        private void UpdateSlamTelegraph()
        {
            float duration = Mathf.Max(0.01f, slamExecuteTime - slamStartTime);
            float progress = Mathf.Clamp01((Time.time - slamStartTime) / duration);
            UpdateTelegraphScale(Mathf.Lerp(0.3f, 1f, progress));

            if (Time.time >= slamExecuteTime)
            {
                ExecuteSlam();
            }
        }

        private void UpdateTelegraphScale(float normalizedRadius)
        {
            if (slamTelegraphVisual == null || bossData == null) return;

            float diameter = bossData.slamRadius * 2f * normalizedRadius;
            // Counteract root localScale so slamRadius is exact in world meters
            float invX = transform.localScale.x > 0.001f ? 1f / transform.localScale.x : 1f;
            float invZ = transform.localScale.z > 0.001f ? 1f / transform.localScale.z : 1f;
            slamTelegraphVisual.localScale = new Vector3(diameter * invX, 0.05f, diameter * invZ);
        }

        private void ExecuteSlam()
        {
            isTelegraphingSlam = false;
            nextSlamTime = Time.time + Mathf.Max(1.5f, bossData.slamCooldown);
            SetBodyMesh(slamImpactMesh);
            slamImpactResetTime = Time.time + slamImpactDuration;

            if (slamTelegraphVisual != null)
            {
                slamTelegraphVisual.gameObject.SetActive(false);
            }

            SpawnHeroSlowZone(transform.position, bossData.rubbleZoneRadius, bossData.rubbleZoneDuration, bossData.rubbleSlowFactor);

            if (target == null || targetDamageable == null || targetDamageable.IsDead) return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float radius = bossData.slamRadius;

            if (diff.sqrMagnitude <= radius * radius)
            {
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(bossData.slamDamage * currentDamageMultiplier));
                if (targetDamageable is ProjectB.Player.HeroHealth heroHealth)
                {
                    heroHealth.TakeDamage(finalDamage, "StoneGolem_Slam");
                }
                else
                {
                    targetDamageable.TakeDamage(finalDamage);
                }
            }
        }

        protected override void Die()
        {
            isTelegraphingSlam = false;
            leapPhase = LeapPhase.None;
            slamImpactResetTime = 0f;
            SetBodyMesh(idleMesh);
            if (slamTelegraphVisual != null)
            {
                slamTelegraphVisual.gameObject.SetActive(false);
            }
            base.Die();
        }
    }
}
