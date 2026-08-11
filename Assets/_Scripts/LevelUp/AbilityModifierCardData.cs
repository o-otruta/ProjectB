using UnityEngine;
using VContainer;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewAbilityModifierCard", menuName = "ProjectB/Data/Cards/Ability Modifier Card")]
    public class AbilityModifierCardData : CardData
    {
        public string targetAbilityId;
        public ModifierType modifierType;
        public float value;

        public override void ApplyEffect(IObjectResolver resolver)
        {
            if (resolver.TryResolve<HeroAbilities>(out var abilities))
            {
                abilities.UpgradeAbility(targetAbilityId, modifierType, value);
            }
            else
            {
                Debug.LogWarning("[AbilityModifierCardData] HeroAbilities not found in DI container.");
            }
        }
    }
}
