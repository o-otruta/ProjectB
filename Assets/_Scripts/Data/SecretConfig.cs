using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "SecretConfig", menuName = "ProjectB/Data/SecretConfig")]
    public class SecretConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "SecretConfig";

        public const float DEFAULT_BASE_SPAWN_CHANCE = 0.10f;
        public const float DEFAULT_CONSTELLATION_BONUS = 0.20f;
        public const float DEFAULT_MIN_DISTANCE = 28f;
        public const float DEFAULT_MAX_DISTANCE = 60f;
        public const float DEFAULT_ARENA_PADDING = 6f;
        public const int DEFAULT_CHEST_COINS = 100;
        public const int DEFAULT_CRYSTAL_FALLBACK_COINS = 150;

        [Header("Spawn Chance")]
        [Range(0f, 1f)]
        [Tooltip("Базовый шанс появления секретного предмета за забег.")]
        public float baseSpawnChancePerRun = DEFAULT_BASE_SPAWN_CHANCE;

        [Range(0f, 1f)]
        [Tooltip("Прибавка к шансу появления секрета при открытом узле Созвездий Secret & Shrine.")]
        public float constellationSpawnChanceBonus = DEFAULT_CONSTELLATION_BONUS;

        [Header("Placement")]
        [Min(0f)]
        [Tooltip("Минимальная дистанция от центра арены до секретного предмета.")]
        public float minDistanceFromCenter = DEFAULT_MIN_DISTANCE;

        [Min(0f)]
        [Tooltip("Максимальная дистанция от центра арены до секретного предмета.")]
        public float maxDistanceFromCenter = DEFAULT_MAX_DISTANCE;

        [Min(0f)]
        [Tooltip("Отступ от границ арены при спавне секретного предмета.")]
        public float arenaPadding = DEFAULT_ARENA_PADDING;

        [Header("Reward Tuning")]
        [Min(0)]
        public int chestCoinReward = DEFAULT_CHEST_COINS;

        [Min(0)]
        public int crystalFallbackCoins = DEFAULT_CRYSTAL_FALLBACK_COINS;

        public float GetEffectiveSpawnChance(bool hasConstellationBonus)
        {
            return GetEffectiveSpawnChance(baseSpawnChancePerRun, constellationSpawnChanceBonus, hasConstellationBonus);
        }

        public static float GetEffectiveSpawnChance(float baseChance, float bonusChance, bool hasConstellationBonus)
        {
            float chance = baseChance + (hasConstellationBonus ? bonusChance : 0f);
            return Mathf.Clamp01(chance);
        }
    }
}
