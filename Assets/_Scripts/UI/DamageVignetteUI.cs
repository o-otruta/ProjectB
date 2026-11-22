using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Player;

namespace ProjectB.UI
{
    /// <summary>
    /// Full-screen crimson vignette overlay that flashes on hero hit and pulses softly at low HP.
    /// Zero GC allocations in Update.
    /// </summary>
    public class DamageVignetteUI : MonoBehaviour
    {
        [SerializeField] private Image vignetteImage;
        [SerializeField] private Color hitFlashColor = new Color(0.95f, 0.08f, 0.08f, 1f);
        [SerializeField] [Range(0f, 1f)] private float maxFlashAlpha = 0.55f;
        [SerializeField] [Min(0.1f)] private float flashDecaySpeed = 2.4f;
        [SerializeField] [Range(0.05f, 0.6f)] private float lowHpThreshold = 0.30f;
        [SerializeField] [Range(0f, 0.6f)] private float lowHpPulseMinAlpha = 0.14f;
        [SerializeField] [Range(0f, 0.8f)] private float lowHpPulseMaxAlpha = 0.36f;
        [SerializeField] [Min(0.1f)] private float lowHpPulseSpeed = 4.2f;

        private HeroHealth heroHealth;
        private bool isSubscribed;
        private float flashAlpha;
        private float pulsePhase;
        private float currentAlpha = -1f;
        private bool isLowHp;
        private bool isDead;

        public float CurrentAlpha => Mathf.Max(0f, currentAlpha);
        public Image VignetteImage => vignetteImage;

        [Inject]
        public void Construct(HeroHealth heroHealth = null)
        {
            Unsubscribe();
            this.heroHealth = heroHealth;
            Subscribe();
            if (this.heroHealth != null)
            {
                HandleHeroHealthChanged(this.heroHealth.CurrentHp, this.heroHealth.MaxHp);
            }
        }

        private void Awake()
        {
            if (vignetteImage == null)
            {
                vignetteImage = GetComponent<Image>();
            }
            if (vignetteImage != null)
            {
                vignetteImage.raycastTarget = false;
            }
            ApplyAlpha(0f);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (isSubscribed || heroHealth == null) return;
            heroHealth.OnDamageTaken += HandleHeroDamageTaken;
            heroHealth.OnHealthChanged += HandleHeroHealthChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || heroHealth == null) return;
            heroHealth.OnDamageTaken -= HandleHeroDamageTaken;
            heroHealth.OnHealthChanged -= HandleHeroHealthChanged;
            isSubscribed = false;
        }

        private void HandleHeroDamageTaken(int amount)
        {
            if (amount > 0)
            {
                TriggerDamageFlash(1f);
            }
        }

        private void HandleHeroHealthChanged(int curHp, int maxHp)
        {
            isDead = curHp <= 0;
            isLowHp = maxHp > 0 && curHp > 0 && ((float)curHp / maxHp) <= lowHpThreshold;
            if (isDead)
            {
                flashAlpha = 0f;
                ApplyAlpha(0f);
            }
            else
            {
                TickVignette(0f);
            }
        }

        public void TriggerDamageFlash(float normalizedStrength = 1f)
        {
            if (isDead) return;
            flashAlpha = Mathf.Max(flashAlpha, Mathf.Clamp01(normalizedStrength) * maxFlashAlpha);
            TickVignette(0f);
        }

        public void TickVignette(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                if (flashAlpha > 0f)
                {
                    flashAlpha = Mathf.Max(0f, flashAlpha - flashDecaySpeed * deltaTime);
                }

                if (isLowHp && !isDead)
                {
                    pulsePhase += deltaTime * lowHpPulseSpeed;
                }
                else
                {
                    pulsePhase = 0f;
                }
            }

            float lowHpAlpha = 0f;
            if (isLowHp && !isDead)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(pulsePhase);
                lowHpAlpha = Mathf.Lerp(lowHpPulseMinAlpha, lowHpPulseMaxAlpha, wave);
            }

            float targetAlpha = isDead ? 0f : Mathf.Max(flashAlpha, lowHpAlpha);
            ApplyAlpha(targetAlpha);
        }

        private void Update()
        {
            TickVignette(Time.unscaledDeltaTime);
        }

        private void ApplyAlpha(float alpha)
        {
            float clamped = Mathf.Clamp01(alpha);
            if (Mathf.Approximately(currentAlpha, clamped)) return;
            currentAlpha = clamped;

            if (vignetteImage != null)
            {
                Color c = hitFlashColor;
                c.a = clamped;
                vignetteImage.color = c;
                bool shouldEnable = clamped > 0.002f;
                if (vignetteImage.enabled != shouldEnable)
                {
                    vignetteImage.enabled = shouldEnable;
                }
            }
        }
    }
}
