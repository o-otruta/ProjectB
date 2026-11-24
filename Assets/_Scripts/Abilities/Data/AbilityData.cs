using UnityEngine;
using ProjectB.LevelUp;

namespace ProjectB.Abilities
{
    public enum AbilityType
    {
        Active,
        Passive
    }

    public abstract class AbilityData : ScriptableObject
    {
        [Header("Base Info")]
        public string id;
        public string abilityName;
        [TextArea] public string description;
        [Header("Localization")]
        public string nameKey;
        public string descriptionKey;
        public Sprite icon;
        public AbilityType type;
        public CardRarity rarity = CardRarity.Common;

        [Header("Meta Unlock")]
        [Tooltip("Если false — способность закрыта по умолчанию и разблокируется через SaveData.unlockedAbilityIds")]
        public bool unlockedByDefault = true;

        // Abstract factory method to allow data to spawn its specific ability script
        public abstract AbilityBase CreateAbility(Transform parent);
    }
}
