using System.Runtime.InteropServices;

namespace ProjectB.Meta
{
    /// <summary>
    /// JS bridge for WebGL builds: prevents page scrolling on gameplay keys, receives tab visibility/blur events,
    /// detects the first user gesture for audio unlock, and flushes IndexedDB storage after saves.
    /// </summary>
    public static class WebBrowserBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ProjectB_RegisterBrowserHooks(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void ProjectB_FlushStorage();
#endif

        public static void RegisterBrowserHooks(string gameObjectName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(gameObjectName))
            {
                ProjectB_RegisterBrowserHooks(gameObjectName);
            }
#endif
        }

        public static void FlushStorage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ProjectB_FlushStorage();
#endif
        }
    }
}
