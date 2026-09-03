using UnityEngine;

namespace ProjectB.Data.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "ProjectB/Data/Enemies/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Stats")]
        [Tooltip("Health points")]
        public int hp = 20;
        
        [Tooltip("Movement speed")]
        public float speed = 3f;
        
        [Tooltip("Damage dealt to the hero on contact")]
        public int contactDamage = 10;
        
        [Tooltip("Cooldown between contact damage (seconds)")]
        public float damageCooldown = 1f;

        [Tooltip("Contact damage radius")]
        public float contactRadius = 1.0f;

        [Header("Rewards")]
        [Tooltip("Amount of XP dropped on death")]
        public int xpDrop = 1;

        [Tooltip("Chance to drop a coin on death (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float coinDropChance = 0.2f;

        [Tooltip("Amount of coins dropped on death")]
        public int coinDrop = 1;
        
        [Header("Visuals")]
        [Tooltip("Visual model prefab for this enemy")]
        public GameObject modelPrefab;
    }
}
