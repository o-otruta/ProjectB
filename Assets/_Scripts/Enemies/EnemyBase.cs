using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Data.Enemies;
using ProjectB.Core.Events;

namespace ProjectB.Enemies
{
    public class EnemyBase : MonoBehaviour, IDamageable, ISlowable
    {
        [SerializeField] protected EnemyData enemyData;

        // Кэш id источника урона по герою (Object.name аллоцирует строку на каждом вызове).
        private string heroDamageSourceId = "EnemyContact";
        protected string HeroDamageSourceId => heroDamageSourceId;
        private IObjectPool<EnemyBase> pool;
        protected Transform target;
        protected GameEventBus eventBus;
        
        protected int currentHp;
        protected int maxHp;
        protected int currentDamage;
        protected float currentDamageMultiplier = 1f;
        private float lastDamageTime;
        protected bool isAlive;
        protected IDamageable targetDamageable;
        protected float slowFactor = 1f;
        private float slowTimer = 0f;

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int HitFlashPropertyId = Shader.PropertyToID("_HitFlash");
        private static readonly int RimColorPropertyId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerPropertyId = Shader.PropertyToID("_RimPower");
        private static readonly Color EliteTintColor = new Color(0.92f, 0.76f, 1.00f, 1f);
        private static readonly Color JuggernautTintColor = new Color(1.00f, 0.90f, 0.72f, 1f);
        private static readonly Color HasteAuraTintColor = new Color(0.76f, 0.96f, 1.00f, 1f);
        private static readonly Color MiniBossTintColor = new Color(1.00f, 0.84f, 0.68f, 1f);
        private static readonly Color EliteRimColor = new Color(0.95f, 0.20f, 1.00f, 1f);
        private static readonly Color JuggernautRimColor = new Color(1.00f, 0.78f, 0.14f, 1f);
        private static readonly Color HasteAuraRimColor = new Color(0.18f, 0.94f, 1.00f, 1f);
        private static readonly Color MiniBossRimColor = new Color(1.00f, 0.45f, 0.10f, 1f);
        private static readonly Color DefaultBossRimColor = new Color(1.00f, 0.72f, 0.22f, 0.85f);
        private static readonly Color HealGlowRimColor = new Color(0.30f, 1.00f, 0.42f, 1f);
        private static readonly Color HealGlowTintColor = new Color(0.78f, 1.00f, 0.80f, 1f);
        private const float HEAL_GLOW_DURATION = 0.6f;
        private const float HIT_FLASH_DURATION = 0.08f;
        private const float HASTE_PULSE_INTERVAL = 0.25f;
        private const float HASTE_BUFF_DURATION = 0.45f;

        private static readonly Collider[] EliteAuraHitBuffer = new Collider[48];
        private static int cachedEnemyMask = -1;

        private Renderer[] enemyRenderers;
        private MaterialPropertyBlock propBlock;
        private float healGlowTimer;
        private float healGlowDuration = HEAL_GLOW_DURATION;
        private bool healGlowRampUp;
        protected Vector3 baseLocalScale = Vector3.one;
        protected float activeScaleMultiplier = 1f;
        private bool baseScaleCaptured;
        private bool isElite;
        private EliteArchetype eliteArchetype;
        protected bool hasBossRim;
        protected bool hasCustomRim;
        protected float eliteSpeedMultiplier = 1f;
        protected float externalHasteMultiplier = 1f;
        private float externalHasteTimer = 0f;
        protected float pullResistance = 1f;
        private float hasteAuraRadius = 3.5f;
        private float hasteAuraSpeedBonus = 0.2f;
        private float nextHasteAuraPulseTime = 0f;
        private float hitFlashTimer;
        protected bool isMiniBoss;
        protected string activeBossId;
        private int shardRewardMultiplier = 1;
        private EnemyRuntimeModifiers runtimeModifiers;

        protected float EventSpeedMultiplier => runtimeModifiers != null ? runtimeModifiers.EventSpeedMultiplier : 1f;

        /// <summary>
        /// Constellation "Boss Damage" bonus applied to all damage taken by bosses and mini-bosses (run-scoped).
        /// </summary>
        protected float BossDamageTakenMultiplier => runtimeModifiers != null ? runtimeModifiers.BossDamageTakenMultiplier : 1f;

