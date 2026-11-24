using System;
using UnityEngine;

namespace ProjectB.Data.Platform
{
    [Serializable]
    public struct CompassMargins
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }

    /// <summary>
    /// Data-driven camera framing, ground-frustum spawn margin, and layout-aware compass HUD margins (Sprint 14.2).
    /// </summary>
    [CreateAssetMenu(fileName = "CameraConfig", menuName = "ProjectB/Platform/Camera Config")]
    public class CameraConfig : ScriptableObject
    {
        public const string RESOURCE_PATH = "CameraConfig";

        [Header("Short-Side Guaranteed View")]
        [Tooltip("Guaranteed FOV along the short screen side (35.98° preserves exact vFOV=60° on 9:16 portrait).")]
        [Range(20f, 80f)] public float shortSideFov = 35.98f;

        [Tooltip("Guaranteed vertical FOV in landscape (aspect >= 1). Separate from portrait shortSideFov so desktop 16:9 has comfortable view distance.")]
        [Range(20f, 80f)] public float landscapeShortSideFov = 50f;

        [Tooltip("Maximum aspect ratio before clamping horizontal expansion (21:9 ≈ 2.333).")]
        [Min(1f)] public float maxAspectRatio = 21f / 9f;

        [Tooltip("Minimum aspect ratio before clamping vertical expansion (9:21 ≈ 0.429).")]
        [Range(0.25f, 1f)] public float minAspectRatio = 9f / 21f;

        [Range(15f, 45f)] public float minVerticalFov = 22f;
        [Range(60f, 100f)] public float maxVerticalFov = 80f;

        [Min(1f)] public float shortSideOrthoSize = 5f;

        [Header("Off-Screen Frustum Spawn Ring")]
        [Tooltip("Extra world-space meters outside the projected camera ground frustum.")]
        [Min(0.5f)] public float spawnMargin = 2.5f;

        [Tooltip("Radial depth in meters of the spawn band beyond spawnMargin.")]
        [Min(0.5f)] public float spawnBandWidth = 4.5f;

        [Tooltip("Extra buffer beyond max spawn radius before leash teleports lagging enemies.")]
        [Min(2f)] public float leashMargin = 10f;

        [Tooltip("Push boss & VoidSummoner minion spawns outside the camera frustum along the caster's direction.")]
        public bool spawnSummonsOffscreen = false;

        [Header("Compass HUD Safe Margins (reference canvas units)")]
        public CompassMargins portraitCompassMargins = new CompassMargins
        {
            left = 85f,
            right = 85f,
            top = 240f,
            bottom = 260f
        };

        public CompassMargins landscapeCompassMargins = new CompassMargins
        {
            left = 220f,
            right = 200f,
            top = 110f,
            bottom = 90f
        };

        public CompassMargins GetCompassMargins(LayoutMode mode)
        {
            return mode == LayoutMode.Landscape ? landscapeCompassMargins : portraitCompassMargins;
        }

        /// <summary>Hidden in-memory instance with default values, used when the Resources asset is missing.</summary>
        public static CameraConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<CameraConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }

        private void OnValidate()
        {
            shortSideFov = Mathf.Clamp(shortSideFov, 20f, 80f);
            landscapeShortSideFov = Mathf.Clamp(landscapeShortSideFov, 20f, 80f);
            maxAspectRatio = Mathf.Max(1f, maxAspectRatio);
            minAspectRatio = Mathf.Clamp(minAspectRatio, 0.25f, 1f);
            minVerticalFov = Mathf.Clamp(minVerticalFov, 15f, 45f);
            maxVerticalFov = Mathf.Clamp(maxVerticalFov, minVerticalFov + 5f, 100f);
            shortSideOrthoSize = Mathf.Max(1f, shortSideOrthoSize);
            spawnMargin = Mathf.Max(0.5f, spawnMargin);
            spawnBandWidth = Mathf.Max(0.5f, spawnBandWidth);
            leashMargin = Mathf.Max(2f, leashMargin);
        }
    }
}
