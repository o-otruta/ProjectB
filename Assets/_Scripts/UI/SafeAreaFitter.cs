using UnityEngine;

namespace ProjectB.UI
{
    /// <summary>
    /// Adjusts a RectTransform's anchors to match Screen.safeArea for notched/tall mobile displays (e.g. 9:19.5, 9:20).
    /// Zero GC allocations in Update; recalculates only when screen dimensions or safeArea change.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;

        private RectTransform rectTransform;
        private Rect lastSafeArea = Rect.zero;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }
            ApplySafeArea();
        }

        private void Update()
        {
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight ||
                Screen.safeArea != lastSafeArea)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x = applyLeft ? Mathf.Clamp01(anchorMin.x / Screen.width) : 0f;
            anchorMin.y = applyBottom ? Mathf.Clamp01(anchorMin.y / Screen.height) : 0f;
            anchorMax.x = applyRight ? Mathf.Clamp01(anchorMax.x / Screen.width) : 1f;
            anchorMax.y = applyTop ? Mathf.Clamp01(anchorMax.y / Screen.height) : 1f;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
