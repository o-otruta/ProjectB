using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Enemies;
using ProjectB.Meta;
using ProjectB.Abilities;

namespace ProjectB.Player
{
    public class HeroUltimate : MonoBehaviour
    {
        private struct SoulBoltState
        {
            public bool active;
            public Vector3 position;
            public Vector3 direction;
            public float distanceTraveled;
            public HashSet<EntityId> hitIds;
        }

        private static readonly Collider[] meteorSearchBuffer = new Collider[128];
        private static readonly Collider[] ultDamageBuffer = new Collider[128];
        private static readonly Transform[] meteorCandidateBuffer = new Transform[128];

        [SerializeField] private HeroData fallbackHeroData;
        [SerializeField] private GameObject shieldVisual;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Soul Nova Visuals (Optional)")]
        [SerializeField] private Mesh soulNovaMesh;
        [SerializeField] private Material soulNovaBaseMaterial;
        [SerializeField] private Material soulNovaGlowMaterial;

        private HeroHealth heroHealth;
        private GameManager gameManager;
        private SelectedHeroService selectedHeroService;
        private GameEventBus eventBus;
        private HeroRuntimeStats runtimeStats;
        private RunStatistics runStatistics;

        private HeroData activeHeroData;
        private HeroUltimateType activeUltimateType;
        private float activeRemaining;
        private float cooldownRemaining;
        private bool isActive;

        private int meteorsRemaining;
        private float meteorInterval;
        private float nextMeteorTimer;

        private int arrowWavesRemaining;
        private int arrowWaveIndex;
        private float arrowWaveInterval;
        private float nextArrowWaveTimer;

        private SoulBoltState[] soulBolts;
        private Transform soulNovaVisualRoot;
        private Transform[] soulBoltVisuals;

        // Divine Consecration (Paladin)
        private Vector3 consecrationCenter;
        private float consecrationTickTimer;
        private Transform consecrationVisual;
        public bool IsConsecrationActive => isActive && activeUltimateType == HeroUltimateType.Consecration;
        public Vector3 ConsecrationCenter => consecrationCenter;

        // Mech Drop (Engineer)
        private UnityEngine.Pool.IObjectPool<LaserTurret> mechTurretPool;
        private Transform mechTurretRoot;
        private GameObject mechTurretPoolPrefab;
        public int LastTurretDropCount { get; private set; }

        private int resolvedEnemyLayerMask;
        private int resolvedObstacleLayerMask = -1;

        public HeroData ActiveHeroData => activeHeroData != null ? activeHeroData : (selectedHeroService?.GetSelectedHero() ?? fallbackHeroData);
        public bool IsActive => isActive;
        public float ActiveRemaining => activeRemaining;
        public float ActiveDuration => ActiveHeroData != null ? Mathf.Max(0.1f, ActiveHeroData.ultimateDuration) : 5f;
        public float CooldownRemaining => cooldownRemaining;
        public float BaseCooldownDuration => ActiveHeroData != null ? Mathf.Max(0.1f, ActiveHeroData.ultimateCooldown) : 25f;
        public float CooldownDuration => runtimeStats != null
            ? runtimeStats.GetEffectiveCooldown(BaseCooldownDuration * runtimeStats.UltimateCooldownMultiplier, 1f)
            : BaseCooldownDuration;
        public bool IsReady => !isActive && cooldownRemaining <= 0f && (heroHealth == null || !heroHealth.IsDead);

        public event Action OnStateChanged;

        [Inject]
        public void Construct(
            HeroHealth heroHealth,
            GameManager gameManager = null,
            SelectedHeroService selectedHeroService = null,
            GameEventBus eventBus = null,
            HeroRuntimeStats runtimeStats = null,
            RunStatistics runStatistics = null)
        {
            this.heroHealth = heroHealth;
            this.gameManager = gameManager;
            this.selectedHeroService = selectedHeroService;
            this.eventBus = eventBus;
            this.runtimeStats = runtimeStats;
            this.runStatistics = runStatistics;
        }

        private void Awake()
        {
            if (heroHealth == null)
            {
                heroHealth = GetComponent<HeroHealth>();
            }

            if (shieldVisual != null)
            {
                shieldVisual.SetActive(false);
            }

            ResolveEnemyLayerMask();
        }

