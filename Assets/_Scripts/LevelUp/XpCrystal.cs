using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.LevelUp
{
    public class XpCrystal : MonoBehaviour
    {
        private IObjectPool<XpCrystal> pool;
        private Transform heroTarget;
        private HeroExperience heroExperience;
        private int xpAmount;
        private float magnetSpeed = 10f;
        
        private bool isMagnetized;

        public bool IsMagnetized => isMagnetized;
        public float CurrentMagnetSpeed => magnetSpeed;

        public void Initialize(Transform hero, int amount, IObjectPool<XpCrystal> crystalPool, HeroExperience heroExp = null)
        {
            heroTarget = hero;
            heroExperience = heroExp != null ? heroExp : (hero != null ? hero.GetComponent<HeroExperience>() : null);
            xpAmount = amount;
            pool = crystalPool;
            isMagnetized = false;
            enabled = false; // Отключаем Update пока кристалл лежит на земле
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
            if (heroExperience != null)
            {
                heroExperience.AddExperience(xpAmount);
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
