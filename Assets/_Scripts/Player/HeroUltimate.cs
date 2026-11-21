using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.Player
{
    public class HeroUltimate : MonoBehaviour
    {
        private const int METEOR_COUNT = 6;
        private const float METEOR_SEARCH_RADIUS = 7f;
        private const float METEOR_EXPLOSION_RADIUS = 3.5f;
        private const float METEOR_BASE_DAMAGE = 80f;

        private static readonly Collider[] meteorSearchBuffer = new Collider[64];
        private static readonly Collider[] meteorDamageBuffer = new Collider[48];
        private static readonly Transform[] meteorCandidateBuffer = new Transform[64];

        [SerializeField] private HeroData fallbackHeroData;
        [SerializeField] private GameObject shieldVisual;
        [SerializeField] private LayerMask enemyLayer;

        private HeroHealth heroHealth;
        private GameManager gameManager;
        private SelectedHeroService selectedHeroService;
        private GameEventBus eventBus;
        private HeroRuntimeStats runtimeStats;

        private HeroData activeHeroData;
        private HeroUltimateType activeUltimateType;
        private float activeRemaining;
        private float cooldownRemaining;
        private bool isActive;
        private int meteorsRemaining;
        private float meteorInterval;
        private float nextMeteorTimer;
        private int resolvedEnemyLayerMask;

        public HeroData ActiveHeroData => activeHeroData != null ? activeHeroData : (selectedHeroService?.GetSelectedHero() ?? fallbackHeroData);
        public bool IsActive => isActive;
        public float ActiveRemaining => activeRemaining;
        public float ActiveDuration => ActiveHeroData != null ? Mathf.Max(0.1f, ActiveHeroData.ultimateDuration) : 5f;
        public float CooldownRemaining => cooldownRemaining;
        public float CooldownDuration => ActiveHeroData != null ? Mathf.Max(0.1f, ActiveHeroData.ultimateCooldown) : 25f;
        public bool IsReady => !isActive && cooldownRemaining <= 0f && (heroHealth == null || !heroHealth.IsDead);

        public event Action OnStateChanged;

        [Inject]
        public void Construct(
            HeroHealth heroHealth,
            GameManager gameManager = null,
            SelectedHeroService selectedHeroService = null,
            GameEventBus eventBus = null,
            HeroRuntimeStats runtimeStats = null)
        {
            this.heroHealth = heroHealth;
            this.gameManager = gameManager;
            this.selectedHeroService = selectedHeroService;
            this.eventBus = eventBus;
            this.runtimeStats = runtimeStats;
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

            ResolveEnemyLayerMask();
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
                if (activeUltimateType == HeroUltimateType.MeteorStrike && meteorsRemaining > 0)
                {
                    nextMeteorTimer -= Time.deltaTime;
                    while (nextMeteorTimer <= 0f && meteorsRemaining > 0)
                    {
                        DropNextMeteor();
                        nextMeteorTimer += meteorInterval;
                    }
                }

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

        public void ResetCooldown()
        {
            cooldownRemaining = 0f;
            OnStateChanged?.Invoke();
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            if (gameManager != null && gameManager.CurrentState != GameState.Playing) return false;

            HeroData data = ActiveHeroData;
            HeroUltimateType ultType = data != null ? data.ultimateType : HeroUltimateType.Shield;

            switch (ultType)
            {
                case HeroUltimateType.MeteorStrike:
                    ActivateMeteorStrike();
                    break;

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
            activeUltimateType = HeroUltimateType.Shield;
            activeRemaining = ActiveDuration;
            cooldownRemaining = 0f;
            meteorsRemaining = 0;

            heroHealth?.SetInvulnerable(true);
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(true);
            }

            OnStateChanged?.Invoke();
        }

        private void ActivateMeteorStrike()
        {
            isActive = true;
            activeUltimateType = HeroUltimateType.MeteorStrike;
            float duration = ActiveDuration;
            activeRemaining = duration;
            cooldownRemaining = 0f;
            meteorsRemaining = METEOR_COUNT;
            meteorInterval = duration / METEOR_COUNT;
            nextMeteorTimer = meteorInterval;

            OnStateChanged?.Invoke();
            DropNextMeteor();
        }

        private void DropNextMeteor()
        {
            if (meteorsRemaining <= 0) return;
            meteorsRemaining--;

            int mask = ResolveEnemyLayerMask();
            Vector3 heroPos = transform.position;
            Vector3 impactPos = FindMeteorImpactPoint(heroPos, mask);

            float damageMult = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(METEOR_BASE_DAMAGE * damageMult));

            int hits = Physics.OverlapSphereNonAlloc(impactPos, METEOR_EXPLOSION_RADIUS, meteorDamageBuffer, mask);
            for (int i = 0; i < hits; i++)
            {
                Collider col = meteorDamageBuffer[i];
                if (col != null && col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    damageable.TakeDamage(finalDamage);
                }
            }

            eventBus?.Publish(new MeteorImpactEvent(impactPos, METEOR_EXPLOSION_RADIUS));
        }

        private Vector3 FindMeteorImpactPoint(Vector3 heroPos, int mask)
        {
            int hits = Physics.OverlapSphereNonAlloc(heroPos, METEOR_SEARCH_RADIUS, meteorSearchBuffer, mask);
            int candidateCount = 0;

            for (int i = 0; i < hits && candidateCount < meteorCandidateBuffer.Length; i++)
            {
                Collider col = meteorSearchBuffer[i];
                if (col == null) continue;
                if (col.TryGetComponent<IDamageable>(out var damageable) && damageable.IsDead) continue;

                meteorCandidateBuffer[candidateCount++] = col.transform;
            }

            if (candidateCount > 0)
            {
                Transform chosen = meteorCandidateBuffer[UnityEngine.Random.Range(0, candidateCount)];
                Vector3 pos = chosen.position;
                pos.y = 0f;
                return pos;
            }

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(2f, 6f);
            return new Vector3(
                heroPos.x + Mathf.Cos(angle) * radius,
                0f,
                heroPos.z + Mathf.Sin(angle) * radius);
        }

        private int ResolveEnemyLayerMask()
        {
            if (resolvedEnemyLayerMask != 0) return resolvedEnemyLayerMask;
            if (enemyLayer.value != 0)
            {
                resolvedEnemyLayerMask = enemyLayer.value;
            }
            else
            {
                int mask = LayerMask.GetMask("Enemy", "Enemies");
                resolvedEnemyLayerMask = mask != 0 ? mask : (1 << 8);
            }
            return resolvedEnemyLayerMask;
        }

        private void DeactivateUltimate()
        {
            isActive = false;
            activeRemaining = 0f;
            meteorsRemaining = 0;
            cooldownRemaining = CooldownDuration;

            if (activeUltimateType == HeroUltimateType.Shield)
            {
                heroHealth?.SetInvulnerable(false);
                if (shieldVisual != null)
                {
                    shieldVisual.SetActive(false);
                }
            }

            OnStateChanged?.Invoke();
        }

        private void HandleHeroDied()
        {
            if (isActive)
            {
                isActive = false;
                activeRemaining = 0f;
                meteorsRemaining = 0;
                if (activeUltimateType == HeroUltimateType.Shield)
                {
                    heroHealth?.SetInvulnerable(false);
                    if (shieldVisual != null)
                    {
                        shieldVisual.SetActive(false);
                    }
                }
                OnStateChanged?.Invoke();
            }
        }
    }
}
