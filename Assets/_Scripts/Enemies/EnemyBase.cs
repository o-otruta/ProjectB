using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        [SerializeField] private EnemyData enemyData;
        private IObjectPool<EnemyBase> pool;
        private Transform target;
        private ProjectB.LevelUp.XpManager xpManager;
        
        private int currentHp;
        private int currentDamage;
        private float lastDamageTime;
        private bool isAlive;
        private IDamageable targetDamageable;

        public bool IsDead => !isAlive;

        public event System.Action<EnemyBase> OnDied;

        /// <summary>Радиус контактного урона (сумма радиусов героя и врага).</summary>
        private const float ContactRadius = 1.0f;

        public void Initialize(EnemyData data, Transform heroTarget, IObjectPool<EnemyBase> enemyPool, float difficultyMultiplier = 1f, ProjectB.LevelUp.XpManager xpManager = null)
        {
            OnDied = null;
            enemyData = data;
            target = heroTarget;
            pool = enemyPool;
            this.xpManager = xpManager;
            
            if (enemyData != null)
            {
                currentHp = Mathf.RoundToInt(enemyData.hp * difficultyMultiplier);
                currentDamage = Mathf.RoundToInt(enemyData.contactDamage * difficultyMultiplier);
            }
            isAlive = true;
            lastDamageTime = 0f;
            targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;
        }

        private Vector3 currentMoveDir;
        private float nextCheckTime;

        private void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            
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

                transform.position += currentMoveDir * (enemyData.speed * Time.deltaTime);
                
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
            
            // Spawn XP-crystal
            if (xpManager != null && enemyData != null)
            {
                xpManager.SpawnXp(transform.position, enemyData.xpDrop);
            }
            
            // TODO: Спавн монет
            
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
    }
}
