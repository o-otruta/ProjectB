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

        [Header("Rewards")]
        [Tooltip("Amount of XP dropped on death")]
        public int xpDrop = 1;
        
        [Header("Visuals")]
        [Tooltip("Visual model prefab for this enemy")]
        public GameObject modelPrefab;
    }
}
