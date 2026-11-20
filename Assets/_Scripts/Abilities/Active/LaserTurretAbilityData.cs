using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "NewLaserTurretAbility", menuName = "ProjectB/Data/Abilities/Laser Turret Ability")]
    public class LaserTurretAbilityData : ActiveAbilityData
    {
        [Header("Laser Turret Specs")]
        public GameObject turretPrefab;
        public float turretLifetime = 8f;
        public float searchRadius = 8f;
        [Tooltip("Базовый урон в секунду (DPS); масштабируется AbilityDamageMultiplier из HeroRuntimeStats")]
        public float dps = 15f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Ability_{id}");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.AddComponent<LaserTurretAbility>();
        }
    }
}
