using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Data.Platform;

namespace ProjectB.Core
{
    /// <summary>
    /// Global screen-layout tracker (Singleton entry point in <see cref="RootLifetimeScope"/>, Sprint 14.1).
    /// Polls <c>Screen.width/height/safeArea</c> once per frame without allocations and raises events only on change:
    /// <see cref="ScreenChanged"/> on any resize / safe-area change, <see cref="LayoutModeChanged"/> when the
    /// Portrait ↔ Landscape mode flips (with hysteresis from <see cref="LayoutConfig"/>).
    /// </summary>
    public sealed class LayoutModeService : ITickable
    {
        private readonly LayoutConfig config;
        private bool hasMetrics;

        public LayoutConfig Config => config;
        public LayoutMode Mode { get; private set; } = LayoutMode.Portrait;
        public bool IsLandscape => Mode == LayoutMode.Landscape;
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        public Rect SafeArea { get; private set; }
        public float Aspect { get; private set; } = 9f / 16f;

        /// <summary>Raised after <see cref="Mode"/> flips. Handlers must be cached delegates (Zero-GC).</summary>
        public event Action<LayoutMode> LayoutModeChanged;
        /// <summary>Raised after any change of screen size or safe area (also on a mode flip, after <see cref="LayoutModeChanged"/>).</summary>
        public event Action ScreenChanged;

        [Inject]
        public LayoutModeService()
            : this(Resources.Load<LayoutConfig>(LayoutConfig.RESOURCE_PATH))
        {
            UpdateMetrics(Screen.width, Screen.height, Screen.safeArea);
        }

        /// <summary>Test / manual constructor: does not sample the real screen until <see cref="UpdateMetrics"/> or <see cref="Tick"/>.</summary>
        public LayoutModeService(LayoutConfig config)
        {
            this.config = config != null ? config : LayoutConfig.CreateRuntimeDefault();
        }

        public void Tick()
        {
            UpdateMetrics(Screen.width, Screen.height, Screen.safeArea);
        }

        /// <summary>Feeds new screen metrics. Returns true if anything changed (events were raised).</summary>
        public bool UpdateMetrics(int width, int height, Rect safeArea)
        {
            if (width <= 0 || height <= 0) return false;
            if (hasMetrics && width == ScreenWidth && height == ScreenHeight && safeArea == SafeArea) return false;

            ScreenWidth = width;
            ScreenHeight = height;
            SafeArea = safeArea;
            Aspect = (float)width / height;

            LayoutMode next = hasMetrics
                ? AdaptiveLayoutMath.Evaluate(Aspect, Mode, config)
                : AdaptiveLayoutMath.EvaluateInitial(Aspect, config);
            hasMetrics = true;

            bool modeChanged = next != Mode;
            Mode = next;

            if (modeChanged)
            {
                LayoutModeChanged?.Invoke(next);
            }
            ScreenChanged?.Invoke();
            return true;
        }
    }
}
