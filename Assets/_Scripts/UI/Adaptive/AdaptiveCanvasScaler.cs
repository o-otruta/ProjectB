using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Core;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>
    /// Drives the sibling <see cref="CanvasScaler"/> from <see cref="LayoutModeService"/> (Sprint 14.1):
    /// reference 1080×1920 in Portrait / 1920×1080 in Landscape, <c>matchWidthOrHeight</c> by the screen's short side
    /// (see <see cref="AdaptiveLayoutMath.GetMatchWidthOrHeight"/>). Replaces hand-tuned CanvasScaler settings.
    /// Recalculates only on screen change events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour, ILayoutModeConsumer
    {
        [Tooltip("Optional. Leave empty to use the global Resources/LayoutConfig.")]
        [SerializeField] private LayoutConfig configOverride;
        [SerializeField, HideInInspector] private LayoutMode appliedMode = LayoutMode.Portrait;

        private CanvasScaler scaler;
        private LayoutModeBinding binding;
        private LayoutConfig fallbackConfig;

        public LayoutMode AppliedMode => appliedMode;
        public CanvasScaler Scaler => EnsureScaler();

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (binding.Enable())
            {
                HandleScreenChanged();
            }
        }

        private void OnDisable()
        {
            binding?.Disable();
        }

        /// <summary>Binds explicitly to a service (tests / manual wiring) and applies its current state.</summary>
        public void Bind(LayoutModeService service)
        {
            EnsureInitialized();
            binding.Bind(service);
            if (service == null) return;
            binding.Enable();
            HandleScreenChanged();
        }

        /// <summary>Editor preview / no-service path: assumes a screen with the mode's reference aspect.</summary>
        public void ApplyLayoutMode(LayoutMode mode)
        {
            LayoutConfig config = ResolveConfig();
            Vector2 reference = config.GetReferenceResolution(mode);
            Apply(mode, reference.x, reference.y, config);
        }

        public void Apply(LayoutMode mode, float screenWidth, float screenHeight, LayoutConfig config)
        {
            CanvasScaler target = EnsureScaler();
            if (target == null || config == null) return;

            Vector2 reference = config.GetReferenceResolution(mode);
            target.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            target.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            target.referenceResolution = reference;
            target.matchWidthOrHeight = AdaptiveLayoutMath.GetMatchWidthOrHeight(mode, screenWidth, screenHeight, reference);
            appliedMode = mode;
        }

        private void HandleScreenChanged()
        {
            LayoutModeService service = binding.Service;
            if (service == null) return;
            Apply(service.Mode, service.ScreenWidth, service.ScreenHeight, configOverride != null ? configOverride : service.Config);
        }

        private LayoutConfig ResolveConfig()
        {
            if (configOverride != null) return configOverride;
            if (binding != null && binding.Service != null) return binding.Service.Config;
            if (fallbackConfig == null)
            {
                fallbackConfig = Resources.Load<LayoutConfig>(LayoutConfig.RESOURCE_PATH);
                if (fallbackConfig == null) fallbackConfig = LayoutConfig.CreateRuntimeDefault();
            }
            return fallbackConfig;
        }

        private void EnsureInitialized()
        {
            EnsureScaler();
            if (binding == null)
            {
                binding = new LayoutModeBinding(null, HandleScreenChanged);
            }
        }

        private CanvasScaler EnsureScaler()
        {
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            return scaler;
        }
    }
}
