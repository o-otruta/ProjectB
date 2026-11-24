using UnityEngine;

namespace ProjectB.LevelUp
{
    public enum WeaponUpgradeType
    {
        /// <summary>+N projectiles fired in a fan at different targets.</summary>
        Multishot = 0,
        /// <summary>+value% attack speed (cooldown divided by 1 + value/100).</summary>
        FireRate = 1,
        /// <summary>+value% base weapon damage.</summary>
        Power = 2,
        /// <summary>+value% melee sweep radius. Offered only for melee weapons (Knight); replaces Multishot there.</summary>
        Radius = 3
    }

    /// <summary>
    /// LevelUp card that upgrades the hero's starting weapon (Sprint 11: WeaponMod_Multishot / FireRate / Power).
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponUpgradeCard", menuName = "ProjectB/Data/Cards/Weapon Upgrade Card")]
    public class WeaponUpgradeCardData : CardData
    {
        public WeaponUpgradeType upgradeType = WeaponUpgradeType.Power;
        public float value = 30f;

        [Tooltip("How many times this upgrade can be taken per run (0 = unlimited)")]
        [Min(0)] public int maxStacks = 0;
    }
}
