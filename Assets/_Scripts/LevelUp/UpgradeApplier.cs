using UnityEngine;
using ProjectB.Player;
using ProjectB.Abilities;
using ProjectB.Core.Events;

namespace ProjectB.LevelUp
{
    public class UpgradeApplier
    {
        private readonly HeroHealth heroHealth;
        private readonly HeroRuntimeStats runtimeStats;
        private readonly HeroAbilities heroAbilities;
        private readonly GameEventBus eventBus;

        public UpgradeApplier(
            HeroHealth heroHealth,
            HeroRuntimeStats runtimeStats,
            HeroAbilities heroAbilities,
            GameEventBus eventBus = null)
        {
            this.heroHealth = heroHealth;
            this.runtimeStats = runtimeStats;
            this.heroAbilities = heroAbilities;
            this.eventBus = eventBus;
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
                case SynergyCardData synergyCard:
                    ApplySynergyCard(synergyCard);
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
                heroAbilities.UpgradeAbility(
                    card.targetAbilityId,
                    card.modifierType,
                    card.value,
                    card.hasSecondaryModifier,
                    card.secondaryModifierType,
                    card.secondaryValue);
            }
        }

        private void ApplySynergyCard(SynergyCardData card)
        {
            if (card == null) return;

            if (heroAbilities != null && card.abilityBonuses != null)
            {
                for (int i = 0; i < card.abilityBonuses.Count; i++)
                {
                    var bonus = card.abilityBonuses[i];
                    if (!string.IsNullOrEmpty(bonus.targetAbilityId) && heroAbilities.HasAbility(bonus.targetAbilityId))
                    {
                        heroAbilities.UpgradeAbility(bonus.targetAbilityId, bonus.modifierType, bonus.value);
                    }
                }
            }

            if (card.globalDamageBonusPercent > 0f)
            {
                runtimeStats?.AddGlobalDamagePercent(card.globalDamageBonusPercent);
            }

            if (card.armorBonus > 0f)
            {
                runtimeStats?.AddArmor(card.armorBonus);
            }

            eventBus?.Publish(new SynergyActivatedEvent(card.synergyId, card.cardName));
        }
    }
}

