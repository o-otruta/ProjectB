using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts
{
    public static partial class Sprint13SetupTool
    {
        private const float HUD_SIDE = 24f;
        
        private static void BuildGameplay(Scene scene)
        {
            Canvas canvas = FindCanvas(scene);
            Transform hud = FindRequired(canvas.transform, "SafeAreaHUD");

            // Overlay root for toasts and the dev menu: stays visible while the HUD fades under modals.
            Transform overlay = canvas.transform.Find("SafeAreaOverlay");
            if (overlay == null)
            {
                var go = Go("SafeAreaOverlay", canvas.transform, typeof(SafeAreaFitter));
                overlay = go.transform;
                Stretch(Rt(go));
            }

            Transform toast = hud.Find("AchievementPopup") ?? overlay.Find("AchievementPopup");
            if (toast != null) toast.SetParent(overlay, false);
            Transform dev = hud.Find("DebugCheatMenu") ?? overlay.Find("DebugCheatMenu");
            if (dev != null) dev.SetParent(overlay, false);

            BuildHudTop(hud);
            BuildAbilitySlots(FindRequired(hud, "AbilityHud").gameObject);
            BuildBossBar(FindRequired(hud, "BossHealthBar").gameObject);
            BuildComboHud(FindRequired(hud, "ComboHud").gameObject);
            BuildEventBanner(FindRequired(hud, "WaveEventBanner").gameObject);
            BuildUltimate(FindRequired(hud, "UltimateButton").gameObject);
            ConfigureCompass(FindRequired(hud, "SecretCompassIndicator").gameObject);
            Transform joystick = FindRequired(canvas.transform, "JoystickBg");
            StyleJoystick(joystick.gameObject);
            if (toast != null) BuildAchievementToast(toast.gameObject, 380f);

            // HUD modal rule: fade bars, slots, combo, compass, ultimate and joystick while not Playing.
            var hudGroup = Group(hud.gameObject);
            var joyGroup = Group(joystick.gameObject);
            var visibility = GetOrAdd<HudVisibilityController>(canvas);
            visibility.EditorSetup(new[] { hudGroup, joyGroup });

            // 13.6: dynamic HUD parts on their own canvases so per-frame updates don't rebuild the whole HUD.
            foreach (string path in new[] { "HpBar", "XpBar", "WaveHud", "CoinHud", "KillsHud", "ComboHud", "AbilityHud", "BossHealthBar" })
            {
                Transform t = hud.Find(path);
                if (t != null) EnsureNestedCanvas(t.gameObject, raycaster: false);
            }
            EnsureNestedCanvas(FindRequired(hud, "UltimateButton").gameObject, raycaster: true);

            // Modals
            BuildLevelUp(canvas);
            Transform settingsRoot = canvas.transform.Find("SettingsPopup");
            if (settingsRoot == null)
            {
                settingsRoot = Go("SettingsPopup", canvas.transform, typeof(SettingsPopupUI)).transform;
            }
            BuildSettingsPopup(settingsRoot.gameObject);
            BuildPause(canvas, settingsRoot.GetComponent<SettingsPopupUI>());
            BuildGameOver(canvas);

            // Draw order: HUD → LevelUp → Pause → Settings → Game Over → overlay (toasts, dev).
            FindRequired(canvas.transform, "CardSelectionPanel").SetAsLastSibling();
            FindRequired(canvas.transform, "PauseMenu").SetAsLastSibling();
            settingsRoot.SetAsLastSibling();
            FindRequired(canvas.transform, "GameOverPanel").SetAsLastSibling();
            overlay.SetAsLastSibling();

            DisableDecorativeRaycasts(canvas.transform);
            EditorUtility.SetDirty(canvas);
        }

        private static void EnsureNestedCanvas(GameObject go, bool raycaster)
        {
            if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
            if (raycaster && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>13.6: raycastTarget off on every non-interactive graphic.</summary>
        private static void DisableDecorativeRaycasts(Transform root)
        {
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.raycastTarget) continue;
                if (g.GetComponent<Selectable>() != null) continue;
                if (g.GetComponent<UnityEngine.EventSystems.IEventSystemHandler>() != null) continue;
                if (g is InvisibleRaycastTarget) continue;
                if (g.name == "Dim" || g.name == "BackgroundDim" || g.name == "Blocker") continue;
                g.raycastTarget = false;
            }
        }

        // ------------------------------------------------------------------ top block

        private static void BuildHudTop(Transform hud)
        {
            // HUD revision: no hero portrait. Row 1 — pause | big timer (+ WAVE caption) | coins & kills;
            // row 2 — "LV 2" plate + XP bar; row 3 — thick red HP bar with a heart. XP and HP differ by colour,
            // thickness and their left emblem (level plate vs heart).
            foreach (string obsolete in new[] { "HeroPortrait", "KillsHud" })
            {
                Transform old = hud.Find(obsolete);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // Pause (left)
            GameObject pause = FindRequired(hud, "PauseButton").gameObject;
            ClearChildren(pause.transform);
            foreach (var o in pause.GetComponents<Shadow>()) Object.DestroyImmediate(o);
            Place(Rt(pause), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HUD_SIDE, -16f), new Vector2(92f, 92f));
            var pauseIcon = Icon(pause.transform, "Icon", Theme.iconPause, 50f);
            Place(Rt(pauseIcon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(50f, 50f));
            var pauseThemed = GetOrAdd<ThemedButton>(pause);
            pauseThemed.EditorSetup(ButtonStyle.Secondary, pause.GetComponent<Image>(), null, pauseIcon, ButtonSound.None);

            // Timer + wave caption (centre)
            GameObject wave = FindRequired(hud, "WaveHud").gameObject;
            var waveUi = wave.GetComponent<WaveHudUI>();
            StripVisuals(wave);
            foreach (var img in wave.GetComponents<Image>()) Object.DestroyImmediate(img);
            Place(Rt(wave), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(420f, 128f));
            var timer = Txt(wave.transform, "TimerText", "00:00", UiTextStyle.Number, UiColor.White, TextAlignmentOptions.Center, true, 2.1f);
            Band(Rt(timer), true, 0f, 92f);
            timer.textWrappingMode = TextWrappingModes.NoWrap;
            var waveText = Txt(wave.transform, "WaveText", "WAVE 1", UiTextStyle.Caption, UiColor.Gold, TextAlignmentOptions.Center, true, 1.15f);
            waveText.GetComponent<ThemedText>().Configure(UiTextStyle.Number, 0.62f);
            Band(Rt(waveText), true, 90f, 34f);
            waveText.textWrappingMode = TextWrappingModes.NoWrap;
            Set(waveUi, "waveText", waveText);
            Set(waveUi, "timerText", timer);

            // Coins + kills (right, stacked)
            GameObject coin = FindRequired(hud, "CoinHud").gameObject;
            var coinUi = coin.GetComponent<CoinHudUI>();
            StripVisuals(coin);
            Place(Rt(coin), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HUD_SIDE, -14f), new Vector2(210f, 48f));
            var coinText = CounterPill(coin, Theme.iconCoin, UiColor.Gold);
            Set(coinUi, "coinsText", coinText);

            var kills = Go("KillsHud", hud, typeof(Image));
            Place(Rt(kills), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HUD_SIDE, -68f), new Vector2(210f, 48f));
            var killsText = CounterPill(kills, Theme.iconSkull, UiColor.TextPrimary);
            kills.AddComponent<KillCounterHudUI>().EditorSetup(killsText);
            kills.transform.SetSiblingIndex(coin.transform.GetSiblingIndex() + 1);

            // XP row: full-width green bar with "LV 2" centred inside
            GameObject xp = FindRequired(hud, "XpBar").gameObject;
            var xpUi = xp.GetComponent<XpBarUI>();
            RemoveSlider(xp);
            StripVisuals(xp);
            Band(Rt(xp), true, 128f, 38f, HUD_SIDE, HUD_SIDE);
            var xpBar = BuildProgressBar(xp, UiColor.XpFill, withGhost: false, labelStyle: UiTextStyle.Number, labelScale: 0.55f, out TextMeshProUGUI levelText);
            levelText.name = "Level";
            levelText.text = "LV 1";

            // Split-colour label: white over the empty part, dark over the green fill (masks follow the fill edge).
            var labelGroup = Go("LabelGroup", xp.transform);
            Stretch(Rt(labelGroup));
            var normalMask = Go("LabelMask", labelGroup.transform, typeof(RectMask2D));
            Stretch(Rt(normalMask));
            levelText.transform.SetParent(normalMask.transform, false);
            Stretch(Rt(levelText), 8f, 1f, 8f, 0f);
            var inverseMask = Go("InvertedMask", labelGroup.transform, typeof(RectMask2D));
            Stretch(Rt(inverseMask));
            var inverted = Txt(inverseMask.transform, "LevelInverted", "LV 1", UiTextStyle.Number, UiColor.Background, TextAlignmentOptions.Center, false, 0.55f);
            Object.DestroyImmediate(inverted.GetComponent<ThemedText>());
            if (Theme.displayFont != null)
            {
                inverted.font = Theme.displayFont;
                inverted.fontSharedMaterial = Theme.displayFont.material;
            }
            inverted.fontSize = levelText.fontSize;
            inverted.textWrappingMode = TextWrappingModes.NoWrap;
            inverted.overflowMode = TextOverflowModes.Overflow;
            Stretch(Rt(inverted), 8f, 1f, 8f, 0f);
            xpBar.EditorSetupSplitLabel(Rt(labelGroup), normalMask.GetComponent<RectMask2D>(), inverted,
                inverseMask.GetComponent<RectMask2D>(), 4f, UiColor.Background);
            Set(xpUi, "xpSlider", null);
            Set(xpUi, "levelText", null);
            Set(xpUi, "progressBar", xpBar);
            Set(xpUi, "levelBadgeText", levelText);

            // HP row: thick bar with ghost trail (thicker + red → reads differently from the XP bar)
            GameObject hp = FindRequired(hud, "HpBar").gameObject;
            var hpUi = hp.GetComponent<HpBarUI>();
            RemoveSlider(hp);
            StripVisuals(hp);
            Band(Rt(hp), true, 178f, 52f, HUD_SIDE, HUD_SIDE);
            var hpBar = BuildProgressBar(hp, UiColor.HpFill, withGhost: true, labelStyle: UiTextStyle.Number, labelScale: 0.6f, out TextMeshProUGUI hpText);
            Set(hpUi, "hpSlider", null);
            Set(hpUi, "hpText", hpText);
            Set(hpUi, "progressBar", hpBar);
        }

        /// <summary>Small dark counter pill: icon on the left, right-aligned number (coins / kills).</summary>
        private static TextMeshProUGUI CounterPill(GameObject root, Sprite icon, UiColor numberColor)
        {
            EnsureImage(root, UiSprite.Pill, UiColor.SurfaceSunken, 0.88f);
            HL(root, 8f, Pad(6, 18, 3, 3), TextAnchor.MiddleLeft, true, true, false, false);
            var ic = Icon(root.transform, "Icon", icon, 40f);
            LE(ic.gameObject, prefW: 40f, prefH: 40f);
            var text = Txt(root.transform, "Value", "0", UiTextStyle.Number, numberColor, TextAlignmentOptions.MidlineRight, true, 0.62f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            LE(text.gameObject, prefW: 0f, flexW: 1f);
            return text;
        }

        private static void RemoveSlider(GameObject go)
        {
            var slider = go.GetComponent<Slider>();
            if (slider != null) Object.DestroyImmediate(slider);
        }

        /// <summary>Frame + optional ghost + fill (Image.Filled) + centred label, wired into a <see cref="ProgressBarUI"/>.</summary>
        private static ProgressBarUI BuildProgressBar(GameObject root, UiColor fillColor, bool withGhost, UiTextStyle labelStyle, float labelScale, out TextMeshProUGUI label)
        {
            var frame = Img(root.transform, "Frame", UiSprite.BarFrame, UiColor.SurfaceSunken, 0.95f);
            Stretch(Rt(frame));
            float inset = Rt(root).sizeDelta.y >= 40f ? 7f : 4f;
            Image ghost = null;
            if (withGhost)
            {
                ghost = Img(root.transform, "Ghost", UiSprite.BarFill, UiColor.Ghost, 0.85f, false, false);
                Stretch(Rt(ghost), inset, inset, inset, inset);
                ghost.type = Image.Type.Filled;
                ghost.fillMethod = Image.FillMethod.Horizontal;
            }
            var fill = Img(root.transform, "Fill", UiSprite.BarFill, fillColor, 1f, false, false);
            Stretch(Rt(fill), inset, inset, inset, inset);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            label = Txt(root.transform, "Label", "100 / 100", labelStyle, UiColor.White, TextAlignmentOptions.Center, false, labelScale);
            Stretch(Rt(label), 8f, 2f, 8f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            var bar = GetOrAdd<ProgressBarUI>(root);
            bar.EditorSetup(fill, ghost, label, withGhost ? ProgressLabelFormat.Ratio : ProgressLabelFormat.None, fillColor);
            return bar;
        }

        // ------------------------------------------------------------------ ability slots

        private static void BuildAbilitySlots(GameObject hudGo)
        {
            var ui = hudGo.GetComponent<AbilityHudUI>();
            StripVisuals(hudGo);
            var rt = Rt(hudGo);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(HUD_SIDE + 4f, -244f);
            rt.sizeDelta = new Vector2(560f, 148f);
            VL(hudGo, 12f, Pad(0), TextAnchor.UpperLeft, false, false, false, false);

            GameObject actives = SlotRow(hudGo.transform, "ActiveSlots", out GameObject activeChip, out TextMeshProUGUI activeChipText);
            GameObject passives = SlotRow(hudGo.transform, "PassiveSlots", out GameObject passiveChip, out TextMeshProUGUI passiveChipText);

            Set(ui, "activeSlotsContainer", actives.transform);
            Set(ui, "passiveSlotsContainer", passives.transform);
            Set(ui, "slotPrefab", AssetDatabase.LoadAssetAtPath<AbilityHudSlotUI>(HUD_SLOT_PREFAB));
            SetFloat(ui, "slotSize", 64f);
            SetBool(ui, "hideLockedSlots", true);
            Set(ui, "activeLockedChip", activeChip);
            Set(ui, "activeLockedChipText", activeChipText);
            Set(ui, "passiveLockedChip", passiveChip);
            Set(ui, "passiveLockedChipText", passiveChipText);
        }

        private static GameObject SlotRow(Transform parent, string name, out GameObject chip, out TextMeshProUGUI chipText)
        {
            var row = Go(name, parent);
            Rt(row).sizeDelta = new Vector2(560f, 68f);
            HL(row, 10f, Pad(0), TextAnchor.MiddleLeft, false, false, false, false);
            var chipImg = Img(row.transform, "LockedChip", UiSprite.Pill, UiColor.SurfaceSunken, 0.75f);
            Rt(chipImg).sizeDelta = new Vector2(84f, 44f);
            chip = chipImg.gameObject;
            var lockIcon = Icon(chip.transform, "Lock", Theme.iconLock, 26f, Theme.textSecondary);
            Place(Rt(lockIcon), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 1f), new Vector2(26f, 26f));
            chipText = Txt(chip.transform, "Count", "+3", UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.MidlineRight, false, 1.1f);
            Stretch(Rt(chipText), 30f, 0f, 12f, 0f);
            chip.SetActive(false);
            return row;
        }

        // ------------------------------------------------------------------ boss bar, combo, event banner

        private static void BuildBossBar(GameObject root)
        {
            var ui = root.GetComponent<BossHealthBarUI>();
            Transform panelT = root.transform.Find("BarPanel");
            ClearChildren(root.transform);
            Place(Rt(root), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(920f, 120f));

            var panel = Img(root.transform, "BarPanel", UiSprite.Panel, UiColor.SurfaceSunken, 0.9f);
            Stretch(Rt(panel));
            var name = Txt(panel.transform, "BossNameText", "STONE GOLEM", UiTextStyle.H2, UiColor.Danger, TextAlignmentOptions.Center, false, 0.72f);
            Band(Rt(name), true, 8f, 44f, 20f, 20f);
            var barGo = Go("HpBar", panel.transform);
            Band(Rt(barGo), false, 18f, 46f, 22f, 22f);
            var bar = BuildProgressBar(barGo, UiColor.Danger, true, UiTextStyle.Number, 0.55f, out TextMeshProUGUI hpValue);
            panel.gameObject.SetActive(false);

            Set(ui, "barRoot", panel.gameObject);
            Set(ui, "bossNameText", name);
            Set(ui, "hpValueText", hpValue);
            Set(ui, "hpSlider", null);
            Set(ui, "fillImage", null);
            Set(ui, "progressBar", bar);
            bar.EditorSetup(bar.FillImage, barGo.transform.Find("Ghost").GetComponent<Image>(), null, ProgressLabelFormat.None, UiColor.Danger);
        }

        private static void BuildComboHud(GameObject root)
        {
            var ui = root.GetComponent<ComboHudUI>();
            ClearChildren(root.transform);
            Place(Rt(root), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-HUD_SIDE, -244f), new Vector2(250f, 132f));
            var content = Img(root.transform, "Content", UiSprite.Card, UiColor.Surface, 0.9f, false, false);
            Stretch(Rt(content));
            var combo = Txt(content.transform, "ComboText", "x5", UiTextStyle.Number, UiColor.TextPrimary, TextAlignmentOptions.Center, false, 1.1f);
            Band(Rt(combo), true, 6f, 60f, 10f, 10f);
            var bonus = Txt(content.transform, "BonusText", "+10% XP", UiTextStyle.Micro, UiColor.Gold, TextAlignmentOptions.Center, false, 1.1f);
            Band(Rt(bonus), true, 66f, 30f, 10f, 10f);
            var timerBg = Img(content.transform, "TimerBarBg", UiSprite.BarFrame, UiColor.SurfaceSunken, 1f, false, false);
            Band(Rt(timerBg), false, 16f, 16f, 18f, 18f);
            var timerFill = Img(timerBg.transform, "TimerFill", UiSprite.BarFill, UiColor.Gold, 1f, false, false);
            Stretch(Rt(timerFill), 3f, 3f, 3f, 3f);
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            content.gameObject.SetActive(false);

            Set(ui, "rootContainer", content.gameObject);
            Set(ui, "comboText", combo);
            Set(ui, "bonusText", bonus);
            Set(ui, "timerFillImage", timerFill);
            Set(ui, "backgroundImage", content);
        }

        private static void BuildEventBanner(GameObject root)
        {
            var ui = root.GetComponent<WaveEventBannerUI>();
            ClearChildren(root.transform);
            Place(Rt(root), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(820f, 190f));
            var content = Img(root.transform, "Content", UiSprite.Panel, UiColor.Surface, 0.94f, false, false);
            Stretch(Rt(content));
            var outline = content.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3f, -3f);
            var title = Txt(content.transform, "TitleText", "SWARM RUSH", UiTextStyle.H1, UiColor.Warning, TextAlignmentOptions.Center, false, 0.8f);
            Band(Rt(title), true, 14f, 66f, 20f, 20f);
            var subtitle = Txt(content.transform, "SubtitleText", "Survive the ring!", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 1.1f);
            Band(Rt(subtitle), true, 80f, 36f, 20f, 20f);
            var status = Txt(content.transform, "StatusText", "18s", UiTextStyle.Number, UiColor.TextPrimary, TextAlignmentOptions.Center, false, 0.6f);
            Band(Rt(status), true, 114f, 32f, 20f, 20f);
            var barBg = Img(content.transform, "BarBg", UiSprite.BarFrame, UiColor.SurfaceSunken, 1f, false, false);
            Band(Rt(barBg), false, 16f, 18f, 40f, 40f);
            var barFill = Img(barBg.transform, "BarFill", UiSprite.BarFill, UiColor.Warning, 1f, false, false);
            Stretch(Rt(barFill), 3f, 3f, 3f, 3f);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            content.gameObject.SetActive(false);

            Set(ui, "bannerRoot", content.gameObject);
            Set(ui, "backgroundImage", content);
            Set(ui, "borderOutline", outline);
            Set(ui, "titleText", title);
            Set(ui, "subtitleText", subtitle);
            Set(ui, "statusText", status);
            Set(ui, "progressFillImage", barFill);
        }

        // ------------------------------------------------------------------ ultimate, compass, joystick

        private static void BuildUltimate(GameObject root)
        {
            var ui = root.GetComponent<UltimateButtonUI>();
            ClearChildren(root.transform);
            Place(Rt(root), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 150f), new Vector2(200f, 200f));
            var face = root.GetComponent<Image>();
            face.sprite = Theme.circleSprite;
            face.type = Image.Type.Simple;
            face.color = Theme.accent;
            face.raycastTarget = true;

            var glow = Img(root.transform, "ReadyGlow", UiSprite.Glow, UiColor.Accent, 0.8f, false, false);
            Place(Rt(glow), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 330f));
            glow.transform.SetAsFirstSibling();
            var pulse = glow.gameObject.AddComponent<PulseUI>();
            pulse.EditorSetup(0.06f, 4f, glow);
            var ring = Img(root.transform, "Ring", UiSprite.RingThin, UiColor.Gold, 1f, false, false);
            Stretch(Rt(ring), -6f, -6f, -6f, -6f);
            var icon = Icon(root.transform, "Icon", null, 128f);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(128f, 128f));
            var fill = Img(root.transform, "CooldownFill", UiSprite.Circle, UiColor.SurfaceSunken, 0.72f, false, false);
            Stretch(Rt(fill));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            fill.gameObject.SetActive(false);
            var status = Txt(root.transform, "StatusText", "", UiTextStyle.Number, UiColor.White, TextAlignmentOptions.Center, false, 1.1f);
            Stretch(Rt(status));
            var label = Txt(root.transform, "LabelText", "AEGIS SHIELD", UiTextStyle.Micro, UiColor.TextPrimary, TextAlignmentOptions.Center, false, 1.1f);
            Place(Rt(label), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(300f, 34f));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSharedMaterial = Theme.numberMaterial;

            Set(ui, "button", root.GetComponent<Button>());
            Set(ui, "backgroundImage", face);
            Set(ui, "iconImage", icon);
            Set(ui, "cooldownFillImage", fill);
            Set(ui, "labelText", label);
            Set(ui, "statusText", status);
            Set(ui, "readyGlow", glow.gameObject);
        }

        private static void ConfigureCompass(GameObject root)
        {
            var ui = root.GetComponent<SecretCompassIndicatorUI>();
            // Arrows stay in the safe band below the HUD block and above the joystick / ultimate.
            SetFloat(ui, "topPadding", 430f);
            SetFloat(ui, "bottomPadding", 460f);
            SetFloat(ui, "sidePadding", 72f);
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (Theme.displayFont != null) text.font = Theme.displayFont;
                if (Theme.numberMaterial != null) text.fontSharedMaterial = Theme.numberMaterial;
                text.fontSize = 26f;
                text.fontStyle = FontStyles.Normal;
                var themed = GetOrAdd<ThemedText>(text);
                themed.Configure(UiTextStyle.Number, 0.65f);
            }
        }

        private static void StyleJoystick(GameObject root)
        {
            Transform ring = root.transform.Find("JoystickRing");
            if (ring == null) return;
            var ringImg = ring.GetComponent<Image>();
            ringImg.sprite = Theme.ringThinSprite;
            ringImg.color = UiTheme.WithAlpha(Theme.white, 0.45f);
            Transform handle = ring.Find("JoystickHandle");
            if (handle != null)
            {
                var h = handle.GetComponent<Image>();
                h.sprite = Theme.knobSprite;
                h.color = UiTheme.WithAlpha(Theme.accent, 0.9f);
            }
        }
    }
}
