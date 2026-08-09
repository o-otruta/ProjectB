using System.Collections.Generic;
using UnityEngine;
using ProjectB.Player; // Для применения баффов к герою

namespace ProjectB.LevelUp
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private CardUI[] cardSlots; // Сюда перетянем 2 карточки из UI
        [SerializeField] private List<CardData> cardPool; // Пул всех доступных карточек
        
        private HeroExperience heroExp;
        private HeroHealth heroHealth;
        // Для MVP: можно добавить ссылки на другие компоненты героя (HeroMovement, Weapon) если нужно

        private void Start()
        {
            selectionPanel.SetActive(false);

            // Находим компоненты героя
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

        private int pendingLevelUps = 0;

        private void HandleLevelUp(int newLevel)
        {
            pendingLevelUps++;
            if (!selectionPanel.activeSelf)
            {
                ShowNextLevelUp();
            }
        }

        private void ShowNextLevelUp()
        {
            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                Time.timeScale = 0f;
                ShowCards();
                selectionPanel.SetActive(true);
            }
            else
            {
                selectionPanel.SetActive(false);
                ResumeGame();
            }
        }

        private void ShowCards()
        {
            if (cardPool == null || cardPool.Count == 0)
            {
                Debug.LogWarning("Card pool is empty!");
                ResumeGame();
                return;
            }

            // Выбираем N уникальных карт
            int countToSelect = Mathf.Min(cardSlots.Length, cardPool.Count);
            List<CardData> selectedCards = new List<CardData>();
            List<CardData> poolCopy = new List<CardData>(cardPool);

            for (int i = 0; i < countToSelect; i++)
            {
                int randomIndex = Random.Range(0, poolCopy.Count);
                selectedCards.Add(poolCopy[randomIndex]);
                poolCopy.RemoveAt(randomIndex);
            }

            // Настраиваем слоты UI
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (i < selectedCards.Count)
                {
                    cardSlots[i].gameObject.SetActive(true);
                    cardSlots[i].Setup(selectedCards[i], OnCardSelected);
                }
                else
                {
                    cardSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnCardSelected(CardData selectedCard)
        {
            ApplyCardEffect(selectedCard);
            
            // Закрываем панель и проверяем, есть ли еще левелапы в очереди
            selectionPanel.SetActive(false);
            ShowNextLevelUp();
        }

        private void ApplyCardEffect(CardData card)
        {
            Debug.Log($"[CardSelection] Applied card: {card.cardName} ({card.cardType})");

            // Применяем эффект в зависимости от типа
            switch (card.cardType)
            {
                case CardType.StatBoost_MaxHP:
                    if (heroHealth != null)
                    {
                        heroHealth.IncreaseMaxHealth((int)card.statAmount);
                    }
                    break;
                case CardType.StatBoost_Damage:
                    // TODO: Увеличить урон (нужен доступ к WeaponData или модификаторам урона)
                    break;
                case CardType.StatBoost_Speed:
                    // TODO: Увеличить скорость
                    break;
                case CardType.StatBoost_Magnet:
                    if (heroExp != null)
                    {
                        heroExp.magnetRadius += card.statAmount;
                        Debug.Log($"Magnet radius increased to {heroExp.magnetRadius}");
                    }
                    break;
                default:
                    Debug.LogWarning($"Effect not implemented for {card.cardType}");
                    break;
            }
        }

        private void ResumeGame()
        {
            Time.timeScale = 1f;
        }
    }
}
