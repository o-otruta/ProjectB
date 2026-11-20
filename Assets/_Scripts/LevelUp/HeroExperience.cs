using System;
using UnityEngine;
using VContainer;
using ProjectB.Meta;
using ProjectB.Core.Events;

namespace ProjectB.LevelUp
{
    public class HeroExperience : MonoBehaviour
    {
        private MetaUpgradeManager metaUpgradeManager;
        private GameEventBus eventBus;
        private float xpMultiplier = 1f;

        [Inject]
        public void Construct(MetaUpgradeManager metaManager, GameEventBus eventBus)
        {
            this.metaUpgradeManager = metaManager;
            this.eventBus = eventBus;
        }
        [Header("Settings")]
        [Tooltip("Базовое значение XP для 1 уровня")]
        [SerializeField] private int baseXP = 10;
        
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

        public void EnsureStatsInitialized()
        {
            if (statsInitialized) return;
            statsInitialized = true;

            if (metaUpgradeManager != null)
            {
                float magnetBonusPct = metaUpgradeManager.GetTotalBonus(ProjectB.Data.MetaUpgradeEffectType.MagnetRadius);
                MagnetRadius *= (1f + magnetBonusPct / 100f);

                float startLevelBonus = metaUpgradeManager.GetTotalBonus(ProjectB.Data.MetaUpgradeEffectType.StartLevel);
                int bonusLevels = Mathf.Max(0, Mathf.RoundToInt(startLevelBonus));
                CurrentLevel = 1 + bonusLevels;
                PendingStartingLevelUps = bonusLevels;

                float xpBonusPct = metaUpgradeManager.GetTotalBonus(ProjectB.Data.MetaUpgradeEffectType.XPBonus);
                xpMultiplier = 1f + xpBonusPct / 100f;
            }

            CalculateXPToNextLevel();
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
            XPToNextLevel = Mathf.RoundToInt(baseXP * Mathf.Pow(CurrentLevel, 1.05f));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, MagnetRadius);
        }
    }
}
