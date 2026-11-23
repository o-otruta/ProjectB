using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Data
{
    /// <summary>
    /// Radial Constellation Talent Tree catalog (loaded from Resources/TalentTree).
    /// </summary>
    [CreateAssetMenu(fileName = "TalentTree", menuName = "ProjectB/Meta/Talent Tree")]
    public class TalentTreeData : ScriptableObject
    {
        public const string RESOURCE_PATH = "TalentTree";

        [Tooltip("Canvas size of the star map in UI units (square).")]
        public float canvasSize = 2200f;

        public List<TalentNodeData> nodes = new List<TalentNodeData>();

        public TalentNodeData GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || nodes == null) return null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].id == id) return nodes[i];
            }
            return null;
        }

        public TalentNodeData GetOrigin()
        {
            if (nodes == null) return null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].nodeType == TalentNodeType.Origin) return nodes[i];
            }
            return null;
        }
    }
}
