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
        public const string SLOT_PREFAB_PATH = "Assets/_Prefabs/UI/AbilityHudSlot.prefab";
        public const int TRACK_NODES = 5;
        public const float NODE_SIZE = 48f;
        public const float BOSS_NODE_SIZE = 52f;
        public const float LEVEL_TAG_WIDTH = 118f;

        // Portrait top layout: row 1 = pause / timer / coins+kills, row 2 = XP bar, row 3 = wave track.
        public const float XP_TOP_Y = -132f;               // below the 96 px pause button
        public const float TRACK_TOP_Y_PORTRAIT = -192f;   // relative to the screen top (WaveHud sits at -6)
        public const float TRACK_TOP_Y_LANDSCAPE = -94f;   // landscape: right under the timer (XP lives top-left)
        public const float TIMER_HEIGHT = 80f;
        public const float TIMER_SCALE = 1.75f;
        public const float PILL_HEIGHT = 50f;
        public const float TOP_STACK_OFFSET = -266f;
        public const float TOP_STACK_OFFSET_LANDSCAPE = -166f;

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

            RestyleSlotPrefab();
            RestyleHpBar(hud.Find("HpBar"));
            RestyleXpBar(hud.Find("XpBar"));
            BuildWaveTrack(hud.Find("WaveHud"));
            RemoveLockedChips(hud.Find("AbilityHud"));
            RestyleAbilityHud(hud.Find("AbilityHud"));
            CompactTopRow(hud);
            RestyleCombo(hud.Find("ComboHud"));
            RestyleUltimate(hud.Find("UltimateButton"));

            var stack = hud.GetComponent<HudTopStack>();
            if (stack != null)
            {
                SetFloat(stack, "topOffset", TOP_STACK_OFFSET);
                SetFloat(stack, "landscapeTopOffset", TOP_STACK_OFFSET_LANDSCAPE);
                EditorUtility.SetDirty(stack);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // ------------------------------------------------------------------ HP under the hero

        private static void RestyleHpBar(Transform hp)
        {
            if (hp == null) return;
            var centre = P(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(168f, 28f));
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
                if (t != null) Stretch(Rt(t), 4f, 4f, 4f, 4f);
            }
            Transform label = hp.Find("Label");
            if (label != null)
            {
                GetOrAdd<ThemedText>(label.gameObject).Configure(UiTextStyle.Number, 0.64f);
                Place(Rt(label), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(160f, 34f));
            }
        }

        // ------------------------------------------------------------------ XP + level tag

        private static void RestyleXpBar(Transform xp)
        {
            if (xp == null) return;
            Presets(Rt(xp),
                P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(LEVEL_TAG_WIDTH * 0.5f, XP_TOP_Y), new Vector2(-48f - LEVEL_TAG_WIDTH, 38f)),
                P(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f + LEVEL_TAG_WIDTH, -46f), new Vector2(316f, 34f)));

            Transform old = xp.Find("LevelTag");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var tag = Img(xp, "LevelTag", UiSprite.Squircle, UiColor.Gold, 1f, false, true);
            Place(Rt(tag), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(8f, 0f), new Vector2(LEVEL_TAG_WIDTH + 12f, 56f));
            var text = Txt(tag.transform, "Text", "LV 1", UiTextStyle.Caption, UiColor.TextDark, TextAlignmentOptions.Center, true, 1.05f);
            Object.DestroyImmediate(text.GetComponent<ThemedText>());
            Stretch(Rt(text), 8f, 14f, 8f, 2f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.font = Theme.bodyBoldFont != null ? Theme.bodyBoldFont : (Theme.displayFont != null ? Theme.displayFont : text.font);
            if (text.font != null)
            {
                text.fontSharedMaterial = (Theme.heavyFlatMaterial != null && Theme.bodyBoldFont != null)
                    ? Theme.heavyFlatMaterial
                    : text.font.material;
            }
            text.fontSize = Theme.captionSize * 1.05f;

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

            var track = Img(waveHud, "WaveTrack", UiSprite.Pill, UiColor.SurfaceSunken, 0.98f, false, true);
            // WaveHud is anchored 6 px below the top in Portrait and 8 px in Landscape.
            Presets(Rt(track),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, TRACK_TOP_Y_PORTRAIT + 6f), new Vector2(0f, 66f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, TRACK_TOP_Y_LANDSCAPE + 8f), new Vector2(0f, 66f)));
            var layout = track.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 18, 4, 14);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            track.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var caption = Txt(track.transform, "Caption", "WAVE", UiTextStyle.Micro, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true, 1.1f);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            Rt(caption).sizeDelta = new Vector2(88f, 40f);
            caption.gameObject.AddComponent<LocalizedText>().EditorSetup("ui.hud.wave_upper_prefix", true);

            var nodes = new Image[TRACK_NODES];
            var labels = new TMP_Text[TRACK_NODES - 1];
            var links = new Image[TRACK_NODES - 1];
            Image bossIcon = null;
            for (int i = 0; i < TRACK_NODES; i++)
            {
                bool last = i == TRACK_NODES - 1;
                float size = last ? BOSS_NODE_SIZE : NODE_SIZE;
                var node = Img(track.transform, last ? "BossNode" : $"Node_{i + 1}", UiSprite.Squircle, UiColor.SurfaceRaised, 1f, false, false);
                Rt(node).sizeDelta = new Vector2(size, size);
                nodes[i] = node;
                if (last)
                {
                    bossIcon = Icon(node.transform, "Icon", Theme.iconSkull, 28f);
                    Place(Rt(bossIcon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(28f, 28f));
                }
                else
                {
                    var label = Txt(node.transform, "Text", (i + 1).ToString(), UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 1.1f);
                    Stretch(Rt(label), 0f, 8f, 0f, 0f);
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

        // ------------------------------------------------------------------ compact top row

        /// <summary>Squircle pause button, timer, and opaque coin / kill pills.</summary>
        private static void CompactTopRow(Transform hud)
        {
            Transform pause = hud.Find("PauseButton");
            if (pause != null)
            {
                Rt(pause).sizeDelta = new Vector2(96f, 96f);
                var pImg = pause.GetComponent<Image>();
                if (pImg != null && Theme.squircleSprite != null)
                {
                    pImg.sprite = Theme.squircleSprite;
                    pImg.type = Image.Type.Sliced;
                }
                if (pause.TryGetComponent(out ThemedButton ptb))
                {
                    Image pIcon = pause.Find("Icon")?.GetComponent<Image>();
                    if (pIcon != null) Rt(pIcon).anchoredPosition = new Vector2(0f, 7f);
                    ptb.EditorSetup(ButtonStyle.Secondary, pImg, null, pIcon, ButtonSound.None, customSprite: true);
                }
                Transform keyHint = pause.Find("KeyHintHost");
                if (keyHint != null)
                {
                    Place(Rt(keyHint), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(36f, 24f));
                }
            }

            Transform timer = hud.Find("WaveHud/TimerText");
            if (timer != null)
            {
                GetOrAdd<ThemedText>(timer.gameObject).Configure(UiTextStyle.Number, TIMER_SCALE);
                var rt = Rt(timer);
                rt.anchoredPosition = new Vector2(0f, -6f);
                rt.sizeDelta = new Vector2(0f, TIMER_HEIGHT);
            }

            Transform coins = hud.Find("CoinHud");
            Transform kills = hud.Find("KillsHud");
            if (coins != null)
            {
                Rt(coins).anchoredPosition = new Vector2(-24f, -12f);
                Rt(coins).sizeDelta = new Vector2(184f, PILL_HEIGHT);
            }
            if (kills != null)
            {
                Rt(kills).anchoredPosition = new Vector2(-24f, -12f - PILL_HEIGHT - 8f);
                Rt(kills).sizeDelta = new Vector2(184f, PILL_HEIGHT);
            }
            foreach (Transform pill in new[] { coins, kills })
            {
                if (pill == null) continue;
                GetOrAdd<ThemedGraphic>(pill.gameObject).Configure(UiColor.SurfaceSunken, 0.98f, UiSprite.Pill);
                if (pill.TryGetComponent(out HorizontalLayoutGroup hl))
                {
                    hl.padding = new RectOffset(12, 18, 4, 10);
                    hl.spacing = 8f;
                }
                Transform icon = pill.Find("Icon");
                if (icon != null) LE(icon.gameObject, prefH: 32f, prefW: 32f);
                Transform value = pill.Find("Value");
                if (value != null)
                {
                    GetOrAdd<ThemedText>(value.gameObject).Configure(UiTextStyle.Number, 0.72f);
                    GetOrAdd<ThemedGraphic>(value.gameObject).Configure(UiColor.TextPrimary);
                }
            }
        }

        // ------------------------------------------------------------------ combo & ultimate & slots

        private static void RestyleCombo(Transform comboHud)
        {
            if (comboHud == null) return;
            Presets(Rt(comboHud),
                P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, TOP_STACK_OFFSET), new Vector2(226f, 160f)),
                P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, TOP_STACK_OFFSET_LANDSCAPE), new Vector2(226f, 160f)));

            Transform content = comboHud.Find("Content");
            if (content == null) return;
            if (content.TryGetComponent(out Image bg))
            {
                bg.enabled = true;
                bg.sprite = Theme.cardSprite;
                bg.type = Image.Type.Sliced;
                bg.material = Theme.surfaceMaterial;
                bg.color = UiTheme.WithAlpha(Theme.surface, 0.98f);
            }
            Transform comboText = content.Find("ComboText");
            if (comboText != null)
            {
                GetOrAdd<ThemedText>(comboText.gameObject).Configure(UiTextStyle.Number, 1.3f);
                Rt(comboText).anchoredPosition = new Vector2(0f, -10f);
                Rt(comboText).sizeDelta = new Vector2(-20f, 62f);
            }
            Transform bonusText = content.Find("BonusText");
            if (bonusText != null)
            {
                GetOrAdd<ThemedText>(bonusText.gameObject).Configure(UiTextStyle.Micro, 1f);
                if (bonusText.TryGetComponent(out TMP_Text bonusTmp))
                {
                    bonusTmp.textWrappingMode = TextWrappingModes.NoWrap;
                    bonusTmp.enableAutoSizing = true;
                    bonusTmp.fontSizeMin = 13f;
                    bonusTmp.fontSizeMax = Theme.microSize;
                }
                Rt(bonusText).anchoredPosition = new Vector2(0f, -72f);
                Rt(bonusText).sizeDelta = new Vector2(-24f, 28f);
            }
            Transform timerBg = content.Find("TimerBarBg");
            if (timerBg != null)
            {
                Rt(timerBg).anchoredPosition = new Vector2(0f, 24f);
                Rt(timerBg).sizeDelta = new Vector2(-36f, 20f);
            }
        }

        private static void RestyleAbilityHud(Transform abilityHud)
        {
            if (abilityHud == null) return;
            Transform actives = abilityHud.Find("ActiveSlots");
            Transform passives = abilityHud.Find("PassiveSlots");
            // Portrait VerticalLayoutGroup stacks sibling 0 on top (actives) and sibling 1 below (passives).
            if (actives != null) actives.SetSiblingIndex(0);
            if (passives != null) passives.SetSiblingIndex(1);

            if (abilityHud.TryGetComponent(out AbilityHudUI ui))
            {
                SetFloat(ui, "slotSize", 84f);
                EditorUtility.SetDirty(ui);
            }
            foreach (Transform row in new[] { actives, passives })
            {
                if (row == null) continue;
                var alg = GetOrAdd<AdaptiveLayoutGroup>(row.gameObject);
                alg.SetSettings(LayoutMode.Portrait, new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 8f, childAlignment = TextAnchor.MiddleLeft });
                alg.SetSettings(LayoutMode.Landscape, new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 8f, childAlignment = TextAnchor.UpperCenter });
                EditorUtility.SetDirty(alg);

                var r = row.GetComponent<ResponsiveRect>();
                if (r != null)
                {
                    var p = r.GetPreset(LayoutMode.Portrait);
                    if (p.captured)
                    {
                        p.sizeDelta = new Vector2(560f, 88f);
                        r.SetPreset(LayoutMode.Portrait, p);
                        p.ApplyTo(Rt(row));
                    }
                    var l = r.GetPreset(LayoutMode.Landscape);
                    if (l.captured)
                    {
                        l.anchoredPosition = row == actives ? Vector2.zero : new Vector2(98f, 0f);
                        l.sizeDelta = new Vector2(88f, 560f);
                        r.SetPreset(LayoutMode.Landscape, l);
                    }
                    EditorUtility.SetDirty(r);
                }
                else
                {
                    Rt(row).sizeDelta = new Vector2(560f, 88f);
                }
            }
            var hudR = abilityHud.GetComponent<ResponsiveRect>();
            if (hudR != null)
            {
                var p = hudR.GetPreset(LayoutMode.Portrait);
                if (p.captured)
                {
                    p.sizeDelta = new Vector2(560f, 192f);
                    hudR.SetPreset(LayoutMode.Portrait, p);
                    p.ApplyTo(Rt(abilityHud));
                }
                var l = hudR.GetPreset(LayoutMode.Landscape);
                if (l.captured)
                {
                    l.sizeDelta = new Vector2(190f, l.sizeDelta.y);
                    hudR.SetPreset(LayoutMode.Landscape, l);
                }
                EditorUtility.SetDirty(hudR);
            }
        }

        private static void RestyleUltimate(Transform ult)
        {
            if (ult == null) return;
            Presets(Rt(ult),
                P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 148f), new Vector2(192f, 192f)),
                P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 72f), new Vector2(168f, 168f)));

            // Transparent root hit surface; RingTrack -> Ring -> Face gives crisp black borders on both sides of the arc.
            var rootImg = ult.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = Theme.circleSprite;
                rootImg.type = Image.Type.Simple;
                rootImg.material = null;
                rootImg.color = Color.clear;
                rootImg.raycastTarget = true;
            }

            Transform oldTrack = ult.Find("RingTrack");
            if (oldTrack != null) Object.DestroyImmediate(oldTrack.gameObject);
            var ringTrack = Img(ult, "RingTrack", UiSprite.Circle, UiColor.SurfaceSunken, 1f, false, true);
            Stretch(Rt(ringTrack), -12f, -12f, -12f, -12f);

            Transform ringT = ult.Find("Ring");
            Image ring = ringT != null ? ringT.GetComponent<Image>() : null;
            if (ring != null)
            {
                ring.sprite = Theme.ringThinSprite;
                ring.color = Theme.gold;
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = true;
                Stretch(Rt(ring), -4f, -4f, -4f, -4f);
            }

            Transform oldFace = ult.Find("Face");
            if (oldFace != null) Object.DestroyImmediate(oldFace.gameObject);
            var faceImg = Img(ult, "Face", UiSprite.Circle, UiColor.SurfaceRaised, 1f, false, false);
            Stretch(Rt(faceImg), 14f, 14f, 14f, 14f);

            Transform readyGlow = ult.Find("ReadyGlow");
            int idx = 0;
            if (readyGlow != null) readyGlow.SetSiblingIndex(idx++);
            ringTrack.transform.SetSiblingIndex(idx++);
            if (ringT != null) ringT.SetSiblingIndex(idx++);
            faceImg.transform.SetSiblingIndex(idx++);

            // Clip square pixel ultimate icon cleanly inside a circular mask.
            Transform icon = ult.Find("Icon") ?? ult.Find("IconMask/Icon");
            if (icon != null)
            {
                Transform maskT = ult.Find("IconMask");
                if (maskT == null)
                {
                    var maskImg = Img(ult, "IconMask", UiSprite.Circle, UiColor.White, 1f, false, false);
                    maskImg.type = Image.Type.Simple;
                    maskImg.material = null;
                    var mask = maskImg.gameObject.AddComponent<Mask>();
                    mask.showMaskGraphic = false;
                    maskT = maskImg.transform;
                }
                else if (maskT.TryGetComponent(out Image maskImg))
                {
                    maskImg.material = null;
                }
                maskT.SetSiblingIndex(idx++);
                Place(Rt(maskT), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(108f, 108f));
                icon.SetParent(maskT, false);
                Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(98f, 98f));
            }

            Transform status = ult.Find("StatusText");
            if (status != null)
            {
                GetOrAdd<ThemedText>(status.gameObject).Configure(UiTextStyle.Number, 0.68f);
                Place(Rt(status), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(180f, 38f));
            }
            Transform label = ult.Find("LabelText");
            if (label != null) label.gameObject.SetActive(false);

            Transform keyHint = ult.Find("KeyHintHost");
            if (keyHint != null)
            {
                Place(Rt(keyHint), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(84f, 28f));
            }

            if (ult.TryGetComponent(out Button btn))
            {
                btn.targetGraphic = faceImg;
            }
            if (ult.TryGetComponent(out UltimateButtonUI ui))
            {
                Set(ui, "backgroundImage", faceImg);
                Set(ui, "chargeRingImage", ring);
                SetBool(ui, "showChargePercent", true);
                EditorUtility.SetDirty(ui);
            }
        }

        /// <summary>Restyles the shared AbilityHudSlot prefab: squircle tile, circular gold level badge.</summary>
        public static void RestyleSlotPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SLOT_PREFAB_PATH);
            if (root == null) return;
            try
            {
                Transform bg = root.transform.Find("Background");
                if (bg != null && bg.TryGetComponent(out Image bgImg))
                {
                    bgImg.sprite = Theme.squircleSprite != null ? Theme.squircleSprite : Theme.cardSprite;
                    bgImg.type = Image.Type.Sliced;
                    bgImg.material = Theme.surfaceMaterial;
                }
                Transform border = root.transform.Find("Border");
                if (border != null && border.TryGetComponent(out Image borderImg))
                {
                    borderImg.sprite = Theme.slotFrameSprite;
                    borderImg.type = Image.Type.Sliced;
                    Stretch(Rt(border), 1f, 6f, 1f, 0f);
                }
                Transform icon = root.transform.Find("Icon");
                if (icon != null)
                {
                    Stretch(Rt(icon), 10f, 14f, 10f, 8f);
                }
                Transform badge = root.transform.Find("LevelBadge");
                Image badgeImg = null;
                if (badge != null)
                {
                    badgeImg = badge.GetComponent<Image>();
                    if (badgeImg != null)
                    {
                        badgeImg.sprite = Theme.circleSprite;
                        badgeImg.type = Image.Type.Simple;
                        badgeImg.material = Theme.surfaceMaterial;
                        badgeImg.color = Theme.gold;
                    }
                    Place(Rt(badge), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 4f), new Vector2(34f, 34f));
                    Transform text = badge.Find("Text");
                    if (text != null && text.TryGetComponent(out TMP_Text tmp))
                    {
                        GetOrAdd<ThemedText>(text.gameObject).Configure(UiTextStyle.Caption, 0.85f);
                        if (Theme.bodyBoldFont != null)
                        {
                            tmp.font = Theme.bodyBoldFont;
                            tmp.fontSharedMaterial = Theme.heavyFlatMaterial != null ? Theme.heavyFlatMaterial : Theme.bodyBoldFont.material;
                        }
                        tmp.color = Theme.textDark;
                        tmp.alignment = TextAlignmentOptions.Center;
                        Stretch(Rt(text), 0f, 2f, 0f, 0f);
                    }
                }
                if (root.TryGetComponent(out AbilityHudSlotUI slotUi))
                {
                    Set(slotUi, "levelBadgeBackground", badgeImg);
                }
                PrefabUtility.SaveAsPrefabAsset(root, SLOT_PREFAB_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
