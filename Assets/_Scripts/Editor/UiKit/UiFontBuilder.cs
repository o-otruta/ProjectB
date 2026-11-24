using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Sprint 13.1 — builds the theme fonts (OFL, with Cyrillic) as static TMP SDF atlases and the TMP material presets:
    /// <b>Russo One</b> (display: titles, numbers, buttons) and <b>Rubik</b> Medium / Bold (body text).
    /// Character set: ASCII + Latin-1 + Cyrillic (А–я, Ёё) + × → • … № and punctuation. LiberationSans stays as fallback.
    /// </summary>
    public static class UiFontBuilder
    {
        public const string FONT_DIR = "Assets/_UI/Fonts";
        public const string DISPLAY_TTF = FONT_DIR + "/RussoOne-Regular.ttf";
        public const string BODY_TTF = FONT_DIR + "/Rubik-Medium.ttf";
        public const string BODY_BOLD_TTF = FONT_DIR + "/Rubik-Bold.ttf";
        public const string DISPLAY_ASSET = FONT_DIR + "/RussoOne SDF.asset";
        public const string BODY_ASSET = FONT_DIR + "/Rubik-Medium SDF.asset";
        public const string BODY_BOLD_ASSET = FONT_DIR + "/Rubik-Bold SDF.asset";
        public const string TITLE_MATERIAL = FONT_DIR + "/RussoOne SDF - Title.mat";
        public const string NUMBER_MATERIAL = FONT_DIR + "/RussoOne SDF - Number.mat";
        public const string BODY_MATERIAL = FONT_DIR + "/Rubik-Medium SDF - Body.mat";
        public const string LIBERATION_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static readonly Color OutlineColor = new Color32(20, 16, 40, 255);

        public static uint[] BuildCharacterSet()
        {
            var set = new SortedSet<uint>();
            for (uint c = 0x20; c <= 0x7E; c++) set.Add(c);
            for (uint c = 0xA0; c <= 0xFF; c++) set.Add(c);
            for (uint c = 0x410; c <= 0x44F; c++) set.Add(c);
            set.Add(0x401); // Ё
            set.Add(0x451); // ё
            foreach (char c in "×→←↑↓•…№–—«»‘’“”‹›€₽‑ ") set.Add(c);
            set.Add(0x202F); // narrow no-break space (thousands separator)
            return new List<uint>(set).ToArray();
        }

        [MenuItem("ProjectB/UI/Build Theme Fonts")]
        public static void BuildAll()
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LIBERATION_PATH);
            TMP_FontAsset display = BuildFont(DISPLAY_TTF, DISPLAY_ASSET, 72, 10, 2048, fallback);
            TMP_FontAsset body = BuildFont(BODY_TTF, BODY_ASSET, 48, 7, 1024, fallback);
            TMP_FontAsset bodyBold = BuildFont(BODY_BOLD_TTF, BODY_BOLD_ASSET, 48, 7, 1024, fallback);

            if (display != null)
            {
                BuildMaterial(display, TITLE_MATERIAL, outline: 0.2f, underlay: true, dilate: 0.08f);
                BuildMaterial(display, NUMBER_MATERIAL, outline: 0.3f, underlay: true, dilate: 0.12f);
            }
            if (body != null)
            {
                BuildMaterial(body, BODY_MATERIAL, outline: 0f, underlay: false, dilate: 0f);
            }

            if (bodyBold != null && body != null)
            {
                // Bold fallback chain so <b> in body text keeps working.
                body.fontWeightTable[7].regularTypeface = bodyBold;
                EditorUtility.SetDirty(body);
            }

            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset BuildFont(string ttfPath, string assetPath, int samplingSize, int padding, int atlasSize, TMP_FontAsset fallback)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError($"[UiFontBuilder] Missing font file {ttfPath}");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (asset == null)
            {
                Debug.LogError($"[UiFontBuilder] Failed to create font asset for {ttfPath}");
                return null;
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            asset.name = baseName;

            if (existing != null)
            {
                // Rebuild in place to keep the GUID that scenes reference.
                EditorUtility.CopySerialized(asset, existing);
                foreach (Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    if (sub is Texture2D || sub is Material) AssetDatabase.RemoveObjectFromAsset(sub);
                }
                asset = existing;
                asset.name = baseName;
            }
            else
            {
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = baseName + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            Material material = asset.material;
            material.name = baseName + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);

            asset.TryAddCharacters(BuildCharacterSet(), out uint[] missing);
            if (missing != null && missing.Length > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (uint u in missing) sb.Append(char.ConvertFromUtf32((int)u)).Append(' ');
                Debug.Log($"[UiFontBuilder] {baseName}: {missing.Length} glyph(s) not in font (fallback used): {sb}");
            }

            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            if (fallback != null)
            {
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }

        private static void BuildMaterial(TMP_FontAsset font, string path, float outline, bool underlay, float dilate)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(font.material);
            else mat.CopyPropertiesFromMaterial(font.material);

            mat.shader = font.material.shader;
            mat.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);

            if (outline > 0f)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            }
            else
            {
                mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            }

            if (underlay)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.04f, 0.02f, 0.1f, 0.85f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.9f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, outline);
            }
            else
            {
                mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            }

            mat.name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            EditorUtility.SetDirty(mat);
        }
    }
}
