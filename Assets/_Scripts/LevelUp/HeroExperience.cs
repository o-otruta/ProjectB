using System;
using UnityEngine;
using VContainer;
using ProjectB.Core.Events;
using ProjectB.Player;

namespace ProjectB.LevelUp
{
    public class HeroExperience : MonoBehaviour
    {
        private HeroRuntimeStats runtimeStats;
        private GameEventBus eventBus;
        private float baseMagnetRadius;
        private float xpMultiplier = 1f;

        [Inject]
        public void Construct(HeroRuntimeStats runtimeStats, GameEventBus eventBus)
        {
            this.runtimeStats = runtimeStats;
            this.eventBus = eventBus;
        }
        [Header("Settings")]
        [Tooltip("Базовое значение XP для 1 уровня")]
        [SerializeField] private int baseXP = 10;

        [Tooltip("Показатель степени кривой XP: baseXP * (level ^ xpExponent)")]
        [SerializeField] private float xpExponent = 1.05f;
        
        [Tooltip("Радиус магнита для сбора кристаллов")]
        [SerializeField] private float _magnetRadius = 3f;
        public float MagnetRadius
        {
            get => _magnetRadius;
            set => _magnetRadius = Mathf.Max(0, value);
        }
        
        [Tooltip("Скорость притягивания кристаллов")]
        [SerializeField] private float magnetSpeed = 15f;
        
        [SerializeField] private LayerMask pickupLayer;

        public int CurrentLevel { get; private set; } = 1;
        public int CurrentXP { get; private set; } = 0;
        public int XPToNextLevel { get; private set; }
        public int PendingStartingLevelUps { get; private set; }

        public event Action<int, int> OnXPChanged; // currentXP, xpToNextLevel
        public event Action<int> OnLevelUp; // newLevel

        private bool statsInitialized;

        private void Start()
        {
            EnsureStatsInitialized();
            OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
        }

        private void OnDestroy()
        {
            if (runtimeStats != null)
            {
                runtimeStats.OnStatsChanged -= RefreshMagnetRadius;
            }
        }

        public void EnsureStatsInitialized()
        {
            if (statsInitialized) return;
            statsInitialized = true;
            baseMagnetRadius = _magnetRadius;

            if (runtimeStats != null)
            {
                runtimeStats.OnStatsChanged += RefreshMagnetRadius;
                RefreshMagnetRadius();

                int bonusLevels = runtimeStats.StartingBonusLevels;
                CurrentLevel = 1 + bonusLevels;
                PendingStartingLevelUps = bonusLevels;

                xpMultiplier = runtimeStats.XpMultiplier;
            }

            CalculateXPToNextLevel();
        }

        private void RefreshMagnetRadius()
        {
            if (runtimeStats != null)
            {
                MagnetRadius = runtimeStats.GetMagnetRadius(baseMagnetRadius);
            }
        }

        public int ConsumeStartingLevelUps()
        {
            EnsureStatsInitialized();
            int count = PendingStartingLevelUps;
            PendingStartingLevelUps = 0;
            return count;
        }

        private const float MAGNET_SCAN_INTERVAL = 0.08f;

        private Collider[] magnetResults = new Collider[64];
        private float nextMagnetScanTime;
        private ProjectB.Player.HeroHealth heroHealth;

        private void Awake()
        {
            heroHealth = GetComponent<ProjectB.Player.HeroHealth>();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (heroHealth != null && heroHealth.IsDead) return;
            if (Time.time < nextMagnetScanTime) return;

            nextMagnetScanTime = Time.time + MAGNET_SCAN_INTERVAL;

            int count = Physics.OverlapSphereNonAlloc(transform.position, MagnetRadius, magnetResults, pickupLayer);
            for (int i = 0; i < count; i++)
            {
                if (magnetResults[i].TryGetComponent<XpCrystal>(out var crystal))
                {
                    crystal.Magnetize(magnetSpeed);
                }
                else if (magnetResults[i].TryGetComponent<CoinPickup>(out var coin))
                {
                    coin.Magnetize(magnetSpeed);
                }
            }
        }

        public void AddExperience(int amount)
        {
            CurrentXP += Mathf.RoundToInt(amount * xpMultiplier);
            
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
            eventBus?.Publish(new HeroLeveledUpEvent(CurrentLevel));
        }

        private void CalculateXPToNextLevel()
        {
            // Formula: baseXP * (level ^ 1.05)
            float exp = xpExponent > 0f ? xpExponent : 1.05f;
            XPToNextLevel = Mathf.Max(1, Mathf.RoundToInt(baseXP * Mathf.Pow(CurrentLevel, exp)));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, MagnetRadius);
        }
    }
}
