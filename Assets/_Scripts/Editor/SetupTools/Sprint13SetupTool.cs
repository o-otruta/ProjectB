using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.EditorScripts.UiKit;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Sprint 13 — UI/UX redesign setup (idempotent). Runs the whole pipeline:
    /// 1) procedural UI kit + icons + atlas + TMP sprite asset + UI sounds (<see cref="UiKitGenerator"/>);
    /// 2) theme fonts with Cyrillic (<see cref="UiFontBuilder"/>);
    /// 3) <c>Resources/UiTheme.asset</c> (<see cref="UiThemeApplier"/>);
    /// 4) rebuilds the UI prefabs, the main menu screens, the HUD and the run modals in place (script components and
    ///    their DI registrations stay, visuals are regenerated from the kit);
    /// 5) applies the theme to both scenes and all UI prefabs.
    /// </summary>
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static partial class Sprint13SetupTool
    {
        public const string MAINMENU_SCENE_PATH = "Assets/_Scenes/MainMenu.unity";
        public const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";

        // Portrait layout constants at the 1080×1920 reference.
        private const float TOP_BAR_HEIGHT = 150f;
        private const float BOTTOM_BAR_HEIGHT = 200f;
        public static void RunAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunAllWithoutPrompt(regenerateAssets: true);
        }
        public static void RunLayoutsOnly()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunAllWithoutPrompt(regenerateAssets: false);
        }

        public static void RunAllWithoutPrompt(bool regenerateAssets)
        {
            string previousScene = SceneManager.GetActiveScene().path;

            if (regenerateAssets)
            {
                UiKitGenerator.GenerateAll();
                UiFontBuilder.BuildAll();
            }
            UiTheme theme = UiThemeApplier.EnsureThemeAsset();
            ConfigureHeroRoles();

            BuildPrefabs();

            Scene menu = EditorSceneManager.OpenScene(MAINMENU_SCENE_PATH, OpenSceneMode.Single);
            BuildMainMenu(menu);
            UiThemeApplier.ApplyToScene(menu, theme);
            EditorSceneManager.SaveScene(menu);

            Scene game = EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Single);
            BuildGameplay(game);
            UiThemeApplier.ApplyToScene(game, theme);
            EditorSceneManager.SaveScene(game);

            UiThemeApplier.ApplyToPrefabs(theme);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
            Debug.Log("[Sprint13Setup] UI redesign applied: kit, fonts, theme, prefabs, MainMenu and Gameplay.");
        }

        /// <summary>Role tags for the hero name plate (data-driven, editable on each HeroData).</summary>
        private static void ConfigureHeroRoles()
        {
            var roles = new System.Collections.Generic.Dictionary<string, string>
            {
                ["knight"] = "Tank",
                ["mage"] = "Area Caster",
                ["ranger"] = "Swift Archer",
                ["necromancer"] = "Soul Reaper",
                ["paladin"] = "Holy Bulwark",
                ["engineer"] = "Turret Builder"
            };
            foreach (string guid in AssetDatabase.FindAssets("t:HeroData"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<ProjectB.Data.HeroData>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero == null || string.IsNullOrEmpty(hero.id) || !roles.TryGetValue(hero.id, out string role)) continue;
                if (hero.role == role) continue;
                hero.role = role;
                EditorUtility.SetDirty(hero);
            }
        }

        private static Transform FindRequired(Transform root, string path)
        {
            Transform t = root.Find(path);
            if (t == null) throw new System.InvalidOperationException($"[Sprint13Setup] '{path}' not found under {root.name}");
            return t;
        }
    }
}
