using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    public class EnemyBase : MonoBehaviour, IDamageable, ISlowable
    {
        [SerializeField] private EnemyData enemyData;
        private IObjectPool<EnemyBase> pool;
        private Transform target;
        private ProjectB.LevelUp.XpManager xpManager;
        private ProjectB.LevelUp.CoinManager coinManager;
        private ProjectB.Core.RunStatistics runStatistics;
        
        private int currentHp;
        private int currentDamage;
        private float lastDamageTime;
        private bool isAlive;
        private IDamageable targetDamageable;
        private float slowFactor = 1f;
        private float slowTimer = 0f;

        private Renderer enemyRenderer;
        private Color originalColor;
        private bool hasOriginalColor;
        private bool isElite;

        public bool IsDead => !isAlive;

        public event System.Action<EnemyBase> OnDied;

        /// <summary>Радиус контактного урона (сумма радиусов героя и врага).</summary>
        private const float ContactRadius = 1.0f;

        public void Initialize(EnemyData data, Transform heroTarget, IObjectPool<EnemyBase> enemyPool, float difficultyMultiplier = 1f, ProjectB.LevelUp.XpManager xpManager = null, ProjectB.LevelUp.CoinManager coinManager = null, ProjectB.Core.RunStatistics runStatistics = null)
        {
            OnDied = null;
            enemyData = data;
            target = heroTarget;
            pool = enemyPool;
            this.xpManager = xpManager;
            this.coinManager = coinManager;
            this.runStatistics = runStatistics;
            
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

        public void MakeElite()
        {
            if (isElite || !isAlive) return;
            isElite = true;

            currentHp *= 3;
            currentDamage *= 2;

            if (enemyRenderer != null && enemyRenderer.material != null)
            {
                enemyRenderer.material.color = new Color(0.6f, 0f, 0.8f); // Фиолетовый цвет для элиты
            }
        }

        private Vector3 currentMoveDir;
        private float nextCheckTime;

        private void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            
            if (slowTimer > 0)
            {
                slowTimer -= Time.deltaTime;
                if (slowTimer <= 0)
                {
                    slowFactor = 1f;
                }
            }

            // Проверяем, не мертв ли герой
            if (targetDamageable != null && targetDamageable.IsDead)
            {
                // Герой мертв, можно остановить врагов или переключить в Idle
                return;
            }

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

            if (distSqr <= ContactRadius * ContactRadius 
                && Time.time >= lastDamageTime + enemyData.damageCooldown)
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

        private void Die()
        {
            if (!isAlive) return; // Guard от двойного вызова
            isAlive = false;
            
            Vector3 GetRandomOffset() 
            {
                Vector2 rand = Random.insideUnitCircle * 0.5f;
                return new Vector3(rand.x, 0f, rand.y);
            }
            
            // Spawn XP-crystal
            if (xpManager != null && enemyData != null)
            {
                xpManager.SpawnXp(transform.position + GetRandomOffset(), enemyData.xpDrop);
            }
            
            // Спавн монет (например, 20% шанс)
            if (coinManager != null && Random.value < 0.2f)
            {
                coinManager.SpawnCoin(transform.position + GetRandomOffset(), 1);
            }

            if (runStatistics != null)
            {
                runStatistics.AddKill();
            }
            
            OnDied?.Invoke(this);
            ReturnToPool();
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