        private void Start()
        {
            activeHeroData = selectedHeroService?.GetSelectedHero() ?? fallbackHeroData;
            if (heroHealth != null)
            {
                heroHealth.OnDied += HandleHeroDied;
            }
            if (activeHeroData != null && activeHeroData.ultimateType == HeroUltimateType.SoulNova)
            {
                EnsureSoulNovaState();
            }
            OnStateChanged?.Invoke();
        }

        public void Prewarm()
        {
            ResolveEnemyLayerMask();
            HeroData data = ActiveHeroData;
            if (data == null || data.ultimateType == HeroUltimateType.SoulNova)
            {
                EnsureSoulNovaState();
            }
        }

        public void SimulateFrameTick()
        {
            Update();
        }

        private void OnDestroy()
        {
            if (heroHealth != null)
            {
                heroHealth.OnDied -= HandleHeroDied;
            }
            if (soulNovaVisualRoot != null)
            {
                if (Application.isPlaying) Destroy(soulNovaVisualRoot.gameObject);
                else DestroyImmediate(soulNovaVisualRoot.gameObject);
                soulNovaVisualRoot = null;
            }
            if (consecrationVisual != null)
            {
                if (Application.isPlaying) Destroy(consecrationVisual.gameObject);
                else DestroyImmediate(consecrationVisual.gameObject);
                consecrationVisual = null;
            }
            if (mechTurretRoot != null)
            {
                mechTurretPool?.Clear();
                if (Application.isPlaying) Destroy(mechTurretRoot.gameObject);
                else DestroyImmediate(mechTurretRoot.gameObject);
                mechTurretRoot = null;
            }
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (heroHealth != null && heroHealth.IsDead) return;
            if (gameManager != null && gameManager.CurrentState != GameState.Playing) return;

            // Optional desktop/editor shortcut for testing
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryActivate();
            }

