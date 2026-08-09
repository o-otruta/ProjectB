using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.LevelUp
{
    public class XpCrystal : MonoBehaviour
    {
        private IObjectPool<XpCrystal> pool;
        private Transform heroTarget;
        private int xpAmount;
        private float magnetSpeed = 10f;
        
        private bool isMagnetized;

        public void Initialize(Transform hero, int amount, IObjectPool<XpCrystal> crystalPool)
        {
            heroTarget = hero;
            xpAmount = amount;
            pool = crystalPool;
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
            if (heroTarget.TryGetComponent<HeroExperience>(out var heroExp))
            {
                heroExp.AddExperience(xpAmount);
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
