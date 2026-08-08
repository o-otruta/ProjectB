using UnityEngine;
using ProjectB.Combat;
using ProjectB.Data;

namespace ProjectB.Player
{
    public class HeroHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private HeroData heroData;
        private int currentHp;
        private bool isDead;

        public bool IsDead => isDead;

        private void Start()
        {
            if (heroData != null)
            {
                currentHp = heroData.maxHp;
            }
            else
            {
                Debug.LogWarning("HeroData is not assigned to HeroHealth!");
                currentHp = 100;
            }
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
    }
}
