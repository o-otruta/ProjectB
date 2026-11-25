#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Core.BalanceBot
{
    [CreateAssetMenu(fileName = "BalanceBotConfig", menuName = "ProjectB/Debug/Balance Bot Config")]
    public class BalanceBotConfig : ScriptableObject
    {
        [Header("Threats & Avoidance")]
        [Min(2f)] public float threatRadius = 12f;
        [Min(0f)] public float repulsionWeight = 4.2f;
        [Min(1f)] public float projectileDodgeRadius = 6.5f;
        [Min(0f)] public float projectileDodgeWeight = 5.0f;
        [Min(0f)] public float frostZoneAvoidWeight = 4.5f;
        [Min(0f)] public float bossTelegraphAvoidWeight = 8.0f;

        [Header("Kiting & Engagement")]
        [Min(0.5f)] public float meleePreferredDistance = 2.4f;
        [Min(1f)] public float rangedPreferredDistance = 5.8f;
        [Min(0f)] public float engageWeight = 1.65f;
        [Min(0f)] public float strafeWeight = 0.95f;
        [Min(1f)] public float strafeFlipIntervalSeconds = 4.5f;

        [Header("Pickups")]
        [Min(2f)] public float pickupSearchRadius = 15f;
        [Min(0.1f)] public float pickupSafeThreatThreshold = 1.5f;
        [Min(0.5f)] public float pickupMinEnemyDistance = 2.5f;
        [Min(0f)] public float xpAttractWeight = 2.1f;
        [Min(0f)] public float coinAttractWeight = 1.15f;

        [Header("Obstacles & Arena Bounds")]
        [Min(1f)] public float obstacleRayLength = 3.6f;
        [Min(1f)] public float arenaWallMargin = 8f;

        [Header("Stuck Recovery & Decision Cadence")]
        [Min(0.25f)] public float stuckWindowSeconds = 1.25f;
        [Min(0.1f)] public float stuckMinDisplacement = 0.65f;
        [Min(0.1f)] public float unstuckDurationSeconds = 0.65f;
        [Range(0.05f, 0.5f)] public float decisionIntervalSeconds = 0.10f;

        [Header("Ultimate")]
        [Min(1)] public int ultimateMinNearbyEnemies = 6;
        [Min(2f)] public float ultimateCheckRadius = 7.5f;
        [Range(0.05f, 0.9f)] public float ultimateLowHpEmergencyThreshold = 0.35f;

        [Header("Batch Runner")]
        public List<string> heroIds = new List<string>
        {
            "knight",
            "mage",
            "ranger",
            "necromancer",
            "paladin",
            "engineer"
        };
        public List<BalanceBotStrategyKind> strategies = new List<BalanceBotStrategyKind>
        {
            BalanceBotStrategyKind.Random,
            BalanceBotStrategyKind.GreedyDamage
        };
        public BalanceBotBuildPreset fixedBuildPreset;
        public List<BalanceBotMetaProfile> metaProfiles = new List<BalanceBotMetaProfile>();

        [Min(1)] public int runsPerCombination = 3;
        public int baseSeed = 1000;
        [Header("Simulation Speed")]
        [Tooltip("Фиксированный шаг: каждый кадр = ровно 1/simulationFps игровых секунд (Time.captureDeltaTime), " +
                 "кадры идут так быстро, как тянет ПК. Симуляция совпадает с игрой на 60 FPS, а не зависит от FPS редактора. " +
                 "Выключено — legacy-режим через Time.timeScale (на 6× кадры крупнее и бот слабее: W13 против W16 на 1×).")]
        public bool useFixedStepSimulation = true;
        [Range(30, 120)] public int simulationFps = 60;
        [Tooltip("Только legacy-режим (useFixedStepSimulation = false).")]
        [Range(1f, 12f)] public float timeScale = 6f;
        [Min(1)] public int maxWaveLimit = 100;
        [Min(1f)] public float maxRunMinutes = 30f;

        public static BalanceBotConfig CreateRuntimeDefault()
        {
            var cfg = CreateInstance<BalanceBotConfig>();
            cfg.hideFlags = HideFlags.DontSave;
            return cfg;
        }
    }
}
#endif
