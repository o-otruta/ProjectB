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
        private Vector3 baseScale = Vector3.one;
        private bool hasBaseScale;
        private int mergeCount;
        
        private bool isMagnetized;

        public int CoinAmount => coinAmount;
        public int MergeCount => mergeCount;
        public bool IsMagnetized => isMagnetized;
        public float CurrentMagnetSpeed => magnetSpeed;

        public void Initialize(Transform hero, int amount, IObjectPool<CoinPickup> coinPool, HeroEconomy economy = null)
        {
            if (!hasBaseScale)
            {
                baseScale = transform.localScale;
                hasBaseScale = true;
            }
            transform.localScale = baseScale;
            mergeCount = 0;

            heroTarget = hero;
            heroEconomy = economy != null ? economy : (hero != null ? hero.GetComponent<HeroEconomy>() : null);
            coinAmount = amount;
            pool = coinPool;
            isMagnetized = false;
            enabled = false; // Отключаем Update пока монета лежит на земле
        }

        public void AddMergedAmount(int additionalAmount, float scaleMultiplier)
        {
            if (additionalAmount <= 0) return;

            if (!hasBaseScale)
            {
                baseScale = transform.localScale;
                hasBaseScale = true;
            }

            coinAmount += additionalAmount;
            mergeCount++;
            transform.localScale = baseScale * Mathf.Max(1f, scaleMultiplier);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!isMagnetized || deltaTime <= 0f || heroTarget == null) return;

            Vector3 targetPos = heroTarget.position + Vector3.up;
            // Fly towards hero
            transform.position = Vector3.MoveTowards(transform.position, targetPos, magnetSpeed * deltaTime);
            
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
