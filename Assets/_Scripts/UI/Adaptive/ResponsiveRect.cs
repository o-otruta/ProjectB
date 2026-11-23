using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>Serialized RectTransform layout (anchors, pivot, position, size) for one layout mode.</summary>
    [Serializable]
    public struct RectPreset
    {
        public bool captured;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;

        public static RectPreset Capture(RectTransform rect)
        {
            return new RectPreset
            {
                captured = true,
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta
            };
        }

        public void ApplyTo(RectTransform rect)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }

    /// <summary>
    /// One hierarchy, two layouts (AUDIT 4.6.Б): stores a Portrait and a Landscape <see cref="RectPreset"/> and applies
    /// the matching one on <c>LayoutModeChanged</c>. Use the inspector buttons <b>Capture Portrait / Capture Landscape</b>
    /// together with the <c>ProjectB/UI/Preview Portrait | Landscape</c> menu. A mode without a captured preset is left untouched.
    /// Don't put it on children driven by a LayoutGroup — use <see cref="AdaptiveLayoutGroup"/> / <see cref="AdaptiveGridLayout"/> there.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsiveRect : MonoBehaviour, ILayoutModeConsumer
    {
        [SerializeField] private RectPreset portrait;
        [SerializeField] private RectPreset landscape;
        [SerializeField, HideInInspector] private LayoutMode appliedMode = LayoutMode.Portrait;

        private RectTransform rectTransform;
        private LayoutModeBinding binding;

        public LayoutMode AppliedMode => appliedMode;
        public RectPreset Portrait => portrait;
        public RectPreset Landscape => landscape;

        private RectTransform Rect
        {
            get
            {
                if (rectTransform == null) rectTransform = (RectTransform)transform;
                return rectTransform;
            }
        }

        private void Reset()
        {
            // Every existing screen was authored for Portrait: start from the current layout.
            portrait = RectPreset.Capture(Rect);
            landscape = default;
            appliedMode = LayoutMode.Portrait;
        }

        private void OnEnable()
        {
            if (binding == null) binding = new LayoutModeBinding(ApplyLayoutMode);
            if (binding.Enable())
            {
                ApplyLayoutMode(binding.Service.Mode);
            }
        }

        private void OnDisable()
        {
            binding?.Disable();
        }

        public bool HasPreset(LayoutMode mode)
        {
            return mode == LayoutMode.Landscape ? landscape.captured : portrait.captured;
        }

        public RectPreset GetPreset(LayoutMode mode)
        {
            return mode == LayoutMode.Landscape ? landscape : portrait;
        }

        public void CapturePreset(LayoutMode mode)
        {
            SetPreset(mode, RectPreset.Capture(Rect));
            appliedMode = mode;
        }

        public void SetPreset(LayoutMode mode, RectPreset preset)
        {
            preset.captured = true;
            if (mode == LayoutMode.Landscape) landscape = preset;
            else portrait = preset;
        }

        public void ApplyLayoutMode(LayoutMode mode)
        {
            appliedMode = mode;
            RectPreset preset = GetPreset(mode);
            if (!preset.captured) return;

            preset.ApplyTo(Rect);
            LayoutRebuilder.MarkLayoutForRebuild(Rect);
        }
    }
}
