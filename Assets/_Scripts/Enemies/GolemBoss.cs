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

            if (Time.time < nextSlamTime) return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float triggerRange = bossData.slamTriggerRange;

            if (diff.sqrMagnitude <= triggerRange * triggerRange)
            {
                BeginSlamTelegraph();
            }
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

            if (target == null || targetDamageable == null || targetDamageable.IsDead) return;

            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float radius = bossData.slamRadius;

            if (diff.sqrMagnitude <= radius * radius)
            {
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(bossData.slamDamage * currentDamageMultiplier));
                targetDamageable.TakeDamage(finalDamage);
            }
        }

        protected override void Die()
        {
            isTelegraphingSlam = false;
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
