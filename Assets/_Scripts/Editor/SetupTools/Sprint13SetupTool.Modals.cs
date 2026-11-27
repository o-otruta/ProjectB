using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data.Platform;
using ProjectB.LevelUp;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts
{
    public static partial class Sprint13SetupTool
    {
        // Portrait / landscape sizes of the LevelUp stack (AUDIT 4.6.Г: stack in portrait, row in landscape).
        private static readonly Vector2 PortraitCardsSize = new Vector2(980f, 852f);
        private static readonly Vector2 LandscapeCardsSize = new Vector2(1560f, 600f);

        // ------------------------------------------------------------------ LevelUp

        private static void BuildLevelUp(Canvas canvas)
        {
            var selection = canvas.GetComponent<CardSelectionUI>();
            GameObject panel = FindRequired(canvas.transform, "CardSelectionPanel").gameObject;

            var cards = panel.GetComponentsInChildren<CardUI>(true);
            System.Array.Sort(cards, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var c in cards) c.transform.SetParent(null, false);

            StripVisuals(panel);
            Stretch(Rt(panel));
            EnsureImage(panel, UiSprite.Solid, UiColor.Overlay, 1f, true);
            panel.AddComponent<ThemedGraphic>().Configure(UiColor.Overlay, 1f, UiSprite.Solid);
            var group = Group(panel);

            var content = Go("Content", panel.transform, typeof(SafeAreaFitter));
            Stretch(Rt(content));

            var title = Txt(content.transform, "Title", "LEVEL UP!", UiTextStyle.Display, UiColor.Gold);
            Band(Rt(title), true, 150f, 120f, 40f, 40f);
            SetPresets(title.gameObject, Rt(title), landscapeTop: 30f, height: 110f);
            var level = Txt(content.transform, "LevelText", "Level 2", UiTextStyle.Body, UiColor.TextSecondary);
            Band(Rt(level), true, 270f, 48f, 40f, 40f);
            SetPresets(level.gameObject, Rt(level), landscapeTop: 134f, height: 44f);

            var container = Go("CardsContainer", content.transform);
            var crt = Rt(container);
            Place(crt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), PortraitCardsSize);
            var stack = container.AddComponent<AdaptiveLayoutGroup>();
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = true;
            stack.SetSettings(LayoutMode.Portrait, new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 24f, childAlignment = TextAnchor.MiddleCenter });
            stack.SetSettings(LayoutMode.Landscape, new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 36f, childAlignment = TextAnchor.MiddleCenter });
            var containerPresets = container.AddComponent<ResponsiveRect>();
            containerPresets.SetPreset(LayoutMode.Portrait, RectPreset.Capture(crt));
            containerPresets.SetPreset(LayoutMode.Landscape, new RectPreset
            {
                anchorMin = new Vector2(0.5f, 0.5f), anchorMax = new Vector2(0.5f, 0.5f), pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = new Vector2(0f, -20f), sizeDelta = LandscapeCardsSize
            });

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].transform.SetParent(container.transform, false);
                cards[i].name = $"Card_{i + 1}";
                BuildCard(cards[i].gameObject, cards[i], i == 2);
            }

            var reroll = Btn(content.transform, "RerollButton", "Reroll", ButtonStyle.Secondary, null, 0f, UiTextStyle.Button, 0.85f, ButtonSound.None);
            Place(Rt(reroll), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(560f, 116f));
            var rerollPresets = reroll.gameObject.AddComponent<ResponsiveRect>();
            rerollPresets.SetPreset(LayoutMode.Portrait, RectPreset.Capture(Rt(reroll)));
            rerollPresets.SetPreset(LayoutMode.Landscape, new RectPreset
            {
                anchorMin = new Vector2(0.5f, 0f), anchorMax = new Vector2(0.5f, 0f), pivot = new Vector2(0.5f, 0f),
                anchoredPosition = new Vector2(0f, 36f), sizeDelta = new Vector2(520f, 104f)
            });

            Set(selection, "selectionPanel", panel);
            SetArray(selection, "cardSlots", cards);
            Set(selection, "rerollButton", reroll.Button);
            Set(selection, "rerollButtonText", reroll.Label);
            Set(selection, "titleText", title);
            Set(selection, "levelText", level);
            Set(selection, "panelGroup", group);
        }

        private static void SetPresets(GameObject go, RectTransform rt, float landscapeTop, float height)
        {
            var presets = GetOrAdd<ResponsiveRect>(go);
            presets.SetPreset(LayoutMode.Portrait, RectPreset.Capture(rt));
            RectPreset land = RectPreset.Capture(rt);
            land.anchoredPosition = new Vector2(land.anchoredPosition.x, -landscapeTop);
            land.sizeDelta = new Vector2(land.sizeDelta.x, height);
            presets.SetPreset(LayoutMode.Landscape, land);
        }

        /// <summary>Applies portrait + landscape presets to a card child (anchor/pivot/pos/size).</summary>
        private static void CardPresets(RectTransform rt, RectPreset portrait, RectPreset landscape)
        {
            portrait.ApplyTo(rt);
            var r = rt.gameObject.AddComponent<ResponsiveRect>();
            r.SetPreset(LayoutMode.Portrait, portrait);
            r.SetPreset(LayoutMode.Landscape, landscape);
        }

        private static RectPreset P(Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            return new RectPreset { captured = true, anchorMin = aMin, anchorMax = aMax, pivot = pivot, anchoredPosition = pos, sizeDelta = size };
        }

        /// <summary>Insets inside the card: left / right / top / bottom → stretched preset.</summary>
        private static RectPreset Inset(float left, float right, float top, float bottom)
        {
            return P(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2((left - right) * 0.5f, (bottom - top) * 0.5f), new Vector2(-(left + right), -(top + bottom)));
        }

        private static void BuildCard(GameObject card, CardUI ui, bool adCard)
        {
            StripVisuals(card);
            foreach (var le in card.GetComponents<LayoutElement>()) Object.DestroyImmediate(le);
            var hit = EnsureImage(card, UiSprite.Solid, UiColor.White, 0f, true);
            var button = GetOrAdd<Button>(card);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            var group = Group(card);

            var glow = Img(card.transform, "Glow", UiSprite.Glow, UiColor.RarityCommon, 0.3f, false, false);
            Stretch(Rt(glow), -40f, -40f, -40f, -40f);
            var bg = Img(card.transform, "Background", UiSprite.Card, UiColor.SurfaceRaised, 1f, false, false);
            Stretch(Rt(bg));
            var frame = Img(card.transform, "Frame", UiSprite.Outline, UiColor.RarityCommon, 1f, false, false);
            Stretch(Rt(frame), 0f, 8f, 0f, 0f);

            var shineMask = Go("ShineMask", card.transform, typeof(RectMask2D));
            Stretch(Rt(shineMask), 6f, 14f, 6f, 6f);
            var shine = Img(shineMask.transform, "Shine", UiSprite.Solid, UiColor.White, 0.22f, false, false);
            var srt = Rt(shine);
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(90f, 200f);
            srt.localRotation = Quaternion.Euler(0f, 0f, -18f);
            shine.gameObject.SetActive(false);

            var iconFrame = Img(card.transform, "IconFrame", UiSprite.Card, UiColor.RarityCommon, 1f, false, false);
            CardPresets(Rt(iconFrame),
                P(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 4f), new Vector2(184f, 184f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(190f, 190f)));
            var iconInner = Img(iconFrame.transform, "Inner", UiSprite.Card, UiColor.SurfaceSunken, 1f, false, false);
            Stretch(Rt(iconInner), 10f, 16f, 10f, 8f);
            var icon = Icon(iconFrame.transform, "Icon", null, 128f);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(128f, 128f));

            var title = Txt(card.transform, "Title", "Card", UiTextStyle.H2, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true, 0.95f, true);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            CardPresets(Rt(title),
                P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(236f, -24f), new Vector2(-466f, 60f)),
                P(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(-40f, 60f)));

            var tagBg = Img(card.transform, "Tag", UiSprite.Pill, UiColor.RarityCommon, 0.25f, false, false);
            CardPresets(Rt(tagBg),
                P(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(236f, -90f), new Vector2(220f, 42f)),
                P(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -314f), new Vector2(220f, 42f)));
            var tag = Txt(tagBg.transform, "Text", "NEW ACTIVE", UiTextStyle.Micro, UiColor.RarityCommon, TextAlignmentOptions.Center, false, 1.05f);
            Stretch(Rt(tag), 8f, 2f, 8f, 0f);
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.overflowMode = TextOverflowModes.Overflow;

            var desc = Txt(card.transform, "Description", "Description", UiTextStyle.Body, UiColor.TextSecondary, TextAlignmentOptions.TopLeft, true, 0.85f, true);
            CardPresets(Rt(desc), Inset(236f, 28f, 142f, 22f), Inset(28f, 28f, 370f, 84f));

            var levelBadge = Img(card.transform, "LevelBadge", UiSprite.Pill, UiColor.Accent, 1f, false, false);
            CardPresets(Rt(levelBadge),
                P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(210f, 46f)),
                P(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(300f, 48f)));
            var levelText = Txt(levelBadge.transform, "Text", "Lv 1 → Lv 2", UiTextStyle.Micro, UiColor.White, TextAlignmentOptions.Center, false, 1.05f);
            Stretch(Rt(levelText), 8f, 2f, 8f, 0f);
            levelText.textWrappingMode = TextWrappingModes.NoWrap;
            levelText.enableAutoSizing = true;
            levelText.fontSizeMin = 14f;
            levelText.fontSizeMax = Theme.microSize * 1.05f;

            GameObject adBadge = null;
            TextMeshProUGUI adText = null;
            GameObject lockOverlay = null;
            if (adCard)
            {
                var lockImg = Img(card.transform, "LockOverlay", UiSprite.Card, UiColor.SurfaceSunken, 0.5f, false, false);
                Stretch(Rt(lockImg));
                lockOverlay = lockImg.gameObject;
                var lockIcon = Icon(lockImg.transform, "Lock", Theme.iconLock, 72f, Theme.textPrimary);
                CardPresets(Rt(lockIcon),
                    P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -22f), new Vector2(64f, 64f)),
                    P(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(84f, 84f)));

                var ad = Img(card.transform, "AdPlaceholder", UiSprite.Pill, UiColor.Ad, 1f, false, false);
                adBadge = ad.gameObject;
                CardPresets(Rt(ad),
                    P(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 24f), new Vector2(150f, 50f)),
                    P(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(150f, 50f)));
                adText = Txt(ad.transform, "Text", "<sprite name=\"ad\"> AD", UiTextStyle.Caption, UiColor.White, TextAlignmentOptions.Center, false, 1.05f);
                Stretch(Rt(adText), 6f, 2f, 6f, 0f);
                adText.textWrappingMode = TextWrappingModes.NoWrap;
                adText.overflowMode = TextOverflowModes.Overflow;
            }

            Set(ui, "nameText", title);
            Set(ui, "descriptionText", desc);
            Set(ui, "bgImage", bg);
            Set(ui, "iconImage", icon);
            Set(ui, "adBadgeObject", adBadge);
            Set(ui, "adBadgeText", adText);
            Set(ui, "levelBadgeObject", levelBadge.gameObject);
            Set(ui, "levelBadgeText", levelText);
            Set(ui, "levelBadgeBg", levelBadge);
            Set(ui, "frameImage", frame);
            Set(ui, "glowImage", glow);
            Set(ui, "iconFrameImage", iconFrame);
            Set(ui, "tagText", tag);
            Set(ui, "tagBg", tagBg);
            Set(ui, "lockOverlay", lockOverlay);
            Set(ui, "shine", srt);
            Set(ui, "canvasGroup", group);
        }

        // ------------------------------------------------------------------ Pause

        private static void BuildPause(Canvas canvas, SettingsPopupUI settings)
        {
            GameObject root = FindRequired(canvas.transform, "PauseMenu").gameObject;
            var ui = root.GetComponent<PauseMenuUI>();
            Transform hud = FindRequired(canvas.transform, "SafeAreaHUD");
            var pauseButton = FindRequired(hud, "PauseButton").GetComponent<Button>();

            ClearChildren(root.transform);
            Stretch(Rt(root));
            var panel = Img(root.transform, "PausePanel", UiSprite.Solid, UiColor.Overlay, 1f, true);
            Stretch(Rt(panel));
            Transition(panel.gameObject, Vector2.zero);

            var window = Img(panel.transform, "Window", UiSprite.Panel, UiColor.Surface);
            Place(Rt(window), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 0f));
            VL(window.gameObject, 22f, Pad(40, 40, 40, 52), TextAnchor.UpperCenter, true, true, true, false);
            window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = Txt(window.transform, "Title", "PAUSED", UiTextStyle.H1, UiColor.TextPrimary);
            LE(title.gameObject, prefH: 84f);

            var summary = Go("Summary", window.transform);
            HL(summary, 14f, Pad(0), TextAnchor.MiddleCenter, true, true, true, false);
            LE(summary, prefH: 150f);
            var wave = StatChip(summary.transform, "WaveChip", Theme.iconFlame, "WAVE", 190f, 150f, out _);
            var time = StatChip(summary.transform, "TimeChip", Theme.iconClock, "TIME", 190f, 150f, out _);
            var kills = StatChip(summary.transform, "KillsChip", Theme.iconSkull, "KILLS", 190f, 150f, out _);
            var coins = StatChip(summary.transform, "CoinsChip", Theme.iconCoin, "COINS", 190f, 150f, out _);

            var buildHeader = Txt(window.transform, "BuildHeader", "BUILD", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft);
            LE(buildHeader.gameObject, prefH: 36f);
            var build = Go("BuildGrid", window.transform, typeof(GridLayoutGroup));
            var grid = build.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96f, 96f);
            grid.spacing = new Vector2(20f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 7;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = Pad(12, 12, 12, 12);

            var synSection = Go("SynergySection", window.transform);
            VL(synSection, 10f, Pad(0), TextAnchor.UpperLeft, true, true, true, false);
            var synHeader = Txt(synSection.transform, "Header", "ACTIVE SYNERGIES", UiTextStyle.Caption, UiColor.Shard, TextAlignmentOptions.MidlineLeft);
            LE(synHeader.gameObject, prefH: 36f);
            var synGrid = Go("Synergies", synSection.transform, typeof(GridLayoutGroup));
            var sg = synGrid.GetComponent<GridLayoutGroup>();
            sg.cellSize = new Vector2(410f, 64f);
            sg.spacing = new Vector2(14f, 10f);
            sg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            sg.constraintCount = 2;
            var chip = Img(synGrid.transform, "SynergyChipTemplate", UiSprite.Pill, UiColor.Shard, 0.2f, false, false);
            HL(chip.gameObject, 10f, Pad(10, 18, 4, 4), TextAnchor.MiddleLeft, true, true, false, false);
            var chipIcon = Icon(chip.transform, "Icon", null, 48f);
            LE(chipIcon.gameObject, prefW: 48f, prefH: 48f);
            var chipText = Txt(chip.transform, "Name", "Synergy", UiTextStyle.Caption, UiColor.Shard, TextAlignmentOptions.MidlineLeft, false, 1.1f);
            chipText.textWrappingMode = TextWrappingModes.NoWrap;
            chip.gameObject.SetActive(false);

            var resume = Btn(window.transform, "ResumeButton", "RESUME", ButtonStyle.Primary, Theme.iconPlay, 52f, UiTextStyle.Button, 1f, ButtonSound.None);
            LE(resume.gameObject, prefH: 132f);
            var settingsBtn = Btn(window.transform, "SettingsButton", "SETTINGS", ButtonStyle.Secondary, Theme.iconGear, 52f);
            LE(settingsBtn.gameObject, prefH: 116f);
            var quit = Btn(window.transform, "QuitToMenuButton", "QUIT TO MENU", ButtonStyle.Danger, null, 0f, UiTextStyle.Button, 0.9f, ButtonSound.None);
            LE(quit.gameObject, prefH: 116f);

            // Quit confirmation
            var confirm = Img(panel.transform, "QuitConfirmPanel", UiSprite.Solid, UiColor.Overlay, 1f, true);
            Stretch(Rt(confirm));
            var dialog = Img(confirm.transform, "Dialog", UiSprite.Panel, UiColor.SurfaceRaised);
            Place(Rt(dialog), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 470f));
            Transition(dialog.gameObject, Vector2.zero, pop: true);
            var dTitle = Txt(dialog.transform, "Title", "QUIT RUN?", UiTextStyle.H1, UiColor.Danger);
            Band(Rt(dTitle), true, 40f, 80f, 30f, 30f);
            var dMsg = Txt(dialog.transform, "Message", "Coins, shards and stats from this run are kept.", UiTextStyle.Body, UiColor.TextSecondary);
            Band(Rt(dMsg), true, 132f, 110f, 50f, 50f);
            var row = Go("ButtonsRow", dialog.transform);
            Band(Rt(row), false, 44f, 124f, 44f, 44f);
            HL(row, 24f, Pad(0), TextAnchor.MiddleCenter, true, true, true, true);
            var cancel = Btn(row.transform, "CancelButton", "CANCEL", ButtonStyle.Secondary, null, 0f, UiTextStyle.Button, 0.9f, ButtonSound.None);
            var confirmBtn = Btn(row.transform, "ConfirmButton", "QUIT", ButtonStyle.Danger, null, 0f, UiTextStyle.Button, 0.9f, ButtonSound.None);
            confirm.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            Set(ui, "pauseButton", pauseButton);
            Set(ui, "pausePanel", panel.gameObject);
            Set(ui, "musicSlider", null);
            Set(ui, "sfxSlider", null);
            Set(ui, "musicValueText", null);
            Set(ui, "sfxValueText", null);
            Set(ui, "resumeButton", resume.Button);
            Set(ui, "quitToMenuButton", quit.Button);
            Set(ui, "quitConfirmPanel", confirm.gameObject);
            Set(ui, "confirmQuitButton", confirmBtn.Button);
            Set(ui, "cancelQuitButton", cancel.Button);
            Set(ui, "quitConfirmText", dMsg);
            Set(ui, "settingsButton", settingsBtn.Button);
            Set(ui, "settingsPopup", settings);
            Set(ui, "waveValueText", wave);
            Set(ui, "timeValueText", time);
            Set(ui, "killsValueText", kills);
            Set(ui, "coinsValueText", coins);
            Set(ui, "buildContainer", build.transform);
            Set(ui, "buildSlotPrefab", AssetDatabase.LoadAssetAtPath<AbilityHudSlotUI>(HUD_SLOT_PREFAB));
            Set(ui, "synergyContainer", synGrid.transform);
            Set(ui, "synergySection", synSection);
            Set(ui, "synergyChipTemplate", chip.gameObject);
        }

        // ------------------------------------------------------------------ Game Over

        private static void BuildGameOver(Canvas canvas)
        {
            GameObject root = FindRequired(canvas.transform, "GameOverPanel").gameObject;
            var ui = root.GetComponent<GameOverUI>();

            StripVisuals(root);
            Stretch(Rt(root));
            var bg = EnsureImage(root, UiSprite.Solid, UiColor.Background, 0.96f, true);
            root.AddComponent<ThemedGraphic>().Configure(UiColor.Background, 0.96f, UiSprite.Solid);
            var group = Group(root);

            var content = Go("Content", root.transform, typeof(SafeAreaFitter));
            Stretch(Rt(content));
            var list = Go("Layout", content.transform);
            Stretch(Rt(list), 48f, 40f, 48f, 70f);
            VL(list, 18f, Pad(0), TextAnchor.MiddleCenter, true, true, true, false);

            var title = Txt(list.transform, "Title", "DEFEATED", UiTextStyle.Display, UiColor.Danger, TextAlignmentOptions.Center, false);
            LE(title.gameObject, prefH: 116f);
            var wave = Txt(list.transform, "WaveText", "WAVE 12", UiTextStyle.H1, UiColor.TextPrimary);
            LE(wave.gameObject, prefH: 76f);

            var bestRow = Go("BestRow", list.transform);
            HL(bestRow, 0f, Pad(0), TextAnchor.MiddleCenter, false, false, false, false);
            LE(bestRow, prefH: 60f);
            var best = Img(bestRow.transform, "NewBestBadge", UiSprite.Pill, UiColor.Gold);
            Rt(best).sizeDelta = new Vector2(300f, 60f);
            HL(best.gameObject, 8f, Pad(16, 22, 4, 4), TextAnchor.MiddleCenter, true, true, false, false);
            var bestIcon = Icon(best.transform, "Star", Theme.iconStar, 40f);
            LE(bestIcon.gameObject, prefW: 40f, prefH: 40f);
            var bestText = Txt(best.transform, "Text", "NEW BEST!", UiTextStyle.Caption, UiColor.Background, TextAlignmentOptions.Center, true, 1.2f);
            bestText.textWrappingMode = TextWrappingModes.NoWrap;
            best.gameObject.SetActive(false);

            var stats = Go("StatChips", list.transform);
            HL(stats, 16f, Pad(0), TextAnchor.MiddleCenter, true, true, true, false);
            LE(stats, minH: 150f, prefH: 150f, flexH: 0f);
            var kills = StatChip(stats.transform, "KillsChip", Theme.iconSkull, "KILLS", 260f, 150f, out _);
            var time = StatChip(stats.transform, "TimeChip", Theme.iconClock, "TIME", 260f, 150f, out _);
            var combo = StatChip(stats.transform, "ComboChip", Theme.iconFlame, "BEST COMBO", 260f, 150f, out GameObject comboChip);

            var rewards = Go("Rewards", list.transform);
            HL(rewards, 18f, Pad(0), TextAnchor.MiddleCenter, false, false, false, false);
            LE(rewards, prefH: 96f);
            var coins = Pill(rewards.transform, "CoinsReward", CurrencyType.Coins, 200f, 92f, out TextMeshProUGUI coinsAmount);
            var shards = Pill(rewards.transform, "ShardsReward", CurrencyType.Shards, 200f, 92f, out TextMeshProUGUI shardsAmount);
            foreach (var (pill, amt) in new[] { (coins, coinsAmount), (shards, shardsAmount) })
            {
                var csf = GetOrAdd<ContentSizeFitter>(pill.gameObject);
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                HL(pill.gameObject, 12f, Pad(18, 28, 6, 8), TextAnchor.MiddleCenter, true, true, false, false);
                LE(pill.gameObject, minW: 200f, prefW: -1f, flexW: -1f, prefH: 92f);
                LE(amt.gameObject, prefW: -1f, flexW: -1f);
                amt.alignment = TextAlignmentOptions.MidlineLeft;
                amt.textWrappingMode = TextWrappingModes.NoWrap;
            }
            SetFloatField(coins, "countUpDuration", 1.1f);
            SetFloatField(shards, "countUpDuration", 0.9f);
            SetStringField(coins, "prefix", "+");
            SetStringField(shards, "prefix", "+");

            // Run achievements
            var achSection = Img(list.transform, "RunAchievementsSection", UiSprite.Card, UiColor.Surface, 0.95f);
            LE(achSection.gameObject, prefH: 190f, minH: 190f);
            var achHeader = Txt(achSection.transform, "Header", "ACHIEVEMENTS UNLOCKED", UiTextStyle.Caption, UiColor.Gold, TextAlignmentOptions.MidlineLeft);
            Band(Rt(achHeader), true, 14f, 40f, 24f, 24f);
            Transform achContent = ScrollList(achSection.transform, "ScrollView", 8f, Pad(16, 16, 4, 12), out ScrollRect achScroll);
            Stretch(Rt(achScroll), 0f, 10f, 0f, 58f);
            achSection.gameObject.SetActive(false);

            // Damage breakdown (auto height)
            var dmgSection = Img(list.transform, "DamageBreakdownSection", UiSprite.Card, UiColor.Surface, 0.95f);
            var dmgLayout = LE(dmgSection.gameObject, prefH: 200f, minH: 200f);
            var dmgHeader = Txt(dmgSection.transform, "Header", "DAMAGE BREAKDOWN", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft);
            Band(Rt(dmgHeader), true, 14f, 40f, 24f, 24f);
            Transform dmgContent = ScrollList(dmgSection.transform, "ScrollView", 6f, Pad(16, 16, 4, 12), out ScrollRect dmgScroll);
            Stretch(Rt(dmgScroll), 0f, 10f, 0f, 58f);
            dmgContent.name = "Container";

            var adRow = Go("AdButtons", list.transform);
            HL(adRow, 18f, Pad(0), TextAnchor.MiddleCenter, true, true, true, true);
            LE(adRow, minH: 124f, prefH: 124f, flexH: 0f);
            var revive = Btn(adRow.transform, "ReviveButton", "<sprite name=\"ad\"> REVIVE", ButtonStyle.Ad, null, 0f, UiTextStyle.Button, 0.85f);
            var x2 = Btn(adRow.transform, "DoubleCoinsButton", "<sprite name=\"ad\"> x2 COINS", ButtonStyle.Ad, null, 0f, UiTextStyle.Button, 0.85f);
            revive.Label.text = "<sprite name=\"ad\"> REVIVE";
            x2.Label.text = "<sprite name=\"ad\"> x2 Coins";

            var navRow = Go("NavButtons", list.transform);
            HL(navRow, 18f, Pad(0), TextAnchor.MiddleCenter, true, true, true, true);
            LE(navRow, minH: 116f, prefH: 116f, flexH: 0f);
            var restart = Btn(navRow.transform, "RestartButton", "RESTART", ButtonStyle.Primary, Theme.iconReroll, 46f, UiTextStyle.Button, 0.85f);
            var menuBtn = Btn(navRow.transform, "MenuButton", "MENU", ButtonStyle.Secondary, null, 0f, UiTextStyle.Button, 0.85f);

            OnClick(revive.Button, ui.OnReviveClicked);
            OnClick(x2.Button, ui.OnDoubleCoinsClicked);
            OnClick(restart.Button, ui.OnRestartClicked);
            OnClick(menuBtn.Button, ui.OnMenuClicked);

            Set(ui, "panel", root);
            Set(ui, "waveText", wave);
            Set(ui, "coinsText", null);
            Set(ui, "statsText", null);
            Set(ui, "damageBreakdownSection", dmgSection.gameObject);
            Set(ui, "damageBreakdownContainer", dmgContent);
            Set(ui, "achievementsSection", achSection.gameObject);
            Set(ui, "achievementsContainer", achContent);
            Set(ui, "reviveButton", revive.Button);
            Set(ui, "doubleCoinsButton", x2.Button);
            Set(ui, "doubleCoinsButtonText", x2.Label);
            Set(ui, "titleText", title);
            Set(ui, "newBestBadge", best.gameObject);
            Set(ui, "killsValueText", kills);
            Set(ui, "timeValueText", time);
            Set(ui, "comboValueText", combo);
            Set(ui, "comboChip", comboChip);
            Set(ui, "coinsLabel", coins);
            Set(ui, "shardsLabel", shards);
            Set(ui, "shardsChip", shards.gameObject);
            Set(ui, "breakdownLayout", dmgLayout);
            SetFloat(ui, "breakdownHeaderHeight", 72f);
            SetFloat(ui, "breakdownMaxHeight", 430f);
            Set(ui, "panelGroup", group);
        }

        private static void SetFloatField(Object target, string field, float value) => SetFloat(target, field, value);

        private static void SetStringField(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
