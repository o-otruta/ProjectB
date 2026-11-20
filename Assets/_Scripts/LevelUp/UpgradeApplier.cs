using UnityEngine;
using ProjectB.Player;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    public class UpgradeApplier
    {
        private readonly HeroHealth heroHealth;
        private readonly HeroRuntimeStats runtimeStats;
        private readonly HeroAbilities heroAbilities;

        public UpgradeApplier(
            HeroHealth heroHealth,
            HeroRuntimeStats runtimeStats,
            HeroAbilities heroAbilities)
        {
            this.heroHealth = heroHealth;
            this.runtimeStats = runtimeStats;
            this.heroAbilities = heroAbilities;
        }

        public void ApplyCard(CardData card, UpgradeManager upgradeManager)
        {
            if (card == null) return;

            switch (card)
            {
                case StatUpgradeCardData statCard:
                    ApplyStatUpgrade(statCard);
                    break;
                case AbilityCardData abilityCard:
                    ApplyAbilityCard(abilityCard, upgradeManager);
                    break;
                case AbilityModifierCardData modifierCard:
                    ApplyModifierCard(modifierCard);
                    break;
                default:
                    Debug.LogWarning($"[UpgradeApplier] Unsupported card type: {card.GetType().Name}");
                    break;
            }
        }

        private void ApplyStatUpgrade(StatUpgradeCardData card)
        {
            switch (card.statType)
            {
                case StatType.MaxHP:
                    heroHealth?.IncreaseMaxHealth(Mathf.RoundToInt(card.value));
                    break;

                case StatType.MoveSpeed:
                    runtimeStats?.AddMoveSpeed(card.value);
                    break;

                case StatType.MagnetRadius:
                    runtimeStats?.AddMagnetRadius(card.value);
                    break;

                case StatType.GlobalDamage:
                    runtimeStats?.AddGlobalDamagePercent(card.value);
                    break;

                case StatType.Armor:
                    runtimeStats?.AddArmor(card.value);
                    break;

                default:
                    Debug.LogWarning($"[UpgradeApplier] Unknown StatType: {card.statType} with value {card.value}");
                    break;
            }
        }

        private void ApplyAbilityCard(AbilityCardData card, UpgradeManager upgradeManager)
        {
            bool added = false;
            if (card.abilityData != null && heroAbilities != null)
            {
                added = heroAbilities.AddAbility(card.abilityData);
            }

            if (added && card.unlockedModifiers != null && card.unlockedModifiers.Count > 0 && upgradeManager != null)
            {
                upgradeManager.AddCardsToPool(card.unlockedModifiers);
            }
        }

        private void ApplyModifierCard(AbilityModifierCardData card)
        {
            if (heroAbilities != null)
            {
                heroAbilities.UpgradeAbility(card.targetAbilityId, card.modifierType, card.value);
            }
        }
    }
}

