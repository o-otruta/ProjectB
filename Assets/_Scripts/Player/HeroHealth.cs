using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;
using ProjectB.Core.Events;

namespace ProjectB.Player
{
    public class HeroHealth : MonoBehaviour, IDamageable
    {
        private static readonly int HitFlashPropertyId = Shader.PropertyToID("_HitFlash");
        private const float HERO_HIT_FLASH_DURATION = 0.14f;

        [SerializeField] private HeroData heroData;
        [SerializeField] private RunRulesConfig runRulesConfig;
        [SerializeField] [Min(0f)] private float postHitInvulnerabilityDuration = 0.18f;

        [Header("Keystone: Second Wind (Constellation Tree)")]
        [SerializeField] [Range(0.05f, 1f)] private float secondWindHpPercent = 0.3f;
        [SerializeField] [Min(0f)] private float secondWindInvulnerability = 2.5f;
        [SerializeField] [Min(0f)] private float secondWindKnockbackRadius = 5f;
        [SerializeField] [Min(0f)] private float secondWindKnockbackDistance = 3f;

        private static readonly Collider[] secondWindBuffer = new Collider[96];
        private bool secondWindUsed;
        private float regenAccumulator;
        private RunRulesConfig cachedDefaultRules;
        private bool triedLoadDefaultRules;

        public event System.Action<int, int> OnHealthChanged;
        public event System.Action<int> OnDamageTaken;
        public event System.Action OnDied;

        private int currentHp;
        private int currentMaxHp;
        private bool isDead;
        private bool isInvulnerable;
        private bool isGodMode;
        private float reviveInvulnerabilityTimer;
        private float postHitInvulnerabilityTimer;
        private float hitFlashTimer;
        private Renderer[] heroRenderers;
        private MaterialPropertyBlock hitFlashPropBlock;
        private HeroRuntimeStats runtimeStats;
        private GameEventBus eventBus;
        private SelectedHeroService selectedHeroService;

        public bool IsDead => isDead;
        public bool IsInvulnerable => isInvulnerable || isGodMode || reviveInvulnerabilityTimer > 0f || postHitInvulnerabilityTimer > 0f;
        public bool IsPostHitInvulnerable => postHitInvulnerabilityTimer > 0f;
        public bool IsHitFlashing => hitFlashTimer > 0f;
        public float PostHitInvulnerabilityDuration => BasePostHitInvulnerabilityDuration + (runtimeStats != null ? runtimeStats.BonusIFrameSeconds : 0f);
        public bool HasSecondWindAvailable => runtimeStats != null && runtimeStats.HasSecondWind && !secondWindUsed;
        public bool SecondWindUsed => secondWindUsed;
        public float SecondWindHpPercent => ResolveRunRulesConfig() is RunRulesConfig cfg ? cfg.secondWindHpPercent : secondWindHpPercent;
        public float SecondWindInvulnerability => ResolveRunRulesConfig() is RunRulesConfig cfg ? cfg.secondWindInvulnerability : secondWindInvulnerability;
        public float SecondWindKnockbackRadius => ResolveRunRulesConfig() is RunRulesConfig cfg ? cfg.secondWindKnockbackRadius : secondWindKnockbackRadius;
        public float SecondWindKnockbackDistance => ResolveRunRulesConfig() is RunRulesConfig cfg ? cfg.secondWindKnockbackDistance : secondWindKnockbackDistance;
        private float BasePostHitInvulnerabilityDuration
        {
            get
            {
                if (runRulesConfig != null) return runRulesConfig.postHitInvulnerabilityDuration;
                if (!Mathf.Approximately(postHitInvulnerabilityDuration, 0.18f)) return postHitInvulnerabilityDuration;
                return ResolveRunRulesConfig() is RunRulesConfig cfg ? cfg.postHitInvulnerabilityDuration : postHitInvulnerabilityDuration;
            }
        }

        public void SetRunRulesConfig(RunRulesConfig rules)
        {
            runRulesConfig = rules;
        }

        private RunRulesConfig ResolveRunRulesConfig()
        {
            if (runRulesConfig != null) return runRulesConfig;
            if (!triedLoadDefaultRules)
            {
                triedLoadDefaultRules = true;
                cachedDefaultRules = Resources.Load<RunRulesConfig>(RunRulesConfig.DEFAULT_RESOURCE_PATH);
            }
            return cachedDefaultRules;
        }
        public bool IsGodMode => isGodMode;
        public int CurrentHp => currentHp;
        public int MaxHp => currentMaxHp;
        public float Armor => runtimeStats != null ? runtimeStats.Armor : 0f;
        public HeroData ActiveHeroData => selectedHeroService?.GetSelectedHero() ?? heroData;

