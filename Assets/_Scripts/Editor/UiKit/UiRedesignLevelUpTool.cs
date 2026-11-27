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
        public const float PIP_WIDTH = 52f;
        public const float PIP_HEIGHT = 24f;
        public const float BUILD_SLOT_SIZE = 72f;
        /// <summary>Between the build strip (ends at -368) and the cards container (starts around -450).</summary>
        public const float FTUE_HINT_PORTRAIT_Y = -200f;   // over the subtitle (206..250 px from the top)
        /// <summary>Portrait card body centre relative to the card centre: slightly lower than the 72 px rarity band.</summary>
        public const float BODY_CENTER_Y = -26f;
        /// <summary>Top of the body block (= top of the 174 px icon) relative to the card centre.</summary>
        public const float BODY_TOP_Y = BODY_CENTER_Y + 88f;
        /// <summary>Card title: H2 (44) × (48/44) = 48 px.</summary>
        public const float TITLE_SCALE = 48f / 44f;
        public const float TITLE_HEIGHT = 60f;
        /// <summary>
        /// Rounder cards without touching the shared kit: a lower sprite PPU multiplier scales the 9-slice corners
        /// (kit_card r20 → ~33 px, header r18 → ~31 px, rarity ring r22 → ~32 px) and thickens the outline a bit.
        /// </summary>
        public const float CARD_CORNER_PPU = 1f;
        public const float HEADER_CORNER_PPU = 1f;
        public const float FRAME_CORNER_PPU = 1f;
        public const float NEW_TAG_TILT = 3f;

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
                BuildTitleBanner(content);
                RestyleSubtitle(content);
                RelayoutCardsContainer(content);
                LevelUpBuildBarUI bar = BuildStrip(content);
                Set(selection, "buildBar", bar);
                RestyleReroll(selection, content);
                RelayoutFtueHint(content);
            }

            EditorUtility.SetDirty(selection);
            EditorSceneManager.MarkSceneDirty(scene);
            return count;
        }

        // ------------------------------------------------------------------ festive header

        private static void BuildTitleBanner(Transform content)
        {
            Transform old = content.Find("TitleBanner");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Transform title = content.Find("Title");
            if (title == null) return;

            var banner = Go("TitleBanner", content).transform;
            banner.SetSiblingIndex(title.GetSiblingIndex());
            Presets(Rt(banner),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(860f, 118f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(820f, 94f)));

            var leftWing = Img(banner, "LeftWing", UiSprite.RibbonTail, UiColor.Warning, 1f, false, true);
            Place(Rt(leftWing), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, -12f), new Vector2(100f, 80f));
            Rt(leftWing).localScale = new Vector3(-1f, 1f, 1f);

            var rightWing = Img(banner, "RightWing", UiSprite.RibbonTail, UiColor.Warning, 1f, false, true);
            Place(Rt(rightWing), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-86f, -12f), new Vector2(100f, 80f));
            Rt(rightWing).localScale = Vector3.one;

            var plate = Img(banner, "CenterPlate", UiSprite.Button, UiColor.Gold, 1f, false, true);
            Stretch(Rt(plate), 76f, 0f, 76f, 0f);

            // Celebratory confetti around the banner: squares and thin strips, gently animated (ConfettiDriftUI).
            (Vector2 pos, Vector2 size, float rot, UiColor col)[] confetti =
            {
                (new Vector2(-430f, 56f), new Vector2(18f, 18f), 18f, UiColor.XpFill),
                (new Vector2(-370f, -50f), new Vector2(10f, 24f), -24f, UiColor.Danger),
                (new Vector2(-250f, 82f), new Vector2(14f, 14f), 40f, UiColor.Gold),
                (new Vector2(-120f, 92f), new Vector2(8f, 22f), 60f, UiColor.Shard),
                (new Vector2(-30f, 80f), new Vector2(16f, 16f), 12f, UiColor.Ad),
                (new Vector2(110f, 96f), new Vector2(10f, 22f), -35f, UiColor.RarityEpic),
                (new Vector2(230f, 76f), new Vector2(14f, 14f), -32f, UiColor.Gold),
                (new Vector2(380f, 58f), new Vector2(18f, 18f), -16f, UiColor.Shard),
                (new Vector2(450f, -42f), new Vector2(10f, 24f), 28f, UiColor.Danger),
                (new Vector2(-460f, -8f), new Vector2(12f, 12f), 50f, UiColor.Ad)
            };
            var pieces = new RectTransform[confetti.Length];
            for (int i = 0; i < confetti.Length; i++)
            {
                var c = Img(banner, $"Confetti_{i}", UiSprite.Solid, confetti[i].col, 0.95f, false, true);
                Place(Rt(c), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), confetti[i].pos, confetti[i].size);
                Rt(c).localRotation = Quaternion.Euler(0f, 0f, confetti[i].rot);
                pieces[i] = Rt(c);
            }
            banner.gameObject.AddComponent<ConfettiDriftUI>().EditorSetup(pieces);

            GetOrAdd<ThemedGraphic>(title.gameObject).Configure(UiColor.White);
            GetOrAdd<ThemedText>(title.gameObject).Configure(UiTextStyle.H1, 1.04f);
            if (title.TryGetComponent(out TMP_Text titleTmp))
            {
                titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
                titleTmp.enableAutoSizing = true;
                titleTmp.fontSizeMin = 30f;
                titleTmp.fontSizeMax = Theme.h1Size * 1.04f;
            }
            Presets(Rt(title),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(670f, 102f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(640f, 88f)));
            title.SetSiblingIndex(banner.GetSiblingIndex() + 1);
        }

        private static void RestyleSubtitle(Transform content)
        {
            Transform level = content.Find("LevelText");
            if (level == null) return;

            GetOrAdd<ThemedGraphic>(level.gameObject).Configure(UiColor.TextPrimary);
            GetOrAdd<ThemedText>(level.gameObject).Configure(UiTextStyle.Body, 0.96f);
            Presets(Rt(level),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(760f, 44f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(760f, 40f)));

            Transform oldDecor = content.Find("SubtitleDecor");
            if (oldDecor != null) Object.DestroyImmediate(oldDecor.gameObject);
            var decor = Go("SubtitleDecor", content).transform;
            decor.SetSiblingIndex(level.GetSiblingIndex());
            Presets(Rt(decor),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(620f, 44f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(620f, 40f)));

            var dL = Img(decor, "DiamondL", UiSprite.Solid, UiColor.Accent, 1f, false, true);
            Place(Rt(dL), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, 16f));
            Rt(dL).localRotation = Quaternion.Euler(0f, 0f, 45f);

            var dR = Img(decor, "DiamondR", UiSprite.Solid, UiColor.Gold, 1f, false, true);
            Place(Rt(dR), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18f, 0f), new Vector2(16f, 16f));
            Rt(dR).localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void RelayoutCardsContainer(Transform content)
        {
            Transform container = content.Find("CardsContainer");
            if (container == null) return;
            var r = GetOrAdd<ResponsiveRect>(container.gameObject);
            // Compact punchy card heights (~332 px per card) matching the mockup.
            var portrait = P(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(980f, 1052f));
            r.SetPreset(LayoutMode.Portrait, portrait);
            portrait.ApplyTo(Rt(container));
            EditorUtility.SetDirty(r);
        }

        // ------------------------------------------------------------------ card

        private static void RestyleCard(CardUI ui)
        {
            Transform card = ui.transform;

            // Glow behind body, ShineMask right on top of body (behind text/icon/band).
            Transform glow = card.Find("Glow");
            if (glow != null)
            {
                glow.SetSiblingIndex(0);
                Stretch(Rt(glow), -65f, -65f, -65f, -65f);
            }

            Transform bg = card.Find("Background");
            if (bg != null)
            {
                bg.SetSiblingIndex(1);
                GetOrAdd<ThemedGraphic>(bg.gameObject).Configure(UiColor.Surface, 1f, UiSprite.Card);
                bg.GetComponent<Image>().pixelsPerUnitMultiplier = CARD_CORNER_PPU;
            }

            Transform shineMask = card.Find("ShineMask");
            if (shineMask != null)
            {
                shineMask.SetSiblingIndex(2);
                Stretch(Rt(shineMask), 14f, 18f, 14f, 68f);
                Transform shine = shineMask.Find("Shine");
                if (shine != null)
                {
                    var shineImg = shine.GetComponent<Image>();
                    if (shineImg != null)
                    {
                        shineImg.sprite = Theme.shineSprite;
                        shineImg.type = Image.Type.Simple;
                        shineImg.color = new Color(1f, 0.96f, 0.84f, 0.36f);
                    }
                    Rt(shine).sizeDelta = new Vector2(220f, 0f);
                }
            }

            // Outer rarity ring sits behind Background so it wraps outside the dark outline without cutting the shadow.
            Transform frame = card.Find("Frame");
            if (frame != null)
            {
                frame.SetSiblingIndex(1);
                Stretch(Rt(frame), -2f, 9f, -2f, -4f);
                var frameImg = frame.GetComponent<Image>();
                frameImg.pixelsPerUnitMultiplier = FRAME_CORNER_PPU;
                frameImg.material = null;
            }

            // Flush top rarity header band.
            Transform band = card.Find("Tag");
            if (band == null) return;
            var bandImage = band.GetComponent<Image>();
            bandImage.sprite = Theme.cardHeaderSprite != null ? Theme.cardHeaderSprite : Theme.rowSprite;
            bandImage.type = Image.Type.Sliced;
            bandImage.pixelsPerUnitMultiplier = HEADER_CORNER_PPU;
            bandImage.material = Theme.surfaceMaterial;
            var bandPreset = P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 72f));
            Presets(Rt(band), bandPreset, bandPreset);

            Transform bandTextT = band.Find("Text");
            var bandText = bandTextT != null ? bandTextT.GetComponent<TextMeshProUGUI>() : null;
            if (bandText != null)
            {
                bandText.alignment = TextAlignmentOptions.MidlineLeft;
                bandText.textWrappingMode = TextWrappingModes.NoWrap;
                bandText.overflowMode = TextOverflowModes.Ellipsis;
                bandText.enableAutoSizing = true;
                bandText.fontSizeMin = 13f;
                bandText.fontSizeMax = Theme.captionSize * 1.25f;
                GetOrAdd<ThemedText>(bandText.gameObject).Configure(UiTextStyle.Caption, 1.25f, true, true);
                Presets(Rt(bandText), Inset(28f, 246f, 0f, 4f), Inset(18f, 192f, 0f, 4f));
            }

            // Level badge stays inside the band, right-aligned.
            Transform badge = card.Find("LevelBadge") ?? band.Find("LevelBadge");
            if (badge != null)
            {
                badge.SetParent(band, false);
                var badgePortrait = P(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(210f, -4f));
                var badgeLandscape = P(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(162f, -4f));
                Presets(Rt(badge), badgePortrait, badgeLandscape);
                var badgeText = badge.GetComponentInChildren<TextMeshProUGUI>(true);
                if (badgeText != null)
                {
                    badgeText.alignment = TextAlignmentOptions.MidlineRight;
                    badgeText.textWrappingMode = TextWrappingModes.NoWrap;
                    badgeText.enableAutoSizing = true;
                    badgeText.fontSizeMin = 15f;
                    badgeText.fontSizeMax = Theme.captionSize * 1.25f;
                    GetOrAdd<ThemedText>(badgeText.gameObject).Configure(UiTextStyle.Caption, 1.25f, true, true);
                    Stretch(Rt(badgeText), 0f, 2f, 0f, 0f);
                }
            }

            // Remove old band-nested pips if present, build new segment pips below the title.
            Transform oldBandPips = band.Find("Pips");
            if (oldBandPips != null) Object.DestroyImmediate(oldBandPips.gameObject);
            Image[] pips = BuildPips(card, out Image pipGlow);

            GameObject newTag = BuildNewTag(card, out TextMeshProUGUI newTagText);

            // Clean icon container (squircle border + recessed flat inner backing).
            Transform iconFrame = card.Find("IconFrame");
            Image innerImg = null;
            if (iconFrame != null)
            {
                var frameImg = iconFrame.GetComponent<Image>();
                if (frameImg != null)
                {
                    frameImg.sprite = Theme.squircleSprite != null ? Theme.squircleSprite : Theme.outlineSprite;
                    frameImg.type = Image.Type.Sliced;
                    frameImg.material = Theme.surfaceMaterial;
                }
                // Portrait: the body (icon + text column) is centred in the area below the 64 px band.
                Presets(Rt(iconFrame),
                    P(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, BODY_CENTER_Y), new Vector2(174f, 174f)),
                    P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(170f, 170f)));

                Transform inner = iconFrame.Find("Inner");
                if (inner != null)
                {
                    innerImg = inner.GetComponent<Image>();
                    if (innerImg != null)
                    {
                        innerImg.sprite = Theme.rowSprite != null ? Theme.rowSprite : Theme.cardSprite;
                        innerImg.type = Image.Type.Sliced;
                        innerImg.material = null;
                    }
                    Stretch(Rt(inner), 10f, 16f, 10f, 10f);
                }
                Transform icon = iconFrame.Find("Icon");
                if (icon != null)
                {
                    Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(120f, 120f));
                }
            }

            Transform keyHint = card.Find("KeyHintHost");
            if (keyHint != null)
            {
                Presets(Rt(keyHint),
                    P(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(12f, BODY_TOP_Y + 12f), new Vector2(44f, 32f)),
                    P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-95f, -84f), new Vector2(44f, 32f)));
            }

            // Card name: balanced bold rounded display title (48 px max at 1080 px width).
            Transform title = card.Find("Title");
            if (title != null)
            {
                if (title.TryGetComponent(out TMP_Text titleTmp))
                {
                    titleTmp.enableAutoSizing = true;
                    titleTmp.fontSizeMin = 24f;
                    titleTmp.fontSizeMax = Theme.h2Size * TITLE_SCALE;
                    titleTmp.textWrappingMode = TextWrappingModes.NoWrap;
                }
                GetOrAdd<ThemedText>(title.gameObject).Configure(UiTextStyle.H2, TITLE_SCALE);
                Presets(Rt(title),
                    P(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(220f, BODY_TOP_Y + 4f), new Vector2(-242f, TITLE_HEIGHT)),
                    P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(-44f, 58f)));
            }

            Transform desc = card.Find("Description");
            if (desc != null)
            {
                GetOrAdd<ThemedText>(desc.gameObject).Configure(UiTextStyle.Body, 0.94f);
                GetOrAdd<ThemedGraphic>(desc.gameObject).Configure(UiColor.TextPrimary, 0.88f);
                Presets(Rt(desc),
                    P(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(220f, BODY_TOP_Y - 94f), new Vector2(-242f, 102f)),
                    Inset(28f, 28f, 368f, 92f));
            }

            // Dimmed body overlay below the header band; hide the overlapping Lock icon.
            Transform lockOverlay = card.Find("LockOverlay");
            if (lockOverlay != null)
            {
                // Same rounded silhouette as the card so no square corners poke out of the bigger radius.
                Stretch(Rt(lockOverlay), 0f, 0f, 0f, 0f);
                var lockImg = lockOverlay.GetComponent<Image>();
                if (lockImg != null)
                {
                    lockImg.sprite = Theme.cardSprite;
                    lockImg.type = Image.Type.Sliced;
                    lockImg.pixelsPerUnitMultiplier = CARD_CORNER_PPU;
                    lockImg.material = null;
                    lockImg.color = UiTheme.WithAlpha(Theme.surfaceSunken, 0.52f);
                }
                Transform lockIcon = lockOverlay.Find("Lock");
                if (lockIcon != null) lockIcon.gameObject.SetActive(false);
            }

            Transform ad = card.Find("AdPlaceholder");
            if (ad != null)
            {
                ad.SetAsLastSibling();
                var adImg = ad.GetComponent<Image>();
                if (adImg != null)
                {
                    adImg.sprite = Theme.buttonSprite;
                    adImg.type = Image.Type.Sliced;
                    GetOrAdd<ThemedGraphic>(ad.gameObject).Configure(UiColor.Ad, 1f, UiSprite.Button);
                }
                Transform adText = ad.Find("Text");
                if (adText != null)
                {
                    GetOrAdd<ThemedText>(adText.gameObject).Configure(UiTextStyle.Button, 0.86f);
                    GetOrAdd<ThemedGraphic>(adText.gameObject).Configure(UiColor.White);
                    Stretch(Rt(adText), 14f, 24f, 14f, 4f);
                }
                Presets(Rt(ad),
                    P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 20f), new Vector2(256f, 78f)),
                    P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(236f, 78f)));
            }

            // Explicit draw order so Frame wraps outside Background, and Tag, NewTag, AdPlaceholder stay crisp on top.
            string[] order =
            {
                "Glow", "Frame", "Background", "ShineMask", "IconFrame", "Title", "Pips",
                "Description", "LockOverlay", "Tag", "KeyHintHost", "NewTag", "AdPlaceholder"
            };
            int siblingIdx = 0;
            foreach (string childName in order)
            {
                Transform child = card.Find(childName);
                if (child != null) child.SetSiblingIndex(siblingIdx++);
            }

            SetBool(ui, "rarityBand", true);
            SetArray(ui, "levelPips", pips);
            Set(ui, "activePipGlow", pipGlow);
            Set(ui, "newTag", newTag);
            Set(ui, "newTagText", newTagText);
            Set(ui, "iconInnerImage", innerImg);
            EditorUtility.SetDirty(ui);
        }

        /// <summary>Row of chunky bordered rectangular segment pips placed on the card directly under the title.</summary>
        private static Image[] BuildPips(Transform card, out Image pipGlow)
        {
            Transform old = card.Find("Pips");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var row = Go("Pips", card);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Presets(Rt(row),
                P(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(220f, BODY_TOP_Y - 60f), new Vector2(0f, PIP_HEIGHT)),
                P(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -334f), new Vector2(0f, PIP_HEIGHT)));

            pipGlow = Img(row.transform, "PipGlow", UiSprite.Glow, UiColor.Gold, 0.75f, false, false);
            LE(pipGlow.gameObject, ignore: true);
            Place(Rt(pipGlow), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 44f));
            pipGlow.gameObject.SetActive(false);

            var pips = new Image[PIP_COUNT];
            for (int i = 0; i < PIP_COUNT; i++)
            {
                var pip = Img(row.transform, $"Pip_{i + 1}", UiSprite.Pip, UiColor.White, 1f, false, false);
                pip.type = Image.Type.Sliced;
                if (Theme.surfaceMaterial != null) pip.material = Theme.surfaceMaterial;
                Rt(pip).sizeDelta = new Vector2(PIP_WIDTH, PIP_HEIGHT);
                pips[i] = pip;
            }
            return pips;
        }

        private static GameObject BuildNewTag(Transform card, out TextMeshProUGUI text)
        {
            Transform old = card.Find("NewTag");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Sticker sitting neatly inside the right end of the rarity band.
            var tag = Img(card, "NewTag", UiSprite.Pill, UiColor.Danger, 1f, false, true);
            var tagPortrait = P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-104f, -36f), new Vector2(156f, 44f));
            var tagLandscape = P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-88f, -36f), new Vector2(140f, 42f));
            Presets(Rt(tag), tagPortrait, tagLandscape);
            Rt(tag).localRotation = Quaternion.Euler(0f, 0f, NEW_TAG_TILT);
            text = Txt(tag.transform, "Text", "NEW!", UiTextStyle.Button, UiColor.White, TextAlignmentOptions.Center, false, 0.66f);
            Stretch(Rt(text), 10f, 10f, 10f, 2f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 26f;
            tag.gameObject.AddComponent<PulseUI>().EditorSetup(0.07f, 4.2f);
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        // ------------------------------------------------------------------ build strip

        private static LevelUpBuildBarUI BuildStrip(Transform content)
        {
            Transform old = content.Find("BuildBar");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var bar = Img(content, "BuildBar", UiSprite.Card, UiColor.Surface, 0.98f, false, true);
            Presets(Rt(bar),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -264f), new Vector2(0f, 114f)),
                P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 34f), new Vector2(0f, 114f)));

            var barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(28, 28, 16, 24);
            barLayout.spacing = 16f;
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = false;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = false;
            bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = Txt(bar.transform, "Label", "YOUR BUILD", UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft, true, 1f);
            label.textWrappingMode = TextWrappingModes.Normal;
            LE(label.gameObject, prefW: 96f, minW: 84f, prefH: 72f);
            label.gameObject.AddComponent<LocalizedText>().EditorSetup("ui.levelup.your_build", true);

            var slotsRow = Go("Slots", bar.transform);
            Rt(slotsRow).sizeDelta = new Vector2(0f, BUILD_SLOT_SIZE);
            var rowLayout = slotsRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            slotsRow.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform actives = SlotGroup(slotsRow.transform, "Actives");
            var divider = Img(slotsRow.transform, "Divider", UiSprite.Solid, UiColor.Outline, 0.8f, false, true);
            Rt(divider).sizeDelta = new Vector2(3f, 56f);
            LE(divider.gameObject, prefW: 3f, minW: 3f, prefH: 56f);
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
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return Rt(go);
        }

        /// <summary>Reroll button: icon + "Reroll cards" + bright green "FREE" pill.</summary>
        private static void RestyleReroll(CardSelectionUI selection, Transform content)
        {
            Transform reroll = content.Find("RerollButton");
            if (reroll == null) return;

            var r = GetOrAdd<ResponsiveRect>(reroll.gameObject);
            var portrait = P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(840f, 124f));
            var landscape = P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(580f, 112f));
            r.SetPreset(LayoutMode.Portrait, portrait);
            r.SetPreset(LayoutMode.Landscape, landscape);
            portrait.ApplyTo(Rt(reroll));
            EditorUtility.SetDirty(r);

            Transform oldRow = reroll.Find("Row");
            if (oldRow != null) Object.DestroyImmediate(oldRow.gameObject);
            Transform oldText = reroll.Find("Text");
            if (oldText != null) Object.DestroyImmediate(oldText.gameObject);

            var row = Go("Row", reroll);
            Stretch(Rt(row), 24f, 28f, 24f, 6f);
            var hl = HL(row, 16f, Pad(0), TextAnchor.MiddleCenter, true, true, false, false);
            hl.childAlignment = TextAnchor.MiddleCenter;

            var icon = Icon(row.transform, "Icon", Theme.iconReroll, 44f, Theme.white);
            LE(icon.gameObject, prefW: 44f, prefH: 44f);

            var label = Txt(row.transform, "Text", "Reroll cards", UiTextStyle.Button, UiColor.White, TextAlignmentOptions.Center, false, 0.85f);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var badge = Img(row.transform, "FreeBadge", UiSprite.Pill, UiColor.XpFill, 1f, false, true);
            HL(badge.gameObject, 0f, Pad(22, 22, 4, 10), TextAnchor.MiddleCenter, true, true, false, false);
            LE(badge.gameObject, prefH: 50f);
            var badgeText = Txt(badge.transform, "Text", "FREE", UiTextStyle.Micro, UiColor.TextDark, TextAlignmentOptions.Center, true, 1.05f);
            GetOrAdd<ThemedText>(badgeText.gameObject).Configure(UiTextStyle.Micro, 1.05f, true, true);
            badgeText.textWrappingMode = TextWrappingModes.NoWrap;

            Transform hint = reroll.Find("KeyHintHost");
            if (hint != null) hint.SetAsLastSibling();

            var tb = reroll.GetComponent<ThemedButton>();
            if (tb != null)
            {
                tb.EditorSetup(ButtonStyle.Secondary, reroll.GetComponent<Image>(), label, icon, ButtonSound.None);
            }

            Set(selection, "rerollButtonText", label);
            Set(selection, "rerollFreeBadge", badge.gameObject);
            Set(selection, "rerollFreeBadgeText", badgeText);
            Set(selection, "rerollIcon", icon);
        }

        /// <summary>
        /// Portrait: the first-LevelUp FTUE hint covers the subtitle ("Level 2 · choose an upgrade" says the same),
        /// so no empty gap is reserved for it between the build strip and the cards.
        /// </summary>
        private static void RelayoutFtueHint(Transform content)
        {
            Transform hint = content.Find("FtueLevelUpHint");
            if (hint == null) return;
            var portrait = P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FTUE_HINT_PORTRAIT_Y), new Vector2(760f, 56f));
            hint.SetAsLastSibling();
            // Opaque: it covers the subtitle, so only the scale pulses (no alpha "glow" pulse on the pill itself).
            var pulse = hint.GetComponent<PulseUI>();
            if (pulse != null)
            {
                Set(pulse, "glow", null);
                EditorUtility.SetDirty(pulse);
            }
            var pill = hint.GetComponent<Image>();
            if (pill != null)
            {
                Color c = pill.color;
                c.a = 1f;
                pill.color = c;
                var themed = pill.GetComponent<ThemedGraphic>();
                if (themed != null) themed.Configure(themed.Color, 1f, themed.Sprite);
            }
            var r = GetOrAdd<ResponsiveRect>(hint.gameObject);
            r.SetPreset(LayoutMode.Portrait, portrait);
            portrait.ApplyTo(Rt(hint));
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
