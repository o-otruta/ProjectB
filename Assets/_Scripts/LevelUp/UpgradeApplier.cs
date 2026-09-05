using UnityEngine;
using ProjectB.Player;
using ProjectB.Combat;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    public class UpgradeApplier
    {
        private readonly HeroHealth heroHealth;
        private readonly HeroMovement heroMovement;
        private readonly HeroExperience heroExp;
        private readonly HeroCombat heroCombat;
        private readonly HeroAbilities heroAbilities;

        public UpgradeApplier(
            HeroHealth heroHealth,
            HeroMovement heroMovement,
            HeroExperience heroExp,
            HeroCombat heroCombat,
            HeroAbilities heroAbilities)
        {
            this.heroHealth = heroHealth;
            this.heroMovement = heroMovement;
            this.heroExp = heroExp;
            this.heroCombat = heroCombat;
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
                    if (heroHealth != null)
                    {
                        heroHealth.IncreaseMaxHealth((int)card.value);
                    }
                    break;

                case StatType.MoveSpeed:
                    if (heroMovement != null)
                    {
                        heroMovement.IncreaseMoveSpeed(card.value);
                    }
                    break;

                case StatType.MagnetRadius:
                    if (heroExp != null)
                    {
                        heroExp.MagnetRadius += card.value;
                        Debug.Log($"[UpgradeApplier] Applied +{card.value} MagnetRadius. New Radius: {heroExp.MagnetRadius}");
                    }
                    break;

                case StatType.GlobalDamage:
                    if (heroCombat != null)
                    {
                        heroCombat.IncreaseDamageMultiplier(card.value / 100f);
                    }
                    break;

                case StatType.Armor:
                    // Armor support placeholder for future expansion
                    Debug.Log($"[UpgradeApplier] Applied +{card.value} Armor.");
                    break;

                default:
                    Debug.Log($"[UpgradeApplier] Unknown StatType: {card.statType} with value {card.value}");
                    break;
            }
        }

        private void ApplyAbilityCard(AbilityCardData card, UpgradeManager upgradeManager)
        {
            if (card.abilityData != null && heroAbilities != null)
            {
                heroAbilities.AddAbility(card.abilityData);
            }

            if (card.unlockedModifiers != null && card.unlockedModifiers.Count > 0 && upgradeManager != null)
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