            if (isActive)
            {
                if (activeUltimateType == HeroUltimateType.MeteorStrike && meteorsRemaining > 0)
                {
                    nextMeteorTimer -= Time.deltaTime;
                    while (nextMeteorTimer <= 0f && meteorsRemaining > 0)
                    {
                        DropNextMeteor();
                        nextMeteorTimer += meteorInterval;
                    }
                }
                else if (activeUltimateType == HeroUltimateType.ArrowRain && arrowWavesRemaining > 0)
                {
                    nextArrowWaveTimer -= Time.deltaTime;
                    while (nextArrowWaveTimer <= 0f && arrowWavesRemaining > 0)
                    {
                        TriggerArrowRainWave();
                        nextArrowWaveTimer += arrowWaveInterval;
                    }
                }
                else if (activeUltimateType == HeroUltimateType.SoulNova)
                {
                    StepSoulNova(Time.deltaTime);
                }
                else if (activeUltimateType == HeroUltimateType.Consecration)
                {
                    StepConsecration(Time.deltaTime);
                }

                activeRemaining -= Time.deltaTime;
                if (activeRemaining <= 0f)
                {
                    DeactivateUltimate();
                }
            }
            else if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
                if (cooldownRemaining <= 0f)
                {
                    cooldownRemaining = 0f;
                    OnStateChanged?.Invoke();
                }
            }
        }

        public void ResetCooldown()
        {
            cooldownRemaining = 0f;
            OnStateChanged?.Invoke();
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            if (gameManager != null && gameManager.CurrentState != GameState.Playing) return false;

            HeroData data = ActiveHeroData;
            HeroUltimateType ultType = data != null ? data.ultimateType : HeroUltimateType.Shield;

            switch (ultType)
            {
                case HeroUltimateType.MeteorStrike:
                    ActivateMeteorStrike();
                    break;

                case HeroUltimateType.ArrowRain:
                    ActivateArrowRain();
                    break;

                case HeroUltimateType.SoulNova:
                    ActivateSoulNova();
                    break;

                case HeroUltimateType.Consecration:
                    ActivateConsecration();
                    break;

                case HeroUltimateType.TurretDrop:
                    ActivateTurretDrop();
                    break;

                case HeroUltimateType.Shield:
                default:
                    ActivateShield();
                    break;
            }

            return true;
        }

        private void ActivateShield()
        {
            isActive = true;
            activeUltimateType = HeroUltimateType.Shield;
            activeRemaining = ActiveDuration;
            cooldownRemaining = 0f;
            meteorsRemaining = 0;
            arrowWavesRemaining = 0;

            heroHealth?.SetInvulnerable(true);
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(true);
            }

            OnStateChanged?.Invoke();
            TriggerShieldShockwave();
        }

        public int TriggerShieldShockwave()
        {
            HeroData data = ActiveHeroData;
            float radius = ScaleArea(data != null ? Mathf.Max(0f, data.shieldPulseRadius) : 4.5f);
            float baseDamage = data != null ? Mathf.Max(0f, data.shieldPulseDamage) : 40f;
            float knockback = data != null ? Mathf.Max(0f, data.shieldPulseKnockback) : 2.5f;
            Vector3 heroPos = transform.position;

            int affected = 0;
            if (radius > 0f)
            {
                int mask = ResolveEnemyLayerMask();
                int obsMask = ResolveObstacleLayerMask();
                float damageMult = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
                float rawDamage = baseDamage * damageMult;

                int hits = Physics.OverlapSphereNonAlloc(heroPos, radius, ultDamageBuffer, mask);
                for (int i = 0; i < hits; i++)
                {
                    Collider col = ultDamageBuffer[i];
                    if (col == null) continue;
                    if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;
                    if (damageable is HeroHealth) continue;

                    if (rawDamage > 0f)
                    {
                        bool isCrit = false;
                        int finalDamage = runtimeStats != null
                            ? runtimeStats.RollDamage(rawDamage, out isCrit)
                            : Mathf.Max(1, Mathf.RoundToInt(rawDamage));

                        if (damageable is EnemyBase enemyDmg)
                        {
                            int dealt = enemyDmg.TakeDamageFrom(finalDamage, isCrit, heroPos, isAoE: true);
                            if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Ultimate, dealt);
                        }
                        else
                        {
                            damageable.TakeDamage(finalDamage);
                        }
                    }

                    if (knockback > 0f && col.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead)
                    {
                        Vector3 pushDir = enemy.transform.position - heroPos;
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude < 0.0001f)
                        {
                            pushDir = transform.forward;
                            pushDir.y = 0f;
                        }

                        if (pushDir.sqrMagnitude > 0.0001f)
                        {
                            pushDir.Normalize();
                            float safeDist = knockback;
                            if (obsMask != 0 && Physics.SphereCast(
                                enemy.transform.position + Vector3.up * 0.5f,
                                0.35f,
                                pushDir,
                                out RaycastHit hit,
                                knockback + 0.2f,
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

                    affected++;
                }
            }

            eventBus?.Publish(new ShieldShockwaveEvent(heroPos, radius));
            return affected;
        }

        private void ActivateMeteorStrike()
        {
            HeroData data = ActiveHeroData;
            int count = data != null ? Mathf.Max(1, data.meteorCount) : 6;

            isActive = true;
            activeUltimateType = HeroUltimateType.MeteorStrike;
            float duration = ActiveDuration;
            activeRemaining = duration;
            cooldownRemaining = 0f;
            meteorsRemaining = count;
            arrowWavesRemaining = 0;
            meteorInterval = duration / count;
            nextMeteorTimer = meteorInterval;

            OnStateChanged?.Invoke();
            DropNextMeteor();
        }

        private void ActivateArrowRain()
        {
            HeroData data = ActiveHeroData;
            int waveCount = data != null ? Mathf.Max(1, data.arrowRainWaveCount) : 3;

            isActive = true;
            activeUltimateType = HeroUltimateType.ArrowRain;
            float duration = Mathf.Max(0.6f, ActiveDuration);
            activeRemaining = duration;
            cooldownRemaining = 0f;
            meteorsRemaining = 0;
            arrowWavesRemaining = waveCount;
            arrowWaveIndex = 0;
            arrowWaveInterval = duration / waveCount;
            nextArrowWaveTimer = arrowWaveInterval;

            OnStateChanged?.Invoke();
            TriggerArrowRainWave();
        }

        public int TriggerArrowRainWave()
        {
            if (arrowWavesRemaining > 0)
            {
                arrowWavesRemaining--;
            }
            arrowWaveIndex++;

            HeroData data = ActiveHeroData;
            float radius = ScaleArea(data != null ? Mathf.Max(1f, data.arrowRainRadius) : 8f);
            float waveDamage = data != null ? Mathf.Max(0f, data.arrowRainWaveDamage) : 50f;
            float slowFactor = data != null ? Mathf.Clamp(data.arrowRainSlowFactor, 0.1f, 1f) : 0.5f;
            float slowDuration = data != null ? Mathf.Max(0f, data.arrowRainSlowDuration) : 4.0f;

            int mask = ResolveEnemyLayerMask();
            Vector3 heroPos = transform.position;
            float damageMult = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            float rawDamage = waveDamage * damageMult;

            int hits = Physics.OverlapSphereNonAlloc(heroPos, radius, ultDamageBuffer, mask);
            int damagedCount = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider col = ultDamageBuffer[i];
                if (col == null) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    bool isCrit = false;
                    int finalDamage = runtimeStats != null
                        ? runtimeStats.RollDamage(rawDamage, out isCrit)
                        : Mathf.Max(1, Mathf.RoundToInt(rawDamage));

                    if (damageable is EnemyBase enemy)
                    {
                        int dealt = enemy.TakeDamageFrom(finalDamage, isCrit, heroPos, isAoE: true);
                        if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Ultimate, dealt);
                    }
                    else
                    {
                        damageable.TakeDamage(finalDamage);
                    }
                    damagedCount++;
                }

                if (col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(slowFactor, slowDuration);
                }
            }

            eventBus?.Publish(new ArrowRainWaveEvent(heroPos, radius, arrowWaveIndex));
            return damagedCount;
        }

        private void ActivateSoulNova()
        {
            HeroData data = ActiveHeroData;
            int boltCount = data != null ? Mathf.Max(1, data.soulNovaBoltCount) : 12;
            float range = data != null ? Mathf.Max(1f, data.soulNovaRange) : 10f;
            float healPercent = data != null ? Mathf.Clamp01(data.soulNovaHealPercent) : 0.35f;

            isActive = true;
            activeUltimateType = HeroUltimateType.SoulNova;
            activeRemaining = Mathf.Max(0.75f, ActiveDuration);
            cooldownRemaining = 0f;
            meteorsRemaining = 0;
            arrowWavesRemaining = 0;

            int healAmount = 0;
            if (heroHealth != null && !heroHealth.IsDead && healPercent > 0f)
            {
                int targetMax = Mathf.Max(1, heroHealth.MaxHp);
                healAmount = Mathf.Max(1, Mathf.RoundToInt(targetMax * healPercent));
                heroHealth.Heal(healAmount);
            }

            EnsureSoulNovaState();
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            float angleStep = (Mathf.PI * 2f) / boltCount;
            for (int i = 0; i < boltCount; i++)
            {
                float angle = angleStep * i;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                soulBolts[i].active = true;
                soulBolts[i].position = origin;
                soulBolts[i].direction = dir;
                soulBolts[i].distanceTraveled = 0f;
                soulBolts[i].hitIds.Clear();

                if (soulBoltVisuals != null && i < soulBoltVisuals.Length && soulBoltVisuals[i] != null)
                {
                    soulBoltVisuals[i].gameObject.SetActive(true);
                    soulBoltVisuals[i].position = origin;
                    soulBoltVisuals[i].rotation = Quaternion.LookRotation(dir);
                }
            }

            OnStateChanged?.Invoke();
            StepSoulNova(0f);
            eventBus?.Publish(new SoulNovaCastEvent(transform.position, range, healAmount));
        }

        public int StepSoulNova(float deltaTime)
        {
            if (soulBolts == null) return 0;

            HeroData data = ActiveHeroData;
            float baseDamage = data != null ? Mathf.Max(0f, data.soulNovaBaseDamage) : 60f;
            float speed = data != null ? Mathf.Max(1f, data.soulNovaSpeed) : 16f;
            float hitRadius = data != null ? Mathf.Max(0.1f, data.soulNovaHitRadius) : 0.42f;
            float range = data != null ? Mathf.Max(1f, data.soulNovaRange) : 10f;

            int mask = ResolveEnemyLayerMask();
            float damageMult = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            float rawDamage = baseDamage * damageMult;
            float stepDist = Mathf.Max(0f, deltaTime) * speed;
            int totalHits = 0;

            for (int b = 0; b < soulBolts.Length; b++)
            {
                if (!soulBolts[b].active) continue;

                soulBolts[b].position += soulBolts[b].direction * stepDist;
                soulBolts[b].distanceTraveled += stepDist;

                if (soulBoltVisuals != null && b < soulBoltVisuals.Length && soulBoltVisuals[b] != null)
                {
                    soulBoltVisuals[b].position = soulBolts[b].position;
                }

                int hits = Physics.OverlapSphereNonAlloc(soulBolts[b].position, hitRadius, ultDamageBuffer, mask);
                for (int i = 0; i < hits; i++)
                {
                    Collider col = ultDamageBuffer[i];
                    if (col == null) continue;
                    if (!col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                    EntityId targetId = col.gameObject.GetEntityId();
                    if (!soulBolts[b].hitIds.Add(targetId)) continue;

                    bool isCrit = false;
                    int finalDamage = runtimeStats != null
                        ? runtimeStats.RollDamage(rawDamage, out isCrit)
                        : Mathf.Max(1, Mathf.RoundToInt(rawDamage));

                    if (damageable is EnemyBase enemy)
                    {
                        Vector3 boltOrigin = soulBolts[b].position - soulBolts[b].direction * 2f;
                        int dealt = enemy.TakeDamageFrom(finalDamage, isCrit, boltOrigin, isAoE: false);
                        if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Ultimate, dealt);
                    }
                    else
                    {
                        damageable.TakeDamage(finalDamage);
                    }
                    totalHits++;
                }

                if (soulBolts[b].distanceTraveled >= range)
                {
                    soulBolts[b].active = false;
                    if (soulBoltVisuals != null && b < soulBoltVisuals.Length && soulBoltVisuals[b] != null)
                    {
                        soulBoltVisuals[b].gameObject.SetActive(false);
                    }
                }
            }

            return totalHits;
        }

        private void EnsureSoulNovaState()
        {
            HeroData data = ActiveHeroData;
            int boltCount = data != null ? Mathf.Max(1, data.soulNovaBoltCount) : 12;

            if (soulBolts == null || soulBolts.Length != boltCount)
            {
                soulBolts = new SoulBoltState[boltCount];
                for (int i = 0; i < boltCount; i++)
                {
                    soulBolts[i].hitIds = new HashSet<EntityId>(32);
                }

                if (soulNovaVisualRoot != null)
                {
                    if (Application.isPlaying) Destroy(soulNovaVisualRoot.gameObject);
                    else DestroyImmediate(soulNovaVisualRoot.gameObject);
                    soulNovaVisualRoot = null;
                    soulBoltVisuals = null;
                }
            }

            if (soulNovaVisualRoot == null && (soulNovaMesh != null || Application.isPlaying))
            {
                var rootGo = new GameObject("SoulNovaVisuals");
                soulNovaVisualRoot = rootGo.transform;
                soulBoltVisuals = new Transform[boltCount];

                for (int i = 0; i < boltCount; i++)
                {
                    GameObject boltGo;
                    if (soulNovaMesh != null)
                    {
                        boltGo = new GameObject($"SoulBolt_{i}");
                        var mf = boltGo.AddComponent<MeshFilter>();
                        mf.sharedMesh = soulNovaMesh;
                        var mr = boltGo.AddComponent<MeshRenderer>();
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        if (soulNovaBaseMaterial != null && soulNovaGlowMaterial != null)
                        {
                            mr.sharedMaterials = new[] { soulNovaBaseMaterial, soulNovaGlowMaterial };
                        }
                        else if (soulNovaBaseMaterial != null)
                        {
                            mr.sharedMaterial = soulNovaBaseMaterial;
                        }
                    }
                    else
                    {
                        boltGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        boltGo.name = $"SoulBolt_{i}";
                        boltGo.transform.localScale = Vector3.one * 0.35f;
                        var col = boltGo.GetComponent<Collider>();
                        if (col != null)
                        {
                            if (Application.isPlaying) Destroy(col);
                            else DestroyImmediate(col);
                        }
                    }

                    boltGo.transform.SetParent(soulNovaVisualRoot, false);
                    boltGo.SetActive(false);
                    soulBoltVisuals[i] = boltGo.transform;
                }
            }
        }

        private void HideSoulNovaVisuals()
        {
            if (soulBolts != null)
            {
                for (int i = 0; i < soulBolts.Length; i++)
                {
                    soulBolts[i].active = false;
                }
            }
            if (soulBoltVisuals != null)
            {
                for (int i = 0; i < soulBoltVisuals.Length; i++)
                {
                    if (soulBoltVisuals[i] != null)
                    {
                        soulBoltVisuals[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void DropNextMeteor()
        {
            if (meteorsRemaining <= 0) return;
            meteorsRemaining--;

            HeroData data = ActiveHeroData;
            float searchRadius = data != null ? Mathf.Max(1f, data.meteorSearchRadius) : 7f;
            float explosionRadius = ScaleArea(data != null ? Mathf.Max(0.5f, data.meteorExplosionRadius) : 3.5f);
            float baseDamage = data != null ? Mathf.Max(0f, data.meteorBaseDamage) : 80f;

            int mask = ResolveEnemyLayerMask();
            Vector3 heroPos = transform.position;
            Vector3 impactPos = FindMeteorImpactPoint(heroPos, mask, searchRadius);

            float damageMult = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            float rawDamage = baseDamage * damageMult;

            int hits = Physics.OverlapSphereNonAlloc(impactPos, explosionRadius, ultDamageBuffer, mask);
            for (int i = 0; i < hits; i++)
            {
                Collider col = ultDamageBuffer[i];
                if (col != null && col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    bool isCrit = false;
                    int finalDamage = runtimeStats != null
                        ? runtimeStats.RollDamage(rawDamage, out isCrit)
                        : Mathf.Max(1, Mathf.RoundToInt(rawDamage));

                    if (damageable is EnemyBase enemy)
                    {
                        int dealt = enemy.TakeDamageFrom(finalDamage, isCrit, impactPos, isAoE: true);
                        if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Ultimate, dealt);
                    }
                    else
                    {
                        damageable.TakeDamage(finalDamage);
                    }
                }
            }

            eventBus?.Publish(new MeteorImpactEvent(impactPos, explosionRadius));
        }

        private Vector3 FindMeteorImpactPoint(Vector3 heroPos, int mask, float searchRadius)
        {
            int hits = Physics.OverlapSphereNonAlloc(heroPos, searchRadius, meteorSearchBuffer, mask);
            int candidateCount = 0;

            for (int i = 0; i < hits && candidateCount < meteorCandidateBuffer.Length; i++)
            {
                Collider col = meteorSearchBuffer[i];
                if (col == null) continue;
                if (col.TryGetComponent<IDamageable>(out var damageable) &&
                    !CombatTargetFilter.IsValidAutoAimTarget(col, damageable, mask))
                {
                    continue;
                }

                meteorCandidateBuffer[candidateCount++] = col.transform;
            }

            if (candidateCount > 0)
            {
                Transform chosen = meteorCandidateBuffer[UnityEngine.Random.Range(0, candidateCount)];
                Vector3 pos = chosen.position;
                pos.y = 0f;
                return pos;
            }

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(2f, 6f);
            return new Vector3(
                heroPos.x + Mathf.Cos(angle) * radius,
                0f,
                heroPos.z + Mathf.Sin(angle) * radius);
        }

        private float ScaleArea(float radius)
        {
            return runtimeStats != null ? runtimeStats.GetScaledArea(radius) : radius;
        }

        // ---------------- Divine Consecration (Paladin) ----------------

        private void ActivateConsecration()
        {
            HeroData data = ActiveHeroData;
            isActive = true;
            activeUltimateType = HeroUltimateType.Consecration;
            activeRemaining = ActiveDuration;
            cooldownRemaining = 0f;
            meteorsRemaining = 0;
            arrowWavesRemaining = 0;

            consecrationCenter = transform.position;
            consecrationCenter.y = 0f;
            consecrationTickTimer = 0f;

            float radius = ConsecrationRadius;
            EnsureConsecrationVisual(data);
            if (consecrationVisual != null)
            {
                consecrationVisual.gameObject.SetActive(true);
                consecrationVisual.position = consecrationCenter + Vector3.up * 0.04f;
                consecrationVisual.localScale = new Vector3(radius, 1f, radius);
            }

            OnStateChanged?.Invoke();
            eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.ConsecrationCast, consecrationCenter, radius));
            TickConsecration();
        }

        public float ConsecrationRadius
        {
            get
            {
                HeroData data = ActiveHeroData;
                return ScaleArea(data != null ? Mathf.Max(0.5f, data.consecrationRadius) : 5.5f);
            }
        }

        public void StepConsecration(float deltaTime)
        {
            HeroData data = ActiveHeroData;
            float interval = data != null ? Mathf.Max(0.05f, data.consecrationTickInterval) : 0.5f;
            consecrationTickTimer += Mathf.Max(0f, deltaTime);
            while (consecrationTickTimer >= interval)
            {
                consecrationTickTimer -= interval;
                TickConsecration();
            }

            if (consecrationVisual != null && consecrationVisual.gameObject.activeSelf)
            {
                consecrationVisual.Rotate(0f, 25f * deltaTime, 0f, Space.World);
            }
        }

        /// <summary>One consecration pulse: burns enemies inside the circle and heals the hero if standing in it.</summary>
        public int TickConsecration()
        {
            HeroData data = ActiveHeroData;
            float radius = ConsecrationRadius;
            float baseDamage = data != null ? Mathf.Max(0f, data.consecrationDamagePerTick) : 18f;
            int heal = data != null ? Mathf.Max(0, data.consecrationHealPerTick) : 3;

            int mask = ResolveEnemyLayerMask();
            float rawDamage = baseDamage * (runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f);
            int hits = Physics.OverlapSphereNonAlloc(consecrationCenter, radius, ultDamageBuffer, mask);
            int damaged = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider col = ultDamageBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;
                if (damageable is HeroHealth) continue;

                bool isCrit = false;
                int finalDamage = runtimeStats != null
                    ? runtimeStats.RollDamage(rawDamage, out isCrit)
                    : Mathf.Max(1, Mathf.RoundToInt(rawDamage));

                if (damageable is EnemyBase enemy)
                {
                    int dealt = enemy.TakeDamageFrom(finalDamage, isCrit, consecrationCenter, isAoE: true);
                    if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Ultimate, dealt);
                }
                else
                {
                    damageable.TakeDamage(finalDamage);
                }
                damaged++;
            }

            if (heal > 0 && heroHealth != null && !heroHealth.IsDead)
            {
                Vector3 offset = transform.position - consecrationCenter;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius)
                {
                    heroHealth.Heal(heal);
                }
            }

            eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.ConsecrationPulse, consecrationCenter, radius));
            return damaged;
        }

        private void EnsureConsecrationVisual(HeroData data)
        {
            if (consecrationVisual != null || data == null || data.consecrationVisualPrefab == null) return;
            GameObject go = Instantiate(data.consecrationVisualPrefab);
            go.name = "ConsecrationVisual";
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
            consecrationVisual = go.transform;
            go.SetActive(false);
        }

        private void HideConsecrationVisual()
        {
            if (consecrationVisual != null) consecrationVisual.gameObject.SetActive(false);
        }

        // ---------------- Mech Drop (Engineer) ----------------

        private void ActivateTurretDrop()
        {
            isActive = true;
            activeUltimateType = HeroUltimateType.TurretDrop;
            activeRemaining = Mathf.Max(0.3f, ActiveDuration);
            cooldownRemaining = 0f;
            meteorsRemaining = 0;
            arrowWavesRemaining = 0;

            OnStateChanged?.Invoke();
            DropTurrets();
        }

        /// <summary>Drops empowered auto-turrets in a ring around the hero with an impact shockwave. Returns turrets spawned.</summary>
        public int DropTurrets()
        {
            HeroData data = ActiveHeroData;
            int count = data != null ? Mathf.Max(1, data.turretDropCount) : 3;
            float ringRadius = data != null ? Mathf.Max(0.5f, data.turretDropRadius) : 2.6f;
            float searchRadius = data != null ? Mathf.Max(1f, data.turretSearchRadius) : 11f;
            float dps = (data != null ? Mathf.Max(0f, data.turretDps) : 34f) * (runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f);
            float lifetime = data != null ? Mathf.Max(0.5f, data.turretLifetime) : 9f;
            float shockRadius = ScaleArea(data != null ? Mathf.Max(0f, data.turretDropShockwaveRadius) : 4f);
            float shockDamage = data != null ? Mathf.Max(0f, data.turretDropShockwaveDamage) : 45f;

            Vector3 heroPos = transform.position;
            heroPos.y = 0f;
            int mask = ResolveEnemyLayerMask();
            EnsureMechTurretPool(data);

            int spawned = 0;
            float angleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = angleOffset + i * (Mathf.PI * 2f / count);
                Vector3 pos = heroPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;

                if (mechTurretPool != null)
                {
                    LaserTurret turret = mechTurretPool.Get();
                    if (turret != null)
                    {
                        turret.transform.position = pos;
                        turret.Initialize(searchRadius, dps, lifetime, mask, mechTurretPool, runtimeStats, runStatistics, DamageSourceIds.Ultimate, 1.3f);
                        spawned++;
                    }
                }

                DealUltAreaDamage(pos, shockRadius, shockDamage, 1.5f);
                eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.TurretDrop, pos, shockRadius));
            }

            LastTurretDropCount = spawned;
            return spawned;
        }

        private void EnsureMechTurretPool(HeroData data)
        {
            GameObject prefab = data != null ? data.turretDropPrefab : null;
            if (prefab == null) return;
            if (mechTurretPool != null && mechTurretPoolPrefab == prefab) return;

            mechTurretPoolPrefab = prefab;
            if (mechTurretRoot == null)
            {
                mechTurretRoot = new GameObject("MechDropTurrets").transform;
            }

            Transform root = mechTurretRoot;
            mechTurretPool = new UnityEngine.Pool.ObjectPool<LaserTurret>(
                () =>
                {
                    GameObject go = Instantiate(prefab, root);
                    go.name = "MechTurret";
                    var t = go.GetComponent<LaserTurret>();
                    if (t == null) t = go.AddComponent<LaserTurret>();
                    return t;
                },
                t => { if (t != null) t.gameObject.SetActive(true); },
                t => { if (t != null) t.gameObject.SetActive(false); },
                t =>
                {
                    if (t == null) return;
                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 6,
                maxSize: 12);
        }

        private int DealUltAreaDamage(Vector3 center, float radius, float baseDamage, float knockback)
        {
            if (radius <= 0f) return 0;
            int mask = ResolveEnemyLayerMask();
            float rawDamage = baseDamage * (runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f);
            int hits = Physics.OverlapSphereNonAlloc(center, radius, ultDamageBuffer, mask);
            int affected = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider col = ultDamageBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;
                if (damageable is HeroHealth) continue;

                if (rawDamage > 0f)
                {
                    bool isCrit = false;
                    int finalDamage = runtimeStats != null
                        ? runtimeStats.RollDamage(rawDamage, out isCrit)
                        : Mathf.Max(1, Mathf.RoundToInt(rawDamage));
                    if (damageable is EnemyBase enemyDmg)
                    {
                        int dealt = enemyDmg.TakeDamageFrom(finalDamage, isCrit, center, isAoE: true);
                        if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Ultimate, dealt);
                    }
                    else
                    {
                        damageable.TakeDamage(finalDamage);
                    }
                }

                if (knockback > 0f && damageable is EnemyBase pushed && !pushed.IsDead)
                {
                    Vector3 dir = pushed.transform.position - center;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f) pushed.ApplyPull(dir.normalized * knockback);
                }
                affected++;
            }
            return affected;
        }

        private int ResolveEnemyLayerMask()
        {
            if (resolvedEnemyLayerMask != 0) return resolvedEnemyLayerMask;
            if (enemyLayer.value != 0)
            {
                resolvedEnemyLayerMask = enemyLayer.value;
            }
            else
            {
                int mask = LayerMask.GetMask("Enemy", "Enemies");
                resolvedEnemyLayerMask = mask != 0 ? mask : (1 << 8);
            }
            return resolvedEnemyLayerMask;
        }

        private int ResolveObstacleLayerMask()
        {
            if (resolvedObstacleLayerMask == -1)
            {
                resolvedObstacleLayerMask = LayerMask.GetMask("Obstacles");
            }
            return resolvedObstacleLayerMask;
        }

        private void DeactivateUltimate()
        {
            isActive = false;
            activeRemaining = 0f;
            meteorsRemaining = 0;
            arrowWavesRemaining = 0;
            cooldownRemaining = CooldownDuration;

            if (activeUltimateType == HeroUltimateType.Shield)
            {
                heroHealth?.SetInvulnerable(false);
                if (shieldVisual != null)
                {
                    shieldVisual.SetActive(false);
                }
            }
            else if (activeUltimateType == HeroUltimateType.SoulNova)
            {
                HideSoulNovaVisuals();
            }
            else if (activeUltimateType == HeroUltimateType.Consecration)
            {
                HideConsecrationVisual();
            }

            OnStateChanged?.Invoke();
        }

        private void HandleHeroDied()
        {
            if (isActive)
            {
                isActive = false;
                activeRemaining = 0f;
                meteorsRemaining = 0;
                arrowWavesRemaining = 0;
                if (activeUltimateType == HeroUltimateType.Shield)
                {
                    heroHealth?.SetInvulnerable(false);
                    if (shieldVisual != null)
                    {
                        shieldVisual.SetActive(false);
                    }
                }
                else if (activeUltimateType == HeroUltimateType.SoulNova)
                {
                    HideSoulNovaVisuals();
                }
                else if (activeUltimateType == HeroUltimateType.Consecration)
                {
                    HideConsecrationVisual();
                }
                OnStateChanged?.Invoke();
            }
        }
    }
}
