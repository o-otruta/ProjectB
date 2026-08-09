using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Core;
using VContainer;

namespace ProjectB.LevelUp
{
    public class UpgradeManager : MonoBehaviour
    {
        public List<CardData> cardPool; // Пул доступных карточек
        
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
                int slots = 3; // Hardcoded fallback
                
                List<CardData> cardsToOffer = GetRandomCards(slots);
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

        private List<CardData> GetRandomCards(int count)
        {
            if (cardPool == null || cardPool.Count == 0) return new List<CardData>();
            
            int countToSelect = Mathf.Min(count, cardPool.Count);
            List<CardData> selectedCards = new List<CardData>();
            List<CardData> poolCopy = new List<CardData>(cardPool);

            for (int i = 0; i < countToSelect; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, poolCopy.Count);
                selectedCards.Add(poolCopy[randomIndex]);
                poolCopy.RemoveAt(randomIndex);
            }
            
            return selectedCards;
        }

        private void OnCardSelected(CardData selectedCard)
        {
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
