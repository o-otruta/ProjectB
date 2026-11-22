using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Player;

namespace ProjectB.LevelUp
{
    public class CoinPickup : MonoBehaviour
    {
        private IObjectPool<CoinPickup> pool;
        private Transform heroTarget;
        private HeroEconomy heroEconomy;
        private int coinAmount;
        private float magnetSpeed = 10f;
        
        private bool isMagnetized;

        public bool IsMagnetized => isMagnetized;
        public float CurrentMagnetSpeed => magnetSpeed;

        public void Initialize(Transform hero, int amount, IObjectPool<CoinPickup> coinPool, HeroEconomy economy = null)
        {
            heroTarget = hero;
            heroEconomy = economy != null ? economy : (hero != null ? hero.GetComponent<HeroEconomy>() : null);
            coinAmount = amount;
            pool = coinPool;
            isMagnetized = false;
            enabled = false; // Отключаем Update пока монета лежит на земле
        }

        private void Update()
        {
            if (!isMagnetized || Time.deltaTime <= 0f || heroTarget == null) return;

            Vector3 targetPos = heroTarget.position + Vector3.up;
            // Fly towards hero
            transform.position = Vector3.MoveTowards(transform.position, targetPos, magnetSpeed * Time.deltaTime);
            
            // If reached hero
            if ((transform.position - targetPos).sqrMagnitude < 0.25f)
            {
                Collect();
            }
        }

        public void Magnetize(float speed)
        {
            if (isMagnetized)
            {
                if (speed > magnetSpeed) magnetSpeed = speed;
                return;
            }
            isMagnetized = true;
            magnetSpeed = speed;
            enabled = true;
        }
        
        private void Collect()
        {
            if (heroEconomy != null)
            {
                heroEconomy.AddCoin(coinAmount);
            }
            
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
