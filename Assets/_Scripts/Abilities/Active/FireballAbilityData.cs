using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewFireballAbility", menuName = "ProjectB/Data/Abilities/Fireball Ability")]
    public class FireballAbilityData : ActiveAbilityData
    {
        [Header("Fireball Specs")]
        public GameObject projectilePrefab;
        public int projectileCount = 1;
        public float projectileSpeed = 8f;
        public float searchRadius = 12f;
        public float explosionRadius = 3f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<FireballAbility>();
        }
    }
}
