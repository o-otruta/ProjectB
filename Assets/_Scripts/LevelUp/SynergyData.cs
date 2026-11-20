using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "NewSynergyData", menuName = "ProjectB/Data/Synergy Data")]
    public class SynergyData : ScriptableObject
    {
        public string id;
        public string synergyName;
        [Tooltip("All ability IDs the hero must own for this synergy card to appear")]
        public List<string> requiredAbilityIds = new List<string>();
        [Tooltip("Special synergy card unlocked into the LevelUp offer pool when all required abilities are equipped")]
        public SynergyCardData rewardCard;
    }
}
