using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewSynergyData", menuName = "ProjectB/Data/Synergy Data")]
    public class SynergyData : ScriptableObject
    {
        public string id;
        public string synergyName;
        [TextArea] public string description;
        [Header("Localization")]
        public string nameKey;
        public string descriptionKey;
        public Sprite icon;
        public CardRarity rarity = CardRarity.Legendary;
        [Tooltip("All ability IDs the hero must own for this synergy to auto-activate")]
        public List<string> requiredAbilityIds = new List<string>();
    }
}
