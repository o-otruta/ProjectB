using UnityEngine;

namespace ProjectB.Data.Meta
{
    [CreateAssetMenu(fileName = "DailyBonusRewardData", menuName = "ProjectB/Meta/DailyBonusRewardData")]
    public class DailyBonusRewardData : ScriptableObject
    {
        [Tooltip("Награды по дням с 1 по 7")]
        public DailyBonusReward[] rewards = new DailyBonusReward[7];

        [Tooltip("Множитель для наград в виде монет с каждым новым циклом")]
        public float cycleCoinMultiplier = 1.2f;
        
        [Tooltip("Максимальное количество циклов, для которых применяется множитель")]
        public int maxMultiplierCycles = 5;
    }
}
