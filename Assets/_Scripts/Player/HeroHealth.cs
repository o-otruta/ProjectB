using UnityEngine;
using ProjectB.Combat;
using ProjectB.Data;
using VContainer;
using ProjectB.Meta;

namespace ProjectB.Player
{
    public class HeroHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private HeroData heroData;
        
        public event System.Action<int, int> OnHealthChanged;
        public event System.Action OnDied;

        private int currentHp;
        private int currentMaxHp;
        private bool isDead;
        private MetaUpgradeManager metaUpgradeManager;

        public bool IsDead => isDead;
        public int CurrentHp => currentHp;
        public int MaxHp => currentMaxHp;

        [Inject]
        public void Construct(MetaUpgradeManager metaManager)
        {
            this.metaUpgradeManager = metaManager;
        }

        private void Start()
        {
            float hpBonus = metaUpgradeManager != null ? metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.HeroHP) : 0f;
            
            if (heroData != null)
            {
                currentMaxHp = Mathf.RoundToInt(heroData.maxHp * (1f + hpBonus / 100f));
            }
            else
            {
                Debug.LogWarning("HeroData is not assigned to HeroHealth!");
                currentMaxHp = Mathf.RoundToInt(100 * (1f + hpBonus / 100f));
            }
            currentHp = currentMaxHp;
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }

        public void TakeDamage(int amount)
        {
            if (isDead) return;

            currentHp -= amount;
            if (currentHp < 0) currentHp = 0;
            
            Debug.Log($"[HeroHealth] Took {amount} damage. Current HP: {currentHp}");
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
            Debug.Log("[HeroHealth] Hero has died. Game Over!");
            OnDied?.Invoke();
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
            Debug.Log($"[HeroHealth] Healed {amount}. Current HP: {currentHp}");
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }

        public void IncreaseMaxHealth(int amount)
        {
            if (isDead) return;
            currentMaxHp += amount;
            currentHp += amount;
            Debug.Log($"[HeroHealth] Max HP increased by {amount}. New Max HP: {currentMaxHp}");
            OnHealthChanged?.Invoke(currentHp, currentMaxHp);
        }
    }
}
