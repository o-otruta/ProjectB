using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.LevelUp
{
    [CreateAssetMenu(fileName = "CardCatalog", menuName = "ProjectB/Data/Card Catalog")]
    public class CardCatalog : ScriptableObject
    {
        public const string DEFAULT_RESOURCE_PATH = "CardCatalog";

        public List<CardData> cards = new List<CardData>();
    }
}
