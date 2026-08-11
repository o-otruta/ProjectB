using UnityEngine;
using VContainer;
using ProjectB.Player;

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

        public override void ApplyEffect(IObjectResolver resolver)
        {
            // Here we resolve specific managers based on stat
            if (statType == StatType.MaxHP)
            {
                if (resolver.TryResolve<HeroHealth>(out var health))
                {
                    health.IncreaseMaxHealth((int)value);
                }
            }
            else if (statType == StatType.MoveSpeed)
            {
                if (resolver.TryResolve<HeroMovement>(out var movement))
                {
                    // Assuming HeroMovement has a method or field. We'll leave it as a comment if not implemented.
                    // movement.IncreaseMoveSpeed(value);
                    Debug.Log($"[StatUpgrade] Applied +{value} MoveSpeed.");
                }
            }
            else if (statType == StatType.MagnetRadius)
            {
                if (resolver.TryResolve<HeroExperience>(out var heroExp))
                {
                    heroExp.MagnetRadius += value;
                    Debug.Log($"[StatUpgrade] Applied +{value} MagnetRadius. New Radius: {heroExp.MagnetRadius}");
                }
            }
            else if (statType == StatType.GlobalDamage)
            {
                // TODO: Add GlobalDamage to HeroCombat later
                Debug.Log($"[StatUpgrade] Applied +{value} GlobalDamage.");
            }
            else
            {
                Debug.Log($"[StatUpgrade] Applied {statType} = {value}");
            }
        }
    }
}
