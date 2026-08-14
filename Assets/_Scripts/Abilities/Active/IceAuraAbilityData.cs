using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewIceAuraAbility", menuName = "ProjectB/Data/Abilities/Ice Aura Ability")]
    public class IceAuraAbilityData : ActiveAbilityData
    {
        [Header("Ice Aura Specs")]
        public float auraRadius = 3f;
        public float slowFactor = 0.5f;
        public float slowDuration = 1f;
        public float tickRate = 0.5f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<IceAuraAbility>();
        }
    }
}
