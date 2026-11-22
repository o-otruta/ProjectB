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
        [SerializeField] [Min(0f)] private float postHitInvulnerabilityDuration = 0.18f;
        
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
        public float PostHitInvulnerabilityDuration => postHitInvulnerabilityDuration;
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

            if (currentHp > 0 && postHitInvulnerabilityDuration > 0f)
            {
                postHitInvulnerabilityTimer = Mathf.Max(postHitInvulnerabilityTimer, postHitInvulnerabilityDuration);
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
            if (isDead) return;
            currentHp = Mathf.Min(currentHp + amount, currentMaxHp);
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
