using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Player;

namespace ProjectB.LevelUp
{
    public class CoinPickup : MonoBehaviour
    {
        private IObjectPool<CoinPickup> pool;
        private Transform heroTarget;
        private int coinAmount;
        private float magnetSpeed = 10f;
        
        private bool isMagnetized;

        public void Initialize(Transform hero, int amount, IObjectPool<CoinPickup> coinPool)
        {
            heroTarget = hero;
            coinAmount = amount;
            pool = coinPool;
            isMagnetized = false;
        }

        private void Update()
        {
            if (heroTarget == null) return;

            if (isMagnetized)
            {
                // Fly towards hero
                transform.position = Vector3.MoveTowards(transform.position, heroTarget.position + Vector3.up, magnetSpeed * Time.deltaTime);
                
                // If reached hero
                if (Vector3.SqrMagnitude(transform.position - (heroTarget.position + Vector3.up)) < 0.25f)
                {
                    Collect();
                }
            }
        }

        public void Magnetize(float speed)
        {
            isMagnetized = true;
            magnetSpeed = speed;
        }
        
        private void Collect()
        {
            if (heroTarget.TryGetComponent<HeroEconomy>(out var heroEconomy))
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
