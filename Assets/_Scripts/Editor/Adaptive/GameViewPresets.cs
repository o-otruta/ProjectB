using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Installs and selects fixed-resolution Game View sizes (Sprint 14.1). Unity has no public API for this,
    /// so it goes through the internal <c>GameViewSizes</c> / <c>GameView</c> types via reflection; failures are logged, never thrown.
    /// </summary>
    public static class GameViewPresets
    {
        public readonly struct Preset
        {
            public readonly int Width;
            public readonly int Height;
            public readonly string Label;

            public Preset(int width, int height, string label)
            {
                Width = width;
                Height = height;
                Label = label;
            }
        }

        public static readonly Preset PortraitDefault = new Preset(1080, 1920, "ProjectB Portrait 9:16");
        public static readonly Preset LandscapeDefault = new Preset(1920, 1080, "ProjectB Landscape 16:9");

        public static readonly Preset[] All =
        {
            PortraitDefault,
            new Preset(1080, 2340, "ProjectB Portrait 9:19.5"),
            new Preset(1080, 2520, "ProjectB Portrait 9:21"),
            LandscapeDefault,
            new Preset(1920, 1200, "ProjectB Landscape 16:10"),
            new Preset(2560, 1080, "ProjectB Landscape 21:9"),
            new Preset(1024, 768, "ProjectB Tablet 4:3"),
            new Preset(960, 600, "ProjectB Web Default 960x600"),
        };

        private const int FIXED_RESOLUTION_TYPE = 1; // UnityEditor.GameViewSizeType.FixedResolution

        private static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
        private static readonly Type SizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
        private static readonly Type SizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
        private static readonly Type SizeKindType = EditorAssembly.GetType("UnityEditor.GameViewSizeType");
        private static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");

        /// <summary>Adds every missing preset to the current platform group. Returns the number of sizes added.</summary>
        public static int InstallAll()
        {
            object group = GetCurrentGroup();
            if (group == null) return 0;

            int added = 0;
            foreach (Preset preset in All)
            {
                if (FindIndex(group, preset.Width, preset.Height) >= 0) continue;
                object size = Activator.CreateInstance(SizeType,
                    Enum.ToObject(SizeKindType, FIXED_RESOLUTION_TYPE), preset.Width, preset.Height, preset.Label);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                added++;
            }

            if (added > 0)
            {
                SizesType.GetMethod("SaveToHDD")?.Invoke(GetSizesInstance(), null);
            }
            return added;
        }

        /// <summary>Selects a fixed resolution in the main Game View, installing presets first if needed.</summary>
        public static bool Select(Preset preset)
        {
            object group = GetCurrentGroup();
            if (group == null) return false;

            int index = FindIndex(group, preset.Width, preset.Height);
            if (index < 0)
            {
                InstallAll();
                index = FindIndex(group, preset.Width, preset.Height);
            }
            if (index < 0 || GameViewType == null) return false;

            EditorWindow gameView = EditorWindow.GetWindow(GameViewType, false, null, false);
            MethodInfo callback = GameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (callback != null)
            {
                callback.Invoke(gameView, new object[] { index, null });
            }
            else
            {
                GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.SetValue(gameView, index);
            }
            gameView.Repaint();
            return true;
        }

        private static object GetSizesInstance()
        {
            if (SizesType == null) return null;
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(SizesType);
            return singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
        }

        private static object GetCurrentGroup()
        {
            try
            {
                object sizes = GetSizesInstance();
                object group = sizes != null ? SizesType.GetProperty("currentGroup")?.GetValue(sizes) : null;
                if (group == null) Debug.LogWarning("[GameViewPresets] Could not access UnityEditor.GameViewSizes (internal API changed?).");
                return group;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameViewPresets] Reflection failed: {e.Message}");
                return null;
            }
        }

        private static int FindIndex(object group, int width, int height)
        {
            Type groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
            for (int i = 0; i < total; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                int w = (int)SizeType.GetProperty("width").GetValue(size);
                int h = (int)SizeType.GetProperty("height").GetValue(size);
                int kind = Convert.ToInt32(SizeType.GetProperty("sizeType").GetValue(size));
                if (w == width && h == height && kind == FIXED_RESOLUTION_TYPE) return i;
            }
            return -1;
        }
    }
}
