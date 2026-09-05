using UnityEngine;

namespace ProjectB.LevelUp
{
    public abstract class CardData : ScriptableObject
    {
        public string cardName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon; // Если null, в UI можно ничего не показывать или ставить цвет
        public CardRarity rarity;
        
        [Tooltip("Удалять ли карту из пула после получения (одноразовая)")]
        public bool isConsumable = false;
    }
}
