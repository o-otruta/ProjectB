using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Data.Platform;

namespace ProjectB.Core
{
    /// <summary>
    /// Applies the build profile's <see cref="OrientationPolicy"/> once at startup (Sprint 14.1).
    /// Profile is picked by scripting define: <c>PLATFORM_CRAZYGAMES</c> / WebGL → Web, <c>STORE_AMAZON</c> → Amazon,
    /// otherwise Google Play. Only mobile players are affected; Editor, Standalone and Web are no-ops.
    /// </summary>
    public sealed class OrientationPolicyService : IInitializable
    {
        private readonly OrientationPolicy policy;

        public OrientationPolicy Policy => policy;
        public bool IsTablet { get; private set; }
        public OrientationRules AppliedRules { get; private set; }

        [Inject]
        public OrientationPolicyService()
            : this(Resources.Load<OrientationPolicy>(CurrentPlatformResourcePath))
        {
        }

        public OrientationPolicyService(OrientationPolicy policy)
        {
            this.policy = policy;
        }

        public static string CurrentPlatformResourcePath
        {
            get
            {
#if PLATFORM_CRAZYGAMES || UNITY_WEBGL
                return OrientationPolicy.WEB_RESOURCE_PATH;
#elif STORE_AMAZON
                return OrientationPolicy.AMAZON_RESOURCE_PATH;
#else
                return OrientationPolicy.GOOGLE_PLAY_RESOURCE_PATH;
#endif
            }
        }

        public void Initialize()
        {
            if (policy == null)
            {
                Debug.LogWarning($"[OrientationPolicyService] No OrientationPolicy at Resources/{CurrentPlatformResourcePath}; keeping Player Settings orientation.");
                return;
            }

            IsTablet = policy.IsTablet(Screen.dpi, Screen.width, Screen.height);
            AppliedRules = IsTablet ? policy.tablet : policy.phone;

            if (!Application.isMobilePlatform || Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return;
            }

            Apply(AppliedRules);
        }

        public static void Apply(OrientationRules rules)
        {
            int allowed = rules.AllowedCount;
            if (allowed == 0) return;

            if (allowed == 1)
            {
                Screen.orientation = rules.portrait ? ScreenOrientation.Portrait
                    : rules.portraitUpsideDown ? ScreenOrientation.PortraitUpsideDown
                    : rules.landscapeLeft ? ScreenOrientation.LandscapeLeft
                    : ScreenOrientation.LandscapeRight;
                return;
            }

            Screen.autorotateToPortrait = rules.portrait;
            Screen.autorotateToPortraitUpsideDown = rules.portraitUpsideDown;
            Screen.autorotateToLandscapeLeft = rules.landscapeLeft;
            Screen.autorotateToLandscapeRight = rules.landscapeRight;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
