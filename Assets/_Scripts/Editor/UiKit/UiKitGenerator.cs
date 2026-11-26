using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using TMPro;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Sprint 13.1 — generates the procedural UI kit into <c>Assets/_UI/Kit/</c>:
    /// white 9-slice frames (rounded, bevelled, with a toy-like bottom "step"), pixel icons (coin, shard, ad, gear…),
    /// redrawn main-menu tab icons, one SpriteAtlas for everything, a TMP Sprite Asset (<c>&lt;sprite name=coin&gt;</c>)
    /// and three synthesized UI sounds. Idempotent: files are rewritten in place (GUIDs are preserved).
    /// </summary>
    public static class UiKitGenerator
    {
        public const string KIT_DIR = "Assets/_UI/Kit";
        public const string FRAMES_DIR = KIT_DIR + "/Frames";
        public const string ICONS_DIR = KIT_DIR + "/Icons";
        public const string ATLAS_PATH = KIT_DIR + "/UiKit.spriteatlasv2";
        public const string SPRITE_SHEET_PATH = KIT_DIR + "/UiIconSheet.png";
        public const string SPRITE_ASSET_PATH = KIT_DIR + "/UiIconSheet.asset";
        public const string AUDIO_DIR = "Assets/_Audio/SFX";

        private const int ICON_SCALE = 8;
        private static readonly string[] InlineSpriteIcons =
        {
            "coin", "shard", "ad", "heart", "speed", "sword", "clock", "skull", "star", "flame", "lock", "check", "play", "reroll"
        };

        public struct FrameSpec
        {
            public string name;
            public int width;
            public int height;
            public Vector4 border; // left, bottom, right, top
        }

        public static readonly FrameSpec[] Frames =
        {
            new FrameSpec { name = "kit_panel", width = 128, height = 128, border = new Vector4(40, 48, 40, 40) },
            new FrameSpec { name = "kit_card", width = 96, height = 96, border = new Vector4(30, 36, 30, 30) },
            new FrameSpec { name = "kit_row", width = 80, height = 80, border = new Vector4(24, 30, 24, 24) },
            new FrameSpec { name = "kit_button", width = 96, height = 104, border = new Vector4(34, 46, 34, 34) },
            new FrameSpec { name = "kit_button_pressed", width = 96, height = 104, border = new Vector4(34, 46, 34, 34) },
            new FrameSpec { name = "kit_pill", width = 128, height = 64, border = new Vector4(32, 32, 32, 32) },
            new FrameSpec { name = "kit_circle", width = 128, height = 128, border = Vector4.zero },
            new FrameSpec { name = "kit_slot_frame", width = 64, height = 64, border = new Vector4(22, 22, 22, 22) },
            new FrameSpec { name = "kit_bar_frame", width = 64, height = 32, border = new Vector4(16, 16, 16, 16) },
            new FrameSpec { name = "kit_bar_fill", width = 16, height = 32, border = Vector4.zero },
            new FrameSpec { name = "kit_solid", width = 8, height = 8, border = new Vector4(2, 2, 2, 2) },
            new FrameSpec { name = "kit_glow", width = 128, height = 128, border = Vector4.zero },
            new FrameSpec { name = "kit_outline", width = 96, height = 96, border = new Vector4(32, 32, 32, 32) },
            new FrameSpec { name = "kit_knob", width = 96, height = 96, border = Vector4.zero },
            new FrameSpec { name = "kit_ring_thin", width = 128, height = 128, border = Vector4.zero },
            new FrameSpec { name = "kit_vignette", width = 128, height = 128, border = Vector4.zero },
            new FrameSpec { name = "kit_starfield", width = 256, height = 256, border = Vector4.zero }
        };

        [MenuItem("ProjectB/UI/Generate UI Kit (Sprites, Icons, Atlas)")]
        public static void GenerateAll()
        {
            EnsureFolder(KIT_DIR);
            EnsureFolder(FRAMES_DIR);
            EnsureFolder(ICONS_DIR);

            foreach (FrameSpec spec in Frames)
            {
                WritePng($"{FRAMES_DIR}/{spec.name}.png", spec.width, spec.height, DrawFrame(spec));
            }

            var iconPixels = new Dictionary<string, Color32[]>();
            var iconSizes = new Dictionary<string, int>();
            foreach (UiKitIcons.IconDef def in UiKitIcons.All())
            {
                var canvas = new PixelCanvas(def.grid);
                def.draw(canvas);
                canvas.Bevel(def.mono ? 0f : 0.28f, def.mono ? 0.18f : 0.3f);
                canvas.Outline(PixelCanvas.OutlineColor);
                Color32[] pixels = canvas.ToPixels(ICON_SCALE, out int size);
                string file = $"{ICONS_DIR}/icon_{def.name}.png";
                WritePng(file, size, size, pixels);
                iconPixels[def.name] = pixels;
                iconSizes[def.name] = size;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (FrameSpec spec in Frames)
            {
                ConfigureSprite($"{FRAMES_DIR}/{spec.name}.png", spec.border, spec.name == "kit_starfield");
            }
            foreach (string name in iconPixels.Keys)
            {
                ConfigureSprite($"{ICONS_DIR}/icon_{name}.png", Vector4.zero, false);
            }

            BuildSpriteAtlas();
            BuildInlineSpriteAsset(iconPixels, iconSizes);
            GenerateUiSounds();

            AssetDatabase.SaveAssets();
            Debug.Log($"[UiKitGenerator] Generated {Frames.Length} frames, {iconPixels.Count} icons, atlas and TMP sprite asset.");
        }

        public static Sprite LoadFrame(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{FRAMES_DIR}/{name}.png");
        public static Sprite LoadIcon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ICONS_DIR}/icon_{name}.png");

        // ------------------------------------------------------------------ frames

        private static Color32[] DrawFrame(FrameSpec spec)
        {
            int w = spec.width, h = spec.height;
            var buf = new Color32[w * h];
            switch (spec.name)
            {
                case "kit_panel": FillBlock(buf, w, h, 30f, outline: 5f, ledge: 7f, highlight: 4f, shade: 6f); break;
                case "kit_card": FillBlock(buf, w, h, 22f, outline: 5f, ledge: 6f, highlight: 3f, shade: 5f); break;
                case "kit_row": FillBlock(buf, w, h, 18f, outline: 4f, ledge: 5f, highlight: 3f, shade: 4f); break;
                case "kit_button": FillBlock(buf, w, h, 24f, outline: 5f, ledge: 10f, highlight: 6f, shade: 7f); break;
                case "kit_button_pressed": FillBlock(buf, w, h, 24f, outline: 5f, ledge: 4f, highlight: 4f, shade: 5f); break;
                case "kit_pill": FillBlock(buf, w, h, h * 0.5f, outline: 4f, ledge: 3f, highlight: 3f, shade: 3f); break;
                case "kit_circle": FillCircleBlock(buf, w, h, outline: 5f, ledge: 6f, highlight: 6f, shade: 6f); break;
                case "kit_slot_frame": FillRing(buf, w, h, 16f, 5f); break;
                case "kit_bar_frame": FillBarFrame(buf, w, h); break;
                case "kit_bar_fill": FillBarFill(buf, w, h); break;
                case "kit_solid": Fill(buf, new Color32(255, 255, 255, 255)); break;
                case "kit_glow": FillGlow(buf, w, h, 0.5f); break;
                case "kit_outline": FillRing(buf, w, h, 24f, 6f); break;
                case "kit_knob": FillCircleBlock(buf, w, h, outline: 5f, ledge: 5f, highlight: 5f, shade: 5f); break;
                case "kit_ring_thin": FillCircleRing(buf, w, h, 8f); break;
                case "kit_vignette": FillVignette(buf, w, h); break;
                case "kit_starfield": FillStarfield(buf, w, h); break;
            }
            return buf;
        }

        private static float RoundedRectSdf(float px, float py, float w, float h, float r)
        {
            float cx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float cy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(cx, 0f), oy = Mathf.Max(cy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(cx, cy), 0f) - r;
        }

        private static Color32 Lum(float l, float a)
        {
            byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(l) * 255f);
            return new Color32(v, v, v, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
        }

        private static void Fill(Color32[] buf, Color32 c)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = c;
        }

        // "Voxel block" look (UI redesign, variant A): a near-black outline that stays dark under any tint, a thicker
        // dark ledge at the bottom (the block's drop), and inside it a lit top edge, a slightly darker face and a
        // shaded bottom band. The face is < 1 so the top edge can read brighter than the tint colour.
        private const float BLOCK_DARK = 0.06f;
        private const float BLOCK_FACE = 0.9f;
        private const float BLOCK_SHADE = 0.7f;
        private const float BLOCK_HIGHLIGHT = 1f;

        /// <summary>Rounded "voxel block": dark outline + bottom ledge, lit top edge, face, shaded bottom band.</summary>
        private static void FillBlock(Color32[] buf, int w, int h, float r, float outline, float ledge, float highlight, float shade)
        {
            float pad = 1f;
            float faceBottom = pad + ledge + outline;
            float faceTop = h - pad - outline;
            float faceR = Mathf.Max(1f, r - outline);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = RoundedRectSdf(px - pad, py - pad, w - pad * 2, h - pad * 2, r);
                float alpha = Mathf.Clamp01(0.5f - d);
                if (alpha <= 0f) { buf[y * w + x] = default; continue; }

                // Face = the silhouette minus the outline ring and the bottom ledge (y-up texture).
                float dFace = RoundedRectSdf(px - pad - outline, py - faceBottom, w - pad * 2 - outline * 2, faceTop - faceBottom, faceR);
                float faceL;
                if (faceTop - py < highlight) faceL = BLOCK_HIGHLIGHT;
                else if (py - faceBottom < shade) faceL = BLOCK_SHADE;
                else faceL = BLOCK_FACE;

                float l = Mathf.Lerp(BLOCK_DARK, faceL, Mathf.Clamp01(0.5f - dFace));
                buf[y * w + x] = Lum(l, alpha);
            }
        }

        /// <summary>Round "voxel block" (icon buttons, knobs): face circle shifted up so the bottom ledge is thicker.</summary>
        private static void FillCircleBlock(Color32[] buf, int w, int h, float outline, float ledge, float highlight, float shade)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.5f - 1f;
            float fcy = cy + ledge * 0.5f;
            float fr = r - outline - ledge * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = px - cx, dy = py - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                float alpha = Mathf.Clamp01(0.5f - d);
                if (alpha <= 0f) { buf[y * w + x] = default; continue; }

                float fdy = py - fcy;
                float dFace = Mathf.Sqrt(dx * dx + fdy * fdy) - fr;
                float faceL;
                if (fcy + fr - py < highlight) faceL = BLOCK_HIGHLIGHT;
                else if (py - (fcy - fr) < shade) faceL = BLOCK_SHADE;
                else faceL = BLOCK_FACE;

                float l = Mathf.Lerp(BLOCK_DARK, faceL, Mathf.Clamp01(0.5f - dFace));
                buf[y * w + x] = Lum(l, alpha);
            }
        }

        private static void FillRing(Color32[] buf, int w, int h, float r, float thickness)
        {
            float pad = 1f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedRectSdf(x + 0.5f - pad, y + 0.5f - pad, w - pad * 2, h - pad * 2, r);
                float outer = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(0.5f + d + thickness);
                float alpha = Mathf.Min(outer, inner);
                buf[y * w + x] = alpha > 0f ? Lum(1f, alpha) : default;
            }
        }

        private static void FillCircleRing(Color32[] buf, int w, int h, float thickness)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.5f - 1f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                float alpha = Mathf.Min(Mathf.Clamp01(0.5f - d), Mathf.Clamp01(0.5f + d + thickness));
                buf[y * w + x] = alpha > 0f ? Lum(1f, alpha) : default;
            }
        }

        private static void FillBarFrame(Color32[] buf, int w, int h)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedRectSdf(x + 0.5f - 1f, y + 0.5f - 1f, w - 2f, h - 2f, h * 0.5f - 1f);
                float alpha = Mathf.Clamp01(0.5f - d);
                if (alpha <= 0f) { buf[y * w + x] = default; continue; }
                // Dark outline (matches the block kit) + recessed track with an inner top shadow.
                float l = -d < 4f ? Mathf.Lerp(0.85f, BLOCK_DARK, Mathf.Clamp01(4.5f + d)) : (y > h - 10 ? 0.7f : 0.85f);
                buf[y * w + x] = Lum(l, alpha);
            }
        }

        private static void FillBarFill(Color32[] buf, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                float l = y >= h - 7 ? 1f : Mathf.Lerp(0.72f, 0.92f, t);
                for (int x = 0; x < w; x++) buf[y * w + x] = Lum(l, 1f);
            }
        }

        private static void FillGlow(Color32[] buf, int w, int h, float power)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
                buf[y * w + x] = Lum(1f, Mathf.Pow(t, 1f / power) * t);
            }
        }

        private static void FillVignette(Color32[] buf, int w, int h)
        {
            float cx = w * 0.5f, cy = h * 0.5f, r = w * 0.72f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / r);
                buf[y * w + x] = Lum(1f, t * t);
            }
        }

        private static void FillStarfield(Color32[] buf, int w, int h)
        {
            var rng = new System.Random(1313);
            for (int i = 0; i < 140; i++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                float a = 0.25f + (float)rng.NextDouble() * 0.75f;
                bool big = rng.NextDouble() < 0.12;
                Plot(buf, w, h, x, y, a);
                if (big)
                {
                    Plot(buf, w, h, x + 1, y, a * 0.6f);
                    Plot(buf, w, h, x - 1, y, a * 0.6f);
                    Plot(buf, w, h, x, y + 1, a * 0.6f);
                    Plot(buf, w, h, x, y - 1, a * 0.6f);
                }
            }
        }

        private static void Plot(Color32[] buf, int w, int h, int x, int y, float a)
        {
            x = (x + w) % w;
            y = (y + h) % h;
            byte alpha = (byte)Mathf.Max(buf[y * w + x].a, Mathf.RoundToInt(a * 255f));
            buf[y * w + x] = new Color32(255, 255, 255, alpha);
        }

        // ------------------------------------------------------------------ import

        private static void WritePng(string assetPath, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void ConfigureSprite(string assetPath, Vector4 border, bool repeat)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void BuildSpriteAtlas()
        {
            try
            {
                if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                {
                    EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
                }

                var atlas = new SpriteAtlasAsset();
                var folders = new UnityEngine.Object[]
                {
                    AssetDatabase.LoadAssetAtPath<DefaultAsset>(FRAMES_DIR),
                    AssetDatabase.LoadAssetAtPath<DefaultAsset>(ICONS_DIR)
                };
                atlas.Add(folders);
                SpriteAtlasAsset.Save(atlas, ATLAS_PATH);
                AssetDatabase.ImportAsset(ATLAS_PATH, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(ATLAS_PATH) as SpriteAtlasImporter;
                if (importer != null)
                {
                    importer.includeInBuild = true;
                    importer.packingSettings = new SpriteAtlasPackingSettings
                    {
                        blockOffset = 1,
                        padding = 4,
                        enableRotation = false,
                        enableTightPacking = false,
                        enableAlphaDilation = true
                    };
                    importer.textureSettings = new SpriteAtlasTextureSettings
                    {
                        filterMode = FilterMode.Bilinear,
                        generateMipMaps = false,
                        readable = false,
                        sRGB = true
                    };
                    importer.SaveAndReimport();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UiKitGenerator] SpriteAtlas creation failed: {e.Message}");
            }
        }

        /// <summary>Packs the inline icons into one sheet and builds a TMP Sprite Asset (names = icon names).</summary>
        private static void BuildInlineSpriteAsset(Dictionary<string, Color32[]> iconPixels, Dictionary<string, int> iconSizes)
        {
            const int cell = 128;
            int count = InlineSpriteIcons.Length;
            int columns = 4;
            int rows = Mathf.CeilToInt(count / (float)columns);
            int sheetW = columns * cell, sheetH = rows * cell;
            var sheet = new Color32[sheetW * sheetH];
            var rects = new List<KeyValuePair<string, Rect>>();

            for (int i = 0; i < count; i++)
            {
                string name = InlineSpriteIcons[i];
                if (!iconPixels.TryGetValue(name, out Color32[] src)) continue;
                int size = iconSizes[name];
                int col = i % columns, row = i / columns;
                int ox = col * cell, oy = sheetH - (row + 1) * cell;
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    int sx = x * size / cell, sy = y * size / cell;
                    sheet[(oy + y) * sheetW + ox + x] = src[sy * size + sx];
                }
                rects.Add(new KeyValuePair<string, Rect>(name, new Rect(ox, oy, cell, cell)));
            }

            WritePng(SPRITE_SHEET_PATH, sheetW, sheetH, sheet);
            AssetDatabase.ImportAsset(SPRITE_SHEET_PATH, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(SPRITE_SHEET_PATH) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
#pragma warning disable 618
            importer.spritesheet = rects.Select(kv => new SpriteMetaData
            {
                name = kv.Key,
                rect = kv.Value,
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0f, 0.2f)
            }).ToArray();
#pragma warning restore 618
            importer.SaveAndReimport();

            var existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SPRITE_ASSET_PATH);
            if (existing != null) AssetDatabase.DeleteAsset(SPRITE_ASSET_PATH);

            Type menuType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.EditorUtilities.TMP_SpriteAssetMenu", false))
                .FirstOrDefault(t => t != null);
            MethodInfo create = menuType?.GetMethod("CreateSpriteAssetFromSelectedObject", BindingFlags.NonPublic | BindingFlags.Static);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SPRITE_SHEET_PATH);
            if (create == null || texture == null)
            {
                Debug.LogWarning("[UiKitGenerator] Could not create TMP sprite asset (TMP API changed).");
                return;
            }
            create.Invoke(null, new object[] { texture });
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ audio

        public static void GenerateUiSounds()
        {
            WriteWav($"{AUDIO_DIR}/sfx_ui_confirm.wav", SynthConfirm());
            WriteWav($"{AUDIO_DIR}/sfx_ui_error.wav", SynthError());
            WriteWav($"{AUDIO_DIR}/sfx_ult_ready.wav", SynthUltReady());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private const int SAMPLE_RATE = 44100;

        private static float[] SynthConfirm()
        {
            int n = (int)(SAMPLE_RATE * 0.16f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float f = t < 0.06f ? 880f : 1318.5f;
                float local = t < 0.06f ? t : t - 0.06f;
                float env = Mathf.Exp(-local * 38f) * Mathf.Clamp01(local * 400f);
                s[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f + Mathf.Sin(4f * Mathf.PI * f * t) * env * 0.12f;
            }
            return s;
        }

        private static float[] SynthError()
        {
            int n = (int)(SAMPLE_RATE * 0.2f);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float f = Mathf.Lerp(190f, 120f, t / 0.2f);
                phase += f / SAMPLE_RATE;
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * phase));
                float env = Mathf.Clamp01(t * 300f) * Mathf.Clamp01((0.2f - t) * 25f);
                float wobble = (t % 0.08f) < 0.05f ? 1f : 0.35f;
                s[i] = square * env * wobble * 0.28f;
            }
            return s;
        }

        private static float[] SynthUltReady()
        {
            int n = (int)(SAMPLE_RATE * 0.42f);
            var s = new float[n];
            float[] notes = { 659.3f, 880f, 1318.5f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float start = k * 0.07f;
                    if (t < start) continue;
                    float lt = t - start;
                    float env = Mathf.Exp(-lt * 9f) * Mathf.Clamp01(lt * 300f);
                    v += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[k] * 2.01f * t)) * env;
                }
                s[i] = v * 0.26f;
            }
            return s;
        }

        private static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataSize);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(SAMPLE_RATE);
                w.Write(SAMPLE_RATE * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                {
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * 32000f));
                }
            }
        }

        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
