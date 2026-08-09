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
        
        private int currentHp;
        private float lastDamageTime;
        private bool isAlive;

        public bool IsDead => !isAlive;

        public event System.Action<EnemyBase> OnDied;

        /// <summary>Радиус контактного урона (сумма радиусов героя и врага).</summary>
        private const float ContactRadius = 1.0f;

        public void Initialize(EnemyData data, Transform heroTarget, IObjectPool<EnemyBase> enemyPool)
        {
            enemyData = data;
            target = heroTarget;
            pool = enemyPool;
            
            if (enemyData != null)
            {
                currentHp = enemyData.hp;
            }
            isAlive = true;
        }

        private void Update()
        {
            if (!isAlive || target == null || enemyData == null) return;
            
            // Проверяем, не мертв ли герой
            if (target.TryGetComponent<IDamageable>(out var targetDamageable) && targetDamageable.IsDead)
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
                transform.position += direction * (enemyData.speed * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // Проверка контакта с героем по дистанции
            if (distSqr <= ContactRadius * ContactRadius 
                && Time.time >= lastDamageTime + enemyData.damageCooldown)
            {
                if (target.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(enemyData.contactDamage);
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
            if (ProjectB.LevelUp.XpManager.Instance != null && enemyData != null)
            {
                ProjectB.LevelUp.XpManager.Instance.SpawnXp(transform.position, enemyData.xpDrop);
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
