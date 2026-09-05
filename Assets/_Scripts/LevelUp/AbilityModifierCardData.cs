using UnityEngine;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewAbilityModifierCard", menuName = "ProjectB/Data/Cards/Ability Modifier Card")]
    public class AbilityModifierCardData : CardData
    {
        public string targetAbilityId;
        public ModifierType modifierType;
        public float value;
    }
}
