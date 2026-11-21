using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Ads;
using ProjectB.Data;

namespace ProjectB.LevelUp
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private CardUI[] cardSlots;
        [SerializeField] private int freeCardsCount = 2;

        private System.Action<CardData> onCardSelectedCallback;
        private List<CardData> currentCards;
        private AdManager adManager;

        public bool IsActive => selectionPanel != null && selectionPanel.activeSelf;

        [Inject]
        public void Construct(AdManager adManager = null)
        {
            this.adManager = adManager;
        }

        private void Awake()
        {
            Hide();
        }

        public void ShowCards(List<CardData> selectedCards, System.Action<CardData> onSelected)
        {
            onCardSelectedCallback = onSelected;
            currentCards = selectedCards;

            RefreshSlots();

            if (selectionPanel != null)
                selectionPanel.SetActive(true);
        }

        public void Hide()
        {
            if (selectionPanel != null)
                selectionPanel.SetActive(false);
        }

        private void RefreshSlots()
        {
            if (cardSlots == null) return;

            bool canWatchAd = adManager != null && adManager.CanShowRewarded();
            int count = currentCards != null ? currentCards.Count : 0;

            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null) continue;

                if (i < count)
                {
                    int slotIndex = i;
                    bool requiresAd = slotIndex >= freeCardsCount;
                    CardData cardData = currentCards[slotIndex];

                    cardSlots[i].gameObject.SetActive(true);
                    cardSlots[i].Setup(
                        cardData,
                        clickedCard => OnCardClicked(slotIndex, clickedCard),
                        requiresAd,
                        canWatchAd);
                }
                else
                {
                    cardSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnCardClicked(int slotIndex, CardData card)
        {
            if (onCardSelectedCallback == null) return;

            bool requiresAd = slotIndex >= freeCardsCount;
            if (!requiresAd)
            {
                CompleteSelection(card);
                return;
            }

            if (adManager == null || !adManager.CanShowRewarded())
            {
                RefreshSlots();
                return;
            }

            adManager.ShowRewarded(
                AdPlacement.LevelUpThirdCard,
                onRewardGranted: () => CompleteSelection(card),
                onFailed: RefreshSlots);
        }

        private void CompleteSelection(CardData card)
        {
            if (onCardSelectedCallback == null) return;

            var callback = onCardSelectedCallback;
            onCardSelectedCallback = null; // Защита от двойного клика (мультитач на смартфонах)
            currentCards = null;

            Hide();
            callback.Invoke(card);
        }
    }
}
