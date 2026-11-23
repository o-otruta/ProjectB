using UnityEditor;
using UnityEngine;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// <c>ProjectB/UI/Preview Portrait | Landscape</c> (Sprint 14.1): switches the Game View to the 1080×1920 / 1920×1080
    /// preset and, in Edit Mode, applies that mode to every <see cref="ILayoutModeConsumer"/> in the open scenes (undoable).
    /// In Play Mode only the Game View changes — <c>LayoutModeService</c> picks the new size up by itself.
    /// Workflow: Preview Landscape → arrange → <i>Capture Landscape</i> on each ResponsiveRect → Preview Portrait before saving.
    /// </summary>
    public static class AdaptiveLayoutPreviewMenu
    {
        private const string MENU_ROOT = "ProjectB/UI/";

        [MenuItem(MENU_ROOT + "Preview Portrait", false, 100)]
        public static void PreviewPortrait()
        {
            Preview(LayoutMode.Portrait, GameViewPresets.PortraitDefault);
        }

        [MenuItem(MENU_ROOT + "Preview Landscape", false, 101)]
        public static void PreviewLandscape()
        {
            Preview(LayoutMode.Landscape, GameViewPresets.LandscapeDefault);
        }

        [MenuItem(MENU_ROOT + "Install Game View Presets", false, 120)]
        public static void InstallPresets()
        {
            int added = GameViewPresets.InstallAll();
            Debug.Log($"[AdaptiveLayout] Game View presets installed: {added} added, {GameViewPresets.All.Length} total.");
        }

        public static void Preview(LayoutMode mode, GameViewPresets.Preset preset)
        {
            if (!GameViewPresets.Select(preset))
            {
                Debug.LogWarning($"[AdaptiveLayout] Could not select Game View size {preset.Width}x{preset.Height}.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            int applied = ApplyToOpenScenes(mode);
            Debug.Log($"[AdaptiveLayout] Preview {mode}: applied to {applied} adaptive component(s).");
        }

        public static int ApplyToOpenScenes(LayoutMode mode)
        {
            int applied = 0;
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not ILayoutModeConsumer consumer) continue;
                if (EditorUtility.IsPersistent(behaviours[i])) continue;

                Undo.RecordObjects(behaviours[i].GetComponents<Component>(), $"Preview {mode} Layout");
                consumer.ApplyLayoutMode(mode);
                EditorUtility.SetDirty(behaviours[i]);
                applied++;
            }
            return applied;
        }
    }
}
