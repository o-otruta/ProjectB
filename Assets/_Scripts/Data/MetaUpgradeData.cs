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
        AbilityDamage,
        BonusActiveSlots = 7,
        BonusPassiveSlots = 8,
        CritMultiplier = 9
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

        public int GetCostForLevel(int currentLevel)
        {
            if (currentLevel < 0 || (maxLevel > 0 && currentLevel >= maxLevel)) return -1;
            return Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, currentLevel));
        }
    }
}
