using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "HeroData", menuName = "ProjectB/Data/HeroData")]
    public class HeroData : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Скорость передвижения героя")]
        public float moveSpeed = 5f;
        
        [Tooltip("Скорость поворота модели в сторону движения")]
        public float rotationSpeed = 15f;
        
        [Header("Stats")]
        [Tooltip("Максимальное здоровье")]
        public int maxHp = 100;
    }
}
