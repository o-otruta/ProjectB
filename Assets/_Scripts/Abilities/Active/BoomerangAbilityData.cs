using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewBoomerangAbility", menuName = "ProjectB/Data/Abilities/Boomerang Ability")]
    public class BoomerangAbilityData : ActiveAbilityData
    {
        [Header("Boomerang Specs")]
        public GameObject boomerangPrefab;
        public int projectileCount = 1;
        public float throwRange = 8f;
        public float travelSpeed = 10f;
        public int pierceCount = 5;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<BoomerangAbility>();
        }
    }
}
