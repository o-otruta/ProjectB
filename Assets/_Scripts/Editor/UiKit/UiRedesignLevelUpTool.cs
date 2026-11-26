using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data.Platform;
using ProjectB.LevelUp;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// UI redesign (variant A), step 2 — LevelUp cards and the "Your build" strip. Restyles the existing card hierarchy
    /// in <c>Gameplay.unity</c> in place (the scene stays the source of truth): the Tag chip becomes a full-width rarity
    /// band holding "RARITY · CATEGORY", level pips and the "Lv a → b" badge; a "NEW!" sticker; icon / title / text
    /// re-laid below the band; the build strip above the cards. Idempotent — safe to run again.
    /// </summary>
    public static class UiRedesignLevelUpTool
    {
        public const string GAMEPLAY_SCENE = "Assets/_Scenes/Gameplay.unity";
        public const string HUD_SLOT_PREFAB = "Assets/_Prefabs/UI/AbilityHudSlot.prefab";
        public const int PIP_COUNT = 10;
        public const float PIP_SIZE = 18f;
        public const float BUILD_SLOT_SIZE = 60f;

        [MenuItem("ProjectB/UI/Redesign/Apply Level-Up Cards + Build Strip")]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(GAMEPLAY_SCENE, OpenSceneMode.Single);
            int cards = Apply(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UiRedesignLevelUpTool] Restyled {cards} card(s) and the build strip in {GAMEPLAY_SCENE}.");
        }

        /// <summary>Restyles every LevelUp card of the scene and (re)builds the build strip. Returns the card count.</summary>
        public static int Apply(Scene scene)
        {
            CardSelectionUI selection = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                selection = root.GetComponentInChildren<CardSelectionUI>(true);
                if (selection != null) break;
            }
            if (selection == null)
            {
                Debug.LogError("[UiRedesignLevelUpTool] CardSelectionUI not found.");
                return 0;
            }

            var so = new SerializedObject(selection);
            SerializedProperty slots = so.FindProperty("cardSlots");
            int count = 0;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var card = slots.GetArrayElementAtIndex(i).objectReferenceValue as CardUI;
                if (card == null) continue;
                RestyleCard(card);
                count++;
            }

            var panel = so.FindProperty("selectionPanel").objectReferenceValue as GameObject;
            Transform content = panel != null ? panel.transform.Find("Content") : null;
            if (content != null)
            {
                LevelUpBuildBarUI bar = BuildStrip(content);
                Set(selection, "buildBar", bar);
                RelayoutReroll(content);
            }

            EditorUtility.SetDirty(selection);
            EditorSceneManager.MarkSceneDirty(scene);
            return count;
        }

        // ------------------------------------------------------------------ card

        private static void RestyleCard(CardUI ui)
        {
            Transform card = ui.transform;

            // Rarity ring sits inside the block outline (5 px) and above the bottom ledge (6 px).
            Transform frame = card.Find("Frame");
            if (frame != null) Stretch(Rt(frame), 5f, 11f, 5f, 5f);

            Transform band = card.Find("Tag");
            if (band == null) return;
            var bandImage = band.GetComponent<Image>();
            bandImage.sprite = Theme.rowSprite;
            bandImage.type = Image.Type.Sliced;
            var bandPreset = P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-24f, 52f));
            Presets(Rt(band), bandPreset, bandPreset);

            Transform bandTextT = band.Find("Text");
            var bandText = bandTextT != null ? bandTextT.GetComponent<TextMeshProUGUI>() : null;
            if (bandText != null)
            {
                GetOrAdd<ThemedText>(bandText.gameObject).Configure(UiTextStyle.Caption, 1f);
                bandText.alignment = TextAlignmentOptions.MidlineLeft;
                bandText.textWrappingMode = TextWrappingModes.NoWrap;
                bandText.overflowMode = TextOverflowModes.Ellipsis;
                bandText.enableAutoSizing = true;
                bandText.fontSizeMin = 14f;
                bandText.fontSizeMax = Theme.captionSize;
                Presets(Rt(bandText), Inset(20f, 400f, 0f, 4f), Inset(20f, 200f, 0f, 4f));
            }

            // Level badge → inside the band, right-aligned, no pill of its own (CardUI clears its colour).
            Transform badge = card.Find("LevelBadge") ?? band.Find("LevelBadge");
            if (badge != null)
            {
                badge.SetParent(band, false);
                var badgePreset = P(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-14f, 2f), new Vector2(190f, -8f));
                Presets(Rt(badge), badgePreset, badgePreset);
                var badgeText = badge.GetComponentInChildren<TextMeshProUGUI>(true);
                if (badgeText != null)
                {
                    badgeText.alignment = TextAlignmentOptions.MidlineRight;
                    Stretch(Rt(badgeText), 0f, 0f, 0f, 0f);
                }
            }

            Image[] pips = BuildPips(band);
            GameObject newTag = BuildNewTag(card, out TextMeshProUGUI newTagText);

            Transform iconFrame = card.Find("IconFrame");
            if (iconFrame != null)
            {
                Presets(Rt(iconFrame),
                    P(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -76f), new Vector2(164f, 164f)),
                    P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(170f, 170f)));
            }

            // Keyboard hint keycap → on the icon's top-left corner, so the band's right side stays free.
            Transform keyHint = card.Find("KeyHintHost");
            if (keyHint != null)
            {
                Presets(Rt(keyHint),
                    P(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -66f), new Vector2(46f, 34f)),
                    P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-97f, -90f), new Vector2(46f, 34f)));
            }

            Transform title = card.Find("Title");
            if (title != null)
            {
                Presets(Rt(title),
                    P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(208f, -76f), new Vector2(-232f, 56f)),
                    P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -282f), new Vector2(-40f, 56f)));
            }

            Transform desc = card.Find("Description");
            if (desc != null) Presets(Rt(desc), Inset(208f, 24f, 138f, 18f), Inset(28f, 28f, 344f, 84f));

            Transform ad = card.Find("AdPlaceholder");
            if (ad != null)
            {
                Presets(Rt(ad),
                    P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 24f), new Vector2(150f, 50f)),
                    P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(170f, 52f)));
            }

            Transform lockIcon = card.Find("LockOverlay/Lock");
            if (lockIcon != null)
            {
                Presets(Rt(lockIcon),
                    P(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, 6f), new Vector2(60f, 60f)),
                    P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(128f, -186f), new Vector2(64f, 64f)));
            }

            SetBool(ui, "rarityBand", true);
            SetArray(ui, "levelPips", pips);
            Set(ui, "newTag", newTag);
            Set(ui, "newTagText", newTagText);
            EditorUtility.SetDirty(ui);
        }

        /// <summary>Row of round pips inside the band (portrait: left of the level badge; landscape: under the band).</summary>
        private static Image[] BuildPips(Transform band)
        {
            Transform old = band.Find("Pips");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var row = Go("Pips", band);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Presets(Rt(row),
                P(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-214f, 2f), new Vector2(0f, PIP_SIZE)),
                P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(0f, PIP_SIZE)));

            var pips = new Image[PIP_COUNT];
            for (int i = 0; i < PIP_COUNT; i++)
            {
                var pip = Img(row.transform, $"Pip_{i + 1}", UiSprite.Circle, UiColor.White, 1f, false, false);
                pip.type = Image.Type.Simple;
                Rt(pip).sizeDelta = new Vector2(PIP_SIZE, PIP_SIZE);
                pips[i] = pip;
            }
            return pips;
        }

        private static GameObject BuildNewTag(Transform card, out TextMeshProUGUI text)
        {
            Transform old = card.Find("NewTag");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var tag = Img(card, "NewTag", UiSprite.Pill, UiColor.Danger, 1f, false, false);
            var preset = P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-20f, 2f), new Vector2(132f, 42f));
            Presets(Rt(tag), preset, preset);
            Rt(tag).localRotation = Quaternion.Euler(0f, 0f, 5f);
            text = Txt(tag.transform, "Text", "NEW!", UiTextStyle.Caption, UiColor.White, TextAlignmentOptions.Center, false, 1f);
            Stretch(Rt(text), 6f, 4f, 6f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        // ------------------------------------------------------------------ build strip

        private static LevelUpBuildBarUI BuildStrip(Transform content)
        {
            Transform old = content.Find("BuildBar");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var bar = Img(content, "BuildBar", UiSprite.Row, UiColor.SurfaceSunken, 0.92f, false, true);
            Presets(Rt(bar),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(980f, 104f)),
                P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 36f), new Vector2(1000f, 104f)));

            var label = Txt(bar.transform, "Label", "YOUR BUILD", UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, true, 1f);
            label.textWrappingMode = TextWrappingModes.Normal;
            Place(Rt(label), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 2f), new Vector2(110f, 80f));
            label.gameObject.AddComponent<LocalizedText>().EditorSetup("ui.levelup.your_build", true);

            var slotsRow = Go("Slots", bar.transform);
            Stretch(Rt(slotsRow), 140f, 6f, 16f, 0f);
            var rowLayout = slotsRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            RectTransform actives = SlotGroup(slotsRow.transform, "Actives");
            var divider = Img(slotsRow.transform, "Divider", UiSprite.Solid, UiColor.Outline, 0.8f, false, true);
            Rt(divider).sizeDelta = new Vector2(3f, 52f);
            LE(divider.gameObject, prefW: 3f, minW: 3f);
            RectTransform passives = SlotGroup(slotsRow.transform, "Passives");

            var ui = bar.gameObject.AddComponent<LevelUpBuildBarUI>();
            ui.EditorSetup(AssetDatabase.LoadAssetAtPath<AbilityHudSlotUI>(HUD_SLOT_PREFAB), actives, passives, BUILD_SLOT_SIZE);
            return ui;
        }

        private static RectTransform SlotGroup(Transform parent, string name)
        {
            var go = Go(name, parent);
            Rt(go).sizeDelta = new Vector2(0f, BUILD_SLOT_SIZE);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return Rt(go);
        }

        /// <summary>Landscape: the build strip takes the bottom centre, so the reroll button moves to the bottom-right.</summary>
        private static void RelayoutReroll(Transform content)
        {
            Transform reroll = content.Find("RerollButton");
            if (reroll == null) return;
            var r = reroll.GetComponent<ResponsiveRect>();
            if (r == null) return;
            r.SetPreset(LayoutMode.Landscape, P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(440f, 104f)));
            EditorUtility.SetDirty(r);
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

        /// <summary>Stretched preset with insets left / right / top / bottom.</summary>
        private static RectPreset Inset(float left, float right, float top, float bottom)
        {
            return P(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2((left - right) * 0.5f, (bottom - top) * 0.5f), new Vector2(-(left + right), -(top + bottom)));
        }
    }
}
