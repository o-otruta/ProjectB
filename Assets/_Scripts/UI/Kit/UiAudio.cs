using ProjectB.Core;
using ProjectB.Data;

namespace ProjectB.UI
{
    /// <summary>UI sound shortcut: resolves the root <see cref="AudioManager"/> lazily (Play Mode only).</summary>
    public static class UiAudio
    {
        private static AudioManager cachedAudio;

        public static void Play(SfxType type)
        {
            if (cachedAudio == null && !RootServices.TryResolve(out cachedAudio)) return;
            cachedAudio?.PlaySfx(type, randomizePitch: false);
        }

        public static void Click() => Play(SfxType.UiClick);
        public static void Confirm() => Play(SfxType.UiConfirm);
        public static void Error() => Play(SfxType.UiError);

        /// <summary>Clears the cached manager (scene reload of the root scope / tests).</summary>
        public static void ResetCache()
        {
            cachedAudio = null;
        }
    }
}
