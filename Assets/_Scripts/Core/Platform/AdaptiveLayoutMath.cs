using UnityEngine;
using ProjectB.Data.Platform;

namespace ProjectB.Core
{
    /// <summary>Pure, allocation-free layout math shared by <see cref="LayoutModeService"/>, adaptive UI components and tests.</summary>
    public static class AdaptiveLayoutMath
    {
        private const float ASPECT_EPSILON = 0.0001f;

        /// <summary>Hysteresis: only crossing a threshold changes the mode; the band in between keeps <paramref name="current"/>.</summary>
        public static LayoutMode Evaluate(float aspect, LayoutMode current, float landscapeEnterAspect, float portraitEnterAspect)
        {
            if (aspect >= landscapeEnterAspect) return LayoutMode.Landscape;
            if (aspect <= portraitEnterAspect) return LayoutMode.Portrait;
            return current;
        }

        public static LayoutMode Evaluate(float aspect, LayoutMode current, LayoutConfig config)
        {
            return Evaluate(aspect, current, config.landscapeEnterAspect, config.portraitEnterAspect);
        }

        /// <summary>First evaluation has no previous mode: inside the hysteresis band pick by the longer side (square → Portrait).</summary>
        public static LayoutMode EvaluateInitial(float aspect, LayoutConfig config)
        {
            LayoutMode fallback = aspect > 1f ? LayoutMode.Landscape : LayoutMode.Portrait;
            return Evaluate(aspect, fallback, config);
        }

        /// <summary>
        /// CanvasScaler <c>matchWidthOrHeight</c> that scales by the screen's short side (AUDIT 4.6.Б) while never letting the
        /// reference area overflow: Portrait → width (0), Landscape → height (1); screens "squarer" than the reference
        /// (a 3:4 tablet in Portrait, a 16:10 tablet in Landscape) match the other axis so the full reference layout still fits.
        /// </summary>
        public static float GetMatchWidthOrHeight(LayoutMode mode, float screenWidth, float screenHeight, Vector2 referenceResolution)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                return mode == LayoutMode.Landscape ? 1f : 0f;
            }

            float screenAspect = screenWidth / screenHeight;
            float referenceAspect = referenceResolution.x / referenceResolution.y;

            if (mode == LayoutMode.Portrait)
            {
                // Short side = width, unless the screen is relatively wider than the reference (height becomes the limit).
                return screenAspect > referenceAspect + ASPECT_EPSILON ? 1f : 0f;
            }

            // Short side = height, unless the screen is relatively narrower than the reference (width becomes the limit).
            return screenAspect < referenceAspect - ASPECT_EPSILON ? 0f : 1f;
        }

        /// <summary>Column count for adaptive grids: ultra-wide Landscape (21:9+) may get an extra column.</summary>
        public static int GetGridColumns(LayoutMode mode, float aspect, int portraitColumns, int landscapeColumns, int ultraWideColumns, float ultraWideAspect)
        {
            if (mode == LayoutMode.Portrait) return Mathf.Max(1, portraitColumns);
            if (ultraWideColumns > 0 && aspect >= ultraWideAspect) return ultraWideColumns;
            return Mathf.Max(1, landscapeColumns);
        }
    }
}
