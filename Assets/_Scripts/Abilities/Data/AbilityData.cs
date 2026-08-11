using UnityEngine;

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
        public Sprite icon;
        public AbilityType type;

        // Abstract factory method to allow data to spawn its specific ability script
        public abstract AbilityBase CreateAbility(Transform parent);
    }
}
