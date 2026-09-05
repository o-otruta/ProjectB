using UnityEngine;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewAbilityCard", menuName = "ProjectB/Data/Cards/Ability Card")]
    public class AbilityCardData : CardData
    {
        public AbilityData abilityData;
        public System.Collections.Generic.List<CardData> unlockedModifiers;
    }
}
