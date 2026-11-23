using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "FleeingLootEnemyData", menuName = "ProjectB/Data/Enemies/FleeingLootEnemyData")]
    public class FleeingLootEnemyData : EnemyData
    {
        [Header("Fleeing & Loot")]
        [Tooltip("Distance beyond which the fleeing goblin blends tangential movement so it remains catchable")]
        public float fleeOrbitDistance = 20f;

        [Min(0)]
        [Tooltip("Coins dropped at the goblin's feet each time it takes damage")]
        public int coinsPerHit = 2;

        [Tooltip("Minimum delay between on-hit coin drops (seconds)")]
        public float hitDropCooldown = 0.25f;

        [Min(1)]
        [Tooltip("Minimum total bonus coins burst on death")]
        public int deathCoinMin = 40;

        [Min(1)]
        [Tooltip("Maximum total bonus coins burst on death")]
        public int deathCoinMax = 80;

        [Min(1)]
        [Tooltip("Number of coin pickups spawned in the death burst")]
        public int deathCoinBurstCount = 12;

        [Tooltip("Max distance at which the Gold Goblin compass arrow is displayed")]
        public float compassRevealDistance = 80f;
    }
}
