using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Abilities;

namespace ProjectB.LevelUp
{
    [Serializable]
    public struct SynergyAbilityBonus
    {
        public string targetAbilityId;
        public ModifierType modifierType;
        public float value;
    }

    [CreateAssetMenu(fileName = "NewSynergyCard", menuName = "ProjectB/Data/Cards/Synergy Card")]
    public class SynergyCardData : CardData
    {
        public string synergyId;
        public List<SynergyAbilityBonus> abilityBonuses = new List<SynergyAbilityBonus>();
        public float globalDamageBonusPercent = 0f;
        public float armorBonus = 0f;

        private void OnEnable()
        {
            isConsumable = true;
        }
    }
}
