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
        [SerializeField] private GameObject adBadgeObject;
        [SerializeField] private TMP_Text adBadgeText;

        private CardData currentData;
        private Action<CardData> onCardClicked;
        private bool isInteractable = true;
        private string currentAdBadgeLabel = string.Empty;
        private Button cachedButton;
        private bool buttonChecked;

        public bool IsInteractable => isInteractable;
        public string AdBadgeLabel => adBadgeText != null ? adBadgeText.text : currentAdBadgeLabel;

        public void Setup(CardData data, Action<CardData> clickCallback, bool requiresAd = false, bool canWatchAd = true)
        {
            currentData = data;
            onCardClicked = clickCallback;
            isInteractable = !requiresAd || canWatchAd;
            currentAdBadgeLabel = requiresAd ? (canWatchAd ? "WATCH AD" : "AD UNAVAILABLE") : string.Empty;

            if (!buttonChecked)
            {
                cachedButton = GetComponent<Button>();
                buttonChecked = true;
            }
            if (cachedButton != null)
            {
                cachedButton.interactable = isInteractable;
            }

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

            if (adBadgeObject != null)
            {
                adBadgeObject.SetActive(requiresAd);
            }
            if (adBadgeText != null && requiresAd)
            {
                adBadgeText.text = currentAdBadgeLabel;
            }

            // Настройка цвета в зависимости от редкости (и доступности рекламы)
            if (bgImage != null)
            {
                Color rarityColor = GetRarityColor(data.rarity);
                bgImage.color = isInteractable
                    ? rarityColor
                    : new Color(rarityColor.r * 0.45f, rarityColor.g * 0.45f, rarityColor.b * 0.45f, 0.7f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable) return;
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
