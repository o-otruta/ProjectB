using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "PickupConfig", menuName = "ProjectB/Data/PickupConfig")]
    public class PickupConfig : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "PickupConfig";

        [Header("Active Pickup Limits")]
        [Min(1)]
        [Tooltip("Максимум одновременно активных XP-кристаллов на арене; сверх порога XP сливается в ближайший кристалл.")]
        public int maxActiveXpCrystals = 300;

        [Min(1)]
        [Tooltip("Максимум одновременно активных монет на арене; сверх порога сумма сливается в ближайшую монету.")]
        public int maxActiveCoins = 300;

        [Header("Merged Visual Scale")]
        [Min(1f)]
        [Tooltip("Максимальный множитель масштаба укрупнённого пикапа после слияний.")]
        public float maxMergedScaleMultiplier = 2.0f;

        [Min(0.01f)]
        [Tooltip("Прирост масштаба на шаг log2(1 + mergeCount).")]
        public float mergeScaleStep = 0.15f;

        public float EvaluateMergedScaleMultiplier(int mergeCount)
        {
            if (mergeCount <= 0) return 1f;
            float maxScale = Mathf.Max(1f, maxMergedScaleMultiplier);
            float step = Mathf.Max(0.01f, mergeScaleStep);
            float scaled = 1f + Mathf.Log(1f + mergeCount, 2f) * step;
            return Mathf.Min(maxScale, scaled);
        }
    }
}
