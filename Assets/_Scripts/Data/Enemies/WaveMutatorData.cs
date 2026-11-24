using UnityEngine;

namespace ProjectB.Data.Enemies
{
    /// <summary>
    /// Data-driven configuration for Sprint 16.3 Wave Mutators (Fog, Elite Surge, Glass Horde).
    /// </summary>
    [CreateAssetMenu(fileName = "WaveMutatorData", menuName = "ProjectB/Data/Enemies/WaveMutatorData")]
    public class WaveMutatorData : ScriptableObject
    {
        [Header("Identity & Localization")]
        public string mutatorId;
        public WaveMutatorType mutatorType = WaveMutatorType.None;
        public string titleFallback;
        public string subtitleFallback;
        public string titleKey;
        public string subtitleKey;

        [Header("Scheduling")]
        [Min(1)]
        [Tooltip("First wave on which this mutator may trigger")]
        public int minWave = 30;

        [Min(0.01f)]
        [Tooltip("Relative weighted selection probability among eligible mutators")]
        public float selectionWeight = 1f;

        [Min(1)]
        [Tooltip("How many waves this mutator stays active")]
        public int durationWaves = 1;

        [Header("Gameplay Multipliers")]
        [Min(0.1f)]
        public float enemyHpMultiplier = 1f;

        [Min(0.1f)]
        public float enemyCountMultiplier = 1f;

        [Min(0f)]
        public float eliteChanceMultiplier = 1f;

        [Min(0.1f)]
        public float xpMultiplier = 1f;

        [Min(0.1f)]
        public float coinMultiplier = 1f;

        [Min(1)]
        public int miniBossShardMultiplier = 1;

        [Header("Fog Visuals")]
        public bool enableFogVisuals = false;
        public Color fogColor = new Color(0.10f, 0.13f, 0.22f, 1f);
        [Min(1f)] public float fogStartDistance = 12f;
        [Min(2f)] public float fogEndDistance = 26f;
        [Range(0f, 1f)] public float fogOverlayAlpha = 0.88f;
        public Color directionalLightTint = new Color(0.68f, 0.78f, 0.96f, 1f);
        [Range(0.2f, 1.2f)] public float directionalLightIntensityScale = 0.68f;

        public bool IsEligibleForWave(int wave)
        {
            return mutatorType != WaveMutatorType.None
                && selectionWeight > 0f
                && wave >= Mathf.Max(1, minWave);
        }
    }
}
