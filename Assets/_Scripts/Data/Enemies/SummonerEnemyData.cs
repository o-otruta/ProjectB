using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "SummonerEnemyData", menuName = "ProjectB/Data/Enemies/SummonerEnemyData")]
    public class SummonerEnemyData : EnemyData
    {
        [Header("Summoner Positioning")]
        [Tooltip("Distance at which the summoner stops advancing toward the hero")]
        public float preferredDistance = 8f;

        [Tooltip("Distance below which the summoner backs away from the hero")]
        public float retreatDistance = 4.8f;

        [Header("Summon & Heal Pulse")]
        [Tooltip("Seconds between minion summons and ally heal pulses")]
        public float summonInterval = 6f;

        [Min(1)]
        [Tooltip("Number of minions summoned per pulse")]
        public int summonCount = 2;

        [Tooltip("Minion EnemyData spawned by this summoner")]
        public EnemyData minionData;

        [Tooltip("Radius within which allied enemies are healed on pulse")]
        public float healRadius = 6f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of MaxHp restored to nearby allies on pulse (0.15 = 15%)")]
        public float healPercent = 0.15f;
    }
}
