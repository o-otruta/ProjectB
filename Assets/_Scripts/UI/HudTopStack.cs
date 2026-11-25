using System;
using UnityEngine;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>
    /// "Notification shade" for the top of the gameplay HUD: visible entries are stacked top-to-bottom in priority
    /// order (boss HP first, then wave events, then toasts); hidden entries take no space and the rest slide up.
    /// Followers (e.g. the combo counter) sit right below the priority entry only, so events may overlap them.
    /// Zero-GC: one pass over a fixed array per frame, unscaled time so it also works under pause / Game Over.
    /// </summary>
    public sealed class HudTopStack : MonoBehaviour, ILayoutModeConsumer
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Top-anchored container that is moved (pivot.y = 1)")]
            public RectTransform slot;
            [Tooltip("Child whose active state means the entry is shown")]
            public GameObject content;
            [Tooltip("Optional: entry counts as hidden while this group is faded out (HUD under modals)")]
            public CanvasGroup visibilityGroup;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [Tooltip("Placed right below entries[0] when it is visible, otherwise at the top offset")]
        [SerializeField] private RectTransform[] priorityFollowers = Array.Empty<RectTransform>();
        [SerializeField] private float topOffset = -250f;
        [SerializeField] private float landscapeTopOffset = -136f;
        [SerializeField, Min(0f)] private float spacing = 14f;
        [SerializeField, Min(0f)] private float slideSpeed = 14f;

        private bool[] wasVisible;
        private LayoutMode currentMode = LayoutMode.Portrait;
        private LayoutModeBinding layoutBinding;

        public float TopOffset => topOffset;
        public float LandscapeTopOffset => landscapeTopOffset;
        public float ActiveTopOffset => currentMode == LayoutMode.Landscape ? landscapeTopOffset : topOffset;
        public float Spacing => spacing;

        private void OnEnable()
        {
            if (layoutBinding == null) layoutBinding = new LayoutModeBinding(ApplyLayoutMode);
            if (layoutBinding.Enable())
            {
                currentMode = layoutBinding.Service.Mode;
            }
            Relayout(snap: true);
        }

        private void OnDisable()
        {
            layoutBinding?.Disable();
        }

        public void ApplyLayoutMode(LayoutMode mode)
        {
            currentMode = mode;
            Relayout(snap: true);
        }

        private void LateUpdate()
        {
            Relayout(snap: false);
        }

        public static bool IsEntryVisible(in Entry e)
        {
            if (e.slot == null || e.content == null || !e.content.activeInHierarchy) return false;
            return e.visibilityGroup == null || e.visibilityGroup.alpha > 0.01f;
        }

        /// <summary>Recomputes target positions; <paramref name="snap"/> jumps instead of sliding.</summary>
        public void Relayout(bool snap)
        {
            if (wasVisible == null || wasVisible.Length != entries.Length) wasVisible = new bool[entries.Length];

            float t = snap ? 1f : 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime);
            float baseTop = ActiveTopOffset;
            float cursor = baseTop;
            float followerY = baseTop;

            for (int i = 0; i < entries.Length; i++)
            {
                bool visible = IsEntryVisible(entries[i]);
                if (!visible)
                {
                    wasVisible[i] = false;
                    continue;
                }

                RectTransform slot = entries[i].slot;
                // A freshly shown entry appears in place; already visible ones slide to their new slot.
                bool snapThis = snap || !wasVisible[i];
                MoveY(slot, cursor, snapThis ? 1f : t);
                wasVisible[i] = true;

                cursor -= slot.rect.height + spacing;
                if (i == 0) followerY = cursor;
            }

            for (int i = 0; i < priorityFollowers.Length; i++)
            {
                if (priorityFollowers[i] != null) MoveY(priorityFollowers[i], followerY, t);
            }
        }

        private static void MoveY(RectTransform rt, float targetY, float t)
        {
            Vector2 p = rt.anchoredPosition;
            float y = t >= 1f ? targetY : Mathf.Lerp(p.y, targetY, t);
            if (Mathf.Abs(y - targetY) < 0.5f) y = targetY;
            if (!Mathf.Approximately(p.y, y)) rt.anchoredPosition = new Vector2(p.x, y);
        }

        public void EditorSetup(Entry[] stackEntries, RectTransform[] followers, float top, float gap)
        {
            entries = stackEntries ?? Array.Empty<Entry>();
            priorityFollowers = followers ?? Array.Empty<RectTransform>();
            topOffset = top;
            spacing = gap;
            wasVisible = null;
        }
    }
}
