using UnityEngine;
using VContainer;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewAbilityCard", menuName = "ProjectB/Data/Cards/Ability Card")]
    public class AbilityCardData : CardData
    {
        public AbilityData abilityData;
        public System.Collections.Generic.List<CardData> unlockedModifiers;

        public override void ApplyEffect(IObjectResolver resolver)
        {
            if (resolver.TryResolve<HeroAbilities>(out var manager))
            {
                manager.AddAbility(abilityData);
            }
            else
            {
                Debug.LogWarning("[AbilityCardData] HeroAbilities not found in DI container.");
            }

            if (unlockedModifiers != null && unlockedModifiers.Count > 0)
            {
                if (resolver.TryResolve<UpgradeManager>(out var upgradeManager))
                {
                    upgradeManager.AddCardsToPool(unlockedModifiers);
                }
            }
        }
    }
}
