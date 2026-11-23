using UnityEngine;
using ProjectB.Player;
using ProjectB.Abilities;
using ProjectB.Core.Events;
using ProjectB.Combat;

namespace ProjectB.LevelUp
{
    public class UpgradeApplier
    {
        private readonly HeroHealth heroHealth;
        private readonly HeroRuntimeStats runtimeStats;
        private readonly HeroAbilities heroAbilities;
        private readonly GameEventBus eventBus;
        private readonly HeroCombat heroCombat;

        public UpgradeApplier(
            HeroHealth heroHealth,
            HeroRuntimeStats runtimeStats,
            HeroAbilities heroAbilities,
            GameEventBus eventBus = null,
            HeroCombat heroCombat = null)
        {
            this.heroCombat = heroCombat;
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
                case WeaponUpgradeCardData weaponCard:
                    ApplyWeaponUpgrade(weaponCard);
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

        private void ApplyWeaponUpgrade(WeaponUpgradeCardData card)
        {
            if (card == null) return;
            if (heroCombat != null)
            {
                heroCombat.ApplyWeaponUpgrade(card.upgradeType, card.value);
            }
            else
            {
                Debug.LogWarning("[UpgradeApplier] HeroCombat missing; weapon upgrade card ignored.");
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

            if (heroAbilities != null)
            {
                heroAbilities.TryActivateSynergyById(card.synergyId, card.cardName);
            }
            else
            {
                eventBus?.Publish(new SynergyActivatedEvent(card.synergyId, card.cardName));
            }
        }
    }
}

