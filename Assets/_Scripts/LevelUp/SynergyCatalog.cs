using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "SynergyCatalog", menuName = "ProjectB/Data/Synergy Catalog")]
    public class SynergyCatalog : ScriptableObject
    {
        public List<SynergyData> synergies = new List<SynergyData>();

        public SynergyData GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || synergies == null) return null;

            for (int i = 0; i < synergies.Count; i++)
            {
                SynergyData syn = synergies[i];
                if (syn != null && syn.id == id)
                {
                    return syn;
                }
            }

            return null;
        }
    }
}
