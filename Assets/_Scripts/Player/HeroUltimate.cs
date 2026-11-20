using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Player
{
    public class HeroUltimate : MonoBehaviour
    {
        [SerializeField] private HeroData fallbackHeroData;
        [SerializeField] private GameObject shieldVisual;

        private HeroHealth heroHealth;
        private GameManager gameManager;
        private SelectedHeroService selectedHeroService;

        private HeroData activeHeroData;
        private float activeRemaining;
        private float cooldownRemaining;
        private bool isActive;

        public HeroData ActiveHeroData => activeHeroData != null ? activeHeroData : (selectedHeroService?.GetSelectedHero() ?? fallbackHeroData);
        public bool IsActive => isActive;
        public float ActiveRemaining => activeRemaining;
        public float ActiveDuration => ActiveHeroData != null ? Mathf.Max(0.1f, ActiveHeroData.ultimateDuration) : 5f;
        public float CooldownRemaining => cooldownRemaining;
        public float CooldownDuration => ActiveHeroData != null ? Mathf.Max(0.1f, ActiveHeroData.ultimateCooldown) : 25f;
        public bool IsReady => !isActive && cooldownRemaining <= 0f && (heroHealth == null || !heroHealth.IsDead);

        public event Action OnStateChanged;

        [Inject]
        public void Construct(HeroHealth heroHealth, GameManager gameManager = null, SelectedHeroService selectedHeroService = null)
        {
            this.heroHealth = heroHealth;
            this.gameManager = gameManager;
            this.selectedHeroService = selectedHeroService;
        }

        private void Awake()
        {
            if (heroHealth == null)
            {
                heroHealth = GetComponent<HeroHealth>();
            }

            if (shieldVisual != null)
            {
                shieldVisual.SetActive(false);
            }
        }

        private void Start()
        {
            activeHeroData = selectedHeroService?.GetSelectedHero() ?? fallbackHeroData;
            if (heroHealth != null)
            {
                heroHealth.OnDied += HandleHeroDied;
            }
            OnStateChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (heroHealth != null)
            {
                heroHealth.OnDied -= HandleHeroDied;
            }
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (heroHealth != null && heroHealth.IsDead) return;
            if (gameManager != null && gameManager.CurrentState != GameState.Playing) return;

            // Optional desktop/editor shortcut for testing
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryActivate();
            }

            if (isActive)
            {
                activeRemaining -= Time.deltaTime;
                if (activeRemaining <= 0f)
                {
                    DeactivateUltimate();
                }
            }
            else if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
                if (cooldownRemaining <= 0f)
                {
                    cooldownRemaining = 0f;
                    OnStateChanged?.Invoke();
                }
            }
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            if (gameManager != null && gameManager.CurrentState != GameState.Playing) return false;

            HeroData data = ActiveHeroData;
            HeroUltimateType ultType = data != null ? data.ultimateType : HeroUltimateType.Shield;

            switch (ultType)
            {
                case HeroUltimateType.Shield:
                default:
                    ActivateShield();
                    break;
            }

            return true;
        }

        private void ActivateShield()
        {
            isActive = true;
            activeRemaining = ActiveDuration;
            cooldownRemaining = 0f;

            heroHealth?.SetInvulnerable(true);
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(true);
            }

            OnStateChanged?.Invoke();
        }

        private void DeactivateUltimate()
        {
            isActive = false;
            activeRemaining = 0f;
            cooldownRemaining = CooldownDuration;

            heroHealth?.SetInvulnerable(false);
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(false);
            }

            OnStateChanged?.Invoke();
        }

        private void HandleHeroDied()
        {
            if (isActive)
            {
                isActive = false;
                activeRemaining = 0f;
                heroHealth?.SetInvulnerable(false);
                if (shieldVisual != null)
                {
                    shieldVisual.SetActive(false);
                }
                OnStateChanged?.Invoke();
            }
        }
    }
}
