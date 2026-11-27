using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectB.Visuals;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Builds <see cref="GameplayShaderWarmup.RESOURCE_PATH"/> containing gameplay shaders
    /// with and without <c>FOG_LINEAR</c> variants and registers it in GraphicsSettings.m_PreloadedShaders.
    /// </summary>
    public static class ShaderWarmupBuilder
    {
        private const string ASSET_PATH = "Assets/Resources/" + GameplayShaderWarmup.RESOURCE_PATH + ".shadervariants";

        private static readonly string[] TargetShaders =
        {
            "ProjectB/VoxelLit",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "ProjectB/IceAuraGround",
            "ProjectB/FrostZoneGround"
        };

        private static readonly string[][] StandardUrpKeywordSets =
        {
            Array.Empty<string>(),
            new[] { "FOG_LINEAR" },
            new[] { "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_EMISSION" },
            new[] { "FOG_LINEAR", "_EMISSION", "INSTANCING_ON" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_EMISSION" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_EMISSION", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_EMISSION", "_PICKUP_MOTION", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_EMISSION" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_EMISSION", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_EMISSION" },
            new[] { "FOG_LINEAR", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_EMISSION", "INSTANCING_ON" },
            new[] { "_SURFACE_TYPE_TRANSPARENT" },
            new[] { "FOG_LINEAR", "_SURFACE_TYPE_TRANSPARENT" },
            new[] { "FOG_LINEAR", "_SURFACE_TYPE_TRANSPARENT", "INSTANCING_ON" },
            new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON" },
            new[] { "FOG_LINEAR", "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON" },
            new[] { "FOG_LINEAR", "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "INSTANCING_ON" }
        };

        private static readonly string[][] CustomGroundKeywordSets =
        {
            Array.Empty<string>(),
            new[] { "FOG_LINEAR" },
            new[] { "INSTANCING_ON" },
            new[] { "FOG_LINEAR", "INSTANCING_ON" }
        };

        [MenuItem("ProjectB/Rendering/Rebuild Shader Warmup Collection")]
        public static void RebuildMenu()
        {
            int added = Rebuild();
            Debug.Log($"[ShaderWarmupBuilder] Rebuilt {ASSET_PATH} with {added} shader variants.");
        }

        public static int Rebuild()
        {
            string dir = Path.GetDirectoryName(ASSET_PATH);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(ASSET_PATH);
            if (collection == null)
            {
                collection = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(collection, ASSET_PATH);
            }
            else
            {
                collection.Clear();
            }

            var seenShaders = new HashSet<Shader>();
            int added = 0;

            for (int i = 0; i < TargetShaders.Length; i++)
            {
                Shader shader = Shader.Find(TargetShaders[i]);
                if (shader == null || !seenShaders.Add(shader)) continue;

                string[][] sets = TargetShaders[i].StartsWith("ProjectB/IceAuraGround", StringComparison.Ordinal) ||
                                  TargetShaders[i].StartsWith("ProjectB/FrostZoneGround", StringComparison.Ordinal)
                    ? CustomGroundKeywordSets
                    : StandardUrpKeywordSets;

                for (int s = 0; s < sets.Length; s++)
                {
                    if (TryAddVariant(collection, shader, PassType.ScriptableRenderPipeline, sets[s]))
                    {
                        added++;
                    }
                }
            }

            // Also scan materials in Assets/_Materials to pick up any other shaders used in gameplay
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Materials" }))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                Shader shader = mat.shader;

                // Add exact material keywords + FOG_LINEAR variant
                string[] matKw = mat.shaderKeywords ?? Array.Empty<string>();
                if (TryAddVariant(collection, shader, PassType.ScriptableRenderPipeline, matKw))
                {
                    added++;
                }

                bool hasFog = Array.IndexOf(matKw, "FOG_LINEAR") >= 0;
                if (!hasFog)
                {
                    string[] withFog = new string[matKw.Length + 1];
                    Array.Copy(matKw, withFog, matKw.Length);
                    withFog[matKw.Length] = "FOG_LINEAR";
                    if (TryAddVariant(collection, shader, PassType.ScriptableRenderPipeline, withFog))
                    {
                        added++;
                    }
                }
            }

            EditorUtility.SetDirty(collection);
            RegisterInGraphicsSettings(collection);
            AssetDatabase.SaveAssets();
            return added;
        }

        private static bool TryAddVariant(ShaderVariantCollection collection, Shader shader, PassType passType, string[] keywords)
        {
            try
            {
                var variant = new ShaderVariantCollection.ShaderVariant(shader, passType, keywords);
                return collection.Add(variant);
            }
            catch
            {
                return false;
            }
        }

        private static void RegisterInGraphicsSettings(ShaderVariantCollection collection)
        {
            UnityEngine.Object gs = GraphicsSettings.GetGraphicsSettings();
            if (gs == null || collection == null) return;

            var so = new SerializedObject(gs);
            SerializedProperty preloaded = so.FindProperty("m_PreloadedShaders");
            if (preloaded == null || !preloaded.isArray) return;

            bool alreadyPresent = false;
            for (int i = 0; i < preloaded.arraySize; i++)
            {
                if (preloaded.GetArrayElementAtIndex(i).objectReferenceValue == collection)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                int idx = preloaded.arraySize;
                preloaded.InsertArrayElementAtIndex(idx);
                preloaded.GetArrayElementAtIndex(idx).objectReferenceValue = collection;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(gs);
        }
    }
}