        public void SetRuntimeModifiers(EnemyRuntimeModifiers modifiers)
        {
            runtimeModifiers = modifiers;
        }

        // --- Sprint 11: Freeze & Taunt ---
        private float frozenTimer;
        private Transform tauntTarget;
        private IDamageable tauntDamageable;
        private float tauntTimer;
        private Transform heroTargetCache;
        private IDamageable heroDamageableCache;

        public bool IsFrozen => frozenTimer > 0f && isAlive;
        public bool IsTaunted => tauntTimer > 0f && tauntTarget != null;
        public Transform TauntTarget => IsTaunted ? tauntTarget : null;
        public virtual bool CanBeTaunted => !IsBoss;

        public bool IsDead => !isAlive;
        public EnemyData EnemyData => enemyData;
        public bool IsElite => isElite;
        public EliteArchetype EliteType => eliteArchetype;
        public bool IsMiniBoss => isMiniBoss;
        public int ShardRewardMultiplier => shardRewardMultiplier;
        public virtual bool IsBoss => false;
        public virtual bool CanBeLeashed => true;
        public virtual string BossId => activeBossId;

        public void SetShardRewardMultiplier(int mult)
        {
            shardRewardMultiplier = Mathf.Max(1, mult);
        }
        public bool IsHitFlashing => hitFlashTimer > 0f;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public float PullResistance => pullResistance;
        public float EffectiveMoveSpeed => enemyData != null
            ? enemyData.speed * slowFactor * eliteSpeedMultiplier * externalHasteMultiplier * EventSpeedMultiplier * GetAdditionalSpeedMultiplier()
            : 0f;
        public bool IsSlowed => slowTimer > 0f && slowFactor < 0.999f;
        public float CurrentSlowFactor => slowTimer > 0f ? slowFactor : 1f;
        public bool HasExternalSpeedBuff => externalHasteTimer > 0f && externalHasteMultiplier > 1.001f;

        public event System.Action<EnemyBase> OnDied;

        protected static int EnemyMask
        {
            get
            {
                if (cachedEnemyMask == -1)
                {
                    cachedEnemyMask = LayerMask.GetMask("Enemy");
                    if (cachedEnemyMask == 0) cachedEnemyMask = 1 << 8;
                }
                return cachedEnemyMask;
            }
        }

        protected virtual void Awake()
        {
            CaptureBaseScale();
        }

        protected void CaptureBaseScale()
        {
            if (!baseScaleCaptured)
            {
                baseLocalScale = transform.localScale;
                baseScaleCaptured = true;
            }
        }

        public virtual void Initialize(EnemyData data, Transform heroTarget, IObjectPool<EnemyBase> enemyPool, GameEventBus bus, float difficultyMultiplier = 1f, float damageDifficultyMultiplier = -1f)
        {
            CaptureBaseScale();
            activeScaleMultiplier = 1f;
            transform.localScale = baseLocalScale;

            OnDied = null;
            ResetTransientStates();
            enemyData = data;
            heroDamageSourceId = data != null ? data.name : "EnemyContact";
            target = heroTarget;
            pool = enemyPool;
            eventBus = bus;

            float effectiveHpMult = Mathf.Max(0.01f, difficultyMultiplier);
            float effectiveDmgMult = damageDifficultyMultiplier >= 0f
                ? Mathf.Max(0.01f, damageDifficultyMultiplier)
                : effectiveHpMult;

            currentDamageMultiplier = effectiveDmgMult;
            if (enemyData != null)
            {
                maxHp = Mathf.Max(1, Mathf.RoundToInt(enemyData.hp * effectiveHpMult));
                currentHp = maxHp;
                currentDamage = Mathf.Max(1, Mathf.RoundToInt(enemyData.contactDamage * effectiveDmgMult));
            }
            isAlive = true;
            lastDamageTime = 0f;
            slowFactor = 1f;
            slowTimer = 0f;
            currentMoveDir = Vector3.zero;
            nextCheckTime = 0f;
            targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;

            if (enemyRenderers == null)
            {
                CacheEnemyRenderers();
            }
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }

            isElite = false;
            eliteArchetype = EliteArchetype.None;
            hasBossRim = false;
            hasCustomRim = false;
            eliteSpeedMultiplier = 1f;
            externalHasteMultiplier = 1f;
            externalHasteTimer = 0f;
            pullResistance = 1f;
            hasteAuraRadius = 3.5f;
            hasteAuraSpeedBonus = 0.2f;
            nextHasteAuraPulseTime = 0f;
            hitFlashTimer = 0f;
            healGlowTimer = 0f;
            healGlowRampUp = false;
            isMiniBoss = false;
            activeBossId = null;
            shardRewardMultiplier = 1;

            ClearTint();
        }

        private void CacheEnemyRenderers()
        {
            var all = GetComponentsInChildren<MeshRenderer>(true);
            int validCount = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !all[i].gameObject.name.Contains("Telegraph"))
                {
                    validCount++;
                }
            }

            enemyRenderers = new Renderer[validCount];
            int idx = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && !all[i].gameObject.name.Contains("Telegraph"))
                {
                    enemyRenderers[idx++] = all[i];
                }
            }
        }

        public virtual void MakeElite(float hpMultiplier = 3f, float damageMultiplier = 2f)
        {
            MakeElite(
                EliteArchetype.Berserker,
                hpMultiplier,
                damageMultiplier,
                speedMultiplier: 1.25f,
                scaleMultiplier: 1f,
                pullResistanceOverride: 1f,
                auraRadius: 0f,
                auraSpeedBonus: 0f);
        }

        public virtual void MakeElite(EliteArchetype archetype, WaveConfig config)
        {
            if (archetype == EliteArchetype.None) archetype = EliteArchetype.Berserker;

            switch (archetype)
            {
                case EliteArchetype.Juggernaut:
                    MakeElite(
                        EliteArchetype.Juggernaut,
                        config != null ? config.juggernautHpMultiplier : 4.5f,
                        config != null ? config.juggernautDamageMultiplier : 1.5f,
                        speedMultiplier: 1f,
                        scaleMultiplier: config != null ? config.juggernautScaleMultiplier : 1.35f,
                        pullResistanceOverride: config != null ? config.juggernautPullResistance : 0.2f,
                        auraRadius: 0f,
                        auraSpeedBonus: 0f);
                    break;

                case EliteArchetype.HasteAura:
                    MakeElite(
                        EliteArchetype.HasteAura,
                        config != null ? config.hasteAuraHpMultiplier : 3.0f,
                        config != null ? config.hasteAuraDamageMultiplier : 1.5f,
                        speedMultiplier: 1f,
                        scaleMultiplier: 1f,
                        pullResistanceOverride: 1f,
                        auraRadius: config != null ? config.hasteAuraRadius : 3.5f,
                        auraSpeedBonus: config != null ? config.hasteAuraSpeedBonus : 0.2f);
                    break;

                case EliteArchetype.Berserker:
                default:
                    MakeElite(
                        EliteArchetype.Berserker,
                        config != null ? config.eliteHpMultiplier : 3f,
                        config != null ? config.eliteDamageMultiplier : 2f,
                        speedMultiplier: config != null ? config.berserkerSpeedMultiplier : 1.25f,
                        scaleMultiplier: 1f,
                        pullResistanceOverride: 1f,
                        auraRadius: 0f,
                        auraSpeedBonus: 0f);
                    break;
            }
        }

        public virtual void MakeElite(
            EliteArchetype archetype,
            float hpMultiplier,
            float damageMultiplier,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            float pullResistanceOverride = 1f,
            float auraRadius = 3.5f,
            float auraSpeedBonus = 0.2f)
        {
            if (isElite || isMiniBoss || IsBoss || !isAlive) return;
            if (archetype == EliteArchetype.None) archetype = EliteArchetype.Berserker;

            CaptureBaseScale();
            isElite = true;
            eliteArchetype = archetype;

            currentDamageMultiplier *= damageMultiplier;
            maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * hpMultiplier));
            currentHp = maxHp;
            currentDamage = Mathf.Max(1, Mathf.RoundToInt(currentDamage * damageMultiplier));
            eliteSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            activeScaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            transform.localScale = baseLocalScale * activeScaleMultiplier;
            pullResistance = Mathf.Clamp01(pullResistanceOverride);
            hasteAuraRadius = Mathf.Max(0.5f, auraRadius);
            hasteAuraSpeedBonus = Mathf.Max(0f, auraSpeedBonus);
            nextHasteAuraPulseTime = Time.time;

            ApplyVisualState(hitFlashTimer > 0f ? Mathf.Clamp01(hitFlashTimer / HIT_FLASH_DURATION) : 0f);
        }

        public static Color GetEliteTintColor(EliteArchetype archetype)
        {
            return archetype switch
            {
                EliteArchetype.Juggernaut => JuggernautTintColor,
                EliteArchetype.HasteAura => HasteAuraTintColor,
                _ => EliteTintColor
            };
        }

        public static Color GetEliteRimColor(EliteArchetype archetype)
        {
            return archetype switch
            {
                EliteArchetype.Juggernaut => JuggernautRimColor,
                EliteArchetype.HasteAura => HasteAuraRimColor,
                _ => EliteRimColor
            };
        }

        protected virtual float GetAdditionalSpeedMultiplier() => 1f;
        protected virtual Color GetBossRimColor() => DefaultBossRimColor;
        protected virtual Color GetBossBaseTint() => Color.white;
        protected virtual Color GetCustomBaseTint() => Color.white;
        protected virtual Color GetCustomRimColor() => Color.clear;

        public virtual void MakeMiniBoss(float hpMultiplier = 5f, float damageMultiplier = 2f, float scaleMultiplier = 1.85f, string displayName = null)
        {
            if (isMiniBoss || IsBoss || !isAlive) return;
            CaptureBaseScale();
            isMiniBoss = true;

            currentDamageMultiplier *= damageMultiplier;
            maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * hpMultiplier));
            currentHp = maxHp;
            currentDamage = Mathf.Max(1, Mathf.RoundToInt(currentDamage * damageMultiplier));
            activeScaleMultiplier = Mathf.Max(1f, scaleMultiplier);
            transform.localScale = baseLocalScale * activeScaleMultiplier;

            activeBossId = $"miniboss_{(enemyData != null ? enemyData.name.ToLowerInvariant() : "enemy")}";
            string rawName = enemyData != null ? enemyData.name.Replace("Data", "") : "Brute";
            string label = !string.IsNullOrEmpty(displayName)
                ? displayName
                : Loc.Format("ui.hud.miniboss_prefix", rawName);

            ApplyVisualState(hitFlashTimer > 0f ? Mathf.Clamp01(hitFlashTimer / HIT_FLASH_DURATION) : 0f);
            eventBus?.Publish(new BossSpawnedEvent(activeBossId, label, currentHp, maxHp, isMiniBoss: true));
        }

        protected void ApplyVisualState(float hitFlashAmount)
        {
            if (enemyRenderers == null || enemyRenderers.Length == 0) return;

            float clampedFlash = Mathf.Clamp01(hitFlashAmount);
            float healGlow = GetHealGlowStrength();
            if (!isElite && !isMiniBoss && !hasBossRim && !hasCustomRim && clampedFlash <= 0.001f && healGlow <= 0.001f)
            {
                ClearTint();
                return;
            }

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            propBlock.Clear();

            Color baseTint = Color.white;
            Color rimColor = Color.clear;
            float rimPower = 2.6f;

            if (isMiniBoss)
            {
                baseTint = MiniBossTintColor;
                rimColor = MiniBossRimColor;
                rimPower = 2.1f;
            }
            else if (hasBossRim)
            {
                baseTint = GetBossBaseTint();
                rimColor = GetBossRimColor();
                rimPower = 2.2f;
            }
            else if (isElite)
            {
                baseTint = GetEliteTintColor(eliteArchetype);
                rimColor = GetEliteRimColor(eliteArchetype);
                rimPower = 2.1f;
            }
            else if (hasCustomRim)
            {
                baseTint = GetCustomBaseTint();
                rimColor = GetCustomRimColor();
                rimPower = 2.1f;
            }

            if (healGlow > 0.001f)
            {
                // Green heal glow overrides the rim so a healed (or about-to-heal) enemy reads instantly.
                rimColor = Color.Lerp(rimColor, HealGlowRimColor, healGlow);
                rimPower = Mathf.Lerp(rimPower, 1.1f, healGlow);
                baseTint = Color.Lerp(baseTint, HealGlowTintColor, healGlow * 0.6f);
            }

            propBlock.SetColor(BaseColorPropertyId, baseTint);
            propBlock.SetColor(RimColorPropertyId, rimColor);
            propBlock.SetFloat(RimPowerPropertyId, rimPower);
            propBlock.SetFloat(HitFlashPropertyId, clampedFlash);

            for (int i = 0; i < enemyRenderers.Length; i++)
            {
                if (enemyRenderers[i] != null)
                {
                    enemyRenderers[i].SetPropertyBlock(propBlock);
                }
            }
        }

        protected void ApplyTint(Color color)
        {
            if (enemyRenderers == null || enemyRenderers.Length == 0) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            propBlock.SetColor(BaseColorPropertyId, color);
            for (int i = 0; i < enemyRenderers.Length; i++)
            {
                if (enemyRenderers[i] != null)
                {
                    enemyRenderers[i].SetPropertyBlock(propBlock);
                }
            }
        }

        protected void ClearTint()
        {
            if (enemyRenderers == null || enemyRenderers.Length == 0) return;

            for (int i = 0; i < enemyRenderers.Length; i++)
            {
                if (enemyRenderers[i] != null)
                {
                    enemyRenderers[i].SetPropertyBlock(null);
                }
            }
        }

        private static int cachedObstacleMask = -1;
        protected Vector3 currentMoveDir;
        protected float nextCheckTime;

        protected static int ObstacleMask
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

        public void SimulateFrameTick()
        {
            Update();
        }

        protected virtual void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            UpdateHitFlash();
            UpdateHealGlow();
            UpdateFreezeAndTaunt(Time.deltaTime);
            UpdateSlowTimer();
            UpdateSpeedBuffTimer();
            UpdateEliteAura();
            if (IsTargetDead()) return;
            UpdateMovement();
            UpdateAttack();
        }

        private void UpdateHitFlash()
        {
            if (hitFlashTimer <= 0f) return;
            hitFlashTimer -= Time.deltaTime;
            if (hitFlashTimer <= 0f)
            {
                hitFlashTimer = 0f;
                RestoreBaseTint();
            }
            else
            {
                ApplyVisualState(Mathf.Clamp01(hitFlashTimer / HIT_FLASH_DURATION));
            }
        }

        protected virtual void RestoreBaseTint()
        {
            ApplyVisualState(0f);
        }

        /// <summary>
        /// Plays the green heal glow. With <paramref name="rampUp"/> the glow grows over the duration
        /// (healer telegraph); otherwise it starts bright and fades (healed target).
        /// </summary>
        public void PlayHealGlow(float duration = HEAL_GLOW_DURATION, bool rampUp = false)
        {
            if (!isAlive || duration <= 0f) return;
            healGlowDuration = duration;
            healGlowTimer = duration;
            healGlowRampUp = rampUp;
            ApplyVisualState(hitFlashTimer > 0f ? Mathf.Clamp01(hitFlashTimer / HIT_FLASH_DURATION) : 0f);
        }

        public bool IsHealGlowing => healGlowTimer > 0f;

        protected float GetHealGlowStrength()
        {
            if (healGlowTimer <= 0f || healGlowDuration <= 0f) return 0f;
            float t = Mathf.Clamp01(healGlowTimer / healGlowDuration);
            return healGlowRampUp ? 1f - t : t;
        }

        private void UpdateHealGlow()
        {
            if (healGlowTimer <= 0f) return;
            healGlowTimer -= Time.deltaTime;
            if (healGlowTimer <= 0f)
            {
                healGlowTimer = 0f;
                healGlowRampUp = false;
                if (hitFlashTimer <= 0f)
                {
                    RestoreBaseTint();
                    return;
                }
            }
            ApplyVisualState(hitFlashTimer > 0f ? Mathf.Clamp01(hitFlashTimer / HIT_FLASH_DURATION) : 0f);
        }

        private void UpdateSlowTimer()
        {
            if (slowTimer > 0f)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0f)
                {
                    slowTimer = 0f;
                    slowFactor = 1f;
                }
            }
        }

        private void UpdateSpeedBuffTimer()
        {
            if (externalHasteTimer > 0f)
            {
                externalHasteTimer -= Time.deltaTime;
                if (externalHasteTimer <= 0f)
                {
                    externalHasteTimer = 0f;
                    externalHasteMultiplier = 1f;
                }
            }
        }

        private void UpdateEliteAura()
        {
            if (isElite && eliteArchetype == EliteArchetype.HasteAura)
            {
                if (Time.time >= nextHasteAuraPulseTime)
                {
                    nextHasteAuraPulseTime = Time.time + HASTE_PULSE_INTERVAL;
                    PulseHasteAura();
                }
            }
        }

        public int PulseHasteAura()
        {
            if (!isAlive || !isElite || eliteArchetype != EliteArchetype.HasteAura) return 0;

            int found = Physics.OverlapSphereNonAlloc(transform.position, hasteAuraRadius, EliteAuraHitBuffer, EnemyMask);
            if (found <= 0) return 0;

            int buffed = 0;
            float targetMult = 1f + hasteAuraSpeedBonus;
            for (int i = 0; i < found; i++)
            {
                Collider col = EliteAuraHitBuffer[i];
                if (col == null) continue;

                if (!col.TryGetComponent<EnemyBase>(out var ally))
                {
                    ally = col.GetComponentInParent<EnemyBase>();
                }
                if (ally == null || ally == this || ally.IsDead || ally.IsElite || ally.IsMiniBoss || ally.IsBoss)
                {
                    continue;
                }

                ally.ApplySpeedBuff(targetMult, HASTE_BUFF_DURATION);
                buffed++;
            }

            return buffed;
        }

        public void ApplySpeedBuff(float multiplier, float duration)
        {
            if (!isAlive || duration <= 0f) return;
            float safeMult = Mathf.Max(1f, multiplier);
            if (externalHasteTimer <= 0f || safeMult > externalHasteMultiplier)
            {
                externalHasteMultiplier = safeMult;
            }
            if (duration > externalHasteTimer)
            {
                externalHasteTimer = duration;
            }
        }

        protected bool IsTargetDead()
        {
            return (targetDamageable != null && targetDamageable.IsDead);
        }

        protected virtual void UpdateMovement()
        {
            // Простой steering к герою в плоскости XZ
            Vector3 direction = (target.position - transform.position);
            direction.y = 0;
            float distSqr = direction.sqrMagnitude;
            
            if (distSqr > 0.01f)
            {
                direction.Normalize();
                
                // Оптимизация: проверяем препятствия не каждый кадр, а раз в 0.1с
                // Рассинхронизация по времени (Random) размазывает нагрузку по кадрам
                if (Time.time >= nextCheckTime)
                {
                    nextCheckTime = Time.time + 0.1f + Random.Range(0f, 0.05f);
                    currentMoveDir = direction;
                    
                    // SphereCast чуть выше земли, чтобы не цеплять пол
                    Vector3 rayStart = transform.position + Vector3.up * 0.5f;
                    // Проверяем только слой Obstacles (там где стены), игнорируем других врагов
                    int obstacleMask = ObstacleMask; 
                    
                    if (Physics.SphereCast(rayStart, 0.4f, direction, out RaycastHit hit, 0.7f, obstacleMask))
                    {
                        // Игнорируем самого героя, чтобы враги не пытались его "обогнуть" как стену
                        if (!hit.transform.IsChildOf(target))
                        {
                            // Проецируем вектор движения на плоскость стены (скольжение)
                            currentMoveDir = Vector3.ProjectOnPlane(direction, hit.normal).normalized;
                            
                            // Если уперлись строго перпендикулярно, толкаем в сторону
                            if (currentMoveDir.sqrMagnitude < 0.01f)
                            {
                                currentMoveDir = Vector3.Cross(hit.normal, Vector3.up).normalized;
                            }
                        }
                    }
                }

                transform.position += currentMoveDir * (EffectiveMoveSpeed * Time.deltaTime);
                
                if (currentMoveDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(currentMoveDir);
                }
            }
        }

        protected virtual void UpdateAttack()
        {
            Vector3 direction = (target.position - transform.position);
            direction.y = 0;
            float distSqr = direction.sqrMagnitude;

            float radius = enemyData != null ? enemyData.contactRadius : 1.0f;
            float cooldown = enemyData != null ? enemyData.damageCooldown : 1.0f;

            if (distSqr <= radius * radius 
                && Time.time >= lastDamageTime + cooldown)
            {
                if (targetDamageable != null)
                {
                    if (targetDamageable is ProjectB.Player.HeroHealth heroHealth)
                    {
                        string src = IsBoss && !string.IsNullOrEmpty(BossId)
                            ? BossId
                            : heroDamageSourceId;
                        heroHealth.TakeDamage(currentDamage, src);
                    }
                    else
                    {
                        targetDamageable.TakeDamage(currentDamage);
                    }
                    lastDamageTime = Time.time;
                }
            }
        }

        public virtual int Heal(int amount)
        {
            if (!isAlive || amount <= 0 || currentHp >= maxHp) return 0;

            int prevHp = currentHp;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            int healed = currentHp - prevHp;

            if (healed > 0 && (isMiniBoss || IsBoss))
            {
                eventBus?.Publish(new BossHealthChangedEvent(BossId, currentHp, maxHp));
            }

            if (healed > 0)
            {
                PlayHealGlow();
            }

            return healed;
        }

        public virtual void TakeDamage(int amount)
        {
            TakeDamage(amount, isCrit: false);
        }

        public virtual void TakeDamage(int amount, bool isCrit)
        {
            ApplyIncomingDamage(amount, isCrit, hasSourcePosition: false, sourcePosition: default, isAoE: false);
        }

        public virtual int TakeDamageFrom(int amount, bool isCrit, Vector3 sourcePosition, bool isAoE = false)
        {
            return ApplyIncomingDamage(amount, isCrit, hasSourcePosition: true, sourcePosition: sourcePosition, isAoE: isAoE);
        }

        protected virtual int ModifyIncomingDamage(int rawAmount, bool hasSourcePosition, Vector3 sourcePosition, bool isAoE, out bool wasBlocked)
        {
            wasBlocked = false;
            return rawAmount;
        }

        protected virtual int AbsorbIncomingDamage(int finalAmount, bool isCrit, bool wasBlocked)
        {
            return finalAmount;
        }

        protected virtual void OnDamageApplied(int finalAmount, bool isCrit, bool wasBlocked)
        {
        }

        protected virtual void OnBeforeDeathBroadcast()
        {
        }

        private int ApplyIncomingDamage(int amount, bool isCrit, bool hasSourcePosition, Vector3 sourcePosition, bool isAoE)
        {
            if (!isAlive || amount <= 0) return 0;

            if ((isMiniBoss || IsBoss) && !Mathf.Approximately(BossDamageTakenMultiplier, 1f))
            {
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * BossDamageTakenMultiplier));
            }

            int finalAmount = Mathf.Max(1, ModifyIncomingDamage(amount, hasSourcePosition, sourcePosition, isAoE, out bool wasBlocked));
            int hpDamage = AbsorbIncomingDamage(finalAmount, isCrit, wasBlocked);
            if (hpDamage > 0)
            {
                currentHp = Mathf.Max(0, currentHp - hpDamage);
            }

            eventBus?.Publish(new EnemyDamagedEvent(transform.position, finalAmount, isMiniBoss || IsBoss, isCrit, wasBlocked));

            if (isMiniBoss || IsBoss)
            {
                eventBus?.Publish(new BossHealthChangedEvent(BossId, currentHp, maxHp));
            }

            OnDamageApplied(finalAmount, isCrit, wasBlocked);

            if (currentHp <= 0)
            {
                Die();
            }
            else
            {
                hitFlashTimer = HIT_FLASH_DURATION;
                ApplyVisualState(1f);
            }

            return finalAmount;
        }

        protected virtual void Die()
        {
            if (!isAlive) return; // Guard от двойного вызова
            isAlive = false;

            if (isMiniBoss || IsBoss)
            {
                eventBus?.Publish(new BossDefeatedEvent(BossId, isMiniBoss));
            }

            eventBus?.Publish(new EnemyDiedEvent(
                transform.position,
                enemyData,
                isElite,
                wasMiniBoss: isMiniBoss,
                wasBoss: IsBoss,
                bossId: BossId,
                eliteArchetype: eliteArchetype,
                shardMultiplier: shardRewardMultiplier));

            OnBeforeDeathBroadcast();
            OnDied?.Invoke(this);
            ReturnToPool();
        }

        public virtual void Despawn()
        {
            if (!isAlive) return;
            isAlive = false;
            ReturnToPool();
        }

        public virtual void Teleport(Vector3 newPosition)
        {
            if (!isAlive) return;
            transform.position = newPosition;
            currentMoveDir = Vector3.zero;
            nextCheckTime = 0f;
        }

        protected void ReturnToPool()
        {
            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void ApplySlow(float factor, float duration)
        {
            if (factor < slowFactor)
            {
                slowFactor = factor;
            }
            if (duration > slowTimer)
            {
                slowTimer = duration;
            }
        }

        public virtual void ApplyPull(Vector3 pullDelta)
        {
            if (!isAlive || pullResistance <= 0f) return;
            transform.position += pullDelta * pullResistance;
        }

        /// <summary>
        /// Freezes the enemy (near-total slow) and marks it <see cref="IsFrozen"/> for shatter reactions.
        /// Bosses are only heavily slowed.
        /// </summary>
        public virtual void ApplyFreeze(float duration, float slowFactorWhileFrozen = 0.05f)
        {
            if (!isAlive || duration <= 0f) return;
            float factor = IsBoss ? Mathf.Max(0.5f, slowFactorWhileFrozen) : Mathf.Clamp(slowFactorWhileFrozen, 0.01f, 1f);
            ApplySlow(factor, duration);
            if (duration > frozenTimer) frozenTimer = duration;
        }

        public void ClearFreeze()
        {
            frozenTimer = 0f;
        }

        /// <summary>
        /// Forces the enemy to chase (and attack) a decoy instead of the hero for the given duration.
        /// </summary>
        public bool ApplyTaunt(Transform decoy, float duration)
        {
            if (!isAlive || decoy == null || duration <= 0f || !CanBeTaunted) return false;

            if (tauntTarget == null)
            {
                heroTargetCache = target;
                heroDamageableCache = targetDamageable;
            }

            tauntTarget = decoy;
            tauntDamageable = decoy.GetComponent<IDamageable>();
            tauntTimer = Mathf.Max(tauntTimer, duration);
            target = decoy;
            targetDamageable = tauntDamageable;
            return true;
        }

        public void ClearTaunt()
        {
            if (tauntTarget == null) return;
            tauntTarget = null;
            tauntDamageable = null;
            tauntTimer = 0f;
            if (heroTargetCache != null)
            {
                target = heroTargetCache;
                targetDamageable = heroDamageableCache;
            }
            heroTargetCache = null;
            heroDamageableCache = null;
        }

        private void UpdateFreezeAndTaunt(float deltaTime)
        {
            if (frozenTimer > 0f)
            {
                frozenTimer = Mathf.Max(0f, frozenTimer - deltaTime);
            }

            if (tauntTarget != null)
            {
                tauntTimer -= deltaTime;
                bool decoyGone = !tauntTarget.gameObject.activeInHierarchy ||
                                 (tauntDamageable != null && tauntDamageable.IsDead);
                if (tauntTimer <= 0f || decoyGone)
                {
                    ClearTaunt();
                }
            }
        }

        /// <summary>Resets Sprint 11 transient states when reused from the pool.</summary>
        protected void ResetTransientStates()
        {
            frozenTimer = 0f;
            tauntTarget = null;
            tauntDamageable = null;
            tauntTimer = 0f;
            heroTargetCache = null;
            heroDamageableCache = null;
        }
    }
}