        [Inject]
        public void Construct(HeroRuntimeStats runtimeStats, GameEventBus eventBus, SelectedHeroService selectedHeroService = null)
        {
            this.runtimeStats = runtimeStats;
            this.eventBus = eventBus;
            this.selectedHeroService = selectedHeroService;
        }

        private void Start()
        {
            HeroData activeHero = ActiveHeroData;
            int baseMaxHp = 100;
            if (activeHero != null)
            {
                baseMaxHp = activeHero.maxHp;
                if (selectedHeroService == null || selectedHeroService.GetSelectedHero() == null)
                {
                    runtimeStats?.ApplyHeroPassive(activeHero, notify: false);
                }
            }
            else
            {
                Debug.LogWarning("HeroData is not assigned to HeroHealth!");
            }

            currentMaxHp = runtimeStats != null ? runtimeStats.GetMaxHp(baseMaxHp) : baseMaxHp;
            currentHp = currentMaxHp;
            RefreshRenderers();
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }

        public void RefreshRenderers()
        {
            Transform visuals = transform.Find("Visuals");
            Transform searchRoot = visuals != null ? visuals : transform;
            var meshRenderers = searchRoot.GetComponentsInChildren<MeshRenderer>(true);

            int validCount = 0;
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null && IsHeroBodyRenderer(meshRenderers[i]))
                {
                    validCount++;
                }
            }

