using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewGlacialShatterAbility", menuName = "ProjectB/Data/Abilities/Glacial Shatter Ability")]
    public class GlacialShatterAbilityData : ActiveAbilityData
    {
        [Header("Glacial Shatter Specs")]
        public GameObject waveVisualPrefab;
        [Min(0.5f)] public float waveRadius = 5f;
        [Min(0.1f)] public float freezeDuration = 1.4f;
        [Range(0.01f, 1f)] public float frozenSlowFactor = 0.05f;
        [Tooltip("Delay after the freeze wave when still-frozen enemies shatter (fraction of freeze duration)")]
        [Range(0.2f, 1f)] public float shatterAtFraction = 0.85f;
        [Min(0f)] public float shatterDamageMultiplier = 1.5f;
        [Min(0.2f)] public float shrapnelRadius = 1.8f;
        [Range(0f, 2f)] public float shrapnelDamageFactor = 0.5f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<GlacialShatterAbility>();
        }
    }
}
