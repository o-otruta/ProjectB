using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewGuardianShieldAbility", menuName = "ProjectB/Data/Abilities/Guardian Shield Ability")]
    public class GuardianShieldAbilityData : ActiveAbilityData
    {
        [Header("Guardian Shield Specs")]
        public GameObject shieldPrefab;
        public Material shieldMaterial;
        public int shieldCount = 2;
        public float orbitSpeed = 150f;
        public float orbitRadius = 1.6f;
        public float knockbackDistance = 1.4f;
        public float shieldHitRadius = 0.75f;
        public float hitCooldownPerEnemy = 0.45f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<GuardianShieldAbility>();
        }
    }
}
