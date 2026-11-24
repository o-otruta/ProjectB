using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "RunRulesConfig", menuName = "ProjectB/Data/RunRulesConfig")]
    public class RunRulesConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "RunRulesConfig";

        public const int DEFAULT_FREE_REROLLS_PER_RUN = 1;
        public const float DEFAULT_POST_HIT_IFRAMES = 0.18f;
        public const float DEFAULT_SECOND_WIND_HP_PERCENT = 0.30f;
        public const float DEFAULT_SECOND_WIND_INVULNERABILITY = 2.5f;
        public const float DEFAULT_SECOND_WIND_KNOCKBACK_RADIUS = 5.0f;
        public const float DEFAULT_SECOND_WIND_KNOCKBACK_DISTANCE = 3.0f;
        public const int DEFAULT_CRYSTALS_PER_GREED_BONUS = 25;
        public const int DEFAULT_MAX_ACTIVE_ENEMIES_FOR_SUMMON = 80;
        public const float DEFAULT_REVIVE_SAFE_RADIUS = 4.5f;
        public const float DEFAULT_REVIVE_PUSH_DISTANCE = 5.5f;

        [Header("LevelUp Rerolls")]
        [Min(0)]
        [Tooltip("Базовое количество бесплатных рероллов карточек за забег.")]
        public int freeRerollsPerRun = DEFAULT_FREE_REROLLS_PER_RUN;

        [Header("Hero Defense & Second Wind")]
        [Min(0f)]
        [Tooltip("Базовая длительность пост-хит неуязвимости героя (секунды).")]
        public float postHitInvulnerabilityDuration = DEFAULT_POST_HIT_IFRAMES;

        [Range(0.05f, 1f)]
        [Tooltip("Доля Max HP, с которой срабатывает Second Wind.")]
        public float secondWindHpPercent = DEFAULT_SECOND_WIND_HP_PERCENT;

        [Min(0f)]
        [Tooltip("Длительность неуязвимости после срабатывания Second Wind.")]
        public float secondWindInvulnerability = DEFAULT_SECOND_WIND_INVULNERABILITY;

        [Min(0f)]
        [Tooltip("Радиус отбрасывания врагов при срабатывании Second Wind.")]
        public float secondWindKnockbackRadius = DEFAULT_SECOND_WIND_KNOCKBACK_RADIUS;

        [Min(0f)]
        [Tooltip("Дистанция отбрасывания врагов при срабатывании Second Wind.")]
        public float secondWindKnockbackDistance = DEFAULT_SECOND_WIND_KNOCKBACK_DISTANCE;

        [Header("Meta & Economy")]
        [Min(1)]
        [Tooltip("Сколько XP-кристаллов нужно собрать для бонуса монет Collector's Greed.")]
        public int crystalsPerGreedBonus = DEFAULT_CRYSTALS_PER_GREED_BONUS;

        [Header("Wave Summons & Revive")]
        [Min(1)]
        [Tooltip("Максимум активных врагов, при котором допускается призыв миньонов боссами/саммонерами.")]
        public int maxActiveEnemiesForSummon = DEFAULT_MAX_ACTIVE_ENEMIES_FOR_SUMMON;

        [Min(0f)]
        [Tooltip("Безопасный радиус вокруг героя при воскрешении за рекламу.")]
        public float reviveSafeRadius = DEFAULT_REVIVE_SAFE_RADIUS;

        [Min(0f)]
        [Tooltip("Дистанция, на которую отталкиваются враги внутри безопасного радиуса при воскрешении.")]
        public float revivePushDistance = DEFAULT_REVIVE_PUSH_DISTANCE;
    }
}
