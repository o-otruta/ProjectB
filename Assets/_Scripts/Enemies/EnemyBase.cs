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
        protected int currentDamage;
        private float lastDamageTime;
        protected bool isAlive;
        protected IDamageable targetDamageable;
        protected float slowFactor = 1f;
        private float slowTimer = 0f;

        private Renderer enemyRenderer;
        private Color originalColor;
        private bool hasOriginalColor;
        private bool isElite;

        public bool IsDead => !isAlive;
        public EnemyData EnemyData => enemyData;
        public bool IsElite => isElite;

        public event System.Action<EnemyBase> OnDied;

        public virtual void Initialize(EnemyData data, Transform heroTarget, IObjectPool<EnemyBase> enemyPool, GameEventBus bus, float difficultyMultiplier = 1f)
        {
            OnDied = null;
            enemyData = data;
            target = heroTarget;
            pool = enemyPool;
            eventBus = bus;
            
            if (enemyData != null)
            {
                currentHp = Mathf.RoundToInt(enemyData.hp * difficultyMultiplier);
                currentDamage = Mathf.RoundToInt(enemyData.contactDamage * difficultyMultiplier);
            }
            isAlive = true;
            lastDamageTime = 0f;
            slowFactor = 1f;
            slowTimer = 0f;
            targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;

            if (enemyRenderer == null)
            {
                enemyRenderer = GetComponentInChildren<Renderer>();
                if (enemyRenderer != null && enemyRenderer.material != null)
                {
                    originalColor = enemyRenderer.material.color;
                    hasOriginalColor = true;
                }
            }

            isElite = false;
            if (hasOriginalColor && enemyRenderer != null && enemyRenderer.material != null)
            {
                enemyRenderer.material.color = originalColor;
            }
        }

        public virtual void MakeElite(float hpMultiplier = 3f, float damageMultiplier = 2f)
        {
            if (isElite || !isAlive) return;
            isElite = true;

            currentHp = Mathf.RoundToInt(currentHp * hpMultiplier);
            currentDamage = Mathf.RoundToInt(currentDamage * damageMultiplier);

            if (enemyRenderer != null && enemyRenderer.material != null)
            {
                enemyRenderer.material.color = new Color(0.6f, 0f, 0.8f); // Фиолетовый цвет для элиты
            }
        }

        private Vector3 currentMoveDir;
        private float nextCheckTime;

        protected virtual void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            UpdateSlowTimer();
            if (IsTargetDead()) return;
            UpdateMovement();
            UpdateAttack();
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

        private bool IsTargetDead()
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
                    int obstacleMask = LayerMask.GetMask("Obstacles"); 
                    
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

        public void TakeDamage(int amount)
        {
            if (!isAlive) return;
            
            currentHp -= amount;
            
            if (currentHp <= 0)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            if (!isAlive) return; // Guard от двойного вызова
            isAlive = false;

            eventBus?.Publish(new EnemyDiedEvent(transform.position, enemyData, isElite));

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

        public void ApplyPull(Vector3 pullDelta)
        {
            if (!isAlive) return;
            transform.position += pullDelta;
        }
    }
}
