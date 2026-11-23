using UnityEngine;

namespace ProjectB.Data.Platform
{
    /// <summary>Screen layout family. Every adaptive UI element keeps one preset per mode (AUDIT 4.6.Б).</summary>
    public enum LayoutMode
    {
        Portrait = 0,
        Landscape = 1
    }

    /// <summary>
    /// Thresholds and reference resolutions for the adaptive layout foundation (Sprint 14.1).
    /// Aspect is always width / height of the full screen.
    /// </summary>
    [CreateAssetMenu(fileName = "LayoutConfig", menuName = "ProjectB/Platform/Layout Config")]
    public class LayoutConfig : ScriptableObject
    {
        public const string RESOURCE_PATH = "LayoutConfig";

        [Header("Layout Mode Hysteresis (aspect = width / height)")]
        [Tooltip("Switch to Landscape when aspect is at or above this value.")]
        [Min(1f)] public float landscapeEnterAspect = 1.15f;
        [Tooltip("Switch to Portrait when aspect is at or below this value. Values in between keep the current mode, so square/4:3 browser windows don't flicker.")]
        [Range(0.1f, 1f)] public float portraitEnterAspect = 0.95f;

        [Header("Canvas Reference Resolutions")]
        public Vector2 portraitReferenceResolution = new Vector2(1080f, 1920f);
        public Vector2 landscapeReferenceResolution = new Vector2(1920f, 1080f);

        [Header("Grid Breakpoints")]
        [Tooltip("Landscape aspect at or above which AdaptiveGridLayout uses its ultra-wide column count (21:9 ≈ 2.33).")]
        [Min(1f)] public float ultraWideAspect = 2.1f;

        public Vector2 GetReferenceResolution(LayoutMode mode)
        {
            return mode == LayoutMode.Landscape ? landscapeReferenceResolution : portraitReferenceResolution;
        }

        /// <summary>Hidden in-memory instance with default values, used when the Resources asset is missing (tests, fresh checkouts).</summary>
        public static LayoutConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<LayoutConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }

        private void OnValidate()
        {
            if (portraitEnterAspect >= landscapeEnterAspect)
            {
                portraitEnterAspect = Mathf.Min(portraitEnterAspect, landscapeEnterAspect - 0.05f);
            }
            portraitReferenceResolution = Vector2.Max(portraitReferenceResolution, Vector2.one);
            landscapeReferenceResolution = Vector2.Max(landscapeReferenceResolution, Vector2.one);
        }
    }
}
