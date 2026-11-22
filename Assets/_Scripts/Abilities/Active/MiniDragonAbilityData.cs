using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewMiniDragonAbility", menuName = "ProjectB/Data/Abilities/Mini Dragon Ability")]
    public class MiniDragonAbilityData : ActiveAbilityData
    {
        [Header("Mini Dragon Specs")]
        public GameObject followerPrefab;
        public Material dragonMaterial;
        public Material flameMaterial;
        public int dragonCount = 1;
        public int coneProjectileCount = 3;
        public float coneSpreadAngle = 26f;
        public float searchRadius = 8f;
        public float projectileSpeed = 14f;
        public float followDistance = 2.0f;
        public float followSmoothTime = 0.22f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<MiniDragonAbility>();
        }
    }
}
