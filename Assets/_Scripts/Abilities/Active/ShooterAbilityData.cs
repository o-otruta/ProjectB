using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewShooterAbility", menuName = "ProjectB/Data/Abilities/Shooter Ability")]
    public class ShooterAbilityData : ActiveAbilityData
    {
        [Header("Shooter Specs")]
        public GameObject projectilePrefab;
        public int projectileCount = 1;
        public float projectileSpeed = 12f;
        public float searchRadius = 10f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<ShooterAbility>();
        }
    }
}
