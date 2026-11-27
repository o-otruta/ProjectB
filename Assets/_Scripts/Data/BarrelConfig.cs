using UnityEngine;

namespace ProjectB.Data
{
    /// <summary>
    /// Data-Driven настройки взрывных бочек: читаемость (idle-пульс), пинок с фитилём,
    /// цепная реакция, фидбек взрыва и FTUE-бочка первого забега.
    /// Загружается из <c>Resources/BarrelConfig</c> и внедряется через <c>GameLifetimeScope</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "BarrelConfig", menuName = "ProjectB/Data/BarrelConfig")]
    public class BarrelConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "BarrelConfig";

        [Header("Idle Readability")]
        [ColorUsage(true, true)]
        [Tooltip("Цвет rim-подсветки бочки в покое (пульсирует).")]
        public Color idleRimColor = new Color(1.6f, 0.55f, 0.1f, 1f);

        [Range(0.5f, 8f)]
        public float idleRimPower = 2.2f;

        [Min(0f)]
        [Tooltip("Скорость idle-пульса rim-подсветки (рад/с).")]
        public float idlePulseSpeed = 3.2f;

        [Range(0f, 1f)]
        [Tooltip("Минимальная яркость rim в idle-пульсе (0..1 от idleRimColor).")]
        public float idlePulseMin = 0.35f;

        [Min(0f)]
        [Tooltip("Амплитуда idle-покачивания визуала (доля масштаба).")]
        public float idleWobbleAmplitude = 0.035f;

        [Header("Kick & Fuse")]
        [Min(0.05f)]
        [Tooltip("Длительность фитиля после касания героем (с).")]
        public float fuseDuration = 0.7f;

        [Min(0f)]
        [Tooltip("Начальная скорость откатывания бочки от героя при пинке (м/с).")]
        public float kickSpeed = 7f;

        [Min(0.1f)]
        [Tooltip("Замедление откатывающейся бочки (м/с²).")]
        public float kickDeceleration = 10f;

        [Tooltip("Взрывать бочку сразу, если она во время отката касается врага.")]
        public bool detonateOnEnemyContact = true;

        [Min(0.1f)]
        [Tooltip("Радиус касания врага во время отката (м).")]
        public float enemyContactRadius = 0.85f;

        [Min(1f)]
        [Tooltip("Частота мигания белой вспышки при горящем фитиле (Гц, растёт к концу).")]
        public float fuseFlashFrequency = 6f;

        [Min(0f)]
        [Tooltip("Насколько бочка раздувается к концу фитиля (доля масштаба).")]
        public float fuseSwell = 0.18f;

        [ColorUsage(true, true)]
        public Color fuseRimColor = new Color(2.4f, 0.9f, 0.15f, 1f);

        [Header("Explosion Feedback")]
        [Min(1)]
        [Tooltip("Минимум задетых врагов для всплывающей надписи BOOM ×N.")]
        public int boomLabelMinEnemies = 2;

        [Min(0f)]
        public float baseShakeIntensity = 0.32f;

        [Min(0f)]
        [Tooltip("Прибавка к тряске камеры за каждое звено цепной реакции.")]
        public float chainShakeBonusPerLink = 0.08f;

        [Min(0f)]
        public float maxShakeIntensity = 0.6f;

        [Header("FTUE (first run)")]
        [Tooltip("Ставить обучающую бочку на пути первой стаи в первом забеге.")]
        public bool ftueSpawnBarrel = true;

        [Min(2f)]
        [Tooltip("Когда ближайший враг ближе этой дистанции к герою — ставим обучающую бочку.")]
        public float ftueTriggerEnemyDistance = 11f;

        [Min(1.5f)]
        [Tooltip("Дистанция от героя до обучающей бочки (в сторону врагов).")]
        public float ftueBarrelDistance = 3.5f;

        [Min(1f)]
        [Tooltip("Подсказка скрывается через столько секунд, если бочку так и не взорвали.")]
        public float ftueHintTimeout = 14f;

        public float GetShakeIntensity(int chainDepth)
        {
            float value = baseShakeIntensity + chainShakeBonusPerLink * Mathf.Max(0, chainDepth);
            return Mathf.Min(value, Mathf.Max(baseShakeIntensity, maxShakeIntensity));
        }
    }
}
