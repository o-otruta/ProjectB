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
        public const string CARD_TITLE_MATERIAL = FONT_DIR + "/Rubik-Bold SDF - CardTitle.mat";
        public const string HEAVY_FLAT_MATERIAL = FONT_DIR + "/Rubik-Bold SDF - HeavyFlat.mat";
        public const string LIBERATION_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static readonly Color OutlineColor = new Color32(12, 10, 28, 255);
        public const float TITLE_OUTLINE = 0.30f;
        public const float TITLE_DILATE = 0.14f;
        public const float NUMBER_OUTLINE = 0.32f;
        public const float NUMBER_DILATE = 0.14f;
        public const float UNDERLAY_OFFSET_Y = -1.0f;

        public static uint[] BuildCharacterSet()
        {
            var set = new SortedSet<uint>();
            for (uint c = 0x20; c <= 0x7E; c++) set.Add(c);
            for (uint c = 0xA0; c <= 0xFF; c++) set.Add(c);
            for (uint c = 0x410; c <= 0x44F; c++) set.Add(c);
            set.Add(0x401); // Ё
            set.Add(0x451); // ё
            // Ukrainian letters (Sprint 12.3)
            set.Add(0x0404); // Є
            set.Add(0x0454); // є
            set.Add(0x0406); // І
            set.Add(0x0456); // і
            set.Add(0x0407); // Ї
            set.Add(0x0457); // ї
            set.Add(0x0490); // Ґ
            set.Add(0x0491); // ґ
            set.Add(0x02BC); // ʼ
            foreach (char c in "×→←↑↓•…№–—«»‘’“”‹›€₽‑ ") set.Add(c);
            set.Add(0x202F); // narrow no-break space (thousands separator)
            return new List<uint>(set).ToArray();
        }

        [MenuItem("ProjectB/UI/Build Theme Fonts")]
        public static void BuildAll()
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LIBERATION_PATH);
            TMP_FontAsset display = BuildFont(DISPLAY_TTF, DISPLAY_ASSET, 72, 12, 2048, fallback);
            TMP_FontAsset body = BuildFont(BODY_TTF, BODY_ASSET, 48, 7, 1024, fallback);
            TMP_FontAsset bodyBold = BuildFont(BODY_BOLD_TTF, BODY_BOLD_ASSET, 72, 12, 2048, fallback);

            if (display != null)
            {
                // Thick dark outline + hard drop (UI redesign, variant A) so titles read on any background.
                BuildMaterial(display, TITLE_MATERIAL, outline: TITLE_OUTLINE, underlay: true, dilate: TITLE_DILATE);
                BuildMaterial(display, NUMBER_MATERIAL, outline: NUMBER_OUTLINE, underlay: true, dilate: NUMBER_DILATE);
            }
            if (body != null)
            {
                BuildMaterial(body, BODY_MATERIAL, outline: 0f, underlay: false, dilate: 0f);
            }
            if (bodyBold != null)
            {
                BuildMaterial(bodyBold, CARD_TITLE_MATERIAL, outline: 0.22f, underlay: true, dilate: 0.22f);
                BuildMaterial(bodyBold, HEAVY_FLAT_MATERIAL, outline: 0f, underlay: false, dilate: 0.20f);
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

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (asset == null)
            {
                Debug.LogError($"[UiFontBuilder] Failed to create font asset for {ttfPath}");
                return null;
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            asset.name = baseName;

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

            Texture2D builtAtlas = asset.atlasTextures[0];
            builtAtlas.Apply(false, false);
            Material builtMat = asset.material;

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                Texture2D existingAtlas = null;
                Material existingMat = null;
                foreach (Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    if (existingAtlas == null && sub is Texture2D tex) existingAtlas = tex;
                    else if (existingMat == null && sub is Material mat) existingMat = mat;
                    else if (sub is Texture2D || sub is Material) AssetDatabase.RemoveObjectFromAsset(sub);
                }

                EditorUtility.CopySerialized(asset, existing);
                existing.name = baseName;

                if (existingAtlas != null)
                {
                    EditorUtility.CopySerialized(builtAtlas, existingAtlas);
                    existingAtlas.name = baseName + " Atlas";
                    Object.DestroyImmediate(builtAtlas);
                }
                else
                {
                    existingAtlas = builtAtlas;
                    existingAtlas.name = baseName + " Atlas";
                    AssetDatabase.AddObjectToAsset(existingAtlas, existing);
                }

                if (existingMat != null)
                {
                    existingMat.shader = builtMat.shader;
                    existingMat.CopyPropertiesFromMaterial(builtMat);
                    existingMat.name = baseName + " Material";
                    Object.DestroyImmediate(builtMat);
                }
                else
                {
                    existingMat = builtMat;
                    existingMat.name = baseName + " Material";
                    AssetDatabase.AddObjectToAsset(existingMat, existing);
                }

                existingMat.SetTexture(ShaderUtilities.ID_MainTex, existingAtlas);
                existing.atlasTextures = new[] { existingAtlas };
                existing.material = existingMat;
                existing.ReadFontAssetDefinition();

                EditorUtility.SetDirty(existingAtlas);
                EditorUtility.SetDirty(existingMat);
                EditorUtility.SetDirty(existing);

                Object.DestroyImmediate(asset);
                asset = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                builtAtlas.name = baseName + " Atlas";
                AssetDatabase.AddObjectToAsset(builtAtlas, asset);
                builtMat.name = baseName + " Material";
                builtMat.SetTexture(ShaderUtilities.ID_MainTex, builtAtlas);
                AssetDatabase.AddObjectToAsset(builtMat, asset);
                asset.ReadFontAssetDefinition();
                EditorUtility.SetDirty(asset);
            }

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
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.03f, 0.02f, 0.09f, 1f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, UNDERLAY_OFFSET_Y);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, Mathf.Max(outline, dilate + outline * 0.6f));
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
