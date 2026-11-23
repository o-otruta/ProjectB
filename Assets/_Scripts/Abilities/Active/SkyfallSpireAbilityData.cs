using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewSkyfallSpireAbility", menuName = "ProjectB/Data/Abilities/Skyfall Spire Ability")]
    public class SkyfallSpireAbilityData : ActiveAbilityData
    {
        [Header("Skyfall Spire Specs")]
        public GameObject spirePrefab;
        public GameObject telegraphPrefab;
        [Min(1)] public int strikeCount = 1;
        [Min(0.5f)] public float strikeRadius = 3.2f;
        [Min(0.1f)] public float telegraphDelay = 0.7f;
        [Min(1f)] public float searchRadius = 12f;
        [Min(0.5f)] public float clusterRadius = 3f;
        [Min(1f)] public float spireDropHeight = 11f;

        [Header("Solar Eclipse (synergy with Black Hole)")]
        [Min(0.05f)] public float eclipseLeadTime = 0.3f;
        [Min(1f)] public float eclipsePullRadiusMultiplier = 2.2f;
        [Range(0f, 1f)] public float eclipsePullStrength = 0.75f;
        [Min(1f)] public float eclipseDamageMultiplier = 1.35f;
        [Range(0.05f, 1f)] public float eclipseBlindSlow = 0.55f;
        [Min(0f)] public float eclipseBlindDuration = 1.6f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<SkyfallSpireAbility>();
        }
    }
}
