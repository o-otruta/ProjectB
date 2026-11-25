#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Abilities;

namespace ProjectB.Core.BalanceBot
{
    [CreateAssetMenu(fileName = "BalanceBotBuildPreset", menuName = "ProjectB/Debug/Balance Bot Build Preset")]
    public class BalanceBotBuildPreset : ScriptableObject
    {
        public string presetId = "Preset_Default";
        public List<AbilityData> priorityAbilities = new List<AbilityData>();
        public bool useFreeRerollForPreset = true;
    }
}
#endif
