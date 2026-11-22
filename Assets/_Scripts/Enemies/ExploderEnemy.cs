using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Suicide exploder enemy: stops near the hero, telegraphs a shrinking/growing danger ring,
    /// and detonates for AoE damage against both the hero and nearby enemies.
    /// Killing it before the fuse finishes cancels the explosion cleanly.
    /// </summary>
    public class ExploderEnemy : EnemyBase
    {
        [SerializeField] private Transform explosionTelegraphVisual;

        private static readonly Color ArmingRedColor = new Color(1f, 0.18f, 0.12f, 1f);
        private static readonly Color ArmingFlashColor = new Color(1f, 0.9f, 0.85f, 1f);
        private static readonly Collider[] DetonateHitBuffer = new Collider[48];

        private ExploderEnemyData exploderData;
        private bool isArming;
        private float fuseElapsed;
        private bool hasDetonated;
        private readonly EnemyBase[] friendlyFireTargets = new EnemyBase[32];

        public bool IsArming => isArming;
        public float FuseProgress => exploderData != null && exploderData.fuseDuration > 0.001f
            ? Mathf.Clamp01(fuseElapsed / exploderData.fuseDuration)
            : 0f;

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);

            exploderData = data as ExploderEnemyData;
            isArming = false;
            fuseElapsed = 0f;
            hasDetonated = false;

            if (explosionTelegraphVisual != null)
            {
                explosionTelegraphVisual.gameObject.SetActive(false);
            }
        }

        protected override void RestoreBaseTint()
        {
            if (isArming)
            {
                ApplyTint(GetArmingTint());
                return;
            }
            base.RestoreBaseTint();
        }

        protected override void UpdateMovement()
        {
            if (isArming) return;
            base.UpdateMovement();
        }

        protected override void UpdateAttack()
        {
            if (exploderData == null)
            {
                base.UpdateAttack();
                return;
            }

            if (!isArming)
            {
                if (target == null) return;
                Vector3 diff = target.position - transform.position;
                diff.y = 0f;
                float triggerDist = Mathf.Max(0.4f, exploderData.triggerDistance);
                if (diff.sqrMagnitude <= triggerDist * triggerDist)
                {
                    BeginArming();
                }
                return;
            }

            TickFuse(Time.deltaTime);
        }

        public void BeginArming()
        {
            if (!isAlive || isArming || hasDetonated) return;
            isArming = true;
            fuseElapsed = 0f;

            if (explosionTelegraphVisual != null)
            {
                explosionTelegraphVisual.gameObject.SetActive(true);
                UpdateTelegraphScale(0.25f);
            }

            ApplyTint(ArmingRedColor);
        }

        public void TickFuse(float deltaTime)
        {
            if (!isAlive || !isArming || hasDetonated || exploderData == null) return;

            fuseElapsed += Mathf.Max(0f, deltaTime);
            float duration = Mathf.Max(0.05f, exploderData.fuseDuration);
            float progress = Mathf.Clamp01(fuseElapsed / duration);

            UpdateTelegraphScale(Mathf.Lerp(0.25f, 1f, progress));
            transform.localScale = baseLocalScale * activeScaleMultiplier * Mathf.Lerp(1f, 1.24f, progress);
            ApplyTint(GetArmingTint());

            if (fuseElapsed >= duration)
            {
                Detonate();
            }
        }

        public int Detonate()
        {
            if (!isAlive || hasDetonated || exploderData == null) return 0;

            hasDetonated = true;
            isArming = false;

            if (explosionTelegraphVisual != null)
            {
                explosionTelegraphVisual.gameObject.SetActive(false);
            }

            float radius = Mathf.Max(0.5f, exploderData.explosionRadius);
            float radiusSq = radius * radius;
            int baseExplosionDamage = Mathf.Max(1, Mathf.RoundToInt(exploderData.explosionDamage * currentDamageMultiplier));
            int damagedCount = 0;

            if (target != null && targetDamageable != null && !targetDamageable.IsDead)
            {
                Vector3 heroDiff = target.position - transform.position;
                heroDiff.y = 0f;
                if (heroDiff.sqrMagnitude <= radiusSq)
                {
                    targetDamageable.TakeDamage(baseExplosionDamage);
                    damagedCount++;
                }
            }

            if (exploderData.friendlyFireMultiplier > 0f)
            {
                int found = Physics.OverlapSphereNonAlloc(transform.position, radius, DetonateHitBuffer, EnemyMask);
                int targetCount = 0;
                for (int i = 0; i < found && targetCount < friendlyFireTargets.Length; i++)
                {
                    Collider col = DetonateHitBuffer[i];
                    if (col == null) continue;

                    EnemyBase ally = col.GetComponent<EnemyBase>();
                    if (ally == null) ally = col.GetComponentInParent<EnemyBase>();
                    if (ally == null || ally == this || ally.IsDead) continue;

                    Vector3 allyDiff = ally.transform.position - transform.position;
                    allyDiff.y = 0f;
                    if (allyDiff.sqrMagnitude > radiusSq) continue;

                    friendlyFireTargets[targetCount++] = ally;
                }

                int friendlyDamage = Mathf.Max(1, Mathf.RoundToInt(baseExplosionDamage * exploderData.friendlyFireMultiplier));
                for (int i = 0; i < targetCount; i++)
                {
                    EnemyBase ally = friendlyFireTargets[i];
                    friendlyFireTargets[i] = null;
                    if (ally != null && !ally.IsDead)
                    {
                        ally.TakeDamage(friendlyDamage);
                        damagedCount++;
                    }
                }
            }

            eventBus?.Publish(new EnemyExplodedEvent(transform.position, radius));
            Die();
            return damagedCount;
        }

        private Color GetArmingTint()
        {
            return (((int)(fuseElapsed * 14f)) & 1) == 0 ? ArmingRedColor : ArmingFlashColor;
        }

        private void UpdateTelegraphScale(float normalizedRadius)
        {
            if (explosionTelegraphVisual == null || exploderData == null) return;

            float diameter = exploderData.explosionRadius * 2f * normalizedRadius;
            float invX = transform.localScale.x > 0.001f ? 1f / transform.localScale.x : 1f;
            float invZ = transform.localScale.z > 0.001f ? 1f / transform.localScale.z : 1f;
            explosionTelegraphVisual.localScale = new Vector3(diameter * invX, 0.05f, diameter * invZ);
        }

        protected override void Die()
        {
            isArming = false;
            if (explosionTelegraphVisual != null)
            {
                explosionTelegraphVisual.gameObject.SetActive(false);
            }
            base.Die();
        }

        public override void Despawn()
        {
            isArming = false;
            if (explosionTelegraphVisual != null)
            {
                explosionTelegraphVisual.gameObject.SetActive(false);
            }
            base.Despawn();
        }
    }
}
