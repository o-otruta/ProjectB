using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewChainLightningAbility", menuName = "ProjectB/Data/Abilities/Chain Lightning Ability")]
    public class ChainLightningAbilityData : ActiveAbilityData
    {
        [Header("Chain Lightning Specs")]
        public int bounceCount = 3;
        public float searchRadius = 9f;
        public float bounceRadius = 5f;
        public float boltVisualDuration = 0.16f;
        public Material lineMaterial;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<ChainLightningAbility>();
        }
    }
}
