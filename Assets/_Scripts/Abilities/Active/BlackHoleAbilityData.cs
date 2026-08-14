using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewBlackHoleAbility", menuName = "ProjectB/Data/Abilities/Black Hole Ability")]
    public class BlackHoleAbilityData : ActiveAbilityData
    {
        [Header("Black Hole Specs")]
        public float pullRadius = 5f;
        public float pullForce = 3f;
        public float tickRate = 0.3f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<BlackHoleAbility>();
        }
    }
}
