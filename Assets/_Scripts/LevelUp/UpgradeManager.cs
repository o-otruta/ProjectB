using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Core;

namespace ProjectB.LevelUp
{
    public class UpgradeManager : MonoBehaviour
    {
        public List<CardData> cardPool; // Пул доступных карточек
        [SerializeField] private CardSelectionUI cardUI;
        
        private HeroExperience heroExp;
        private HeroHealth heroHealth;
        private GameManager gameManager;
        
        private int pendingLevelUps = 0;

        private void Start()
        {
            gameManager = FindAnyObjectByType<GameManager>();
            if (cardUI == null)
            {
                cardUI = FindAnyObjectByType<CardSelectionUI>();
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                heroExp = player.GetComponent<HeroExperience>();
                heroHealth = player.GetComponent<HeroHealth>();
                
                if (heroExp != null)
                {
                    heroExp.OnLevelUp += HandleLevelUp;
                }
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
            Debug.Log($"[UpgradeManager] Applied card: {card.cardName} ({card.cardType})");

            switch (card.cardType)
            {
                case CardType.StatBoost_MaxHP:
                    if (heroHealth != null)
                    {
                        heroHealth.IncreaseMaxHealth((int)card.statAmount);
                    }
                    break;
                case CardType.StatBoost_Damage:
                    // TODO: Implement damage boost
                    break;
                case CardType.StatBoost_Speed:
                    // TODO: Implement speed boost
                    break;
                case CardType.StatBoost_Magnet:
                    if (heroExp != null)
                    {
                        heroExp.MagnetRadius += card.statAmount;
                    }
                    break;
            }
        }

        private void ResumeGame()
        {
            if (gameManager != null) gameManager.ResumeFromLevelUp();
        }
    }
}
