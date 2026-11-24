using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;
using ProjectB.UI;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Sprint 13.1 — <c>ProjectB/UI/Apply Theme</c>: creates / refreshes <c>Resources/UiTheme.asset</c> from the
    /// generated kit and fonts, makes the theme fonts the TMP defaults, then repaints and re-fonts both scenes and all
    /// UI prefabs in one click: every Themed* component re-applies the theme, LiberationSans texts move to the theme
    /// fonts and builtin <c>UISprite</c> / <c>Background</c> / <c>Knob</c> images move to kit sprites.
    /// </summary>
    public static class UiThemeApplier
    {
        public const string THEME_PATH = "Assets/Resources/" + UiTheme.RESOURCE_PATH + ".asset";
        public const string TMP_SETTINGS_PATH = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        public const string AUDIO_CATALOG_PATH = "Assets/Resources/AudioCatalog.asset";
        public static readonly string[] ScenePaths = { "Assets/_Scenes/MainMenu.unity", "Assets/_Scenes/Gameplay.unity" };
        public const string UI_PREFAB_DIR = "Assets/_Prefabs/UI";

        [MenuItem("ProjectB/UI/Apply Theme")]
        public static void ApplyThemeMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;
            UiTheme theme = EnsureThemeAsset();
            int prefabs = ApplyToPrefabs(theme);
            int scenes = 0;
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplyToScene(scene, theme);
                EditorSceneManager.SaveScene(scene);
                scenes++;
            }
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log($"[UiThemeApplier] Theme applied to {scenes} scene(s) and {prefabs} prefab(s).");
        }

        /// <summary>Creates or updates the theme asset with the generated kit sprites, icons, fonts and presets.</summary>
        public static UiTheme EnsureThemeAsset()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME_PATH);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, THEME_PATH);
            }

            theme.panelSprite = UiKitGenerator.LoadFrame("kit_panel");
            theme.cardSprite = UiKitGenerator.LoadFrame("kit_card");
            theme.rowSprite = UiKitGenerator.LoadFrame("kit_row");
            theme.buttonSprite = UiKitGenerator.LoadFrame("kit_button");
            theme.buttonPressedSprite = UiKitGenerator.LoadFrame("kit_button_pressed");
            theme.pillSprite = UiKitGenerator.LoadFrame("kit_pill");
            theme.circleSprite = UiKitGenerator.LoadFrame("kit_circle");
            theme.slotFrameSprite = UiKitGenerator.LoadFrame("kit_slot_frame");
            theme.barFrameSprite = UiKitGenerator.LoadFrame("kit_bar_frame");
            theme.barFillSprite = UiKitGenerator.LoadFrame("kit_bar_fill");
            theme.solidSprite = UiKitGenerator.LoadFrame("kit_solid");
            theme.glowSprite = UiKitGenerator.LoadFrame("kit_glow");
            theme.outlineSprite = UiKitGenerator.LoadFrame("kit_outline");
            theme.knobSprite = UiKitGenerator.LoadFrame("kit_knob");
            theme.ringThinSprite = UiKitGenerator.LoadFrame("kit_ring_thin");
            theme.vignetteSprite = UiKitGenerator.LoadFrame("kit_vignette");
            theme.starfieldSprite = UiKitGenerator.LoadFrame("kit_starfield");

            theme.iconCoin = UiKitGenerator.LoadIcon("coin");
            theme.iconShard = UiKitGenerator.LoadIcon("shard");
            theme.iconAd = UiKitGenerator.LoadIcon("ad");
            theme.iconGear = UiKitGenerator.LoadIcon("gear");
            theme.iconPause = UiKitGenerator.LoadIcon("pause");
            theme.iconLock = UiKitGenerator.LoadIcon("lock");
            theme.iconCheck = UiKitGenerator.LoadIcon("check");
            theme.iconChevronLeft = UiKitGenerator.LoadIcon("chevron_left");
            theme.iconChevronRight = UiKitGenerator.LoadIcon("chevron_right");
            theme.iconHeart = UiKitGenerator.LoadIcon("heart");
            theme.iconSpeed = UiKitGenerator.LoadIcon("speed");
            theme.iconSword = UiKitGenerator.LoadIcon("sword");
            theme.iconClock = UiKitGenerator.LoadIcon("clock");
            theme.iconSkull = UiKitGenerator.LoadIcon("skull");
            theme.iconStar = UiKitGenerator.LoadIcon("star");
            theme.iconPlus = UiKitGenerator.LoadIcon("plus");
            theme.iconMinus = UiKitGenerator.LoadIcon("minus");
            theme.iconTarget = UiKitGenerator.LoadIcon("target");
            theme.iconClose = UiKitGenerator.LoadIcon("close");
            theme.iconPlay = UiKitGenerator.LoadIcon("play");
            theme.iconReroll = UiKitGenerator.LoadIcon("reroll");
            theme.iconFlame = UiKitGenerator.LoadIcon("flame");
            theme.iconMusic = UiKitGenerator.LoadIcon("music");
            theme.iconSound = UiKitGenerator.LoadIcon("sound");
            theme.iconGlobe = UiKitGenerator.LoadIcon("globe");

            theme.tabUpgrades = UiKitGenerator.LoadIcon("tab_upgrades");
            theme.tabAbilities = UiKitGenerator.LoadIcon("tab_abilities");
            theme.tabPlay = UiKitGenerator.LoadIcon("tab_play");
            theme.tabAchievements = UiKitGenerator.LoadIcon("tab_achievements");
            theme.tabBonus = UiKitGenerator.LoadIcon("tab_bonus");

            theme.displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontBuilder.DISPLAY_ASSET);
            theme.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontBuilder.BODY_ASSET);
            theme.bodyBoldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontBuilder.BODY_BOLD_ASSET);
            theme.titleMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiFontBuilder.TITLE_MATERIAL);
            theme.numberMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiFontBuilder.NUMBER_MATERIAL);
            theme.bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(UiFontBuilder.BODY_MATERIAL);
            theme.iconSpriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(UiKitGenerator.SPRITE_ASSET_PATH);

            EditorUtility.SetDirty(theme);
            ConfigureTmpSettings(theme);
            ConfigureAudioCatalog();
            AssetDatabase.SaveAssets();
            UiTheme.ResetCache();
            return theme;
        }

        /// <summary>Body font becomes the TMP default (runtime-created texts), the icon sheet the default sprite asset.</summary>
        private static void ConfigureTmpSettings(UiTheme theme)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TMP_SETTINGS_PATH);
            if (settings == null) return;
            var so = new SerializedObject(settings);
            if (theme.bodyFont != null)
            {
                SerializedProperty font = so.FindProperty("m_defaultFontAsset");
                if (font != null) font.objectReferenceValue = theme.bodyFont;
            }
            if (theme.iconSpriteAsset != null)
            {
                SerializedProperty sprite = so.FindProperty("m_defaultSpriteAsset");
                if (sprite != null) sprite.objectReferenceValue = theme.iconSpriteAsset;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAudioCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AUDIO_CATALOG_PATH);
            if (catalog == null) return;
            catalog.uiConfirmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiKitGenerator.AUDIO_DIR + "/sfx_ui_confirm.wav");
            catalog.uiErrorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiKitGenerator.AUDIO_DIR + "/sfx_ui_error.wav");
            catalog.ultReadyClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiKitGenerator.AUDIO_DIR + "/sfx_ult_ready.wav");
            EditorUtility.SetDirty(catalog);
        }

        public static int ApplyToPrefabs(UiTheme theme)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { UI_PREFAB_DIR }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyToHierarchy(root.transform, theme);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return count;
        }

        public static void ApplyToScene(Scene scene, UiTheme theme)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ApplyToHierarchy(root.transform, theme);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void ApplyToHierarchy(Transform root, UiTheme theme)
        {
            TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontBuilder.LIBERATION_PATH);

            foreach (var t in root.GetComponentsInChildren<ThemedGraphic>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }
            foreach (var t in root.GetComponentsInChildren<ThemedText>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }
            foreach (var t in root.GetComponentsInChildren<ThemedButton>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }
            foreach (var t in root.GetComponentsInChildren<CurrencyLabel>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }
            foreach (var t in root.GetComponentsInChildren<BadgeUI>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }
            foreach (var t in root.GetComponentsInChildren<ProgressBarUI>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }
            foreach (var t in root.GetComponentsInChildren<TabBarUI>(true)) { t.ApplyTheme(theme); EditorUtility.SetDirty(t); }

            // Any remaining LiberationSans text (scripts that build rows at runtime keep their own) → theme fonts.
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponent<ThemedText>() != null) continue;
                if (text.font != null && text.font != liberation) continue;
                bool display = (text.fontStyle & FontStyles.Bold) != 0 || text.fontSize >= 40f;
                TMP_FontAsset font = display ? theme.displayFont : theme.bodyFont;
                if (font == null) continue;
                text.font = font;
                text.fontSharedMaterial = font.material;
                if (display) text.fontStyle &= ~FontStyles.Bold;
                if (theme.iconSpriteAsset != null) text.spriteAsset = theme.iconSpriteAsset;
                EditorUtility.SetDirty(text);
            }

            // Builtin UI sprites → kit sprites.
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                Sprite replacement = MapBuiltinSprite(image.sprite, theme);
                if (replacement == null) continue;
                image.sprite = replacement;
                image.type = replacement.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                EditorUtility.SetDirty(image);
            }
        }

        public static bool IsBuiltinUiSprite(Sprite sprite)
        {
            if (sprite == null) return false;
            string path = AssetDatabase.GetAssetPath(sprite);
            return path == "Resources/unity_builtin_extra" &&
                   (sprite.name == "UISprite" || sprite.name == "Background" || sprite.name == "Knob" || sprite.name == "InputFieldBackground" || sprite.name == "UIMask" || sprite.name == "Checkmark" || sprite.name == "DropdownArrow");
        }

        private static Sprite MapBuiltinSprite(Sprite sprite, UiTheme theme)
        {
            if (!IsBuiltinUiSprite(sprite)) return null;
            switch (sprite.name)
            {
                case "Knob": return theme.knobSprite;
                case "Background": return theme.barFrameSprite;
                case "Checkmark": return theme.iconCheck;
                default: return theme.rowSprite;
            }
        }

        public static List<string> FindLiberationTexts(Transform root)
        {
            var result = new List<string>();
            TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontBuilder.LIBERATION_PATH);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == null || text.font == liberation) result.Add(text.name);
            }
            return result;
        }
    }
}
