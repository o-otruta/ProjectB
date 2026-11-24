using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts
{
    public static partial class Sprint13SetupTool
    {
        private const string ABILITY_CATALOG_PATH = "Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset";

        private static void BuildMainMenu(Scene scene)
        {
            Canvas canvas = FindCanvas(scene);
            Transform safe = FindRequired(canvas.transform, "SafeAreaRoot");
            var menu = canvas.GetComponent<MainMenuUI>();

            // Camera: theme background behind every opaque screen, hero framed above the info card.
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Theme.background;
            }
            GameObject hero = GameObject.Find("Hero");
            if (cam != null && hero != null)
            {
                // 14.4: hero centred above the info card in Portrait, centre of the left half in Landscape.
                var framing = GetOrAdd<MenuPreviewFraming>(cam.gameObject);
                SetFloat(framing, "portraitDistance", 7.5f);
                SetFloat(framing, "landscapeDistance", 5.3f);
                var so = new SerializedObject(framing);
                so.FindProperty("portraitViewportPoint").vector2Value = new Vector2(0.5f, 0.59f);
                so.FindProperty("landscapeViewportPoint").vector2Value = new Vector2(0.25f, 0.47f);
                so.ApplyModifiedPropertiesWithoutUndo();
                framing.EditorSetup(hero.transform);
            }

            var topBar = BuildTopBar(FindRequired(safe, "TopBar").gameObject, menu, out CurrencyLabel coins, out CurrencyLabel shards, out TextMeshProUGUI coinsText);
            var tabBar = BuildBottomBar(FindRequired(safe, "BottomBar").gameObject, menu, out BottomTabUI[] tabs);

            BuildPlayScreen(FindRequired(safe, "PlayScreenPanel").gameObject, menu);
            BuildUpgradesScreen(FindRequired(safe, "UpgradesPanel").gameObject, menu);
            BuildAbilitiesScreen(FindRequired(safe, "AbilitiesPanel").gameObject);
            BuildAchievementsScreen(FindRequired(safe, "AchievementsPanel").gameObject);
            BuildDailyBonusScreen(FindRequired(safe, "DailyBonusScreenPanel").gameObject);
            RestyleConstellation(FindRequired(safe, "ConstellationScreen").gameObject);
            BuildSettingsPopup(FindRequired(safe, "SettingsPopup").gameObject);
            BuildAchievementToast(FindRequired(safe, "AchievementPopup").gameObject, TOP_BAR_HEIGHT + 16f);

            // Draw order: screens, then header + tab bar, then popups.
            topBar.transform.SetSiblingIndex(safe.childCount - 1);
            tabBar.transform.SetSiblingIndex(safe.childCount - 1);
            FindRequired(safe, "SettingsPopup").SetAsLastSibling();
            FindRequired(safe, "AchievementPopup").SetAsLastSibling();

            Set(menu, "coinsText", coinsText);
            Set(menu, "coinsLabel", coins);
            Set(menu, "shardsLabel", shards);
            Set(menu, "tabBar", tabBar);
            SetArray(menu, "tabs", tabs);
            Set(menu, "achievementsBadge", null);
            Set(menu, "bonusBadge", null);
            Set(menu, "previewCamera", cam);
            Set(menu, "heroPreviewRoot", hero);

            EditorUtility.SetDirty(menu);
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var canvas = root.GetComponent<Canvas>();
                if (canvas != null && root.name == "Canvas") return canvas;
            }
            throw new System.InvalidOperationException("[Sprint13Setup] Canvas not found");
        }

        /// <summary>Opaque full-area screen between the header and the tab bar.</summary>
        private static void PrepareScreen(GameObject screen, bool opaque)
        {
            StripVisuals(screen);
            var rt = Rt(screen);
            Stretch(rt, 0f, BOTTOM_BAR_HEIGHT, 0f, TOP_BAR_HEIGHT);
            if (opaque)
            {
                var img = EnsureImage(screen, UiSprite.Solid, UiColor.Background, 1f, true);
                screen.AddComponent<ThemedGraphic>().Configure(UiColor.Background, 1f, UiSprite.Solid);
                img.type = Image.Type.Sliced;
            }
            Transition(screen, new Vector2(0f, -36f));
        }

        private static TextMeshProUGUI ScreenTitle(Transform parent, string text, float y = 28f)
        {
            var title = Txt(parent, "Title", text, UiTextStyle.H1, UiColor.TextPrimary, TextAlignmentOptions.Center);
            Band(Rt(title), true, y, 80f, 40f, 40f);
            return title;
        }

        // ------------------------------------------------------------------ header

        private static GameObject BuildTopBar(GameObject bar, MainMenuUI menu, out CurrencyLabel coins, out CurrencyLabel shards, out TextMeshProUGUI coinsText)
        {
            StripVisuals(bar);
            Band(Rt(bar), true, 0f, TOP_BAR_HEIGHT);
            EnsureImage(bar, UiSprite.Solid, UiColor.SurfaceSunken, 1f, true);
            bar.AddComponent<ThemedGraphic>().Configure(UiColor.SurfaceSunken, 1f, UiSprite.Solid);
            var line = Img(bar.transform, "BottomLine", UiSprite.Solid, UiColor.Outline, 0.6f);
            Band(Rt(line), false, 0f, 3f);

            var settings = Btn(bar.transform, "SettingsButton", null, ButtonStyle.Icon, Theme.iconGear, 60f);
            Place(Rt(settings), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(104f, 104f));
            OnClick(settings.Button, menu.OnSettingsClicked);

            var right = Go("Currencies", bar.transform);
            var rrt = Rt(right);
            rrt.anchorMin = new Vector2(1f, 0.5f);
            rrt.anchorMax = new Vector2(1f, 0.5f);
            rrt.pivot = new Vector2(1f, 0.5f);
            rrt.anchoredPosition = new Vector2(-28f, 0f);
            rrt.sizeDelta = new Vector2(620f, 88f);
            HL(right, 16f, Pad(0), TextAnchor.MiddleRight, false, false, false, false);
            shards = Pill(right.transform, "ShardsPill", CurrencyType.Shards, 210f, 84f, out _);
            coins = Pill(right.transform, "CoinsPill", CurrencyType.Coins, 300f, 84f, out coinsText);
            return bar;
        }

        // ------------------------------------------------------------------ bottom tabs

        private static TabBarUI BuildBottomBar(GameObject bar, MainMenuUI menu, out BottomTabUI[] tabs)
        {
            // Keep the 5 tab buttons (and their names); rebuild their visuals.
            var tabNames = new[] { "UpgradesButton", "AbilitiesButton", "PlayButton", "AchievementsButton", "BonusButton" };
            var labels = new[] { "Upgrades", "Abilities", "Play", "Awards", "Bonus" };
            var icons = new[] { Theme.tabUpgrades, Theme.tabAbilities, Theme.tabPlay, Theme.tabAchievements, Theme.tabBonus };
            UnityEngine.Events.UnityAction[] actions =
            {
                menu.OnUpgradesClicked, menu.OnAbilitiesClicked, menu.OnPlayClicked, menu.OnAchievementsClicked, menu.OnBonusClicked
            };

            var existing = new GameObject[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                Transform t = bar.transform.Find(tabNames[i]);
                existing[i] = t != null ? t.gameObject : null;
                if (t != null) t.SetParent(null, false);
            }
            StripVisuals(bar);
            Band(Rt(bar), false, 0f, BOTTOM_BAR_HEIGHT);
            var bg = EnsureImage(bar, UiSprite.Solid, UiColor.SurfaceSunken, 1f, true);
            var line = Img(bar.transform, "TopLine", UiSprite.Solid, UiColor.Outline, 0.6f);
            Band(Rt(line), true, 0f, 3f);
            LE(line.gameObject, ignore: true);
            HL(bar, 0f, Pad(8, 8, 14, 18), TextAnchor.MiddleCenter, true, true, false, true);

            tabs = new BottomTabUI[tabNames.Length];
            var badges = new BadgeUI[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                GameObject tab = existing[i] ?? Go(tabNames[i], bar.transform, typeof(Image), typeof(Button));
                tab.transform.SetParent(bar.transform, false);
                foreach (var c in tab.GetComponents<ThemedButton>()) Object.DestroyImmediate(c);
                ClearChildren(tab.transform);
                var hit = EnsureImage(tab, UiSprite.Solid, UiColor.White, 0f, true);
                var button = GetOrAdd<Button>(tab);
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
                OnClick(button, actions[i]);

                var plate = Img(tab.transform, "Background", UiSprite.Card, UiColor.Accent, 1f, false, false);
                Stretch(Rt(plate), 6f, 0f, 6f, 0f);
                var icon = Icon(tab.transform, "Icon", icons[i], 112f);
                Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(112f, 112f));
                var label = Txt(tab.transform, "Label", labels[i], UiTextStyle.Caption, UiColor.TextPrimary, TextAlignmentOptions.Center, false);
                Band(Rt(label), false, 10f, 34f, 4f, 4f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;

                var badgeImg = Img(tab.transform, "Badge", UiSprite.Circle, UiColor.Danger, 1f, false, false);
                Place(Rt(badgeImg), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(46f, -26f), new Vector2(48f, 48f));
                var badgeText = Txt(badgeImg.transform, "Count", "1", UiTextStyle.Micro, UiColor.White, TextAlignmentOptions.Center, false, 1.1f);
                Stretch(Rt(badgeText), 0f, 2f, 0f, 0f);
                var badge = badgeImg.gameObject.AddComponent<BadgeUI>();
                badge.EditorSetup(badgeImg, badgeText, UiColor.Danger);
                badgeImg.gameObject.SetActive(false);
                badges[i] = badge;

                var tabUi = GetOrAdd<BottomTabUI>(tab);
                tabUi.iconRect = Rt(icon);
                tabUi.iconImage = icon;
                tabUi.labelText = label;
                tabUi.bgImage = plate;
                tabUi.button = button;
                tabUi.layoutElement = LE(tab, flexW: 1f, prefW: i == MainMenuUI.TAB_PLAY ? 260f : 190f);
                tabUi.activeWidth = 260f;
                tabUi.inactiveWidth = 190f;
                tabUi.activeIconY = 20f;
                tabUi.inactiveIconY = 0f;
                tabUi.activeIconScale = 1.12f;
                tabUi.inactiveIconScale = 0.92f;
                tabUi.animationDuration = 0.2f;
                EditorUtility.SetDirty(tabUi);
                tabs[i] = tabUi;
            }

            var tabBar = GetOrAdd<TabBarUI>(bar);
            tabBar.EditorSetup(tabs, badges, bg);
            return tabBar;
        }

        // ------------------------------------------------------------------ Play

        private static void BuildPlayScreen(GameObject screen, MainMenuUI menu)
        {
            var ui = screen.GetComponent<HeroSelectionUI>();
            PrepareScreen(screen, opaque: false);

            // Name plate
            var plate = Img(screen.transform, "NamePlate", UiSprite.Panel, UiColor.Surface, 0.88f);
            Place(Rt(plate), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(640f, 156f));
            var name = Txt(plate.transform, "HeroNameText", "KNIGHT", UiTextStyle.H1, UiColor.TextPrimary, TextAlignmentOptions.Center);
            Band(Rt(name), true, 14f, 78f, 20f, 20f);
            var role = Txt(plate.transform, "RoleText", "TANK", UiTextStyle.Caption, UiColor.Gold, TextAlignmentOptions.Center, false);
            Band(Rt(role), true, 92f, 36f, 20f, 20f);
            var status = Txt(screen.transform, "HeroStatusText", "READY FOR BATTLE", UiTextStyle.Caption, UiColor.Success, TextAlignmentOptions.Center, false);
            Band(Rt(status), true, 196f, 40f, 60f, 60f);

            var dots = Go("Pagination", screen.transform);
            Band(Rt(dots), true, 244f, 30f, 200f, 200f);
            HL(dots, 18f, Pad(0), TextAnchor.MiddleCenter, false, false, false, false);
            var dot = Img(dots.transform, "Dot", UiSprite.Circle, UiColor.TextSecondary, 1f, false, false);
            Rt(dot).sizeDelta = new Vector2(18f, 18f);

            var prev = Btn(screen.transform, "PrevHeroButton", null, ButtonStyle.Icon, Theme.iconChevronLeft, 60f);
            Place(Rt(prev), new Vector2(0f, 0.62f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(112f, 112f));
            var next = Btn(screen.transform, "NextHeroButton", null, ButtonStyle.Icon, Theme.iconChevronRight, 60f);
            Place(Rt(next), new Vector2(1f, 0.62f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(112f, 112f));

            var lockOverlay = Img(screen.transform, "LockOverlay", UiSprite.Panel, UiColor.SurfaceSunken, 0.9f);
            Place(Rt(lockOverlay), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 230f));
            var lockIcon = Icon(lockOverlay.transform, "LockIcon", Theme.iconLock, 96f, Theme.warning);
            Place(Rt(lockIcon), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(96f, 96f));
            var lockText = Txt(lockOverlay.transform, "Requirement", "Reach wave 25", UiTextStyle.Body, UiColor.Warning, TextAlignmentOptions.Center, false);
            Band(Rt(lockText), false, 24f, 84f, 24f, 24f);

            // Info card
            var card = Img(screen.transform, "HeroInfoCard", UiSprite.Panel, UiColor.Surface, 0.94f);
            var crt = Rt(card);
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 214f);
            crt.sizeDelta = new Vector2(-64f, 0f);
            VL(card.gameObject, 8f, Pad(26, 26, 22, 30), TextAnchor.UpperCenter, true, true, true, false);
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var stats = Go("StatChips", card.transform);
            HL(stats, 16f, Pad(0, 0, 0, 6), TextAnchor.MiddleCenter, true, true, true, false);
            LE(stats, prefH: 76f);
            var hp = StatPill(stats.transform, "HpChip", Theme.iconHeart, "HP");
            var speed = StatPill(stats.transform, "SpeedChip", Theme.iconSpeed, "SPEED");

            var passive = InfoRow(card.transform, "PassiveRow", null, "PASSIVE", out Image passiveIcon, out TextMeshProUGUI passiveName, out Button passiveBtn);
            var passiveDesc = RowDescription(card.transform, "PassiveText");
            var weapon = InfoRow(card.transform, "WeaponRow", Theme.iconSword, "WEAPON", out _, out TextMeshProUGUI weaponName, out Button weaponBtn);
            var weaponDesc = RowDescription(card.transform, "WeaponText");
            var ult = InfoRow(card.transform, "UltimateRow", null, "ULTIMATE", out Image ultIcon, out TextMeshProUGUI ultName, out Button ultBtn);
            var ultDesc = RowDescription(card.transform, "UltimateText");

            var start = Btn(screen.transform, "StartButton", "START", ButtonStyle.Primary, null, 0f, UiTextStyle.Display, 0.72f, ButtonSound.None);
            Place(Rt(start), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(640f, 150f));
            OnClick(start.Button, menu.StartGame);
            var pulse = start.gameObject.AddComponent<PulseUI>();
            pulse.EditorSetup(0.035f, 3.2f);

            // 14.4 Landscape (AUDIT 4.6.Г): hero preview in the left half, stats + START in the right half.
            Landscape(Rt(plate), L(new Vector2(0.25f, 1f), new Vector2(0.25f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(600f, 150f)));
            Landscape(Rt(status), L(new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(-80f, 40f)));
            Landscape(Rt(dots), L(new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(-400f, 30f)));
            Landscape(Rt(prev), L(new Vector2(0f, 0.42f), new Vector2(0f, 0.42f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(104f, 104f)));
            Landscape(Rt(next), L(new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(104f, 104f)));
            Landscape(Rt(lockOverlay), L(new Vector2(0.25f, 0.42f), new Vector2(0.25f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 220f)));
            Landscape(crt, L(new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-12f, -24f), new Vector2(-72f, 0f)));
            Landscape(Rt(start), L(new Vector2(0.75f, 0f), new Vector2(0.75f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(600f, 132f)));

            Set(ui, "heroNameText", name);
            Set(ui, "heroStatusText", status);
            Set(ui, "passiveText", passiveDesc);
            Set(ui, "ultimateText", null);
            Set(ui, "statsText", null);
            Set(ui, "prevHeroButton", prev.Button);
            Set(ui, "nextHeroButton", next.Button);
            Set(ui, "startRunButton", start.Button);
            Set(ui, "startButtonText", start.Label);
            Set(ui, "roleText", role);
            Set(ui, "paginationContainer", dots.transform);
            Set(ui, "paginationDotTemplate", dot);
            Set(ui, "lockOverlay", lockOverlay.gameObject);
            Set(ui, "lockRequirementText", lockText);
            Set(ui, "hpValueText", hp);
            Set(ui, "speedValueText", speed);
            Set(ui, "passiveIcon", passiveIcon);
            Set(ui, "passiveNameText", passiveName);
            Set(ui, "weaponNameText", weaponName);
            Set(ui, "weaponDescriptionText", weaponDesc);
            Set(ui, "ultimateIcon", ultIcon);
            Set(ui, "ultimateNameText", ultName);
            Set(ui, "ultimateDescriptionText", ultDesc);
            Set(ui, "passiveRowButton", passiveBtn);
            Set(ui, "weaponRowButton", weaponBtn);
            Set(ui, "ultimateRowButton", ultBtn);
            Set(ui, "startPulse", pulse);
        }

        private static RectPreset L(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            return new RectPreset { captured = true, anchorMin = anchorMin, anchorMax = anchorMax, pivot = pivot, anchoredPosition = position, sizeDelta = size };
        }

        /// <summary>Keeps the current (portrait) layout and adds a landscape preset via <see cref="ResponsiveRect"/>.</summary>
        private static void Landscape(RectTransform rt, RectPreset landscape)
        {
            var presets = GetOrAdd<ResponsiveRect>(rt.gameObject);
            presets.SetPreset(ProjectB.Data.Platform.LayoutMode.Portrait, RectPreset.Capture(rt));
            presets.SetPreset(ProjectB.Data.Platform.LayoutMode.Landscape, landscape);
        }

        private static TextMeshProUGUI StatPill(Transform parent, string name, Sprite icon, string caption)
        {
            var pill = Img(parent, name, UiSprite.Pill, UiColor.SurfaceSunken);
            HL(pill.gameObject, 10f, Pad(16, 24, 6, 6), TextAnchor.MiddleCenter, true, true, false, false);
            LE(pill.gameObject, flexW: 1f, prefH: 72f);
            var ic = Icon(pill.transform, "Icon", icon, 48f);
            LE(ic.gameObject, prefW: 48f, prefH: 48f);
            var cap = Txt(pill.transform, "Caption", caption, UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft);
            cap.textWrappingMode = TextWrappingModes.NoWrap;
            var value = Txt(pill.transform, "Value", "100", UiTextStyle.Number, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true, 0.8f);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Overflow;
            return value;
        }

        private static GameObject InfoRow(Transform parent, string name, Sprite icon, string caption, out Image iconImage, out TextMeshProUGUI nameText, out Button button)
        {
            var row = Img(parent, name, UiSprite.Row, UiColor.SurfaceRaised, 1f, true);
            button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = row;
            HL(row.gameObject, 16f, Pad(14, 18, 8, 14), TextAnchor.MiddleLeft, true, true, false, false);
            LE(row.gameObject, prefH: 84f);
            iconImage = Icon(row.transform, "Icon", icon, 60f);
            LE(iconImage.gameObject, prefW: 60f, prefH: 60f);
            var cap = Txt(row.transform, "Caption", caption, UiTextStyle.Micro, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft);
            cap.textWrappingMode = TextWrappingModes.NoWrap;
            LE(cap.gameObject, prefW: 128f);
            nameText = Txt(row.transform, "Name", "Name", UiTextStyle.Body, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.GetComponent<ThemedText>().Configure(UiTextStyle.Caption, 1.3f);
            LE(nameText.gameObject, flexW: 1f);
            var chevron = Icon(row.transform, "Chevron", Theme.iconChevronRight, 32f, Theme.textSecondary);
            chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            LE(chevron.gameObject, prefW: 32f, prefH: 32f);
            return row.gameObject;
        }

        private static TextMeshProUGUI RowDescription(Transform parent, string name)
        {
            var text = Txt(parent, name, "Description", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.TopLeft, true, 1.1f);
            text.margin = new Vector4(18f, 2f, 18f, 6f);
            text.gameObject.SetActive(false);
            return text;
        }

        // ------------------------------------------------------------------ Upgrades

        private static void BuildUpgradesScreen(GameObject screen, MainMenuUI menu)
        {
            var ui = screen.GetComponent<MetaUpgradeUI>();
            var oldContent = AssetDatabase.LoadAssetAtPath<UpgradeItemUI>(UPGRADE_ITEM_PREFAB);
            PrepareScreen(screen, opaque: true);
            ScreenTitle(screen.transform, "UPGRADES");

            // Constellation banner
            var banner = Btn(screen.transform, "ConstellationBanner", null, ButtonStyle.Secondary, null);
            banner.EditorSetup(ButtonStyle.Secondary, banner.Face, null, null, ButtonSound.Click, true);
            banner.Face.sprite = Theme.panelSprite;
            Band(Rt(banner), true, 128f, 196f, 40f, 40f);
            var stars = Img(banner.transform, "Starfield", UiSprite.Starfield, UiColor.White, 0.7f, false, false);
            Stretch(Rt(stars), 12f, 18f, 12f, 8f);
            stars.type = Image.Type.Tiled;
            var starIcon = Icon(banner.transform, "StarIcon", Theme.iconStar, 104f);
            Place(Rt(starIcon), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 6f), new Vector2(104f, 104f));
            var bTitle = Txt(banner.transform, "Title", "CONSTELLATIONS", UiTextStyle.H2, UiColor.Gold, TextAlignmentOptions.MidlineLeft);
            Place(Rt(bTitle), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(160f, 8f), new Vector2(560f, 60f));
            var bProgress = Txt(banner.transform, "Progress", "0/33 stars lit", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.MidlineLeft);
            Place(Rt(bProgress), new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(160f, 0f), new Vector2(560f, 40f));
            var shardsPill = Img(banner.transform, "ShardsPill", UiSprite.Pill, UiColor.SurfaceSunken, 0.9f);
            Place(Rt(shardsPill), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-84f, 6f), new Vector2(170f, 76f));
            HL(shardsPill.gameObject, 8f, Pad(12, 18, 4, 4), TextAnchor.MiddleCenter, true, true, false, false);
            var shardIcon = Icon(shardsPill.transform, "Icon", Theme.iconShard, 52f);
            LE(shardIcon.gameObject, prefW: 52f, prefH: 52f);
            var shardText = Txt(shardsPill.transform, "Amount", "0", UiTextStyle.Number, UiColor.Shard, TextAlignmentOptions.MidlineLeft, true, 0.8f);
            shardText.textWrappingMode = TextWrappingModes.NoWrap;
            var chevron = Icon(banner.transform, "Chevron", Theme.iconChevronRight, 48f);
            Place(Rt(chevron), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 6f), new Vector2(48f, 48f));
            OnClick(banner.Button, menu.OnConstellationClicked);

            Transform content = ScrollList(screen.transform, "ScrollView", 18f, Pad(40, 40, 8, 40), out ScrollRect scroll);
            Stretch(Rt(scroll), 0f, 0f, 0f, 340f);

            Set(ui, "panel", screen);
            Set(ui, "itemsContainer", content);
            Set(ui, "itemPrefab", oldContent);
            Set(ui, "constellationBanner", banner.Button);
            Set(ui, "bannerShardsText", shardText);
            Set(ui, "bannerProgressText", bProgress);
        }

        // ------------------------------------------------------------------ Abilities

        private static void BuildAbilitiesScreen(GameObject screen)
        {
            var ui = screen.GetComponent<AbilitiesScreenUI>();
            PrepareScreen(screen, opaque: true);
            var title = ScreenTitle(screen.transform, "ABILITIES");

            var chips = Go("HeaderChips", screen.transform);
            Band(Rt(chips), true, 124f, 64f, 40f, 40f);
            HL(chips, 12f, Pad(0), TextAnchor.MiddleCenter, true, true, false, true);
            var collected = HeaderChip(chips.transform, "CollectedChip", Theme.iconStar, "16/27");
            var synergies = HeaderChip(chips.transform, "SynergiesChip", Theme.iconShard, "Synergies 12");
            var slots = HeaderChip(chips.transform, "SlotsChip", Theme.iconPlus, "Slots 3/6 · 3/6");

            var filter = Img(screen.transform, "Filter", UiSprite.Pill, UiColor.SurfaceSunken);
            Band(Rt(filter), true, 208f, 92f, 40f, 40f);
            HL(filter.gameObject, 6f, Pad(8, 8, 8, 8), TextAnchor.MiddleCenter, true, true, true, true);
            string[] names = { "Active", "Passive", "Synergies" };
            var buttons = new Button[3];
            var backgrounds = new Image[3];
            var labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var seg = Img(filter.transform, $"Filter{names[i]}", UiSprite.Pill, UiColor.Accent, 1f, true, false);
                var btn = seg.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = seg;
                var label = Txt(seg.transform, "Label", names[i], UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 1.2f);
                Stretch(Rt(label));
                buttons[i] = btn;
                backgrounds[i] = seg;
                labels[i] = label;
            }

            Transform content = ScrollList(screen.transform, "ScrollView", 16f, Pad(40, 40, 8, 40), out ScrollRect scroll);
            Stretch(Rt(scroll), 0f, 0f, 0f, 320f);

            Set(ui, "contentContainer", content);
            Set(ui, "headerText", title);
            Set(ui, "itemPrefab", AssetDatabase.LoadAssetAtPath<AbilityCollectionItemUI>(ABILITY_ITEM_PREFAB));
            Set(ui, "collectedChipText", collected);
            Set(ui, "synergiesChipText", synergies);
            Set(ui, "slotsChipText", slots);
            SetArray(ui, "filterButtons", buttons);
            SetArray(ui, "filterBackgrounds", backgrounds);
            SetArray(ui, "filterLabels", labels);
            Set(ui, "scrollRect", scroll);
        }

        private static TextMeshProUGUI HeaderChip(Transform parent, string name, Sprite icon, string text)
        {
            var chip = Img(parent, name, UiSprite.Pill, UiColor.SurfaceRaised);
            HL(chip.gameObject, 8f, Pad(14, 22, 4, 4), TextAnchor.MiddleCenter, true, true, false, false);
            var ic = Icon(chip.transform, "Icon", icon, 40f);
            LE(ic.gameObject, prefW: 40f, prefH: 40f);
            var t = Txt(chip.transform, "Text", text, UiTextStyle.Caption, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        // ------------------------------------------------------------------ Achievements

        private static void BuildAchievementsScreen(GameObject screen)
        {
            var ui = screen.GetComponent<AchievementScreenUI>();
            Transform popupT = screen.transform.Find("RewardPopup");
            GameObject popup = popupT != null ? popupT.gameObject : null;
            if (popup != null) popup.transform.SetParent(null, false);

            PrepareScreen(screen, opaque: true);
            ScreenTitle(screen.transform, "ACHIEVEMENTS");
            var summary = Txt(screen.transform, "Summary", "0 / 27 completed", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.Center);
            Band(Rt(summary), true, 112f, 40f, 40f, 40f);

            Transform content = ScrollList(screen.transform, "ScrollView", 16f, Pad(40, 40, 8, 40), out ScrollRect scroll);
            Stretch(Rt(scroll), 0f, 0f, 0f, 168f);

            if (popup == null) popup = Go("RewardPopup", screen.transform, typeof(AchievementRewardPopupUI));
            popup.transform.SetParent(screen.transform, false);
            var popupUi = popup.GetComponent<AchievementRewardPopupUI>();
            BuildRewardPopup(popup, popupUi);

            Set(ui, "contentContainer", content);
            Set(ui, "itemPrefab", AssetDatabase.LoadAssetAtPath<AchievementItemUI>(ACHIEVEMENT_ITEM_PREFAB));
            Set(ui, "rewardPopup", popupUi);
            Set(ui, "abilityCatalog", AssetDatabase.LoadAssetAtPath<AbilityCatalog>(ABILITY_CATALOG_PATH));
            Set(ui, "summaryText", summary);
        }

        private static void BuildRewardPopup(GameObject popup, AchievementRewardPopupUI ui)
        {
            StripVisuals(popup);
            foreach (var img in popup.GetComponents<Image>()) Object.DestroyImmediate(img);
            Stretch(Rt(popup));
            var dim = Img(popup.transform, "BackgroundDim", UiSprite.Solid, UiColor.Overlay, 1f, true);
            Stretch(Rt(dim));
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(dimButton.onClick, ui.Hide);

            var window = Img(popup.transform, "Window", UiSprite.Panel, UiColor.SurfaceRaised);
            Place(Rt(window), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 640f));
            var frame = Img(window.transform, "Frame", UiSprite.Outline, UiColor.Gold, 1f, false, true);
            Stretch(Rt(frame), 0f, 10f, 0f, 0f);
            var glow = Img(window.transform, "Glow", UiSprite.Glow, UiColor.Gold, 0.45f);
            Place(Rt(glow), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(360f, 360f));
            var title = Txt(window.transform, "Title", "Reward Unlocked!", UiTextStyle.H1, UiColor.Gold, TextAlignmentOptions.Center);
            Band(Rt(title), true, 34f, 80f, 30f, 30f);
            var icon = Icon(window.transform, "Icon", null, 200f);
            Place(Rt(icon), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(200f, 200f));
            var desc = Txt(window.transform, "Description", "Reward", UiTextStyle.Body, UiColor.TextPrimary, TextAlignmentOptions.Center);
            Band(Rt(desc), true, 380f, 110f, 40f, 40f);
            var close = Btn(window.transform, "CloseButton", "OK", ButtonStyle.Primary);
            Place(Rt(close), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(400f, 120f));

            Set(ui, "popupPanel", popup);
            Set(ui, "rewardIconImage", icon);
            Set(ui, "titleText", title);
            Set(ui, "descriptionText", desc);
            Set(ui, "closeButton", close.Button);
            Set(ui, "window", Rt(window));
            popup.SetActive(false);
        }

        // ------------------------------------------------------------------ Daily bonus

        private static void BuildDailyBonusScreen(GameObject screen)
        {
            var ui = screen.GetComponent<DailyBonusScreenUI>();
            // Keep the DailyBonusItemUI components (and their names) — rebuild their visuals.
            var items = screen.GetComponentsInChildren<DailyBonusItemUI>(true);
            System.Array.Sort(items, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var item in items) item.transform.SetParent(null, false);

            PrepareScreen(screen, opaque: true);
            ScreenTitle(screen.transform, "DAILY BONUS", 36f);
            var cycle = Txt(screen.transform, "CycleText", "Cycle 1 · ×1", UiTextStyle.Body, UiColor.Gold, TextAlignmentOptions.Center);
            Band(Rt(cycle), true, 124f, 48f, 40f, 40f);
            var timer = Txt(screen.transform, "TimerText", "Next reward in 12h 00m 00s", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.Center);
            Band(Rt(timer), true, 172f, 40f, 40f, 40f);

            var days = Go("DaysContainer", screen.transform);
            Band(Rt(days), true, 236f, 780f, 36f, 36f);
            VL(days, 20f, Pad(0), TextAnchor.UpperCenter, true, true, true, true);
            var row1 = Go("Row1", days.transform);
            HL(row1, 16f, Pad(0), TextAnchor.MiddleCenter, true, true, true, true);
            var row2 = Go("Row2", days.transform);
            HL(row2, 16f, Pad(0), TextAnchor.MiddleCenter, true, true, true, true);

            for (int i = 0; i < items.Length; i++)
            {
                DailyBonusItemUI item = items[i];
                item.transform.SetParent(i < 4 ? row1.transform : row2.transform, false);
                BuildDayCell(item.gameObject, item, i + 1);
            }

            var claim = Btn(screen.transform, "ClaimButton", "CLAIM", ButtonStyle.Primary, null, 0f, UiTextStyle.Button, 1.2f, ButtonSound.None);
            Place(Rt(claim), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(720f, 140f));
            var doubleBtn = Btn(screen.transform, "ClaimDoubleButton", "<sprite name=\"ad\"> CLAIM ×2", ButtonStyle.Ad, null, 0f, UiTextStyle.Button, 1f, ButtonSound.None);
            Place(Rt(doubleBtn), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(720f, 124f));
            doubleBtn.Label.text = "<sprite name=\"ad\"> CLAIM ×2";
            var allClaimed = Txt(screen.transform, "AllClaimedMessage", "All set for today — come back tomorrow!", UiTextStyle.Body, UiColor.TextSecondary, TextAlignmentOptions.Center);
            Band(Rt(allClaimed), false, 120f, 100f, 60f, 60f);
            allClaimed.gameObject.SetActive(false);

            SetArray(ui, "dayItems", items);
            Set(ui, "claimButton", claim.Button);
            Set(ui, "claimDoubleButton", doubleBtn.Button);
            Set(ui, "allClaimedMessage", allClaimed.gameObject);
            Set(ui, "cycleText", cycle);
            Set(ui, "timerText", timer);
        }

        private static void BuildDayCell(GameObject cell, DailyBonusItemUI ui, int day)
        {
            StripVisuals(cell);
            var bg = EnsureImage(cell, UiSprite.Card, UiColor.Surface);
            var group = Group(cell);
            LE(cell, prefH: 320f, flexW: day == 7 ? 2.1f : 1f, flexH: 1f);

            var highlight = Img(cell.transform, "CurrentDayHighlight", UiSprite.Outline, UiColor.Gold, 1f, false, false);
            Stretch(Rt(highlight), -6f, -2f, -6f, -6f);
            var glow = Img(highlight.transform, "Glow", UiSprite.Glow, UiColor.Gold, 0.5f, false, false);
            Stretch(Rt(glow), -40f, -40f, -40f, -40f);
            glow.transform.SetAsFirstSibling();
            var pulse = highlight.gameObject.AddComponent<PulseUI>();
            pulse.EditorSetup(0.02f, 3.6f, glow);

            var dayText = Txt(cell.transform, "DayText", $"Day {day}", UiTextStyle.Caption, UiColor.TextSecondary, TextAlignmentOptions.Center, false, 1.1f);
            Band(Rt(dayText), true, 14f, 40f, 6f, 6f);
            float iconSize = day == 7 ? 170f : 120f;
            var icon = Icon(cell.transform, "Icon", null, iconSize);
            Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(iconSize, iconSize));
            var amount = Txt(cell.transform, "AmountText", "100", UiTextStyle.Caption, UiColor.Gold, TextAlignmentOptions.Center, true, 1.2f);
            Band(Rt(amount), false, 22f, 46f, 4f, 4f);
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            amount.overflowMode = TextOverflowModes.Overflow;

            var claimed = Img(cell.transform, "ClaimedOverlay", UiSprite.Card, UiColor.SurfaceSunken, 0.55f, false, false);
            Stretch(Rt(claimed));
            var check = Icon(claimed.transform, "Check", Theme.iconCheck, 96f, Theme.success);
            Place(Rt(check), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(96f, 96f));
            claimed.gameObject.SetActive(false);
            highlight.gameObject.SetActive(false);

            Set(ui, "dayText", dayText);
            Set(ui, "iconImage", icon);
            Set(ui, "amountText", amount);
            Set(ui, "claimedOverlay", claimed.gameObject);
            Set(ui, "currentDayHighlight", highlight.gameObject);
            Set(ui, "background", bg);
            Set(ui, "canvasGroup", group);
            Set(ui, "todayPulse", pulse);
        }

        // ------------------------------------------------------------------ Constellation

        private static void RestyleConstellation(GameObject screen)
        {
            var ui = screen.GetComponent<ConstellationTreeUI>();
            Transform panel = FindRequired(screen.transform, "ConstellationPanel");
            Stretch(Rt(screen));
            Stretch(Rt(panel), 0f, BOTTOM_BAR_HEIGHT, 0f, TOP_BAR_HEIGHT);
            var panelImg = panel.GetComponent<Image>();
            if (panelImg != null) panelImg.color = Theme.background;
            Transition(panel.gameObject, new Vector2(0f, -24f));

            Transform viewport = FindRequired(panel, "Viewport");
            var vrt = Rt(viewport);
            Stretch(vrt, 0f, 0f, 0f, 120f);
            var vImg = viewport.GetComponent<Image>();
            if (vImg != null) vImg.color = UiTheme.Hex(0x0C0B1C);

            // Compact header: title + progress (currencies live in the shared top bar).
            Transform header = FindRequired(panel, "Header");
            Band(Rt(header), true, 0f, 120f);
            var hImg = header.GetComponent<Image>();
            if (hImg != null)
            {
                hImg.sprite = Theme.solidSprite;
                hImg.color = UiTheme.WithAlpha(Theme.surface, 0.96f);
            }
            var title = FindRequired(header, "Title").GetComponent<TextMeshProUGUI>();
            StyleText(title, UiTextStyle.H2, UiColor.Gold);
            Place(Rt(title), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -14f), new Vector2(640f, 60f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var progress = FindRequired(header, "ProgressText").GetComponent<TextMeshProUGUI>();
            StyleText(progress, UiTextStyle.Caption, UiColor.TextSecondary);
            Place(Rt(progress), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -74f), new Vector2(640f, 36f));
            progress.alignment = TextAlignmentOptions.MidlineLeft;
            Transform shardsText = header.Find("ShardsText");
            if (shardsText != null) shardsText.gameObject.SetActive(false);
            Set(ui, "shardsText", null);

            ReplaceWithIconButton(FindRequired(header, "BackButton").gameObject, null, "UPGRADES", ButtonStyle.Secondary,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(270f, 92f), Theme.iconChevronLeft);
            ReplaceWithIconButton(FindRequired(panel, "ZoomInButton").gameObject, Theme.iconPlus, null, ButtonStyle.Icon,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 290f), new Vector2(108f, 108f));
            ReplaceWithIconButton(FindRequired(panel, "ZoomOutButton").gameObject, Theme.iconMinus, null, ButtonStyle.Icon,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 166f), new Vector2(108f, 108f));
            ReplaceWithIconButton(FindRequired(panel, "CenterButton").gameObject, Theme.iconTarget, null, ButtonStyle.Icon,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 42f), new Vector2(108f, 108f));

            Transform sheet = FindRequired(panel, "DetailSheet");
            var sheetImg = sheet.GetComponent<Image>();
            if (sheetImg != null)
            {
                sheetImg.sprite = Theme.panelSprite;
                sheetImg.type = Image.Type.Sliced;
                sheetImg.color = Theme.surfaceRaised;
            }
            StyleText(FindRequired(sheet, "Title").GetComponent<TextMeshProUGUI>(), UiTextStyle.H2, UiColor.TextPrimary);
            StyleText(FindRequired(sheet, "Type").GetComponent<TextMeshProUGUI>(), UiTextStyle.Caption, UiColor.Shard);
            StyleText(FindRequired(sheet, "Description").GetComponent<TextMeshProUGUI>(), UiTextStyle.Caption, UiColor.TextSecondary);
            StyleText(FindRequired(sheet, "Status").GetComponent<TextMeshProUGUI>(), UiTextStyle.Caption, UiColor.Warning);
            RestyleLabeledButton(FindRequired(sheet, "UnlockButton").gameObject, ButtonStyle.Primary);
            RestyleLabeledButton(FindRequired(sheet, "UnlockCoinsButton").gameObject, ButtonStyle.Success);
            ReplaceWithIconButton(FindRequired(sheet, "CloseButton").gameObject, Theme.iconClose, null, ButtonStyle.Icon,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(84f, 84f));

            Set(ui, "font", Theme.bodyBoldFont != null ? Theme.bodyBoldFont : Theme.bodyFont);
        }

        private static void StyleText(TMP_Text text, UiTextStyle style, UiColor color)
        {
            if (text == null) return;
            var themed = GetOrAdd<ThemedText>(text);
            themed.Configure(style);
            var g = GetOrAdd<ThemedGraphic>(text);
            g.Configure(color);
            if (Theme.iconSpriteAsset != null) text.spriteAsset = Theme.iconSpriteAsset;
        }

        /// <summary>Turns an existing Button into a themed button (keeps the GameObject, its references and listeners).</summary>
        private static ThemedButton ReplaceWithIconButton(GameObject go, Sprite icon, string label, ButtonStyle style,
            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite labelIcon = null)
        {
            TMP_Text existingLabel = go.GetComponentInChildren<TMP_Text>(true);
            foreach (Transform child in go.transform)
            {
                if (existingLabel != null && child == existingLabel.transform) continue;
                Object.DestroyImmediate(child.gameObject);
            }
            Place(Rt(go), anchor, pivot, pos, size);
            var face = GetOrAdd<Image>(go);
            face.raycastTarget = true;

            Image iconImage = null;
            if (icon != null)
            {
                if (existingLabel != null) existingLabel.gameObject.SetActive(false);
                iconImage = Icon(go.transform, "Icon", icon, size.y * 0.52f);
                Place(Rt(iconImage), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(size.y * 0.52f, size.y * 0.52f));
            }
            else if (existingLabel != null)
            {
                existingLabel.gameObject.SetActive(true);
                if (!string.IsNullOrEmpty(label)) existingLabel.text = label;
                StyleText(existingLabel, UiTextStyle.Button, UiColor.White);
                existingLabel.GetComponent<ThemedText>().Configure(UiTextStyle.Button, 0.8f);
                Object.DestroyImmediate(existingLabel.GetComponent<ThemedGraphic>());
                Stretch(Rt(existingLabel), labelIcon != null ? 64f : 12f, 12f, 12f, 2f);
                existingLabel.textWrappingMode = TextWrappingModes.NoWrap;
                existingLabel.enableAutoSizing = true;
                existingLabel.fontSizeMin = 18f;
                existingLabel.fontSizeMax = Theme.buttonSize * 0.8f;
                if (labelIcon != null)
                {
                    var li = Icon(go.transform, "LabelIcon", labelIcon, 44f);
                    Place(Rt(li), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 5f), new Vector2(44f, 44f));
                    iconImage = li;
                }
            }

            var themed = GetOrAdd<ThemedButton>(go);
            themed.EditorSetup(style, face, existingLabel != null && existingLabel.gameObject.activeSelf ? existingLabel : null, iconImage);
            return themed;
        }

        private static void RestyleLabeledButton(GameObject go, ButtonStyle style)
        {
            var face = go.GetComponent<Image>();
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                StyleText(label, UiTextStyle.Button, UiColor.White);
                label.GetComponent<ThemedText>().Configure(UiTextStyle.Button, 0.7f);
                Object.DestroyImmediate(label.GetComponent<ThemedGraphic>());
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
            }
            var themed = GetOrAdd<ThemedButton>(go);
            themed.EditorSetup(style, face, label, null);
        }

        // ------------------------------------------------------------------ Settings

        private static void BuildSettingsPopup(GameObject root)
        {
            var ui = root.GetComponent<SettingsPopupUI>();
            StripVisuals(root);
            Stretch(Rt(root));

            var panel = Go("PopupPanel", root.transform);
            Stretch(Rt(panel));
            var dim = Img(panel.transform, "Dim", UiSprite.Solid, UiColor.Overlay, 1f, true);
            Stretch(Rt(dim));
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            Transition(panel, Vector2.zero);

            var window = Img(panel.transform, "Window", UiSprite.Panel, UiColor.Surface);
            Place(Rt(window), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 820f));
            var title = Txt(window.transform, "Title", "SETTINGS", UiTextStyle.H1, UiColor.TextPrimary);
            Band(Rt(title), true, 36f, 80f, 40f, 120f);
            var close = Btn(window.transform, "CloseButton", null, ButtonStyle.Icon, Theme.iconClose, 48f);
            Place(Rt(close), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(96f, 96f));

            var rows = Go("Rows", window.transform);
            Stretch(Rt(rows), 48f, 200f, 48f, 150f);
            VL(rows, 28f, Pad(0), TextAnchor.UpperCenter, true, true, true, false);
            var music = SettingsRow(rows.transform, "MusicVolumeRow", Theme.iconMusic, "Music", out TextMeshProUGUI musicValue);
            var musicSlider = Slider(music.transform, "Slider");
            LE(musicSlider.gameObject, prefH: 72f);
            var sfx = SettingsRow(rows.transform, "SfxVolumeRow", Theme.iconSound, "Sound", out TextMeshProUGUI sfxValue);
            var sfxSlider = Slider(sfx.transform, "Slider");
            LE(sfxSlider.gameObject, prefH: 72f);
            var lang = SettingsRow(rows.transform, "LanguageRow", Theme.iconGlobe, "Language", out TextMeshProUGUI langValue);
            langValue.text = "English";
            Txt(lang.transform, "Hint", "More languages soon", UiTextStyle.Caption, UiColor.TextDisabled, TextAlignmentOptions.MidlineLeft);

            var done = Btn(window.transform, "DoneButton", "DONE", ButtonStyle.Primary);
            Place(Rt(done), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(520f, 124f));
            UnityEventTools.AddPersistentListener(done.Button.onClick, ui.Hide);

            Set(ui, "popupPanel", panel);
            Set(ui, "musicSlider", musicSlider);
            Set(ui, "sfxSlider", sfxSlider);
            Set(ui, "musicValueText", musicValue);
            Set(ui, "sfxValueText", sfxValue);
            Set(ui, "closeButton", close.Button);
            Set(ui, "languageRow", lang);
            Set(ui, "backgroundButton", dimButton);
            panel.SetActive(false);
        }

        private static GameObject SettingsRow(Transform parent, string name, Sprite icon, string label, out TextMeshProUGUI value)
        {
            var row = Go(name, parent);
            VL(row, 8f, Pad(0), TextAnchor.UpperLeft, true, true, true, false);
            var head = Go("Header", row.transform);
            HL(head, 14f, Pad(0), TextAnchor.MiddleLeft, true, true, false, false);
            LE(head, prefH: 52f);
            var ic = Icon(head.transform, "Icon", icon, 48f, Theme.textSecondary);
            LE(ic.gameObject, prefW: 48f, prefH: 48f);
            var l = Txt(head.transform, "Label", label, UiTextStyle.Body, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft);
            LE(l.gameObject, flexW: 1f);
            value = Txt(head.transform, "Value", "100%", UiTextStyle.Number, UiColor.Gold, TextAlignmentOptions.MidlineRight, true, 0.8f);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            LE(value.gameObject, prefW: 200f);
            return row;
        }

        // ------------------------------------------------------------------ Achievement toast (both scenes)

        private static void BuildAchievementToast(GameObject root, float topOffset)
        {
            var ui = root.GetComponent<AchievementPopupUI>();
            StripVisuals(root);
            Stretch(Rt(root));

            var panel = Img(root.transform, "PopupPanel", UiSprite.Panel, UiColor.SurfaceRaised);
            Place(Rt(panel), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -topOffset), new Vector2(900f, 156f));
            var frame = Img(panel.transform, "Frame", UiSprite.Outline, UiColor.Gold, 1f, false, true);
            Stretch(Rt(frame), 0f, 10f, 0f, 0f);
            LE(frame.gameObject, ignore: true);
            HL(panel.gameObject, 20f, Pad(24, 28, 18, 28), TextAnchor.MiddleLeft, true, true, false, false);
            Transition(panel.gameObject, new Vector2(0f, 90f));
            var icon = Icon(panel.transform, "Icon", Theme.iconStar, 104f);
            LE(icon.gameObject, prefW: 104f, prefH: 104f);
            var texts = Go("Texts", panel.transform);
            VL(texts, 2f, Pad(0), TextAnchor.MiddleLeft, true, true, true, false);
            LE(texts, flexW: 1f);
            var subtitle = Txt(texts.transform, "Subtitle", "Achievement Unlocked!", UiTextStyle.Caption, UiColor.Gold, TextAlignmentOptions.MidlineLeft);
            var title = Txt(texts.transform, "Title", "Title", UiTextStyle.H2, UiColor.TextPrimary, TextAlignmentOptions.MidlineLeft, true, 0.8f);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            Set(ui, "popupPanel", panel.gameObject);
            Set(ui, "iconImage", icon);
            Set(ui, "titleText", title);
            Set(ui, "subtitleText", subtitle);
            panel.gameObject.SetActive(false);
        }
    }
}
