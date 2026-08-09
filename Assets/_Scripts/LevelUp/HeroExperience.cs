using System;
using UnityEngine;

namespace ProjectB.LevelUp
{
    public class HeroExperience : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Базовое значение XP для 1 уровня")]
        [SerializeField] private int baseXP = 10;
        
        [Tooltip("Радиус магнита для сбора кристаллов")]
        public float magnetRadius = 3f;
        
        [Tooltip("Скорость притягивания кристаллов")]
        [SerializeField] private float magnetSpeed = 15f;

        public int CurrentLevel { get; private set; } = 1;
        public int CurrentXP { get; private set; } = 0;
        public int XPToNextLevel { get; private set; }

        public event Action<int, int> OnXPChanged; // currentXP, xpToNextLevel
        public event Action<int> OnLevelUp; // newLevel

        private void Start()
        {
            CalculateXPToNextLevel();
            OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
        }

        private Collider[] magnetResults = new Collider[64];

        private void Update()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, magnetRadius, magnetResults);
            for (int i = 0; i < count; i++)
            {
                if (magnetResults[i].TryGetComponent<XpCrystal>(out var crystal))
                {
                    crystal.Magnetize(magnetSpeed);
                }
            }
        }

        public void AddExperience(int amount)
        {
            CurrentXP += amount;
            
            while (CurrentXP >= XPToNextLevel)
            {
                CurrentXP -= XPToNextLevel;
                LevelUp();
            }
            
            OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
        }

        private void LevelUp()
        {
            CurrentLevel++;
            CalculateXPToNextLevel();
            
            Debug.Log($"[HeroExperience] Level Up! Now level {CurrentLevel}");
            
            OnLevelUp?.Invoke(CurrentLevel);
        }

        private void CalculateXPToNextLevel()
        {
            // Formula: baseXP * (level ^ 1.05)
            XPToNextLevel = Mathf.RoundToInt(baseXP * Mathf.Pow(CurrentLevel, 1.05f));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, magnetRadius);
        }
    }
}
