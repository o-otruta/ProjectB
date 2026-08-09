using UnityEngine;
using VContainer;
using ProjectB.Player;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewStatCard", menuName = "ProjectB/Cards/Stat Card")]
    public class StatCardData : CardData
    {
        public enum StatType
        {
            MaxHP,
            Damage,
            Speed,
            Magnet
        }

        [Header("Stat Boost Details")]
        public StatType statType;
        
        [Tooltip("Размер бонуса (например, +10 к HP, +0.5 к магниту)")]
        public float statAmount;

        public override void ApplyEffect(IObjectResolver resolver)
        {
            Debug.Log($"[StatCardData] Applying {statType} boost of {statAmount}");
            
            switch (statType)
            {
                case StatType.MaxHP:
                    var heroHealth = resolver.Resolve<HeroHealth>();
                    if (heroHealth != null)
                    {
                        heroHealth.IncreaseMaxHealth((int)statAmount);
                    }
                    break;
                    
                case StatType.Damage:
                    // TODO: Implement damage boost when combat system relies on it
                    break;
                    
                case StatType.Speed:
                    var heroMovement = resolver.Resolve<HeroMovement>();
                    if (heroMovement != null)
                    {
                        // TODO: add an AddSpeed method in HeroMovement
                        Debug.LogWarning("Speed boost not fully implemented in HeroMovement yet.");
                    }
                    break;
                    
                case StatType.Magnet:
                    var heroExp = resolver.Resolve<HeroExperience>();
                    if (heroExp != null)
                    {
                        heroExp.MagnetRadius += statAmount;
                    }
                    break;
            }
        }
    }
}
