using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    public enum LayoutAxis
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>Per-mode settings of <see cref="AdaptiveLayoutGroup"/>.</summary>
    [Serializable]
    public struct StackModeSettings
    {
        public LayoutAxis axis;
        public float spacing;
        public TextAnchor childAlignment;
    }

    /// <summary>
    /// Horizontal-or-vertical layout group whose axis (and spacing / alignment) follows the layout mode (Sprint 14.1):
    /// e.g. LevelUp cards stack vertically in Portrait and sit in a row in Landscape. A single component on one
    /// GameObject — Unity forbids a Horizontal and a Vertical group side by side, so this replaces either of them.
    /// </summary>
    [AddComponentMenu("Layout/Adaptive Layout Group (ProjectB)")]
    public sealed class AdaptiveLayoutGroup : HorizontalOrVerticalLayoutGroup, ILayoutModeConsumer
    {
        [SerializeField] private StackModeSettings portraitSettings = new StackModeSettings
        {
            axis = LayoutAxis.Vertical,
            spacing = 24f,
            childAlignment = TextAnchor.UpperCenter
        };

        [SerializeField] private StackModeSettings landscapeSettings = new StackModeSettings
        {
            axis = LayoutAxis.Horizontal,
            spacing = 24f,
            childAlignment = TextAnchor.MiddleCenter
        };

        [SerializeField, HideInInspector] private LayoutMode appliedMode = LayoutMode.Portrait;

        private LayoutModeBinding binding;

        public LayoutMode AppliedMode => appliedMode;
        public bool IsVertical => GetSettings(appliedMode).axis == LayoutAxis.Vertical;

        public StackModeSettings GetSettings(LayoutMode mode)
        {
            return mode == LayoutMode.Landscape ? landscapeSettings : portraitSettings;
        }

        public void SetSettings(LayoutMode mode, StackModeSettings settings)
        {
            if (mode == LayoutMode.Landscape) landscapeSettings = settings;
            else portraitSettings = settings;
            if (mode == appliedMode) ApplyLayoutMode(mode);
        }

        protected override void OnEnable()
        {
            if (binding == null) binding = new LayoutModeBinding(ApplyLayoutMode);
            if (binding.Enable())
            {
                ApplySettingsSilently(binding.Service.Mode);
            }
            else
            {
                ApplySettingsSilently(appliedMode);
            }
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            binding?.Disable();
            base.OnDisable();
        }

        public void ApplyLayoutMode(LayoutMode mode)
        {
            ApplySettingsSilently(mode);
            SetDirty();
        }

        private void ApplySettingsSilently(LayoutMode mode)
        {
            appliedMode = mode;
            StackModeSettings settings = GetSettings(mode);
            m_Spacing = settings.spacing;
            m_ChildAlignment = settings.childAlignment;
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcAlongAxis(0, IsVertical);
        }

        public override void CalculateLayoutInputVertical()
        {
            CalcAlongAxis(1, IsVertical);
        }

        public override void SetLayoutHorizontal()
        {
            SetChildrenAlongAxis(0, IsVertical);
        }

        public override void SetLayoutVertical()
        {
            SetChildrenAlongAxis(1, IsVertical);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            ApplySettingsSilently(appliedMode);
            base.OnValidate();
        }
#endif
    }
}
