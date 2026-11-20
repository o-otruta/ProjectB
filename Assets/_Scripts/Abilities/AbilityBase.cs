using UnityEngine;

namespace ProjectB.Abilities
{
    public abstract class AbilityBase : MonoBehaviour
    {
        public AbilityData Data { get; protected set; }
        public int Level { get; protected set; } = 1;

        public virtual void Initialize(AbilityData data)
        {
            Data = data;
            Level = 1;
        }

        public void IncrementLevel()
        {
            Level++;
        }

        public abstract void ApplyModifier(ModifierType type, float value);
    }
}
