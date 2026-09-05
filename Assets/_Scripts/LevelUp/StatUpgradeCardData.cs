using UnityEngine;

namespace ProjectB.LevelUp
{
    public enum StatType
    {
        MaxHP,
        MoveSpeed,
        GlobalDamage,
        Armor,
        MagnetRadius
    }

    [CreateAssetMenu(fileName = "NewStatUpgradeCard", menuName = "ProjectB/Data/Cards/Stat Upgrade Card")]
    public class StatUpgradeCardData : CardData
    {
        public StatType statType;
        public float value;
    }
}
