using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data.Platform;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// UI redesign (variant A), step 4 — gameplay HUD in <c>Gameplay.unity</c>, restyled in place (idempotent):
    /// HP becomes a compact bar pinned under the hero; the XP bar gets a gold "LV n" tag; "WAVE n" becomes a wave
    /// track up to the next mini-boss / boss; the "🔒 +N" locked-slot chips are removed.
    /// </summary>
    public static class UiRedesignHudTool
    {
        public const string GAMEPLAY_SCENE = "Assets/_Scenes/Gameplay.unity";
        public const int TRACK_NODES = 5;
        public const float NODE_SIZE = 40f;
        public const float BOSS_NODE_SIZE = 46f;
        public const float LEVEL_TAG_WIDTH = 112f;

        [MenuItem("ProjectB/UI/Redesign/Apply HUD (HP Under Hero, Wave Track)")]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(GAMEPLAY_SCENE, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[UiRedesignHudTool] HUD restyled.");
        }

        public static void Apply(Scene scene)
        {
            Transform hud = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "SafeAreaHUD") hud = t;
                }
            }
            if (hud == null)
            {
                Debug.LogError("[UiRedesignHudTool] SafeAreaHUD not found.");
                return;
            }

            RestyleHpBar(hud.Find("HpBar"));
            RestyleXpBar(hud.Find("XpBar"));
            BuildWaveTrack(hud.Find("WaveHud"));
            RemoveLockedChips(hud.Find("AbilityHud"));

            var stack = hud.GetComponent<HudTopStack>();
            if (stack != null)
            {
                SetFloat(stack, "topOffset", -204f);
                EditorUtility.SetDirty(stack);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // ------------------------------------------------------------------ HP under the hero

        private static void RestyleHpBar(Transform hp)
        {
            if (hp == null) return;
            var centre = P(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 30f));
            Presets(Rt(hp), centre, centre);

            var ui = hp.GetComponent<HpBarUI>();
            SetBool(ui, "followHero", true);
            // The compact bar shows only the current HP through ProgressBarUI; drop the "cur / max" text writer.
            Set(ui, "hpText", null);
            EditorUtility.SetDirty(ui);

            var bar = hp.GetComponent<ProgressBarUI>();
            if (bar != null)
            {
                SetInt(bar, "labelFormat", (int)ProgressLabelFormat.Current);
                EditorUtility.SetDirty(bar);
            }

            foreach (string child in new[] { "Ghost", "Fill" })
            {
                Transform t = hp.Find(child);
                if (t != null) Stretch(Rt(t), 5f, 5f, 5f, 5f);
            }
            Transform label = hp.Find("Label");
            if (label != null)
            {
                GetOrAdd<ThemedText>(label.gameObject).Configure(UiTextStyle.Number, 0.55f);
                Stretch(Rt(label), 0f, 0f, 0f, 1f);
            }
        }

        // ------------------------------------------------------------------ XP + level tag

        private static void RestyleXpBar(Transform xp)
        {
            if (xp == null) return;
            Presets(Rt(xp),
                P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(LEVEL_TAG_WIDTH * 0.5f, -148f), new Vector2(-48f - LEVEL_TAG_WIDTH, 34f)),
                P(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f + LEVEL_TAG_WIDTH, -46f), new Vector2(316f, 32f)));

            Transform old = xp.Find("LevelTag");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var tag = Img(xp, "LevelTag", UiSprite.Pill, UiColor.Gold, 1f, false, true);
            Place(Rt(tag), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(8f, 0f), new Vector2(LEVEL_TAG_WIDTH + 8f, 48f));
            var text = Txt(tag.transform, "Text", "LV 1", UiTextStyle.Caption, UiColor.Background, TextAlignmentOptions.Center, true, 1.05f);
            Stretch(Rt(text), 8f, 6f, 8f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.font = Theme.displayFont != null ? Theme.displayFont : text.font;

            // The tag now carries the level: the old in-bar label group goes away.
            Transform labelGroup = xp.Find("LabelGroup");
            if (labelGroup != null) labelGroup.gameObject.SetActive(false);
            Set(xp.GetComponent<XpBarUI>(), "levelBadgeText", text);
        }

        // ------------------------------------------------------------------ wave track

        private static void BuildWaveTrack(Transform waveHud)
        {
            if (waveHud == null) return;
            Transform waveText = waveHud.Find("WaveText");
            if (waveText != null) waveText.gameObject.SetActive(false);

            Transform old = waveHud.Find("WaveTrack");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var track = Img(waveHud, "WaveTrack", UiSprite.Pill, UiColor.SurfaceSunken, 0.85f, false, true);
            Place(Rt(track), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(0f, 54f));
            var layout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 14, 4, 6);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            track.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var caption = Txt(track.transform, "Caption", "WAVE", UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, true, 1.05f);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            Rt(caption).sizeDelta = new Vector2(96f, 40f);
            caption.gameObject.AddComponent<LocalizedText>().EditorSetup("ui.hud.wave_upper_prefix", true);

            var nodes = new Image[TRACK_NODES];
            var labels = new TMP_Text[TRACK_NODES - 1];
            var links = new Image[TRACK_NODES - 1];
            Image bossIcon = null;
            for (int i = 0; i < TRACK_NODES; i++)
            {
                bool last = i == TRACK_NODES - 1;
                float size = last ? BOSS_NODE_SIZE : NODE_SIZE;
                var node = Img(track.transform, last ? "BossNode" : $"Node_{i + 1}", UiSprite.Row, UiColor.SurfaceRaised, 1f, false, false);
                Rt(node).sizeDelta = new Vector2(size, size);
                nodes[i] = node;
                if (last)
                {
                    bossIcon = Icon(node.transform, "Icon", Theme.iconSkull, 30f);
                    Place(Rt(bossIcon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(30f, 30f));
                }
                else
                {
                    var label = Txt(node.transform, "Text", (i + 1).ToString(), UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 1.05f);
                    Stretch(Rt(label), 0f, 4f, 0f, 0f);
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    labels[i] = label;

                    var link = Img(track.transform, $"Link_{i + 1}", UiSprite.Solid, UiColor.Outline, 1f, false, false);
                    Rt(link).sizeDelta = new Vector2(10f, 5f);
                    links[i] = link;
                }
            }

            var ui = track.gameObject.AddComponent<WaveTrackUI>();
            ui.EditorSetup(nodes, labels, links, bossIcon);
        }

        // ------------------------------------------------------------------ slots

        private static void RemoveLockedChips(Transform abilityHud)
        {
            if (abilityHud == null) return;
            var ui = abilityHud.GetComponent<AbilityHudUI>();
            foreach (string row in new[] { "ActiveSlots", "PassiveSlots" })
            {
                Transform chip = abilityHud.Find(row + "/LockedChip");
                if (chip != null) Object.DestroyImmediate(chip.gameObject);
            }
            if (ui != null)
            {
                Set(ui, "activeLockedChip", null);
                Set(ui, "activeLockedChipText", null);
                Set(ui, "passiveLockedChip", null);
                Set(ui, "passiveLockedChipText", null);
                SetBool(ui, "hideLockedSlots", true);
                EditorUtility.SetDirty(ui);
            }
        }

        // ------------------------------------------------------------------ presets

        private static void Presets(RectTransform rt, RectPreset portrait, RectPreset landscape)
        {
            portrait.ApplyTo(rt);
            var r = GetOrAdd<ResponsiveRect>(rt.gameObject);
            r.SetPreset(LayoutMode.Portrait, portrait);
            r.SetPreset(LayoutMode.Landscape, landscape);
            EditorUtility.SetDirty(r);
        }

        private static RectPreset P(Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            return new RectPreset { captured = true, anchorMin = aMin, anchorMax = aMax, pivot = pivot, anchoredPosition = pos, sizeDelta = size };
        }
    }
}
