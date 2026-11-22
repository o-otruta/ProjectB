using UnityEngine;
using ProjectB.Data.Combat;

namespace ProjectB.Data
{
    public enum HeroPassiveType
    {
        None = 0,
        MaxHpPercent = 1,
        GlobalDamagePercent = 2,
        MoveSpeedPercent = 3,
        XpAndMagnetPercent = 4
    }

    public enum HeroUltimateType
    {
        Shield = 0,
        MeteorStrike = 1,
        ArrowRain = 2,
        SoulNova = 3
    }

    [CreateAssetMenu(fileName = "HeroData", menuName = "ProjectB/Data/HeroData")]
    public class HeroData : ScriptableObject
    {
        [Header("Identity")]
        public string id = "knight";
        public string heroName = "Knight";
        [TextArea(2, 4)]
        public string description = "Sturdy warrior firing steady bolts (10 dmg, 2/s, 8m range) and protected by an impenetrable shield.";
        public Sprite icon;
        public bool unlockedByDefault = true;

        [Header("Movement")]
        [Tooltip("Hero base movement speed")]
        public float moveSpeed = 5f;
        
        [Tooltip("Visual model turn speed towards movement direction")]
        public float rotationSpeed = 15f;
        
        [Header("Stats")]
        [Tooltip("Base maximum health before passive and meta bonuses")]
        public int maxHp = 100;

        [Header("Loadout & Visuals")]
        [Tooltip("Optional override for hero starting weapon")]
        public WeaponData startingWeapon;
        [Tooltip("Optional visual model prefab for menu preview / future heroes")]
        public GameObject visualModelPrefab;

        [Header("Passive Ability")]
        public HeroPassiveType passiveType = HeroPassiveType.MaxHpPercent;
        [Tooltip("Percentage bonus value, e.g. 100 = +100%")]
        public float passiveValue = 100f;
        public string passiveName = "Iron Will";
        [TextArea(1, 3)]
        public string passiveDescription = "+100% Max HP\n<b>Weapon:</b> Steady Bolt (10 dmg, 2/s, 8m range)";

        [Header("Ultimate Ability")]
        public HeroUltimateType ultimateType = HeroUltimateType.Shield;
        public string ultimateName = "Aegis Shield";
        [TextArea(1, 3)]
        public string ultimateDescription = "Become completely invulnerable to all damage for 5 seconds.";
        public float ultimateDuration = 5f;
        public float ultimateCooldown = 25f;
        public Sprite ultimateIcon;
    }
}
