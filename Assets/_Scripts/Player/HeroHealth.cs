using UnityEngine;
using ProjectB.Combat;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;
using ProjectB.Core.Events;

namespace ProjectB.Player
{
    public class HeroHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private HeroData heroData;
        
        public event System.Action<int, int> OnHealthChanged;
        public event System.Action<int> OnDamageTaken;
        public event System.Action OnDied;

        private int currentHp;
        private int currentMaxHp;
        private bool isDead;
        private bool isInvulnerable;
        private HeroRuntimeStats runtimeStats;
        private GameEventBus eventBus;
        private SelectedHeroService selectedHeroService;

        public bool IsDead => isDead;
        public bool IsInvulnerable => isInvulnerable;
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
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }

        public void SetInvulnerable(bool invulnerable)
        {
            isInvulnerable = invulnerable;
        }

        public void TakeDamage(int amount)
        {
            if (isDead || isInvulnerable || amount <= 0) return;

            int finalDamage = runtimeStats != null ? runtimeStats.CalculateMitigatedDamage(amount) : amount;
            if (finalDamage <= 0) return;

            currentHp -= finalDamage;
            if (currentHp < 0) currentHp = 0;

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
            Debug.Log("[HeroHealth] Hero has died. Game Over!");
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
