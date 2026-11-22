using System.Collections.Generic;
using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class FireballProjectile : AbilityProjectile
    {
        private static readonly Collider[] explosionHits = new Collider[64];
        private static readonly Collider[] steamHits = new Collider[64];
        private static int cachedObstacleMask = -1;

        private const float STEAM_BLAST_RADIUS = 4.2f;
        private const float STEAM_BLAST_DAMAGE_MULT = 1.8f;
        private const float STEAM_BLAST_KNOCKBACK = 2.6f;
        private const float TOXIC_INFERNO_DETONATION_MULT = 1.6f;

        private float explosionRadius;
        private Vector3 lastKnownTargetPos;
        private GameEventBus eventBus;
        private HeroAbilities heroAbilities;

        private static int ObstacleMask
        {
            get
            {
                if (cachedObstacleMask == -1)
                {
                    cachedObstacleMask = LayerMask.GetMask("Obstacles");
                }
                return cachedObstacleMask;
            }
        }

        public void InitializeFireball(
            float damage,
            float speed,
            float explosionRadius,
            Transform target,
            LayerMask enemyLayer,
            UnityEngine.Pool.IObjectPool<AbilityProjectile> pool,
            GameEventBus eventBus = null,
            HeroRuntimeStats runtimeStats = null,
            HeroAbilities heroAbilities = null,
            RunStatistics runStatistics = null,
            string sourceId = "Fireball",
            float visualScale = 1f)
        {
            base.Initialize(damage, speed, target, enemyLayer, pool, runtimeStats);
            ConfigureCombatContext(runStatistics, sourceId, canOverchargeFromTurrets: false, visualScale: visualScale);
            this.explosionRadius = explosionRadius;
            this.lastKnownTargetPos = target != null ? target.position : transform.position;
            this.eventBus = eventBus;
            this.heroAbilities = heroAbilities;
        }

        public void DetonateAtCurrentPosition()
        {
            HitTarget();
        }

        protected override void Update()
        {
            if (isReturned || Time.deltaTime <= 0f) return;

            if (Time.time > spawnTime + MAX_LIFETIME)
            {
                ReturnToPool();
                return;
            }

            // Если цель жива — обновляем точку взрыва; если умерла в полёте — долетаем до последней позиции и взрываемся
            if (target != null && target.gameObject.activeInHierarchy && (targetDamageable == null || !targetDamageable.IsDead))
            {
                lastKnownTargetPos = target.position;
            }

            Vector3 toTarget = lastKnownTargetPos - transform.position;
            float distanceThisFrame = speed * Time.deltaTime;

            if (toTarget.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
            {
                transform.position = lastKnownTargetPos;
                HitTarget();
                return;
            }

            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * distanceThisFrame;
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        protected override void HitTarget()
        {
            bool hasSteamBlast = heroAbilities != null && heroAbilities.HasSynergy(SynergyIds.SteamBlast);
            bool hasToxicInferno = heroAbilities != null && heroAbilities.HasSynergy(SynergyIds.ToxicInferno);
            bool anySlowedHit = false;

            int hits = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, explosionHits, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                Collider col = explosionHits[i];
                if (col != null && col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    EnemyBase enemy = damageable as EnemyBase;
                    if (enemy != null && enemy.IsSlowed)
                    {
                        anySlowedHit = true;
                    }

                    int finalDamage = RollHitDamage(damage, out bool isCrit);
                    DealDamage(damageable, finalDamage, isCrit);
                    if (enemy != null && !string.IsNullOrEmpty(sourceId))
                    {
                        runStatistics?.RecordDamage(sourceId, finalDamage);
                    }
                }
            }

            if (hasToxicInferno)
            {
                IgniteNearbyPoisonPuddles();
            }

            if (hasSteamBlast && anySlowedHit)
            {
                TriggerSteamBlastNova();
            }

            eventBus?.Publish(new FireballExplodedEvent(transform.position, explosionRadius));
            ReturnToPool();
        }

        private void IgniteNearbyPoisonPuddles()
        {
            IReadOnlyList<PoisonPuddle> puddles = PoisonPuddle.ActivePuddles;
            if (puddles == null || puddles.Count == 0) return;

            float detonationDmg = damage * TOXIC_INFERNO_DETONATION_MULT;
            Vector3 center = transform.position;

            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                PoisonPuddle puddle = puddles[i];
                if (puddle == null || puddle.IsIgnited) continue;

                Vector3 delta = puddle.transform.position - center;
                delta.y = 0f;
                float touchRadius = explosionRadius + puddle.Radius;
                if (delta.sqrMagnitude <= touchRadius * touchRadius)
                {
                    puddle.Ignite(detonationDmg, eventBus, runStatistics);
                }
            }
        }

        private void TriggerSteamBlastNova()
        {
            Vector3 center = transform.position;
            float steamRawDamage = damage * STEAM_BLAST_DAMAGE_MULT;
            int obsMask = ObstacleMask;

            int hits = Physics.OverlapSphereNonAlloc(center, STEAM_BLAST_RADIUS, steamHits, enemyLayer);
            for (int i = 0; i < hits; i++)
            {
                Collider col = steamHits[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                int finalDamage = RollHitDamage(steamRawDamage, out bool isCrit);
                DealDamage(damageable, finalDamage, isCrit);

                if (damageable is EnemyBase enemy)
                {
                    runStatistics?.RecordDamage(SynergyIds.SteamBlast, finalDamage);

                    if (!enemy.IsDead)
                    {
                        Vector3 pushDir = enemy.transform.position - center;
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude > 0.0001f)
                        {
                            pushDir.Normalize();
                            float safeDist = STEAM_BLAST_KNOCKBACK;
                            if (obsMask != 0 && Physics.SphereCast(
                                enemy.transform.position + Vector3.up * 0.5f,
                                0.35f,
                                pushDir,
                                out RaycastHit hit,
                                STEAM_BLAST_KNOCKBACK + 0.2f,
                                obsMask,
                                QueryTriggerInteraction.Ignore))
                            {
                                safeDist = Mathf.Max(0f, hit.distance - 0.25f);
                            }

                            if (safeDist > 0.01f)
                            {
                                enemy.ApplyPull(pushDir * safeDist);
                            }
                        }
                    }
                }
            }

            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.SteamBlastNova, center, STEAM_BLAST_RADIUS));
        }
    }
}