            heroRenderers = new Renderer[validCount];
            int idx = 0;
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null && IsHeroBodyRenderer(meshRenderers[i]))
                {
                    heroRenderers[idx++] = meshRenderers[i];
                }
            }
        }

        private static bool IsHeroBodyRenderer(MeshRenderer mr)
        {
            string name = mr.gameObject.name;
            return !name.Contains("ShieldVisual") && !name.Contains("Telegraph");
        }

        private void ApplyHitFlash(float amount)
        {
            if (heroRenderers == null || heroRenderers.Length == 0) return;

            float clamped = Mathf.Clamp01(amount);
            if (clamped <= 0.001f)
            {
                for (int i = 0; i < heroRenderers.Length; i++)
                {
                    if (heroRenderers[i] != null)
                    {
                        heroRenderers[i].SetPropertyBlock(null);
                    }
                }
                return;
            }

            if (hitFlashPropBlock == null) hitFlashPropBlock = new MaterialPropertyBlock();
            hitFlashPropBlock.Clear();
            hitFlashPropBlock.SetFloat(HitFlashPropertyId, clamped);
            for (int i = 0; i < heroRenderers.Length; i++)
            {
                if (heroRenderers[i] != null)
                {
                    heroRenderers[i].SetPropertyBlock(hitFlashPropBlock);
                }
            }
        }

        public void SetInvulnerable(bool invulnerable)
        {
            isInvulnerable = invulnerable;
        }

        public void SetGodMode(bool enabled)
        {
            isGodMode = enabled;
        }

        private void Update()
        {
            if (Time.deltaTime > 0f)
            {
                TickInvulnerability(Time.deltaTime);
                TickPassiveRegen(Time.deltaTime);
            }
        }

        /// <summary>Hero passive regeneration (Paladin: +2 HP/s). Zero-GC.</summary>
        public void TickPassiveRegen(float deltaTime)
        {
            float regen = runtimeStats != null ? runtimeStats.HeroPassiveRegenPerSecond : 0f;
            if (regen <= 0f || deltaTime <= 0f || isDead) return;
            if (currentHp >= currentMaxHp)
            {
                regenAccumulator = 0f;
                return;
            }

            regenAccumulator += regen * deltaTime;
            if (regenAccumulator >= 1f)
            {
                int amount = Mathf.FloorToInt(regenAccumulator);
                regenAccumulator -= amount;
                Heal(amount);
            }
        }

        public void TickInvulnerability(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (reviveInvulnerabilityTimer > 0f)
            {
                reviveInvulnerabilityTimer = Mathf.Max(0f, reviveInvulnerabilityTimer - deltaTime);
            }

            if (postHitInvulnerabilityTimer > 0f)
            {
                postHitInvulnerabilityTimer = Mathf.Max(0f, postHitInvulnerabilityTimer - deltaTime);
            }

            if (hitFlashTimer > 0f)
            {
                hitFlashTimer = Mathf.Max(0f, hitFlashTimer - deltaTime);
                ApplyHitFlash(hitFlashTimer > 0f ? Mathf.Clamp01(hitFlashTimer / HERO_HIT_FLASH_DURATION) : 0f);
            }
        }

        public void Revive(float invulnerabilityDuration = 3f)
        {
            isDead = false;
            currentHp = currentMaxHp;
            postHitInvulnerabilityTimer = 0f;
            hitFlashTimer = 0f;
            ApplyHitFlash(0f);
            reviveInvulnerabilityTimer = Mathf.Max(0f, invulnerabilityDuration);
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }

        public void TakeDamage(int amount)
        {
            if (isDead || IsInvulnerable || amount <= 0) return;

            int finalDamage = runtimeStats != null ? runtimeStats.CalculateMitigatedDamage(amount) : amount;
            if (finalDamage <= 0) return;

            currentHp -= finalDamage;
            if (currentHp < 0) currentHp = 0;

            if (currentHp <= 0 && TryTriggerSecondWind())
            {
                OnDamageTaken?.Invoke(finalDamage);
                OnHealthChanged?.Invoke(currentHp, currentMaxHp);
                return;
            }

            float iFrames = PostHitInvulnerabilityDuration;
            if (currentHp > 0 && iFrames > 0f)
            {
                postHitInvulnerabilityTimer = Mathf.Max(postHitInvulnerabilityTimer, iFrames);
            }

            if (heroRenderers == null)
            {
                RefreshRenderers();
            }
            if (currentHp > 0)
            {
                hitFlashTimer = HERO_HIT_FLASH_DURATION;
                ApplyHitFlash(1f);
            }

            OnDamageTaken?.Invoke(finalDamage);
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
            
            if (currentHp <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Keystone "Second Wind": once per run survive a lethal hit with 30% HP, 2.5s invulnerability and a knockback wave.
        /// </summary>
        private bool TryTriggerSecondWind()
        {
            if (!HasSecondWindAvailable) return false;

            secondWindUsed = true;
            float hpPct = SecondWindHpPercent;
            float invuln = SecondWindInvulnerability;
            float kbRadius = SecondWindKnockbackRadius;
            float kbDist = SecondWindKnockbackDistance;

            currentHp = Mathf.Max(1, Mathf.RoundToInt(currentMaxHp * hpPct));
            reviveInvulnerabilityTimer = Mathf.Max(reviveInvulnerabilityTimer, invuln);
            postHitInvulnerabilityTimer = 0f;
            hitFlashTimer = HERO_HIT_FLASH_DURATION;
            ApplyHitFlash(1f);

            int pushed = PushEnemiesAway(transform.position, kbRadius, kbDist);
            eventBus?.Publish(new SecondWindTriggeredEvent(transform.position, kbRadius, pushed));
            return true;
        }

        private static int PushEnemiesAway(Vector3 center, float radius, float distance)
        {
            if (radius <= 0f || distance <= 0f) return 0;
            int mask = LayerMask.GetMask("Enemy", "Enemies");
            if (mask == 0) mask = 1 << 8;

            int hits = Physics.OverlapSphereNonAlloc(center, radius, secondWindBuffer, mask);
            int pushed = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider col = secondWindBuffer[i];
                if (col == null || !col.TryGetComponent<ProjectB.Enemies.EnemyBase>(out var enemy) || enemy.IsDead) continue;

                Vector3 dir = enemy.transform.position - center;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                enemy.ApplyPull(dir.normalized * distance);
                pushed++;
            }
            return pushed;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            isInvulnerable = false;
            postHitInvulnerabilityTimer = 0f;
            reviveInvulnerabilityTimer = 0f;
            hitFlashTimer = 0f;
            ApplyHitFlash(0f);
            DevLog.Log("[HeroHealth] Hero has died. Game Over!");
            OnDied?.Invoke();
            eventBus?.Publish(new HeroDiedEvent());
        }

        [ContextMenu("Debug Kill")]
        public void DebugKill()
        {
            if (!isDead)
            {
                currentHp = 0;
                OnHealthChanged?.Invoke(currentHp, currentMaxHp);
                Die();
            }
        }

        public void Heal(int amount)
        {
            if (isDead || amount <= 0) return;
            int finalAmount = runtimeStats != null ? runtimeStats.GetHealAmount(amount) : amount;
            currentHp = Mathf.Min(currentHp + finalAmount, currentMaxHp);
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }

        public void IncreaseMaxHealth(int amount)
        {
            if (isDead || amount <= 0) return;
            runtimeStats?.AddMaxHp(amount);
            currentMaxHp += amount;
            currentHp += amount;
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }
    }
}
