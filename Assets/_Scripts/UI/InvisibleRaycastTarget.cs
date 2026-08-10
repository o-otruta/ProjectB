using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>
    /// A Graphic that is completely invisible but still acts as a raycast target.
    /// This avoids the "alpha 0.01" hack and saves rendering overhead by not generating any geometry.
    /// </summary>
    public class InvisibleRaycastTarget : Graphic
    {
        public override void SetMaterialDirty() { }
        public override void SetVerticesDirty() { }
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
