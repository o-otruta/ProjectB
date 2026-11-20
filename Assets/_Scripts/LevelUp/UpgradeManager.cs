using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Core;
using ProjectB.Meta;
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
        private HeroAbilities heroAbilities;
        private UpgradeApplier upgradeApplier;
        private SaveManager saveManager;
        
        private int pendingLevelUps = 0;

        [Inject]
        public void Construct(
            GameManager gameManager,
            CardSelectionUI cardUI,
            HeroExperience heroExp,
            HeroAbilities heroAbilities,
            UpgradeApplier upgradeApplier,
            SaveManager saveManager)
        {
            this.gameManager = gameManager;
            this.cardUI = cardUI;
            this.heroExp = heroExp;
            this.heroAbilities = heroAbilities;
            this.upgradeApplier = upgradeApplier;
            this.saveManager = saveManager;
        }

        private void Start()
        {
            if (heroExp != null)
            {
                heroExp.OnLevelUp += HandleLevelUp;
            }
            if (gameManager != null)
            {
                gameManager.OnGameOver += HandleGameOver;
            }
            
            // Формируем пул на текущий забег: фиксируем снапшот разблокированных способностей на старте забега,
            // чтобы способности, открытые ачивками посреди забега, появлялись начиная со следующего забега.
            InitializeRunPool();

            // Выдаём стартовые выборы карточек за купленный мета-апгрейд StartLevel
            if (heroExp != null)
            {
                int startingLevelUps = heroExp.ConsumeStartingLevelUps();
                if (startingLevelUps > 0)
                {
                    StartCoroutine(GrantStartingLevelUpsNextFrame(startingLevelUps));
                }
            }
        }

        public void InitializeRunPool()
        {
            dynamicPool.Clear();
            if (cardPool == null) return;

            for (int i = 0; i < cardPool.Count; i++)
            {
                var card = cardPool[i];
                if (card == null) continue;

                if (card is AbilityCardData abilityCard)
                {
                    if (abilityCard.abilityData == null)
                    {
                        Debug.LogWarning($"[UpgradeManager] AbilityCardData '{abilityCard.name}' has null abilityData. Skipping.");
                        continue;
                    }

                    if (!IsAbilityUnlockedForRun(abilityCard.abilityData))
                    {
                        continue;
                    }
                }

                if (!dynamicPool.Contains(card))
                {
                    dynamicPool.Add(card);
                }
            }
        }

        private bool IsAbilityUnlockedForRun(AbilityData abilityData)
        {
            if (abilityData == null) return false;
            if (abilityData.unlockedByDefault) return true;
            return saveManager != null && saveManager.IsAbilityUnlocked(abilityData.id);
        }

        private System.Collections.IEnumerator GrantStartingLevelUpsNextFrame(int count)
        {
            yield return null;
            if (gameManager != null && gameManager.IsGameOver) yield break;

            pendingLevelUps += count;
            if (cardUI != null && !cardUI.IsActive)
            {
                ProcessNextLevelUp();
            }
        }

        private void OnDestroy()
        {
            if (heroExp != null)
            {
                heroExp.OnLevelUp -= HandleLevelUp;
            }
            if (gameManager != null)
            {
                gameManager.OnGameOver -= HandleGameOver;
            }
        }

        private void HandleGameOver(int wave, int coins)
        {
            pendingLevelUps = 0;
            if (cardUI != null && cardUI.IsActive)
            {
                cardUI.Hide();
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            if (gameManager != null && gameManager.IsGameOver) return;

            pendingLevelUps++;
            
            if (cardUI != null && !cardUI.IsActive)
            {
                ProcessNextLevelUp();
            }
        }

        private void ProcessNextLevelUp()
        {
            if (gameManager != null && gameManager.IsGameOver)
            {
                pendingLevelUps = 0;
                if (cardUI != null) cardUI.Hide();
                return;
            }

            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                if (gameManager != null && !gameManager.PauseForLevelUp())
                {
                    pendingLevelUps = 0;
                    if (cardUI != null) cardUI.Hide();
                    return;
                }
                
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
            for (int i = 0; i < newCards.Count; i++)
            {
                var card = newCards[i];
                if (card != null && !dynamicPool.Contains(card))
                {
                    dynamicPool.Add(card);
                }
            }
        }

        public List<CardData> GetRandomCards(int count)
        {
            if (dynamicPool == null || dynamicPool.Count == 0) return new List<CardData>();
            
            List<CardData> validPool = new List<CardData>();
            bool canAddActive = heroAbilities == null || heroAbilities.CanAddActive();
            bool canAddPassive = heroAbilities == null || heroAbilities.CanAddPassive();

            // Filter out invalid, locked, already acquired, or slot-capped abilities and orphan modifiers
            foreach (var card in dynamicPool)
            {
                if (card == null) continue;

                if (card is AbilityCardData abilityCard)
                {
                    if (abilityCard.abilityData == null)
                        continue;

                    // Skip if we already have this ability
                    if (heroAbilities != null && heroAbilities.HasAbility(abilityCard.abilityData.id))
                        continue;
                        
                    if (abilityCard.abilityData.type == AbilityType.Active && !canAddActive)
                        continue;
                    if (abilityCard.abilityData.type == AbilityType.Passive && !canAddPassive)
                        continue;
                }
                else if (card is AbilityModifierCardData modifierCard)
                {
                    if (string.IsNullOrEmpty(modifierCard.targetAbilityId))
                        continue;

                    if (heroAbilities != null && !heroAbilities.HasAbility(modifierCard.targetAbilityId))
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
            if (upgradeApplier != null)
            {
                upgradeApplier.ApplyCard(card, this);
            }
        }

        private void ResumeGame()
        {
            if (gameManager != null) gameManager.ResumeFromLevelUp();
        }
    }
}
