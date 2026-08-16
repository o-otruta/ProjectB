using UnityEngine;

namespace ProjectB.Data
{
    public enum MetaUpgradeEffectType
    {
        HeroHP,
        HeroDamage,
        HeroSpeed,
        MagnetRadius,
        StartLevel,
        XPBonus,
        AbilityDamage
    }

    [CreateAssetMenu(fileName = "NewMetaUpgrade", menuName = "ProjectB/Meta Upgrade Data")]
    public class MetaUpgradeData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Уникальный идентификатор, используемый в сохранениях")]
        public string id;
        
        [Header("UI Info")]
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Cost Calculation")]
        [Tooltip("Стоимость первого уровня (базовая)")]
        public int baseCost;
        [Tooltip("Множитель стоимости. Формула: cost = baseCost * Mathf.Pow(multiplier, level)")]
        public float costMultiplier = 1.2f;

        [Header("Progression")]
        public int maxLevel;
        [Tooltip("Значение, которое добавляется за каждый купленный уровень")]
        public float effectPerLevel;
        public MetaUpgradeEffectType effectType;
    }
}
