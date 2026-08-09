using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace ProjectB.LevelUp
{
    public class CardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;

        private CardData currentData;
        private Action<CardData> onCardClicked;

        public void Setup(CardData data, Action<CardData> clickCallback)
        {
            currentData = data;
            onCardClicked = clickCallback;

            if (nameText != null) nameText.text = data.cardName;
            if (descriptionText != null) descriptionText.text = data.description;
            
            if (iconImage != null)
            {
                if (data.icon != null)
                {
                    iconImage.sprite = data.icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false); // Прячем иконку если её нет
                }
            }

            // Настройка цвета в зависимости от редкости
            if (bgImage != null)
            {
                bgImage.color = GetRarityColor(data.rarity);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onCardClicked?.Invoke(currentData);
        }

        private Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => new Color(0.8f, 0.8f, 0.8f, 1f), // Сероватый
                CardRarity.Rare => new Color(0.2f, 0.5f, 1f, 1f), // Синий
                CardRarity.Epic => new Color(0.6f, 0.2f, 0.8f, 1f), // Фиолетовый
                CardRarity.Legendary => new Color(1f, 0.8f, 0.2f, 1f), // Золотой
                _ => Color.white
            };
        }
    }
}
