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
    /// and heals nearby allied enemies for 15% of their MaxHP on each pulse.
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

            if (Time.time >= nextPulseTime)
            {
                ExecuteSummonAndHealPulse();
                nextPulseTime = Time.time + Mathf.Max(2f, summonerData.summonInterval);
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

                    int healAmount = Mathf.Max(1, Mathf.RoundToInt(ally.MaxHp * healPct));
                    if (ally.Heal(healAmount) > 0)
                    {
                        healedAllies++;
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
