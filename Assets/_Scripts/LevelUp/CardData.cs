using UnityEngine;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewCardData", menuName = "ProjectB/Card Data")]
    public class CardData : ScriptableObject
    {
        public string cardName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon; // Если null, в UI можно ничего не показывать или ставить цвет
        public CardRarity rarity;
        
        [Header("Effect Details")]
        public CardType cardType;
        
        [Tooltip("Размер бонуса (например, +10 к HP, +0.5 к магниту)")]
        public float statAmount;
    }
}
