using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "PickupConfig", menuName = "ProjectB/Data/PickupConfig")]
    public class PickupConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "PickupConfig";

        public const int DEFAULT_MAX_ACTIVE_XP_CRYSTALS = 300;
        public const int DEFAULT_MAX_ACTIVE_COINS = 300;
        public const float DEFAULT_MAX_MERGED_SCALE_MULTIPLIER = 2.0f;
        public const float DEFAULT_MERGE_SCALE_STEP = 0.15f;

        [Header("Active Pickup Limits")]
        [Min(1)]
        [Tooltip("Максимум одновременно активных XP-кристаллов на арене; сверх порога XP сливается в ближайший кристалл.")]
        public int maxActiveXpCrystals = DEFAULT_MAX_ACTIVE_XP_CRYSTALS;

        [Min(1)]
        [Tooltip("Максимум одновременно активных монет на арене; сверх порога сумма сливается в ближайшую монету.")]
        public int maxActiveCoins = DEFAULT_MAX_ACTIVE_COINS;

        [Header("Merged Visual Scale")]
        [Min(1f)]
        [Tooltip("Максимальный множитель масштаба укрупнённого пикапа после слияний.")]
        public float maxMergedScaleMultiplier = DEFAULT_MAX_MERGED_SCALE_MULTIPLIER;

        [Min(0.01f)]
        [Tooltip("Прирост масштаба на шаг log2(1 + mergeCount).")]
        public float mergeScaleStep = DEFAULT_MERGE_SCALE_STEP;

        public float EvaluateMergedScaleMultiplier(int mergeCount)
        {
            return EvaluateMergedScaleMultiplier(mergeCount, maxMergedScaleMultiplier, mergeScaleStep);
        }

        public static float EvaluateDefaultMergedScaleMultiplier(int mergeCount)
        {
            return EvaluateMergedScaleMultiplier(mergeCount, DEFAULT_MAX_MERGED_SCALE_MULTIPLIER, DEFAULT_MERGE_SCALE_STEP);
        }

        public static float EvaluateMergedScaleMultiplier(int mergeCount, float maxScaleMultiplier, float scaleStep)
        {
            if (mergeCount <= 0) return 1f;
            float maxScale = Mathf.Max(1f, maxScaleMultiplier);
            float step = Mathf.Max(0.01f, scaleStep);
            float scaled = 1f + Mathf.Log(1f + mergeCount, 2f) * step;
            return Mathf.Min(maxScale, scaled);
        }
    }
}
