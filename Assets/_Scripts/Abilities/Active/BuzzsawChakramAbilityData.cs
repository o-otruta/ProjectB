using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewBuzzsawChakramAbility", menuName = "ProjectB/Data/Abilities/Buzzsaw Chakram Ability")]
    public class BuzzsawChakramAbilityData : ActiveAbilityData
    {
        [Header("Buzzsaw Chakram Specs")]
        public GameObject discPrefab;
        [Min(1)] public int discCount = 1;
        [Min(0)] public int ricochets = 3;
        [Min(1f)] public float ricochetRange = 5.5f;
        [Min(1f)] public float discSpeed = 15f;
        [Min(1f)] public float searchRadius = 9f;
        [Min(0.1f)] public float hitRadius = 0.55f;

        [Header("Ricochet Barrage (synergy with Critical Lens)")]
        [Min(0)] public int miniDiscRicochets = 2;
        [Range(0.1f, 1f)] public float miniDiscDamageFactor = 0.5f;
        [Range(0.2f, 1f)] public float miniDiscScale = 0.6f;
        [Min(1f)] public float critSpeedBoost = 1.15f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<BuzzsawChakramAbility>();
        }
    }
}
