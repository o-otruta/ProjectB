using System;
using UnityEngine;

namespace ProjectB.Data.Platform
{
    /// <summary>Allowed device orientations for one device class.</summary>
    [Serializable]
    public struct OrientationRules
    {
        public bool portrait;
        public bool portraitUpsideDown;
        public bool landscapeLeft;
        public bool landscapeRight;

        public static OrientationRules PortraitOnly => new OrientationRules { portrait = true };
        public static OrientationRules All => new OrientationRules
        {
            portrait = true,
            portraitUpsideDown = true,
            landscapeLeft = true,
            landscapeRight = true
        };

        public int AllowedCount =>
            (portrait ? 1 : 0) + (portraitUpsideDown ? 1 : 0) + (landscapeLeft ? 1 : 0) + (landscapeRight ? 1 : 0);

        public bool AllowsLandscape => landscapeLeft || landscapeRight;
    }

    /// <summary>
    /// Orientation policy of one build profile (Sprint 14.1, AUDIT 4.6.А). Applied once at startup by
    /// <c>OrientationPolicyService</c>; only has an effect on mobile players (Android / iOS).
    /// </summary>
    [CreateAssetMenu(fileName = "OrientationPolicy", menuName = "ProjectB/Platform/Orientation Policy")]
    public class OrientationPolicy : ScriptableObject
    {
        public const string RESOURCE_FOLDER = "Platform/";
        public const string GOOGLE_PLAY_RESOURCE_PATH = RESOURCE_FOLDER + "OrientationPolicy_GooglePlay";
        public const string AMAZON_RESOURCE_PATH = RESOURCE_FOLDER + "OrientationPolicy_Amazon";
        public const string WEB_RESOURCE_PATH = RESOURCE_FOLDER + "OrientationPolicy_Web";

        [Header("Device Classes")]
        public OrientationRules phone = OrientationRules.PortraitOnly;
        public OrientationRules tablet = OrientationRules.All;

        [Header("Tablet Detection (from Screen.dpi)")]
        [Tooltip("Minimum physical diagonal in inches for a device to count as a tablet.")]
        [Min(0f)] public float tabletMinDiagonalInches = 7f;
        [Tooltip("Minimum physical short side in inches (Android 'sw600dp' ≈ 3.75\"). Rejects 6.9\" phablets whose diagonal is close to 7\" but whose screen is narrow.")]
        [Min(0f)] public float tabletMinShortSideInches = 3.4f;

        /// <summary>Tablet if both the physical diagonal and the short side clear their thresholds. Unknown DPI → phone.</summary>
        public bool IsTablet(float dpi, int screenWidth, int screenHeight)
        {
            if (dpi <= 0f || screenWidth <= 0 || screenHeight <= 0) return false;

            float widthInches = screenWidth / dpi;
            float heightInches = screenHeight / dpi;
            float diagonal = Mathf.Sqrt(widthInches * widthInches + heightInches * heightInches);
            float shortSide = Mathf.Min(widthInches, heightInches);
            return diagonal >= tabletMinDiagonalInches && shortSide >= tabletMinShortSideInches;
        }

        public OrientationRules Resolve(float dpi, int screenWidth, int screenHeight)
        {
            return IsTablet(dpi, screenWidth, screenHeight) ? tablet : phone;
        }
    }
}
