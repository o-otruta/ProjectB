using UnityEngine;

namespace ProjectB.Abilities
{
    public abstract class AbilityBase : MonoBehaviour
    {
        public AbilityData Data { get; protected set; }
        public virtual void Initialize(AbilityData data)
        {
            Data = data;
        }

        public abstract void ApplyModifier(ModifierType type, float value);
    }
}
