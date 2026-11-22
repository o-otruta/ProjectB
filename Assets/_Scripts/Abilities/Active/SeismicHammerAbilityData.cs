using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewSeismicHammerAbility", menuName = "ProjectB/Data/Abilities/Seismic Hammer Ability")]
    public class SeismicHammerAbilityData : ActiveAbilityData
    {
        [Header("Seismic Hammer Specs")]
        public GameObject waveVisualPrefab;
        public Material waveMaterial;
        public float slamRadius = 4.5f;
        [Range(0.05f, 1f)] public float slowFactor = 0.2f;
        public float slowDuration = 1.5f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<SeismicHammerAbility>();
        }
    }
}
