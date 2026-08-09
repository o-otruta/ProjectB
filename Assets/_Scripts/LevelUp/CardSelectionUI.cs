using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.LevelUp
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private CardUI[] cardSlots; 

        private System.Action<CardData> onCardSelectedCallback;

        public bool IsActive => selectionPanel != null && selectionPanel.activeSelf;

        private void Start()
        {
            Hide();
        }

        public void ShowCards(List<CardData> selectedCards, System.Action<CardData> onSelected)
        {
            onCardSelectedCallback = onSelected;

            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (i < selectedCards.Count)
                {
                    cardSlots[i].gameObject.SetActive(true);
                    cardSlots[i].Setup(selectedCards[i], OnCardClicked);
                }
                else
                {
                    cardSlots[i].gameObject.SetActive(false);
                }
            }
            
            selectionPanel.SetActive(true);
        }

        public void Hide()
        {
            if (selectionPanel != null)
                selectionPanel.SetActive(false);
        }

        private void OnCardClicked(CardData card)
        {
            if (onCardSelectedCallback == null) return;
            
            var callback = onCardSelectedCallback;
            onCardSelectedCallback = null; // Защита от двойного клика (мультитач на смартфонах)
            
            Hide();
            callback.Invoke(card);
        }
    }
}
