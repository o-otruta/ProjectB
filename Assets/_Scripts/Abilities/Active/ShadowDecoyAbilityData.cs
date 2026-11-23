using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewShadowDecoyAbility", menuName = "ProjectB/Data/Abilities/Shadow Decoy Ability")]
    public class ShadowDecoyAbilityData : ActiveAbilityData
    {
        [Header("Shadow Decoy Specs")]
        public GameObject decoyPrefab;
        [Min(1)] public int decoyCount = 1;
        [Min(0.5f)] public float decoyLifetime = 3.5f;
        [Min(1f)] public float tauntRadius = 7f;
        [Min(0.1f)] public float tauntPulseInterval = 0.4f;
        [Min(0.5f)] public float explosionRadius = 3.5f;
        [Min(0f)] public float deployDistance = 2.5f;

        [Header("Phantom Legion (synergy with Mini Dragon)")]
        public GameObject phantomFireballPrefab;
        [Min(1)] public int phantomFireballCount = 8;
        [Range(0.1f, 2f)] public float phantomFireballDamageFactor = 0.6f;
        [Min(1f)] public float phantomFireballSpeed = 12f;
        [Min(0.1f)] public float phantomFireballLifetime = 1.1f;
        [Min(0.2f)] public float phantomFireballRadius = 1.6f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<ShadowDecoyAbility>();
        }
    }
}
