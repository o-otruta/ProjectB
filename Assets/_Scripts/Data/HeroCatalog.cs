using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Data
{
    [CreateAssetMenu(fileName = "HeroCatalog", menuName = "ProjectB/Data/HeroCatalog")]
    public class HeroCatalog : ScriptableObject
    {
        public List<HeroData> heroes = new List<HeroData>();

        public HeroData GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || heroes == null) return null;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null && heroes[i].id == id)
                {
                    return heroes[i];
                }
            }
            return null;
        }

        public HeroData GetDefaultHero()
        {
            if (heroes == null || heroes.Count == 0) return null;

            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null && heroes[i].unlockedByDefault)
                {
                    return heroes[i];
                }
            }

            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null) return heroes[i];
            }

            return null;
        }
    }
}
