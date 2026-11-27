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
    /// Idle: pulsing orange rim + fuse sparks so it reads as interactive among decorations.
    /// Hero touch kicks the barrel away and lights a short fuse (<see cref="BarrelConfig.fuseDuration"/>);
    /// a rolling barrel detonates early on enemy contact. Any damage detonates it instantly.
    /// Deals high AoE damage to nearby enemies and chain-detonates adjacent barrels without harming the hero.
    /// Zero-GC in <c>Update</c>.
    /// </summary>
    public class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        private const int MAX_OVERLAP_HITS = 48;
        private const int MAX_CONTACT_HITS = 8;
        private const float MIN_ROLL_SPEED = 0.05f;
        private const float KICK_TILT_DEGREES = 18f;

        private static readonly int RimColorPropertyId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerPropertyId = Shader.PropertyToID("_RimPower");
        private static readonly int HitFlashPropertyId = Shader.PropertyToID("_HitFlash");

        private static BarrelConfig s_fallbackConfig;

        [Header("Stats")]
        [SerializeField] private int maxHp = 1;
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private int explosionDamage = 150;
        [SerializeField] private float heroTouchRadius = 1.25f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Visuals")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Optional looping spark emitter on the fuse; emission rate is boosted while the fuse burns.")]
        [SerializeField] private ParticleSystem fuseSpark;
        [SerializeField, Min(0f)] private float idleSparkRate = 6f;
        [SerializeField, Min(0f)] private float litSparkRate = 45f;

        private readonly Collider[] overlapBuffer = new Collider[MAX_OVERLAP_HITS];
        private readonly IDamageable[] targetBuffer = new IDamageable[MAX_OVERLAP_HITS];
        private static readonly Collider[] ContactBuffer = new Collider[MAX_CONTACT_HITS];

        private HeroHealth heroHealth;
        private GameEventBus eventBus;
        private RunStatistics runStatistics;
        private BarrelConfig config;
        private Collider cachedCollider;
        private Renderer[] visualRenderers;
        private MaterialPropertyBlock propBlock;
        private Vector3 visualBaseScale = Vector3.one;
        private float pulsePhase;
        private int currentHp;
        private bool isDetonated;
        private bool isLit;
        private float fuseRemaining;
        private Vector3 kickVelocity;
        private int resolvedEnemyMask;
        private int obstacleMask = -1;

        public bool IsDead => isDetonated;
        public bool IsLit => isLit;
        public float FuseRemaining => fuseRemaining;
        public Vector3 KickVelocity => kickVelocity;
        public float ExplosionRadius => explosionRadius;
        public int ExplosionDamage => explosionDamage;
        public BarrelConfig Config => ResolveConfig();

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            currentHp = Mathf.Max(1, maxHp);
            CacheVisuals();
        }

        public void Initialize(HeroHealth heroHealth, GameEventBus eventBus, RunStatistics runStatistics = null, BarrelConfig config = null)
        {
            this.heroHealth = heroHealth;
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            if (config != null) this.config = config;
            currentHp = Mathf.Max(1, maxHp);
            isDetonated = false;
            isLit = false;
            fuseRemaining = 0f;
            kickVelocity = Vector3.zero;

            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }
            if (cachedCollider != null)
            {
                cachedCollider.enabled = true;
            }

            CacheVisuals();
            pulsePhase = Mathf.Abs(transform.position.x * 0.37f + transform.position.z * 0.61f) % (Mathf.PI * 2f);
            ResetVisualState();

            gameObject.SetActive(true);
            ApplyVisualState(0f);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances idle visuals / hero touch / fuse and kick roll by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (isDetonated || dt <= 0f) return;

            if (isLit)
            {
                TickLit(dt);
                return;
            }

            if (heroHealth != null && !heroHealth.IsDead)
            {
                Vector3 diff = transform.position - heroHealth.transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude <= heroTouchRadius * heroTouchRadius)
                {
                    Kick(diff);
                    return;
                }
            }

            UpdateIdleVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isDetonated || isLit || other == null) return;

            if (heroHealth != null &&
                (other.transform == heroHealth.transform || other.transform.IsChildOf(heroHealth.transform)))
            {
                Vector3 diff = transform.position - heroHealth.transform.position;
                diff.y = 0f;
                Kick(diff);
            }
        }

        /// <summary>
        /// Lights the fuse and rolls the barrel along <paramref name="direction"/> (XZ).
        /// Returns false if the barrel is already lit or detonated.
        /// </summary>
        public bool Kick(Vector3 direction)
        {
            if (isDetonated || isLit) return false;

            BarrelConfig cfg = ResolveConfig();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = heroHealth != null ? heroHealth.transform.forward : Vector3.forward;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            isLit = true;
            fuseRemaining = Mathf.Max(0.05f, cfg.fuseDuration);
            kickVelocity = direction * cfg.kickSpeed;
            SetSparkRate(litSparkRate);

            eventBus?.Publish(new BarrelIgnitedEvent(transform.position, direction));
            ApplyVisualState(0f);
            return true;
        }

        private void TickLit(float dt)
        {
            BarrelConfig cfg = ResolveConfig();

            float speed = kickVelocity.magnitude;
            if (speed > MIN_ROLL_SPEED)
            {
                Vector3 dir = kickVelocity / speed;
                float step = speed * dt;
                int mask = ResolveObstacleMask();
                Vector3 origin = transform.position + Vector3.up * 0.6f;
                if (mask != 0 && Physics.SphereCast(origin, 0.45f, dir, out RaycastHit hit, step + 0.05f, mask, QueryTriggerInteraction.Ignore))
                {
                    step = Mathf.Max(0f, hit.distance - 0.05f);
                    kickVelocity = Vector3.zero;
                }
                else
                {
                    float newSpeed = Mathf.Max(0f, speed - cfg.kickDeceleration * dt);
                    kickVelocity = dir * newSpeed;
                }

                if (step > 0f)
                {
                    transform.position += dir * step;
                }

                if (visualRoot != null)
                {
                    // Tilt the visual child forward while sliding (upright barrel "skids" away from the kick).
                    float speedNorm = cfg.kickSpeed > 0f ? Mathf.Clamp01(kickVelocity.magnitude / cfg.kickSpeed) : 0f;
                    Vector3 axis = Vector3.Cross(Vector3.up, dir);
                    visualRoot.rotation = Quaternion.AngleAxis(KICK_TILT_DEGREES * speedNorm, axis) * transform.rotation;
                }

                if (cfg.detonateOnEnemyContact && HasEnemyContact(cfg.enemyContactRadius))
                {
                    Detonate();
                    return;
                }
            }

            fuseRemaining -= dt;
            if (fuseRemaining <= 0f)
            {
                Detonate();
                return;
            }

            float fuseTotal = Mathf.Max(0.05f, cfg.fuseDuration);
            ApplyVisualState(1f - Mathf.Clamp01(fuseRemaining / fuseTotal));
        }

        private bool HasEnemyContact(float radius)
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.6f, radius, ContactBuffer, ResolveEnemyLayerMask(), QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits; i++)
            {
                Collider col = ContactBuffer[i];
                ContactBuffer[i] = null;
                if (col == null || col == cachedCollider) continue;
                EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    for (int j = i + 1; j < hits; j++) ContactBuffer[j] = null;
                    return true;
                }
            }
            return false;
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
            return DetonateInternal(0);
        }

        private int DetonateInternal(int chainDepth)
        {
            if (isDetonated) return 0;
            isDetonated = true;
            isLit = false;
            kickVelocity = Vector3.zero;

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

            // Publish before chaining so listeners see the chain in order (root first).
            int enemiesHit = 0;
            for (int i = 0; i < targetCount; i++)
            {
                IDamageable target = targetBuffer[i];
                if (target == null || target.IsDead || target is ExplosiveBarrel) continue;

                if (target is EnemyBase enemy)
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

            ResetVisualState();
            eventBus?.Publish(new BarrelExplodedEvent(transform.position, explosionRadius, enemiesHit, chainDepth));
            gameObject.SetActive(false);

            for (int i = 0; i < targetCount; i++)
            {
                IDamageable target = targetBuffer[i];
                targetBuffer[i] = null;
                if (target is ExplosiveBarrel adjacentBarrel && !adjacentBarrel.IsDead)
                {
                    adjacentBarrel.DetonateInternal(chainDepth + 1);
                }
            }

            return enemiesHit;
        }

        private void CacheVisuals()
        {
            if (visualRenderers != null) return;
            Transform root = visualRoot != null ? visualRoot : transform;
            visualRenderers = root.GetComponentsInChildren<Renderer>(true);
            if (visualRoot != null) visualBaseScale = visualRoot.localScale;
            propBlock ??= new MaterialPropertyBlock();
        }

        private void UpdateIdleVisuals()
        {
            if (visualRenderers == null || visualRenderers.Length == 0) return;

            bool anyVisible = false;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null && visualRenderers[i].isVisible)
                {
                    anyVisible = true;
                    break;
                }
            }
            if (!anyVisible) return;

            ApplyVisualState(0f);
        }

        /// <param name="fuseProgress">0 = idle (or fuse just lit), 1 = about to explode. Ignored while idle.</param>
        private void ApplyVisualState(float fuseProgress)
        {
            if (visualRenderers == null || propBlock == null) return;
            BarrelConfig cfg = ResolveConfig();
            float t = Time.time;

            Color rim;
            float flash = 0f;
            float scaleMul;

            if (isLit)
            {
                // Flash accelerates towards the end of the fuse.
                float freq = cfg.fuseFlashFrequency * (1f + 2f * fuseProgress);
                float wave = Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.5f + 0.5f;
                flash = wave * (0.35f + 0.55f * fuseProgress);
                rim = cfg.fuseRimColor;
                scaleMul = 1f + cfg.fuseSwell * fuseProgress + 0.04f * wave;
            }
            else
            {
                float wave = Mathf.Sin(t * cfg.idlePulseSpeed + pulsePhase) * 0.5f + 0.5f;
                float intensity = Mathf.Lerp(cfg.idlePulseMin, 1f, wave);
                rim = cfg.idleRimColor * intensity;
                rim.a = 1f;
                scaleMul = 1f + cfg.idleWobbleAmplitude * (wave - 0.5f);
            }

            propBlock.SetColor(RimColorPropertyId, rim);
            propBlock.SetFloat(RimPowerPropertyId, cfg.idleRimPower);
            propBlock.SetFloat(HitFlashPropertyId, flash);
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null) visualRenderers[i].SetPropertyBlock(propBlock);
            }

            if (visualRoot != null)
            {
                if (isLit)
                {
                    // Uniform swell while the fuse burns.
                    visualRoot.localScale = visualBaseScale * scaleMul;
                }
                else
                {
                    // Gentle squash & stretch "breathing" in idle (volume-preserving-ish).
                    float squash = 2f - scaleMul;
                    visualRoot.localScale = new Vector3(
                        visualBaseScale.x * squash,
                        visualBaseScale.y * scaleMul,
                        visualBaseScale.z * squash);
                }
            }
        }

        private void ResetVisualState()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = visualBaseScale;
                visualRoot.localRotation = Quaternion.identity;
            }
            SetSparkRate(idleSparkRate);
            if (visualRenderers == null) return;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null) visualRenderers[i].SetPropertyBlock(null);
            }
        }

        private void SetSparkRate(float rate)
        {
            if (fuseSpark == null) return;
            var emission = fuseSpark.emission;
            emission.rateOverTime = rate;
        }

        private BarrelConfig ResolveConfig()
        {
            if (config != null) return config;
            if (s_fallbackConfig == null)
            {
                s_fallbackConfig = Resources.Load<BarrelConfig>(BarrelConfig.DEFAULT_RESOURCE_PATH);
                if (s_fallbackConfig == null)
                {
                    s_fallbackConfig = ScriptableObject.CreateInstance<BarrelConfig>();
                    s_fallbackConfig.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            config = s_fallbackConfig;
            return config;
        }

        private int ResolveObstacleMask()
        {
            if (obstacleMask < 0) obstacleMask = LayerMask.GetMask("Obstacles");
            return obstacleMask;
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
