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
        private static readonly Color EliteTintColor = new Color(0.6f, 0f, 0.8f);
        private static readonly Color MiniBossTintColor = new Color(1f, 0.42f, 0.12f);
        private static readonly Color HitFlashColor = new Color(1f, 0.92f, 0.85f);
        private const float HIT_FLASH_DURATION = 0.08f;

        private Renderer[] enemyRenderers;
        private MaterialPropertyBlock propBlock;
        private Vector3 baseLocalScale = Vector3.one;
        private bool baseScaleCaptured;
        private bool isElite;
        private float hitFlashTimer;
        protected bool isMiniBoss;
        protected string activeBossId;

        public bool IsDead => !isAlive;
        public EnemyData EnemyData => enemyData;
        public bool IsElite => isElite;
        public bool IsMiniBoss => isMiniBoss;
        public virtual bool IsBoss => false;
        public virtual string BossId => activeBossId;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;

        public event System.Action<EnemyBase> OnDied;

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

            isElite = false;
            isMiniBoss = false;
            hitFlashTimer = 0f;
            activeBossId = null;
            ClearTint();
        }

        public virtual void MakeElite(float hpMultiplier = 3f, float damageMultiplier = 2f)
        {
            if (isElite || isMiniBoss || IsBoss || !isAlive) return;
            isElite = true;

            currentDamageMultiplier *= damageMultiplier;
            maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * hpMultiplier));
            currentHp = maxHp;
            currentDamage = Mathf.Max(1, Mathf.RoundToInt(currentDamage * damageMultiplier));

            ApplyTint(EliteTintColor);
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
            transform.localScale = baseLocalScale * Mathf.Max(1f, scaleMultiplier);

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

        protected virtual void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            UpdateHitFlash();
            UpdateSlowTimer();
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

        private void RestoreBaseTint()
        {
            if (isMiniBoss)
            {
                ApplyTint(MiniBossTintColor);
            }
            else if (isElite)
            {
                ApplyTint(EliteTintColor);
            }
            else
            {
                ClearTint();
            }
        }

        private void UpdateSlowTimer()
        {
            if (slowTimer > 0)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0)
                {
                    slowFactor = 1f;
                }
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

                transform.position += currentMoveDir * (enemyData.speed * slowFactor * Time.deltaTime);
                
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
                bossId: BossId));

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
            if (!isAlive) return;
            transform.position += pullDelta;
        }
    }
}
