using UnityEngine;
using ProjectB.Combat;
using ProjectB.Data;

namespace ProjectB.Player
{
    public class HeroHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private HeroData heroData;
        private int currentHp;
        private int currentMaxHp;
        private bool isDead;

        public bool IsDead => isDead;

        private void Start()
        {
            if (heroData != null)
            {
                currentMaxHp = heroData.maxHp;
            }
            else
            {
                Debug.LogWarning("HeroData is not assigned to HeroHealth!");
                currentMaxHp = 100;
            }
            currentHp = currentMaxHp;
        }

        public void TakeDamage(int amount)
        {
            if (isDead) return;

            currentHp -= amount;
            Debug.Log($"[HeroHealth] Took {amount} damage. Current HP: {currentHp}");
            
            // TODO: Update HUD HP Bar
            
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
            // TODO: Trigger Game Over Screen (Phase 1.9)
        }

        public void Heal(int amount)
        {
            if (isDead) return;
            currentHp = Mathf.Min(currentHp + amount, currentMaxHp);
            Debug.Log($"[HeroHealth] Healed {amount}. Current HP: {currentHp}");
        }

        public void IncreaseMaxHealth(int amount)
        {
            if (isDead) return;
            currentMaxHp += amount;
            currentHp += amount;
            Debug.Log($"[HeroHealth] Max HP increased by {amount}. New Max HP: {currentMaxHp}");
        }
    }
}
