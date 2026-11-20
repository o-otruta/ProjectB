using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Abilities
{
    [CreateAssetMenu(fileName = "AbilityCatalog", menuName = "ProjectB/Abilities/Ability Catalog")]
    public class AbilityCatalog : ScriptableObject
    {
        public List<AbilityData> abilities = new List<AbilityData>();
    }
}
