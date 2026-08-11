using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewOrbitalAbility", menuName = "ProjectB/Data/Abilities/Orbital Ability")]
    public class OrbitalCompanionAbilityData : ActiveAbilityData
    {
        public GameObject prefab;
        public int count = 1; // Default count at level 1
        public float orbitSpeed = 90f;
        public float orbitDistance = 2f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<OrbitalCompanionAbility>();
        }
    }
}
