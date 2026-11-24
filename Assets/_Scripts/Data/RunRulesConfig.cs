using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "RunRulesConfig", menuName = "ProjectB/Data/RunRulesConfig")]
    public class RunRulesConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "RunRulesConfig";

        [Header("LevelUp Rerolls")]
        [Min(0)]
        [Tooltip("Базовое количество бесплатных рероллов карточек за забег.")]
        public int freeRerollsPerRun = 1;

        [Header("Hero Defense & Second Wind")]
        [Min(0f)]
        [Tooltip("Базовая длительность пост-хит неуязвимости героя (секунды).")]
        public float postHitInvulnerabilityDuration = 0.18f;

        [Range(0.05f, 1f)]
        [Tooltip("Доля Max HP, с которой срабатывает Second Wind.")]
        public float secondWindHpPercent = 0.30f;

        [Min(0f)]
        [Tooltip("Длительность неуязвимости после срабатывания Second Wind.")]
        public float secondWindInvulnerability = 2.5f;

        [Min(0f)]
        [Tooltip("Радиус отбрасывания врагов при срабатывании Second Wind.")]
        public float secondWindKnockbackRadius = 5.0f;

        [Min(0f)]
        [Tooltip("Дистанция отбрасывания врагов при срабатывании Second Wind.")]
        public float secondWindKnockbackDistance = 3.0f;

        [Header("Meta & Economy")]
        [Min(1)]
        [Tooltip("Сколько XP-кристаллов нужно собрать для бонуса монет Collector's Greed.")]
        public int crystalsPerGreedBonus = 25;

        [Header("Wave Summons & Revive")]
        [Min(1)]
        [Tooltip("Максимум активных врагов, при котором допускается призыв миньонов боссами/саммонерами.")]
        public int maxActiveEnemiesForSummon = 80;

        [Min(0f)]
        [Tooltip("Безопасный радиус вокруг героя при воскрешении за рекламу.")]
        public float reviveSafeRadius = 4.5f;

        [Min(0f)]
        [Tooltip("Дистанция, на которую отталкиваются враги внутри безопасного радиуса при воскрешении.")]
        public float revivePushDistance = 5.5f;
    }
}
