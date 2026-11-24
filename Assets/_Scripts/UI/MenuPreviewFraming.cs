using UnityEngine;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>
    /// Frames the main-menu hero preview per layout mode (AUDIT 4.6.Г / 14.4): in Portrait the hero sits in the
    /// middle above the info card, in Landscape it moves to the centre of the left half while the stats panel takes
    /// the right half. The camera keeps its rotation and is placed so the target projects onto the configured
    /// viewport point at the configured distance. Recomputed on <c>LayoutModeChanged</c> and on screen resize.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MenuPreviewFraming : MonoBehaviour, ILayoutModeConsumer
    {
        [SerializeField] private Transform target;
        [Tooltip("World offset from the target pivot to the visual centre of the hero.")]
        [SerializeField] private Vector3 focusOffset = Vector3.zero;

        [Header("Portrait")]
        [SerializeField] private Vector2 portraitViewportPoint = new Vector2(0.5f, 0.59f);
        [SerializeField, Min(1f)] private float portraitDistance = 7.5f;

        [Header("Landscape")]
        [SerializeField] private Vector2 landscapeViewportPoint = new Vector2(0.25f, 0.47f);
        [SerializeField, Min(1f)] private float landscapeDistance = 5.3f;

        [SerializeField, HideInInspector] private LayoutMode appliedMode = LayoutMode.Portrait;

        private Camera cam;
        private LayoutModeBinding binding;

        public LayoutMode AppliedMode => appliedMode;

        private Camera Cam => cam != null ? cam : (cam = GetComponent<Camera>());

        private void OnEnable()
        {
            if (binding == null) binding = new LayoutModeBinding(ApplyLayoutMode, Reframe);
            ApplyLayoutMode(binding.Enable() ? binding.Service.Mode : appliedMode);
        }

        private void OnDisable()
        {
            binding?.Disable();
        }

        public void ApplyLayoutMode(LayoutMode mode)
        {
            appliedMode = mode;
            Reframe();
        }

        /// <summary>Re-applies the framing of the current mode (aspect may have changed).</summary>
        public void Reframe()
        {
            if (target == null) return;
            bool landscape = appliedMode == LayoutMode.Landscape;
            transform.position = CalculateCameraPosition(
                target.position + focusOffset,
                transform.rotation,
                Cam.fieldOfView,
                Cam.aspect,
                landscape ? landscapeViewportPoint : portraitViewportPoint,
                landscape ? landscapeDistance : portraitDistance);
        }

        /// <summary>
        /// Camera position (for a fixed rotation) that puts <paramref name="focus"/> at <paramref name="viewportPoint"/>
        /// (0..1, origin bottom-left) at <paramref name="distance"/> along the view direction.
        /// </summary>
        public static Vector3 CalculateCameraPosition(Vector3 focus, Quaternion rotation, float verticalFov, float aspect, Vector2 viewportPoint, float distance)
        {
            float halfHeight = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * distance;
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            float ndcX = viewportPoint.x * 2f - 1f;
            float ndcY = viewportPoint.y * 2f - 1f;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            return focus - forward * distance - right * (ndcX * halfWidth) - up * (ndcY * halfHeight);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform previewTarget)
        {
            target = previewTarget;
            Reframe();
        }
#endif
    }
}
