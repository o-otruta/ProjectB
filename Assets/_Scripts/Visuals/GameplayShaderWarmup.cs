using UnityEngine;

namespace ProjectB.Visuals
{
    /// <summary>
    /// Pre-warms gameplay shader variants (including FOG_LINEAR combinations) before frame 1
    /// so mutators like Dense Fog never trigger async cyan placeholder flashes or mid-run hitches.
    /// </summary>
    public static class GameplayShaderWarmup
    {
        public const string RESOURCE_PATH = "Shaders/ProjectB_GameplayWarmup";

        private static bool warmedUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            warmedUp = false;
        }

        public static void WarmUpIfNeeded()
        {
            if (warmedUp) return;
            warmedUp = true;

            ShaderVariantCollection collection = Resources.Load<ShaderVariantCollection>(RESOURCE_PATH);
            if (collection == null) return;

#if UNITY_EDITOR
            bool prevAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            try
            {
                collection.WarmUp();
            }
            finally
            {
                UnityEditor.ShaderUtil.allowAsyncCompilation = prevAsync;
            }
#else
            collection.WarmUp();
#endif
        }
    }
}
