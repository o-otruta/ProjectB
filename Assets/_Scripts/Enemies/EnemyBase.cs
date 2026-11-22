using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Data.Enemies;
using ProjectB.Core.Events;

namespace ProjectB.Enemies
{
    public class EnemyBase : MonoBehaviour, IDamageable, ISlowable
    {
        [SerializeField] protected EnemyData enemyData;
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
        private static readonly Color EliteTintColor = new Color(0.68f, 0.12f, 0.92f, 1f);
        private static readonly Color JuggernautTintColor = new Color(0.95f, 0.68f, 0.18f, 1f);
        private static readonly Color HasteAuraTintColor = new Color(0.22f, 0.85f, 1f, 1f);
        private static readonly Color MiniBossTintColor = new Color(1f, 0.42f, 0.12f);
        private static readonly Color HitFlashColor = new Color(1f, 0.92f, 0.85f);
        private const float HIT_FLASH_DURATION = 0.08f;
        private const float HASTE_PULSE_INTERVAL = 0.25f;
        private const float HASTE_BUFF_DURATION = 0.45f;

        private static readonly Collider[] EliteAuraHitBuffer = new Collider[48];
        private static int cachedEnemyMask = -1;

        private Renderer[] enemyRenderers;
        private MaterialPropertyBlock propBlock;
        protected Vector3 baseLocalScale = Vector3.one;
        protected float activeScaleMultiplier = 1f;
        private bool baseScaleCaptured;
        private bool isElite;
        private EliteArchetype eliteArchetype;
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

        public bool IsDead => !isAlive;
        public EnemyData EnemyData => enemyData;
        public bool IsElite => isElite;
        public EliteArchetype EliteType => eliteArchetype;
        public bool IsMiniBoss => isMiniBoss;
        public virtual bool IsBoss => false;
        public virtual string BossId => activeBossId;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public float PullResistance => pullResistance;
        public float EffectiveMoveSpeed => enemyData != null ? enemyData.speed * slowFactor * eliteSpeedMultiplier * externalHasteMultiplier : 0f;
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

        private void CaptureBaseScale()
        {
            if (!baseScaleCaptured)
            {
                baseLocalScale = transform.localScale;
                baseScaleCaptured = true;
            }
        }

        public virtual void Initialize(EnemyData data, Transform heroTarget, IObjectPool<EnemyBase> enemyPool, GameEventBus bus, float difficultyMultiplier = 1f)
        {
            CaptureBaseScale();
            activeScaleMultiplier = 1f;
            transform.localScale = baseLocalScale;

            OnDied = null;
            enemyData = data;
            target = heroTarget;
            pool = enemyPool;
            eventBus = bus;
            
            currentDamageMultiplier = difficultyMultiplier;
            if (enemyData != null)
            {
                maxHp = Mathf.Max(1, Mathf.RoundToInt(enemyData.hp * difficultyMultiplier));
                currentHp = maxHp;
                currentDamage = Mathf.Max(1, Mathf.RoundToInt(enemyData.contactDamage * difficultyMultiplier));
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
                enemyRenderers = GetComponentsInChildren<Renderer>(true);
            }
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }

            isElite = false;
            eliteArchetype = EliteArchetype.None;
            eliteSpeedMultiplier = 1f;
            externalHasteMultiplier = 1f;
            externalHasteTimer = 0f;
            pullResistance = 1f;
            hasteAuraRadius = 3.5f;
            hasteAuraSpeedBonus = 0.2f;
            nextHasteAuraPulseTime = 0f;
            hitFlashTimer = 0f;
            isMiniBoss = false;
            activeBossId = null;

            ClearTint();
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
                auraSpeedBonus: 0f,
                auraMaterial: null);
        }

        public virtual void MakeElite(EliteArchetype archetype, WaveConfig config)
        {
            if (archetype == EliteArchetype.None) archetype = EliteArchetype.Berserker;

            Material auraMat = config != null ? config.eliteAuraMaterial : null;
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
                        auraSpeedBonus: 0f,
                        auraMaterial: auraMat);
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
                        auraSpeedBonus: config != null ? config.hasteAuraSpeedBonus : 0.2f,
                        auraMaterial: auraMat);
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
                        auraSpeedBonus: 0f,
                        auraMaterial: auraMat);
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
            float auraSpeedBonus = 0.2f,
            Material auraMaterial = null)
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

            Color tint = GetEliteTintColor(archetype);
            ApplyTint(tint);
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
            string label = !string.IsNullOrEmpty(displayName)
                ? displayName
                : $"Mini-Boss: {(enemyData != null ? enemyData.name.Replace("Data", "") : "Brute")}";

            ApplyTint(MiniBossTintColor);
            eventBus?.Publish(new BossSpawnedEvent(activeBossId, label, currentHp, maxHp, isMiniBoss: true));
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
        }

        protected virtual void RestoreBaseTint()
        {
            if (isMiniBoss)
            {
                ApplyTint(MiniBossTintColor);
            }
            else if (isElite)
            {
                ApplyTint(GetEliteTintColor(eliteArchetype));
            }
            else
            {
                ClearTint();
            }
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
                    targetDamageable.TakeDamage(currentDamage);
                    lastDamageTime = Time.time;
                }
            }
        }

        public virtual void TakeDamage(int amount)
        {
            TakeDamage(amount, isCrit: false);
        }

        public virtual void TakeDamage(int amount, bool isCrit)
        {
            if (!isAlive || amount <= 0) return;
            
            currentHp = Mathf.Max(0, currentHp - amount);

            eventBus?.Publish(new EnemyDamagedEvent(transform.position, amount, isMiniBoss || IsBoss, isCrit));

            if (isMiniBoss || IsBoss)
            {
                eventBus?.Publish(new BossHealthChangedEvent(BossId, currentHp, maxHp));
            }
            
            if (currentHp <= 0)
            {
                Die();
            }
            else
            {
                hitFlashTimer = HIT_FLASH_DURATION;
                ApplyTint(HitFlashColor);
            }
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
                eliteArchetype: eliteArchetype));

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

        private void ReturnToPool()
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
    }
}
