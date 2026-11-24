using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "SecretConfig", menuName = "ProjectB/Data/SecretConfig")]
    public class SecretConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "SecretConfig";

        [Header("Spawn Chance")]
        [Range(0f, 1f)]
        [Tooltip("Базовый шанс появления секретного предмета за забег.")]
        public float baseSpawnChancePerRun = 0.10f;

        [Range(0f, 1f)]
        [Tooltip("Прибавка к шансу появления секрета при открытом узле Созвездий Secret & Shrine.")]
        public float constellationSpawnChanceBonus = 0.20f;

        [Header("Placement")]
        [Min(0f)]
        [Tooltip("Минимальная дистанция от центра арены до секретного предмета.")]
        public float minDistanceFromCenter = 28f;

        [Min(0f)]
        [Tooltip("Максимальная дистанция от центра арены до секретного предмета.")]
        public float maxDistanceFromCenter = 60f;

        [Min(0f)]
        [Tooltip("Отступ от границ арены при спавне секретного предмета.")]
        public float arenaPadding = 6f;

        [Header("Reward Tuning")]
        [Min(0)]
        public int chestCoinReward = 100;

        [Min(0)]
        public int crystalFallbackCoins = 150;

        public float GetEffectiveSpawnChance(bool hasConstellationBonus)
        {
            float chance = baseSpawnChancePerRun + (hasConstellationBonus ? constellationSpawnChanceBonus : 0f);
            return Mathf.Clamp01(chance);
        }
    }
}
