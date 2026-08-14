using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Core;
using VContainer;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    public class UpgradeManager : MonoBehaviour
    {
        [Tooltip("Количество карточек на выбор при повышении уровня")]
        public int maxCardsToOffer = 3;
        
        public List<CardData> cardPool; // Пул доступных карточек
        private List<CardData> dynamicPool = new List<CardData>(); // Пул добавленных модификаторов
        
        private CardSelectionUI cardUI;
        private HeroExperience heroExp;
        private GameManager gameManager;
        private IObjectResolver objectResolver;
        
        private int pendingLevelUps = 0;

        [Inject]
        public void Construct(IObjectResolver resolver, GameManager gameManager, CardSelectionUI cardUI, HeroExperience heroExp)
        {
            this.objectResolver = resolver;
            this.gameManager = gameManager;
            this.cardUI = cardUI;
            this.heroExp = heroExp;
        }

        private void Start()
        {
            if (heroExp != null)
            {
                heroExp.OnLevelUp += HandleLevelUp;
            }
            
            // Initialize dynamic pool with base pool
            if (cardPool != null)
            {
                dynamicPool.AddRange(cardPool);
            }
        }

        private void OnDestroy()
        {
            if (heroExp != null)
            {
                heroExp.OnLevelUp -= HandleLevelUp;
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            pendingLevelUps++;
            
            if (cardUI != null && !cardUI.IsActive)
            {
                ProcessNextLevelUp();
            }
        }

        private void ProcessNextLevelUp()
        {
            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                if (gameManager != null) gameManager.PauseForLevelUp();
                
                // Determine how many slots the UI has. By default 3 in our project.
                List<CardData> cardsToOffer = GetRandomCards(maxCardsToOffer);
                if (cardsToOffer.Count > 0)
                {
                    cardUI.ShowCards(cardsToOffer, OnCardSelected);
                }
                else
                {
                    // No cards left
                    ResumeGame();
                }
            }
            else
            {
                if (cardUI != null) cardUI.Hide();
                ResumeGame();
            }
        }

        public void AddCardsToPool(List<CardData> newCards)
        {
            if (newCards == null) return;
            dynamicPool.AddRange(newCards);
            Debug.Log($"[UpgradeManager] Added {newCards.Count} cards to dynamic pool. Total: {dynamicPool.Count}");
        }

        private List<CardData> GetRandomCards(int count)
        {
            if (dynamicPool == null || dynamicPool.Count == 0) return new List<CardData>();
            
            List<CardData> validPool = new List<CardData>();
            bool canAddActive = true;
            bool canAddPassive = true;

            HeroAbilities heroAbilities = null;
            if (objectResolver.TryResolve<HeroAbilities>(out var abilities))
            {
                canAddActive = abilities.CanAddActive();
                canAddPassive = abilities.CanAddPassive();
                heroAbilities = abilities;
            }

            // Filter out abilities if slots are full or already acquired
            foreach (var card in dynamicPool)
            {
                if (card is AbilityCardData abilityCard)
                {
                    // Skip if we already have this ability
                    if (heroAbilities != null && heroAbilities.HasAbility(abilityCard.abilityData.id))
                        continue;
                        
                    if (abilityCard.abilityData.type == AbilityType.Active && !canAddActive)
                        continue;
                    if (abilityCard.abilityData.type == AbilityType.Passive && !canAddPassive)
                        continue;
                }
                
                validPool.Add(card);
            }

            if (validPool.Count == 0) return new List<CardData>();

            int countToSelect = Mathf.Min(count, validPool.Count);
            List<CardData> selectedCards = new List<CardData>();

            for (int i = 0; i < countToSelect; i++)
            {
                CardData selectedCard = GetWeightedRandomCard(validPool);
                if (selectedCard != null)
                {
                    selectedCards.Add(selectedCard);
                    validPool.Remove(selectedCard);
                }
            }
            
            return selectedCards;
        }

        private CardData GetWeightedRandomCard(List<CardData> validPool)
        {
            float totalWeight = 0f;
            foreach (var card in validPool)
            {
                totalWeight += GetRarityWeight(card.rarity);
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var card in validPool)
            {
                currentWeight += GetRarityWeight(card.rarity);
                if (randomValue <= currentWeight)
                {
                    return card;
                }
            }

            return validPool[0]; // Fallback
        }

        private float GetRarityWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return 50f;
                case CardRarity.Rare: return 30f;
                case CardRarity.Epic: return 15f;
                case CardRarity.Legendary: return 5f;
                default: return 50f;
            }
        }

        private void OnCardSelected(CardData selectedCard)
        {
            if (selectedCard.isConsumable)
            {
                dynamicPool.Remove(selectedCard);
                Debug.Log($"[UpgradeManager] Removed consumable card {selectedCard.cardName} from pool.");
            }
            
            ApplyCardEffect(selectedCard);
            ProcessNextLevelUp();
        }

        private void ApplyCardEffect(CardData card)
        {
            Debug.Log($"[UpgradeManager] Applying card: {card.cardName}");
            card.ApplyEffect(objectResolver);
        }

        private void ResumeGame()
        {
            if (gameManager != null) gameManager.ResumeFromLevelUp();
        }
    }
}
