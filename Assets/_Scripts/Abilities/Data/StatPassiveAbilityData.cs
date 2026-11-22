using UnityEngine;

namespace ProjectB.Abilities
{
    public enum PassiveEffectType
    {
        DamagePercent = 0,
        ArmorFlat = 1,
        MoveSpeedFlat = 2,
        HealthRegenPerSecond = 3,
        MagnetRadiusFlat = 4,
        MaxHpFlat = 5,
        CooldownReductionPercent = 6,
        CritChancePercent = 7,
        CoinBonusPercent = 8
    }

    [CreateAssetMenu(fileName = "NewStatPassiveAbility", menuName = "ProjectB/Abilities/Stat Passive Ability")]
    public class StatPassiveAbilityData : PassiveAbilityData
    {
        [Header("Stat Passive Effect")]
        public PassiveEffectType effectType = PassiveEffectType.DamagePercent;
        public float baseValue = 15f;

        public override AbilityBase CreateAbility(Transform parent)
        {
            var go = new GameObject($"Passive_{id}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<StatPassiveAbility>();
        }
    }
}
