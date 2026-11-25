using UnityEngine;
using ProjectB.Data.Combat;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "BossData", menuName = "ProjectB/Data/Enemies/BossData")]
    public class BossData : EnemyData
    {
        [Header("Boss Identity")]
        public string bossId = "golem";
        public string bossDisplayName = "Stone Golem";
        public string nameKey;
        public bool isMiniBoss = false;
        [Range(0f, 1f)]
        [Tooltip("Multiplier applied to external pull forces like Black Hole (0 = immune, 1 = full pull)")]
        public float pullResistance = 0.15f;

        [Header("Summoning")]
        public EnemyData minionData;
        public int summonCount = 3;
        public float summonCooldown = 10f;

        [Header("Timed Enrage (anti-kite, all bosses except mini-bosses)")]
        [Tooltip("Через сколько секунд боя босс впадает в ярость (0 = выключено). Гарантирует, что бой не тянется бесконечно")]
        [Min(0f)] public float enrageAfterSeconds = 75f;
        [Tooltip("Множитель скорости в момент ярости")]
        [Min(1f)] public float enrageSpeedMultiplier = 1.6f;
        [Tooltip("Множитель урона в момент ярости")]
        [Min(1f)] public float enrageDamageMultiplier = 1.5f;
        [Tooltip("Каждые N секунд ярость усиливается ещё на один шаг")]
        [Min(5f)] public float enrageRampInterval = 30f;
        [Min(0f)] public float enrageRampSpeedStep = 0.3f;
        [Min(0f)] public float enrageRampDamageStep = 0.25f;
        [Min(1f)] public float enrageMaxSpeedMultiplier = 2.8f;
        [Min(1f)] public float enrageMaxDamageMultiplier = 3f;

        [Header("Golem — Ground Slam AoE")]
        public float slamCooldown = 5.5f;
        public float slamTriggerRange = 6.5f;
        public float slamRadius = 4.5f;
        public float slamTelegraphDuration = 1.1f;
        public int slamDamage = 30;

        [Header("Golem — Leap (gap closer)")]
        [Tooltip("Golem прыгает к герою, если тот дальше этой дистанции (0 кулдаун = выключено)")]
        public float leapMinDistance = 9f;
        public float leapCooldown = 7f;
        [Tooltip("Присед перед прыжком — окно, чтобы заметить и уйти")]
        public float leapWindupDuration = 0.65f;
        public float leapAirDuration = 0.6f;
        [Tooltip("Упреждение точки приземления по скорости героя (0..1)")]
        [Range(0f, 1f)] public float leapLeadFactor = 0.5f;
        [Tooltip("Максимальная длина прыжка, м")]
        public float leapMaxDistance = 16f;
        public float leapImpactRadius = 3.5f;
        public int leapDamage = 24;

        [Header("Golem — Rubble Slow Zone (after slam and leap)")]
        [Tooltip("Радиус зоны обломков, которая замедляет героя (0 = выключено)")]
        public float rubbleZoneRadius = 3.2f;
        public float rubbleZoneDuration = 4f;
        [Range(0.25f, 0.95f)] public float rubbleSlowFactor = 0.6f;

        [Header("Necromancer — Teleport & Volley")]
        public float stopDistance = 9f;
        public float teleportCooldown = 7f;
        public float teleportMinDistance = 7f;
        public float teleportMaxDistance = 12f;
        public WeaponData volleyWeaponData;
        public int volleyProjectileCount = 5;
        public float volleySpreadAngle = 55f;
        public float volleyCooldown = 3.2f;

        [Header("Frost Lich — Cross Ice Spikes")]
        public float frostStopDistance = 7.5f;
        public float spikeCooldown = 5.5f;
        public float spikeTelegraphDuration = 1.0f;
        public int spikeLineCount = 6;
        public float spikeLineLength = 10f;
        public float spikeLineHalfWidth = 0.85f;
        public int spikeDamage = 35;
        [Range(0.25f, 1f)]
        public float spikeSlowFactor = 0.55f;
        public float spikeSlowDuration = 2.5f;

        [Header("Frost Lich — Blizzard Ring")]
        public float blizzardCooldown = 9.0f;
        public float blizzardTelegraphDuration = 1.15f;
        public float blizzardActiveDuration = 3.2f;
        public float blizzardSafeInnerRadius = 3.0f;
        public float blizzardOuterRadius = 9.0f;
        public float blizzardTickInterval = 0.5f;
        public int blizzardTickDamage = 14;
        [Range(0.25f, 1f)]
        public float blizzardSlowFactor = 0.65f;

        [Header("Demon Lord — Phase 1 & Phase 2 Enrage")]
        [Range(0.1f, 0.9f)]
        public float demonEnrageHpThreshold = 0.5f;
        public float demonEnrageSpeedMultiplier = 1.35f;
        public float demonChargeCooldown = 5.5f;
        public float demonChargeTriggerRange = 10f;
        public float demonChargeWindupDuration = 0.425f;
        public float demonChargeSpeed = 13f;
        public float demonChargeDistance = 11f;
        public float demonChargeHitRadius = 1.45f;
        public int demonChargeDamage = 32;
        public float demonFireballCooldown = 4.5f;
        public int demonPhase1FireballCount = 8;
        public int demonPhase2FireballCount = 12;
        public WeaponData demonFireballWeaponData;
        public float demonMeteorCooldown = 6.5f;
        public int demonMeteorCount = 5;
        public float demonMeteorRadius = 2.6f;
        public float demonMeteorTelegraphDuration = 1.05f;
        public int demonMeteorDamage = 38;
    }
}
