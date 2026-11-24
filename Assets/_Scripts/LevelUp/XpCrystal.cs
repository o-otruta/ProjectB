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
        private Vector3 baseScale = Vector3.one;
        private bool hasBaseScale;
        private int mergeCount;
        
        private bool isMagnetized;

        public int XpAmount => xpAmount;
        public int MergeCount => mergeCount;
        public bool IsMagnetized => isMagnetized;
        public float CurrentMagnetSpeed => magnetSpeed;

        public void Initialize(Transform hero, int amount, IObjectPool<XpCrystal> crystalPool, HeroExperience heroExp = null)
        {
            if (!hasBaseScale)
            {
                baseScale = transform.localScale;
                hasBaseScale = true;
            }
            transform.localScale = baseScale;
            mergeCount = 0;

            heroTarget = hero;
            heroExperience = heroExp != null ? heroExp : (hero != null ? hero.GetComponent<HeroExperience>() : null);
            xpAmount = amount;
            pool = crystalPool;
            isMagnetized = false;
            enabled = false; // Отключаем Update пока кристалл лежит на земле
        }

        public void AddMergedAmount(int additionalAmount, float scaleMultiplier)
        {
            if (additionalAmount <= 0) return;

            if (!hasBaseScale)
            {
                baseScale = transform.localScale;
                hasBaseScale = true;
            }

            xpAmount += additionalAmount;
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
