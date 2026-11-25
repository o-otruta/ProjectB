#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Core.BalanceBot
{
    /// <summary>
    /// Zero-GC gameplay AI controller for automated balance runs:
    /// - Threat repulsion (1/d^2), enemy projectile dodging, frost zone & boss telegraph avoidance
    /// - Melee/ranged combat spacing + circular kiting
    /// - Safe XP crystal & coin pickup attraction
    /// - 8-ray RaycastNonAlloc obstacle avoidance + arena boundary clamping
    /// - Stuck detection & recovery
    /// - Smart Ultimate trigger & automated free-slot LevelUp card selection
    /// </summary>
    public sealed class BalanceBotBrain : MonoBehaviour
    {
        private static readonly Vector3[] RayDirs =
        {
            new Vector3( 1f, 0f,  0f),
            new Vector3( 0.70710678f, 0f,  0.70710678f),
            new Vector3( 0f, 0f,  1f),
            new Vector3(-0.70710678f, 0f,  0.70710678f),
            new Vector3(-1f, 0f,  0f),
            new Vector3(-0.70710678f, 0f, -0.70710678f),
            new Vector3( 0f, 0f, -1f),
            new Vector3( 0.70710678f, 0f, -0.70710678f)
        };

        private readonly Collider[] enemyHitBuffer = new Collider[128];
        private readonly Collider[] pickupHitBuffer = new Collider[128];
        private readonly RaycastHit[] rayHitBuffer = new RaycastHit[4];
        private readonly List<string> equippedActiveIdCache = new List<string>(8);

        private BalanceBotConfig config;
        private ICardPickStrategy cardStrategy;
        private CardPickContext cardContext;

        private MoveInputService moveInput;
        private GameManager gameManager;
        private HeroHealth heroHealth;
        private HeroCombat heroCombat;
        private HeroAbilities heroAbilities;
        private HeroUltimate heroUltimate;
        private RunStatistics runStatistics;
        private WaveManager waveManager;
        private ArenaGenerator arenaGenerator;
        private EnemyProjectilePool enemyProjectilePool;
        private CardSelectionUI cardSelectionUI;
        private GameEventBus eventBus;

        private Func<string, bool> cachedHasAbility;
        private Func<string, int> cachedGetAbilityLevel;
        private Func<string, long> cachedGetAbilityDamage;
        private Action cachedOnCardsShown;
        private Action<BossSpawnedEvent> cachedOnBossSpawned;
        private Action<BossDefeatedEvent> cachedOnBossDefeated;

        private BossBase activeBoss;
        private int enemyLayerMask;
        private int pickupLayerMask;
        private int obstacleLayerMask;

        private Vector2 currentMoveDir;
        private float nextDecisionTime;
        private float strafeSign = 1f;
        private float nextStrafeFlipTime;
        private Vector3 stuckAnchorPos;
        private float stuckTimer;
        private float unstuckTimer;
        private Vector2 unstuckDir;
        private float arenaHalfBound = 240f;
        private bool pendingCardSelection;
        private bool isBound;
        private System.Random stuckRng = new System.Random(42);

        public void Initialize(
            BalanceBotConfig botConfig,
            ICardPickStrategy strategy,
            MoveInputService moveInput,
            GameManager gameManager,
            HeroHealth heroHealth,
            HeroCombat heroCombat,
            HeroAbilities heroAbilities,
            HeroUltimate heroUltimate,
            RunStatistics runStatistics,
            WaveManager waveManager,
            ArenaGenerator arenaGenerator,
            EnemyProjectilePool enemyProjectilePool,
            CardSelectionUI cardSelectionUI,
            GameEventBus eventBus,
            int seed)
        {
            Unbind();

            config = botConfig != null ? botConfig : BalanceBotConfig.CreateRuntimeDefault();
            cardStrategy = strategy ?? new GreedyDamageCardPickStrategy();
            stuckRng = new System.Random(seed != 0 ? seed : 42);

            this.moveInput = moveInput;
            this.gameManager = gameManager;
            this.heroHealth = heroHealth;
            this.heroCombat = heroCombat;
            this.heroAbilities = heroAbilities;
            this.heroUltimate = heroUltimate;
            this.runStatistics = runStatistics;
            this.waveManager = waveManager;
            this.arenaGenerator = arenaGenerator;
            this.enemyProjectilePool = enemyProjectilePool;
            this.cardSelectionUI = cardSelectionUI;
            this.eventBus = eventBus;

            ResolveLayerMasks();

            if (arenaGenerator != null)
            {
                arenaHalfBound = Mathf.Max(15f, arenaGenerator.ArenaSize * 0.5f - config.arenaWallMargin);
            }

            cachedHasAbility ??= HasAbilityLookup;
            cachedGetAbilityLevel ??= GetAbilityLevelLookup;
            cachedGetAbilityDamage ??= GetAbilityDamageLookup;
            cachedOnCardsShown ??= HandleCardsShown;
            cachedOnBossSpawned ??= HandleBossSpawned;
            cachedOnBossDefeated ??= HandleBossDefeated;

            cardContext ??= new CardPickContext();
            cardContext.HasAbility = cachedHasAbility;
            cardContext.GetAbilityLevel = cachedGetAbilityLevel;
            cardContext.GetAbilityDamage = cachedGetAbilityDamage;
            cardContext.EquippedActiveIds = equippedActiveIdCache;

            if (this.cardSelectionUI != null)
            {
                this.cardSelectionUI.OnCardsShown += cachedOnCardsShown;
                if (this.cardSelectionUI.IsActive)
                {
                    pendingCardSelection = true;
                }
            }

            if (this.eventBus != null)
            {
                this.eventBus.Subscribe(cachedOnBossSpawned);
                this.eventBus.Subscribe(cachedOnBossDefeated);
            }

            if (heroHealth != null)
            {
                stuckAnchorPos = heroHealth.transform.position;
            }

            nextDecisionTime = 0f;
            nextStrafeFlipTime = Time.time + config.strafeFlipIntervalSeconds;
            stuckTimer = 0f;
            unstuckTimer = 0f;
            currentMoveDir = Vector2.right;
            isBound = true;
        }

        public void Unbind()
        {
            if (!isBound) return;
            isBound = false;

            if (cardSelectionUI != null && cachedOnCardsShown != null)
            {
                cardSelectionUI.OnCardsShown -= cachedOnCardsShown;
            }
            if (eventBus != null)
            {
                if (cachedOnBossSpawned != null) eventBus.Unsubscribe(cachedOnBossSpawned);
                if (cachedOnBossDefeated != null) eventBus.Unsubscribe(cachedOnBossDefeated);
            }
            moveInput?.SetOverride(null);
            activeBoss = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (!isBound || gameManager == null) return;

            if (gameManager.CurrentState == GameState.LevelUp && cardSelectionUI != null && cardSelectionUI.IsActive)
            {
                if (pendingCardSelection)
                {
                    ProcessCardSelection();
                }
                return;
            }

            if (gameManager.CurrentState != GameState.Playing || heroHealth == null || heroHealth.IsDead)
            {
                moveInput?.SetOverride(null);
                return;
            }

            if (Time.time >= nextDecisionTime)
            {
                float interval = config != null ? Mathf.Max(0.05f, config.decisionIntervalSeconds) : 0.1f;
                nextDecisionTime = Time.time + interval;
                StepDecision(interval);
            }

            moveInput?.SetOverride(currentMoveDir);
        }

        /// <summary>
        /// Zero-GC decision step callable directly in tests or by Update at ~10 Hz.
        /// </summary>
        public void StepDecision(float stepDelta)
        {
            if (heroHealth == null || heroHealth.IsDead) return;

            Vector3 heroPos = heroHealth.transform.position;
            heroPos.y = 0f;

            if (Time.time >= nextStrafeFlipTime)
            {
                strafeSign = -strafeSign;
                nextStrafeFlipTime = Time.time + config.strafeFlipIntervalSeconds;
            }

            // 1. Stuck detector
            stuckTimer += stepDelta;
            if (unstuckTimer > 0f)
            {
                unstuckTimer -= stepDelta;
                currentMoveDir = unstuckDir;
                return;
            }

            if (stuckTimer >= config.stuckWindowSeconds)
            {
                float movedSq = (heroPos - stuckAnchorPos).sqrMagnitude;
                stuckAnchorPos = heroPos;
                stuckTimer = 0f;

                if (movedSq < config.stuckMinDisplacement * config.stuckMinDisplacement && currentMoveDir.sqrMagnitude > 0.05f)
                {
                    PickUnstuckDirection(heroPos);
                    unstuckTimer = config.unstuckDurationSeconds;
                    strafeSign = -strafeSign;
                    currentMoveDir = unstuckDir;
                    return;
                }
            }

            // 2. Evaluate enemy threats, nearest enemy, and ultimate readiness
            Vector3 repulsion = Vector3.zero;
            Vector3 nearestEnemyPos = Vector3.zero;
            float nearestEnemyDistSq = float.MaxValue;
            int enemiesInUltRadius = 0;
            int veryCloseEnemies = 0;
            float ultRadiusSq = config.ultimateCheckRadius * config.ultimateCheckRadius;
            float threatRadiusSq = config.threatRadius * config.threatRadius;
            const float veryCloseRadiusSq = 1.7f * 1.7f;

            int enemyCount = Physics.OverlapSphereNonAlloc(heroPos, config.threatRadius, enemyHitBuffer, enemyLayerMask);
            for (int i = 0; i < enemyCount; i++)
            {
                Collider col = enemyHitBuffer[i];
                if (col == null) continue;

                // TryGetComponent: неудачный GetComponent в редакторе аллоцирует сообщение об ошибке.
                if (!col.TryGetComponent(out EnemyBase enemy)) enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 ePos = enemy.transform.position;
                Vector3 toHero = heroPos - ePos;
                toHero.y = 0f;
                float distSq = toHero.sqrMagnitude;
                if (distSq > threatRadiusSq) continue;

                if (distSq < nearestEnemyDistSq)
                {
                    nearestEnemyDistSq = distSq;
                    nearestEnemyPos = ePos;
                }
                if (distSq <= ultRadiusSq)
                {
                    enemiesInUltRadius++;
                }
                if (distSq < veryCloseRadiusSq)
                {
                    veryCloseEnemies++;
                }

                float weight = config.repulsionWeight;
                if (enemy.IsMiniBoss || enemy.IsBoss) weight *= 1.8f;
                if (enemy is ExploderEnemy) weight *= 2.2f;

                if (enemy is ChargerEnemy charger &&
                    (charger.CurrentState == ChargerEnemy.ChargeState.WindingUp ||
                     charger.CurrentState == ChargerEnemy.ChargeState.Charging))
                {
                    Vector3 cDir = charger.LockedChargeDirection;
                    Vector3 perp = new Vector3(-cDir.z, 0f, cDir.x);
                    if (Vector3.Dot(perp, toHero) < 0f) perp = -perp;
                    repulsion += perp * config.bossTelegraphAvoidWeight;
                }

                float safeDistSq = Mathf.Max(0.4f, distSq);
                Vector3 awayDir = distSq > 0.0001f ? toHero / Mathf.Sqrt(distSq) : Vector3.forward;
                repulsion += awayDir * (weight / safeDistSq);
            }

            // 3. Boss telegraphs (outside/inside threat sphere)
            AddBossTelegraphAvoidance(heroPos, ref repulsion);

            // 4. Enemy projectile dodging (O(N), zero PhysX)
            AddProjectileAvoidance(heroPos, ref repulsion);

            // 5. Frost Wisp slow zones avoidance (O(N), zero PhysX)
            AddFrostZoneAvoidance(heroPos, ref repulsion);

            // 6. Ultimate activation check
            if (heroUltimate != null && heroUltimate.IsReady)
            {
                float hpRatio = heroHealth.MaxHp > 0 ? (float)heroHealth.CurrentHp / heroHealth.MaxHp : 1f;
                if (enemiesInUltRadius >= config.ultimateMinNearbyEnemies ||
                    (enemiesInUltRadius >= 2 && hpRatio <= config.ultimateLowHpEmergencyThreshold))
                {
                    heroUltimate.TryActivate();
                }
            }

            // 7. Combat engagement & circular kiting
            bool isMeleePrimary = IsMeleePrimaryHero();
            bool meleeHasNoActiveAbilities = isMeleePrimary && (heroAbilities == null || heroAbilities.ActiveAbilities.Count == 0);
            if (meleeHasNoActiveAbilities && veryCloseEnemies == 0 && nearestEnemyDistSq < float.MaxValue)
            {
                repulsion *= 0.22f;
            }

            Vector3 desired = repulsion;
            float preferredDist = isMeleePrimary ? config.meleePreferredDistance : config.rangedPreferredDistance;

            if (nearestEnemyDistSq < float.MaxValue)
            {
                float nearestDist = Mathf.Sqrt(nearestEnemyDistSq);
                Vector3 toNearest = nearestEnemyPos - heroPos;
                toNearest.y = 0f;
                Vector3 dirToNearest = nearestDist > 0.01f ? toNearest / nearestDist : Vector3.forward;
                Vector3 tangent = new Vector3(-dirToNearest.z, 0f, dirToNearest.x) * strafeSign;

                if (meleeHasNoActiveAbilities && veryCloseEnemies == 0)
                {
                    desired += tangent * (config.strafeWeight * 1.15f);
                    if (nearestDist > 1.85f)
                    {
                        desired += dirToNearest * (config.engageWeight * 1.6f);
                    }
                }
                else
                {
                    desired += tangent * config.strafeWeight;
                    // Engage toward enemies if outside preferred combat reach so melee Knight and short-range heroes keep hitting
                    if (nearestDist > preferredDist)
                    {
                        float engageScale = isMeleePrimary ? config.engageWeight * 1.4f : config.engageWeight;
                        desired += dirToNearest * engageScale;
                    }
                }
            }

            // 8. Pickup attraction when close-range threat is low
            float threatMag = repulsion.magnitude;
            if (nearestEnemyDistSq > config.pickupMinEnemyDistance * config.pickupMinEnemyDistance &&
                threatMag < config.pickupSafeThreatThreshold)
            {
                Vector3 pickupPull = ComputePickupAttraction(heroPos);
                desired += pickupPull;
            }

            // 9. Arena wall boundary push
            if (Mathf.Abs(heroPos.x) > arenaHalfBound)
            {
                desired.x += -Mathf.Sign(heroPos.x) * 4.5f;
            }
            if (Mathf.Abs(heroPos.z) > arenaHalfBound)
            {
                desired.z += -Mathf.Sign(heroPos.z) * 4.5f;
            }

            // Default fallback drift if completely calm
            if (desired.sqrMagnitude < 0.001f)
            {
                desired = new Vector3(Mathf.Cos(Time.time * 0.6f), 0f, Mathf.Sin(Time.time * 0.6f));
            }

            desired.y = 0f;
            Vector3 desiredNorm = desired.normalized;

            // 10. 8-ray RaycastNonAlloc obstacle avoidance
            Vector3 steered = SteerAroundObstacles(heroPos, desiredNorm);
            currentMoveDir = new Vector2(steered.x, steered.z);
        }

        private void AddBossTelegraphAvoidance(Vector3 heroPos, ref Vector3 repulsion)
        {
            if (activeBoss == null || activeBoss.IsDead)
            {
                activeBoss = null;
                return;
            }

            Vector3 bossPos = activeBoss.transform.position;
            bossPos.y = 0f;

            if (activeBoss is GolemBoss golem && golem.IsTelegraphingSlam)
            {
                float safeR = golem.SlamRadius + 1.5f;
                Vector3 diff = heroPos - bossPos;
                diff.y = 0f;
                if (diff.sqrMagnitude < safeR * safeR)
                {
                    Vector3 push = diff.sqrMagnitude > 0.01f ? diff.normalized : Vector3.forward;
                    repulsion += push * config.bossTelegraphAvoidWeight;
                }
            }
            else if (activeBoss is FrostLichBoss lich && lich.BossData != null)
            {
                var st = lich.CurrentState;
                if (st == FrostLichBoss.FrostLichState.TelegraphingBlizzard || st == FrostLichBoss.FrostLichState.ActiveBlizzard)
                {
                    Vector3 diff = heroPos - bossPos;
                    diff.y = 0f;
                    float dist = diff.magnitude;
                    float safeInner = lich.BossData.blizzardSafeInnerRadius;
                    float outer = lich.BossData.blizzardOuterRadius;
                    if (dist > safeInner * 0.85f && dist < outer + 1f)
                    {
                        // Move into the safe inner eye if close, or outside outer radius
                        Vector3 dir = dist > 0.01f ? diff / dist : Vector3.forward;
                        if (dist < (safeInner + outer) * 0.45f)
                        {
                            repulsion -= dir * config.bossTelegraphAvoidWeight;
                        }
                        else
                        {
                            repulsion += dir * config.bossTelegraphAvoidWeight;
                        }
                    }
                }
            }
            else if (activeBoss is DemonLordBoss demon && demon.BossData != null)
            {
                var st = demon.CurrentState;
                if (st == DemonLordBoss.DemonLordState.WindingUpCharge || st == DemonLordBoss.DemonLordState.Charging)
                {
                    Vector3 cDir = demon.LockedChargeDirection;
                    Vector3 perp = new Vector3(-cDir.z, 0f, cDir.x);
                    Vector3 diff = heroPos - bossPos;
                    diff.y = 0f;
                    if (Vector3.Dot(perp, diff) < 0f) perp = -perp;
                    repulsion += perp * config.bossTelegraphAvoidWeight;
                }
                else if (st == DemonLordBoss.DemonLordState.TelegraphingMeteors)
                {
                    float meteorSafeR = demon.BossData.demonMeteorRadius + 1.2f;
                    float meteorSafeRSq = meteorSafeR * meteorSafeR;
                    int zoneCount = demon.ActiveMeteorZoneCount;
                    for (int i = 0; i < zoneCount; i++)
                    {
                        Vector3 zp = demon.GetMeteorZonePosition(i);
                        Vector3 diff = heroPos - zp;
                        diff.y = 0f;
                        if (diff.sqrMagnitude < meteorSafeRSq)
                        {
                            Vector3 push = diff.sqrMagnitude > 0.01f ? diff.normalized : Vector3.forward;
                            repulsion += push * config.bossTelegraphAvoidWeight;
                        }
                    }
                }
            }
        }

        private void AddProjectileAvoidance(Vector3 heroPos, ref Vector3 repulsion)
        {
            if (enemyProjectilePool == null) return;
            var projectiles = enemyProjectilePool.ActiveProjectiles;
            if (projectiles == null || projectiles.Count == 0) return;

            float dodgeRadiusSq = config.projectileDodgeRadius * config.projectileDodgeRadius;
            int count = projectiles.Count;

            for (int i = 0; i < count; i++)
            {
                EnemyProjectile proj = projectiles[i];
                if (proj == null || !proj.IsActive) continue;

                Vector3 pPos = proj.transform.position;
                Vector3 toHero = heroPos - pPos;
                toHero.y = 0f;
                float distSq = toHero.sqrMagnitude;
                if (distSq > dodgeRadiusSq || distSq < 0.001f) continue;

                Vector3 pDir = proj.Direction;
                pDir.y = 0f;
                if (Vector3.Dot(toHero, pDir) <= 0f) continue; // Moving away already

                // Dodge perpendicular to projectile velocity
                Vector3 perp = new Vector3(-pDir.z, 0f, pDir.x);
                if (Vector3.Dot(perp, toHero) < 0f) perp = -perp;
                float urgency = 1f - Mathf.Clamp01(Mathf.Sqrt(distSq) / config.projectileDodgeRadius);
                repulsion += perp * (config.projectileDodgeWeight * (0.5f + urgency));
            }
        }

        private void AddFrostZoneAvoidance(Vector3 heroPos, ref Vector3 repulsion)
        {
            if (waveManager == null) return;
            var zones = waveManager.ActiveFrostZones;
            if (zones == null || zones.Count == 0) return;

            int count = zones.Count;
            for (int i = 0; i < count; i++)
            {
                FrostSlowZone z = zones[i];
                if (z == null || !z.IsActive) continue;

                Vector3 diff = heroPos - z.transform.position;
                diff.y = 0f;
                float safeR = z.Radius + 0.9f;
                if (diff.sqrMagnitude < safeR * safeR)
                {
                    Vector3 away = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector3.forward;
                    repulsion += away * config.frostZoneAvoidWeight;
                }
            }
        }

        private Vector3 ComputePickupAttraction(Vector3 heroPos)
        {
            int count = Physics.OverlapSphereNonAlloc(heroPos, config.pickupSearchRadius, pickupHitBuffer, pickupLayerMask);
            if (count == 0) return Vector3.zero;

            Vector3 bestXpPos = Vector3.zero;
            float bestXpDistSq = float.MaxValue;
            Vector3 bestCoinPos = Vector3.zero;
            float bestCoinDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = pickupHitBuffer[i];
                if (col == null) continue;

                Vector3 pos = col.transform.position;
                Vector3 diff = pos - heroPos;
                diff.y = 0f;
                float dSq = diff.sqrMagnitude;

                if (col.TryGetComponent<XpCrystal>(out _))
                {
                    if (dSq < bestXpDistSq)
                    {
                        bestXpDistSq = dSq;
                        bestXpPos = pos;
                    }
                }
                else if (col.TryGetComponent<CoinPickup>(out _))
                {
                    if (dSq < bestCoinDistSq)
                    {
                        bestCoinDistSq = dSq;
                        bestCoinPos = pos;
                    }
                }
            }

            if (bestXpDistSq < float.MaxValue && bestXpDistSq > 0.25f)
            {
                Vector3 dir = bestXpPos - heroPos;
                dir.y = 0f;
                return dir.normalized * config.xpAttractWeight;
            }

            if (bestCoinDistSq < float.MaxValue && bestCoinDistSq > 0.25f)
            {
                Vector3 dir = bestCoinPos - heroPos;
                dir.y = 0f;
                return dir.normalized * config.coinAttractWeight;
            }

            return Vector3.zero;
        }

        private Vector3 SteerAroundObstacles(Vector3 heroPos, Vector3 desiredDir)
        {
            if (obstacleLayerMask == 0) return desiredDir;

            Vector3 origin = heroPos + Vector3.up * 0.6f;
            float rayLen = config.obstacleRayLength;

            if (Physics.RaycastNonAlloc(origin, desiredDir, rayHitBuffer, rayLen * 0.85f, obstacleLayerMask, QueryTriggerInteraction.Ignore) == 0)
            {
                return desiredDir;
            }

            Vector3 bestDir = desiredDir;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < RayDirs.Length; i++)
            {
                Vector3 cand = RayDirs[i];
                int hits = Physics.RaycastNonAlloc(origin, cand, rayHitBuffer, rayLen, obstacleLayerMask, QueryTriggerInteraction.Ignore);
                float clearance = hits == 0 ? 1f : (rayHitBuffer[0].distance / rayLen) - 1.1f;
                float align = Vector3.Dot(cand, desiredDir);
                float score = align + clearance * 1.4f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = cand;
                }
            }

            return bestDir;
        }

        private void PickUnstuckDirection(Vector3 heroPos)
        {
            Vector3 origin = heroPos + Vector3.up * 0.6f;
            float rayLen = config.obstacleRayLength;
            int start = stuckRng.Next(0, RayDirs.Length);

            for (int offset = 0; offset < RayDirs.Length; offset++)
            {
                Vector3 cand = RayDirs[(start + offset) % RayDirs.Length];
                if (obstacleLayerMask == 0 ||
                    Physics.RaycastNonAlloc(origin, cand, rayHitBuffer, rayLen, obstacleLayerMask, QueryTriggerInteraction.Ignore) == 0)
                {
                    unstuckDir = new Vector2(cand.x, cand.z);
                    return;
                }
            }

            Vector3 fallback = RayDirs[start];
            unstuckDir = new Vector2(fallback.x, fallback.z);
        }

        private bool IsMeleePrimaryHero()
        {
            if (heroCombat == null || heroCombat.ActiveWeapons == null || heroCombat.ActiveWeapons.Count == 0)
            {
                return false;
            }
            return heroCombat.ActiveWeapons[0] != null && heroCombat.ActiveWeapons[0].IsMelee;
        }

        private void HandleCardsShown()
        {
            pendingCardSelection = true;
        }

        private void ProcessCardSelection()
        {
            if (cardSelectionUI == null || !cardSelectionUI.IsActive)
            {
                pendingCardSelection = false;
                return;
            }

            pendingCardSelection = false;
            RefreshCardContext();

            CardPickDecision decision = cardStrategy.Choose(cardContext);
            if (decision.RequestFreeReroll && cardSelectionUI.CanFreeReroll)
            {
                if (cardSelectionUI.TryFreeRerollForAutomation())
                {
                    RefreshCardContext();
                    decision = cardStrategy.Choose(cardContext);
                }
            }

            int freeCount = cardContext.FreeOfferCount;
            int slot = Mathf.Clamp(decision.SlotIndex, 0, Mathf.Max(0, freeCount - 1));
            if (!cardSelectionUI.SelectCardForAutomation(slot))
            {
                cardSelectionUI.SelectCardForAutomation(0);
            }
        }

        private void RefreshCardContext()
        {
            equippedActiveIdCache.Clear();
            if (heroAbilities != null && heroAbilities.ActiveAbilities != null)
            {
                var actives = heroAbilities.ActiveAbilities;
                for (int i = 0; i < actives.Count; i++)
                {
                    ActiveAbility a = actives[i];
                    if (a != null && a.Data != null && !string.IsNullOrEmpty(a.Data.id))
                    {
                        equippedActiveIdCache.Add(a.Data.id);
                    }
                }
            }

            cardContext.OfferedCards = cardSelectionUI != null ? cardSelectionUI.CurrentOfferedCards : null;
            cardContext.FreeSlotsCount = cardSelectionUI != null ? cardSelectionUI.FreeSlotsCount : 2;
            cardContext.CanFreeReroll = cardSelectionUI != null && cardSelectionUI.CanFreeReroll;
            cardContext.CanAddActive = heroAbilities == null || heroAbilities.CanAddActive();
            cardContext.CanAddPassive = heroAbilities == null || heroAbilities.CanAddPassive();
        }

        private bool HasAbilityLookup(string id) => heroAbilities != null && heroAbilities.HasAbility(id);
        private int GetAbilityLevelLookup(string id) => heroAbilities != null ? heroAbilities.GetAbilityLevel(id) : 0;
        private long GetAbilityDamageLookup(string id) => runStatistics != null ? runStatistics.GetDamageBySource(id) : 0L;

        private void HandleBossSpawned(BossSpawnedEvent evt)
        {
            if (waveManager == null || waveManager.ActiveEnemies == null) return;
            // Cache BossBase reference once on boss spawn (outside Update)
            var all = FindObjectsByType<BossBase>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !all[i].IsDead)
                {
                    activeBoss = all[i];
                    return;
                }
            }
        }

        private void HandleBossDefeated(BossDefeatedEvent evt)
        {
            activeBoss = null;
        }

        private void ResolveLayerMasks()
        {
            int enemyMask = LayerMask.GetMask("Enemy", "Enemies");
            enemyLayerMask = enemyMask != 0 ? enemyMask : (1 << 8);

            int pickupsMask = LayerMask.GetMask("Pickups");
            pickupLayerMask = pickupsMask != 0 ? pickupsMask : ~0;

            obstacleLayerMask = LayerMask.GetMask("Obstacles");
        }
    }
}
#endif
