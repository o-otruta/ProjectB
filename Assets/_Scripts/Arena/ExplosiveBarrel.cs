using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Arena
{
    /// <summary>
    /// Interactive voxel powder barrel placed around the arena.
    /// Detonates on damage or hero touch, dealing high AoE damage to nearby enemies
    /// and chain-detonating adjacent barrels without harming the hero.
    /// </summary>
    public class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        private const int MAX_OVERLAP_HITS = 48;

        [Header("Stats")]
        [SerializeField] private int maxHp = 1;
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private int explosionDamage = 150;
        [SerializeField] private float heroTouchRadius = 1.25f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Visuals")]
        [SerializeField] private Transform visualRoot;

        private readonly Collider[] overlapBuffer = new Collider[MAX_OVERLAP_HITS];
        private readonly IDamageable[] targetBuffer = new IDamageable[MAX_OVERLAP_HITS];

        private HeroHealth heroHealth;
        private GameEventBus eventBus;
        private RunStatistics runStatistics;
        private Collider cachedCollider;
        private int currentHp;
        private bool isDetonated;
        private int resolvedEnemyMask;

        public bool IsDead => isDetonated;
        public float ExplosionRadius => explosionRadius;
        public int ExplosionDamage => explosionDamage;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            currentHp = Mathf.Max(1, maxHp);
        }

        public void Initialize(HeroHealth heroHealth, GameEventBus eventBus, RunStatistics runStatistics = null)
        {
            this.heroHealth = heroHealth;
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            currentHp = Mathf.Max(1, maxHp);
            isDetonated = false;

            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }
            if (cachedCollider != null)
            {
                cachedCollider.enabled = true;
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (isDetonated || Time.deltaTime <= 0f) return;

            if (heroHealth != null && !heroHealth.IsDead)
            {
                Vector3 diff = heroHealth.transform.position - transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude <= heroTouchRadius * heroTouchRadius)
                {
                    Detonate();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isDetonated || other == null) return;

            if (heroHealth != null &&
                (other.transform == heroHealth.transform || other.transform.IsChildOf(heroHealth.transform)))
            {
                Detonate();
            }
        }

        public void TakeDamage(int amount)
        {
            if (isDetonated || amount <= 0) return;

            currentHp -= amount;
            if (currentHp <= 0)
            {
                Detonate();
            }
        }

        public int Detonate()
        {
            if (isDetonated) return 0;
            isDetonated = true;

            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }
            if (cachedCollider != null)
            {
                cachedCollider.enabled = false;
            }

            int mask = ResolveEnemyLayerMask();
            int hits = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, overlapBuffer, mask);
            int targetCount = 0;

            for (int i = 0; i < hits && targetCount < targetBuffer.Length; i++)
            {
                Collider col = overlapBuffer[i];
                if (col == null) continue;

                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = col.GetComponentInParent<IDamageable>();
                }

                if (damageable == null || ReferenceEquals(damageable, this) || damageable.IsDead) continue;
                if (damageable is HeroHealth) continue;

                bool duplicate = false;
                for (int j = 0; j < targetCount; j++)
                {
                    if (ReferenceEquals(targetBuffer[j], damageable))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                targetBuffer[targetCount++] = damageable;
            }

            int enemiesHit = 0;
            for (int i = 0; i < targetCount; i++)
            {
                IDamageable target = targetBuffer[i];
                targetBuffer[i] = null;
                if (target == null || target.IsDead) continue;

                if (target is ExplosiveBarrel adjacentBarrel)
                {
                    adjacentBarrel.TakeDamage(1);
                }
                else if (target is EnemyBase enemy)
                {
                    int dealt = enemy.TakeDamageFrom(explosionDamage, isCrit: false, transform.position, isAoE: true);
                    if (dealt > 0) runStatistics?.RecordDamage(DamageSourceIds.Barrel, dealt);
                    enemiesHit++;
                }
                else
                {
                    target.TakeDamage(explosionDamage);
                    enemiesHit++;
                }
            }

            eventBus?.Publish(new BarrelExplodedEvent(transform.position, explosionRadius, enemiesHit));
            gameObject.SetActive(false);
            return enemiesHit;
        }

        private int ResolveEnemyLayerMask()
        {
            if (resolvedEnemyMask != 0) return resolvedEnemyMask;
            if (enemyLayer.value != 0)
            {
                resolvedEnemyMask = enemyLayer.value;
            }
            else
            {
                int mask = LayerMask.GetMask("Enemy", "Enemies");
                resolvedEnemyMask = mask != 0 ? mask : (1 << 8);
            }
            return resolvedEnemyMask;
        }
    }
}
