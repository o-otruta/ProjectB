using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Sprint 14.1 — Adaptive Foundation setup (idempotent):
    /// creates LayoutConfig + per-store OrientationPolicy assets, moves Player Settings to "Auto Rotation, Portrait only"
    /// (the runtime policy unlocks Landscape on tablets / Amazon), adds <see cref="AdaptiveCanvasScaler"/> to every
    /// CanvasScaler in MainMenu and Gameplay, and installs the Game View presets.
    /// </summary>
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint14SetupTool
    {
        private const string RESOURCES_DIR = "Assets/Resources";
        private const string LAYOUT_CONFIG_PATH = RESOURCES_DIR + "/" + LayoutConfig.RESOURCE_PATH + ".asset";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        private const string MAINMENU_SCENE_PATH = "Assets/_Scenes/MainMenu.unity";
        public static void RunAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string previousScene = SceneManager.GetActiveScene().path;

            EnsureAssets();
            ConfigurePlayerSettings();
            int scalers = SetupScene(MAINMENU_SCENE_PATH) + SetupScene(GAMEPLAY_SCENE_PATH);
            int presets = GameViewPresets.InstallAll();

            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            Debug.Log($"[Sprint14Setup] Done: AdaptiveCanvasScaler on {scalers} canvas(es), {presets} Game View preset(s) added.");
        }

        public static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder(RESOURCES_DIR + "/Platform"))
            {
                AssetDatabase.CreateFolder(RESOURCES_DIR, "Platform");
            }

            if (AssetDatabase.LoadAssetAtPath<LayoutConfig>(LAYOUT_CONFIG_PATH) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LayoutConfig>(), LAYOUT_CONFIG_PATH);
            }

            // Google Play: phone Portrait-lock, tablet auto-rotate (AUDIT 4.6.А).
            EnsurePolicy(OrientationPolicy.GOOGLE_PLAY_RESOURCE_PATH, OrientationRules.PortraitOnly, OrientationRules.All);
            // Amazon (Fire tablets): auto-rotate in every direction on every device.
            EnsurePolicy(OrientationPolicy.AMAZON_RESOURCE_PATH, OrientationRules.All, OrientationRules.All);
            // Web: the browser window decides; no restrictions.
            EnsurePolicy(OrientationPolicy.WEB_RESOURCE_PATH, OrientationRules.All, OrientationRules.All);

            AssetDatabase.SaveAssets();
        }

        private static void EnsurePolicy(string resourcePath, OrientationRules phone, OrientationRules tablet)
        {
            string assetPath = $"{RESOURCES_DIR}/{resourcePath}.asset";
            if (AssetDatabase.LoadAssetAtPath<OrientationPolicy>(assetPath) != null) return;

            var policy = ScriptableObject.CreateInstance<OrientationPolicy>();
            policy.phone = phone;
            policy.tablet = tablet;
            AssetDatabase.CreateAsset(policy, assetPath);
        }

        public static void ConfigurePlayerSettings()
        {
            // Auto Rotation restricted to Portrait behaves exactly like the old Portrait lock on phones,
            // but lets OrientationPolicyService enable Landscape at runtime without a manifest change.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = true;
            AssetDatabase.SaveAssets();
        }

        private static int SetupScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int count = 0;

            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var adaptive = scaler.GetComponent<AdaptiveCanvasScaler>();
                if (adaptive == null) adaptive = Undo.AddComponent<AdaptiveCanvasScaler>(scaler.gameObject);
                adaptive.ApplyLayoutMode(LayoutMode.Portrait);
                EditorUtility.SetDirty(scaler);
                EditorUtility.SetDirty(adaptive);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return count;
        }
    }
}
