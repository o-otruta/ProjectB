using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Non-destructive editor wiring for Sprint 14.4 (Landscape layouts across MainMenu, HUD, Pause, Game Over,
    /// Settings, and HUD Canvas consolidation for &lt;= 10 draw calls). Keeps Portrait as the saved scene default
    /// and never invokes legacy Sprint*SetupTool scripts.
    /// </summary>
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint14LandscapeLayoutTool
    {
        public const string MAIN_MENU_SCENE = "Assets/_Scenes/MainMenu.unity";
        public const string GAMEPLAY_SCENE = "Assets/_Scenes/Gameplay.unity";
        public const string CAMERA_CONFIG_PATH = "Assets/Resources/CameraConfig.asset";

        public static void ApplyAll()
        {
            WireMainMenu();
            WireGameplay();
            UpdateCameraConfigMargins();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint14LandscapeLayoutTool] Sprint 14.4 landscape layouts and HUD canvas consolidation applied.");
        }

        private static void WireMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MAIN_MENU_SCENE, OpenSceneMode.Single);
            Canvas canvas = FindCanvas(scene);
            if (canvas == null)
            {
                Debug.LogError("[Sprint14LandscapeLayoutTool] MainMenu Canvas not found.");
                return;
            }

            // First reset scene to Portrait if it had any preview mode left active, before capturing any missing Portrait presets
            AdaptiveLayoutPreviewMenu.ApplyToOpenScenes(LayoutMode.Portrait);

            Transform safe = canvas.transform.Find("SafeAreaRoot");
            if (safe == null)
            {
                Debug.LogError("[Sprint14LandscapeLayoutTool] MainMenu SafeAreaRoot not found.");
                return;
            }

            // Re-frame 3D hero slightly to the right to compensate for the 200px left nav rail in Landscape
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.TryGetComponent<MenuPreviewFraming>(out var framing))
            {
                var soFraming = new SerializedObject(framing);
                soFraming.FindProperty("landscapeViewportPoint").vector2Value = new Vector2(0.32f, 0.45f);
                soFraming.FindProperty("landscapeDistance").floatValue = 5.4f;
                soFraming.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(framing);
            }

            // Top bar: slightly slimmer in Landscape (150 -> 120)
            SetLandscapePreset(safe.Find("TopBar"),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 120f));

            // BottomBar -> Left vertical rail in Landscape (200px wide below TopBar)
            Transform bottomBar = safe.Find("BottomBar");
            if (bottomBar != null)
            {
                ReplaceWithAdaptiveLayoutGroup(
                    bottomBar.gameObject,
                    portrait: new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 0f, childAlignment = TextAnchor.MiddleCenter },
                    landscape: new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 8f, childAlignment = TextAnchor.MiddleCenter },
                    padding: new RectOffset(8, 8, 12, 12),
                    ctrlW: true, ctrlH: true, expandW: true, expandH: true);

                SetLandscapePreset(bottomBar,
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(0f, -60f), new Vector2(200f, -120f));

                Transform topLine = bottomBar.Find("TopLine");
                if (topLine != null)
                {
                    if (!topLine.TryGetComponent<LayoutElement>(out var le)) le = topLine.gameObject.AddComponent<LayoutElement>();
                    le.ignoreLayout = true;
                    EditorUtility.SetDirty(le);

                    SetLandscapePreset(topLine,
                        new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                        Vector2.zero, new Vector2(3f, 0f));
                }
            }

            // FTUE Upgrades hint bubble moves beside left Upgrades rail button
            SetLandscapePreset(safe.Find("FtueUpgradesHint"),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(212f, -148f), new Vector2(380f, 68f));

            // Main tab panels occupy right of the 200px rail and below the 120px TopBar
            var landscapePanelPreset = Preset(
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(100f, -60f), new Vector2(-200f, -120f));

            SetLandscapePreset(safe.Find("PlayScreenPanel"), landscapePanelPreset);

            Transform upgrades = safe.Find("UpgradesPanel");
            if (upgrades != null)
            {
                SetLandscapePreset(upgrades, landscapePanelPreset);
                SetLandscapePreset(upgrades.Find("Title"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -16f), new Vector2(-80f, 68f));
                SetLandscapePreset(upgrades.Find("ConstellationBanner"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -94f), new Vector2(-80f, 148f));
                SetLandscapePreset(upgrades.Find("ScrollView"),
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -128f), new Vector2(0f, -256f));

                Transform content = upgrades.Find("ScrollView/Viewport/Content");
                if (content != null)
                {
                    ConfigureAdaptiveGrid(
                        content.gameObject,
                        portrait: new GridModeSettings { columns = 1, cellSize = new Vector2(1000f, 190f), spacing = new Vector2(20f, 18f) },
                        landscape: new GridModeSettings { columns = 2, cellSize = new Vector2(780f, 190f), spacing = new Vector2(24f, 18f) },
                        ultraWideColumns: 3,
                        padding: new RectOffset(40, 40, 14, 28));
                }
            }

            Transform abilities = safe.Find("AbilitiesPanel");
            if (abilities != null)
            {
                SetLandscapePreset(abilities, landscapePanelPreset);
                SetLandscapePreset(abilities.Find("Title"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -14f), new Vector2(-80f, 64f));
                SetLandscapePreset(abilities.Find("HeaderChips"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -84f), new Vector2(-80f, 52f));
                SetLandscapePreset(abilities.Find("Filter"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -144f), new Vector2(-80f, 72f));
                SetLandscapePreset(abilities.Find("ScrollView"),
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -114f), new Vector2(0f, -228f));

                Transform content = abilities.Find("ScrollView/Viewport/Content");
                if (content != null)
                {
                    ConfigureAdaptiveGrid(
                        content.gameObject,
                        portrait: new GridModeSettings { columns = 1, cellSize = new Vector2(1000f, 320f), spacing = new Vector2(20f, 18f) },
                        landscape: new GridModeSettings { columns = 2, cellSize = new Vector2(780f, 356f), spacing = new Vector2(24f, 18f) },
                        ultraWideColumns: 3,
                        padding: new RectOffset(40, 40, 12, 28));
                }
            }

            Transform achievements = safe.Find("AchievementsPanel");
            if (achievements != null)
            {
                SetLandscapePreset(achievements, landscapePanelPreset);
                SetLandscapePreset(achievements.Find("Title"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -16f), new Vector2(-80f, 68f));
                SetLandscapePreset(achievements.Find("Summary"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -88f), new Vector2(-80f, 36f));
                SetLandscapePreset(achievements.Find("ScrollView"),
                    new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -68f), new Vector2(0f, -136f));

                Transform content = achievements.Find("ScrollView/Viewport/Content");
                if (content != null)
                {
                    ConfigureAdaptiveGrid(
                        content.gameObject,
                        portrait: new GridModeSettings { columns = 1, cellSize = new Vector2(1000f, 240f), spacing = new Vector2(20f, 18f) },
                        landscape: new GridModeSettings { columns = 2, cellSize = new Vector2(780f, 280f), spacing = new Vector2(24f, 18f) },
                        ultraWideColumns: 3,
                        padding: new RectOffset(40, 40, 12, 28));
                }
            }

            Transform daily = safe.Find("DailyBonusScreenPanel");
            if (daily != null)
            {
                SetLandscapePreset(daily, landscapePanelPreset);
                SetLandscapePreset(daily.Find("Title"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -18f), new Vector2(-80f, 68f));
                SetLandscapePreset(daily.Find("CycleText"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -92f), new Vector2(-80f, 40f));
                SetLandscapePreset(daily.Find("TimerText"),
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -136f), new Vector2(-80f, 34f));

                Transform daysContainer = daily.Find("DaysContainer");
                if (daysContainer != null)
                {
                    ReplaceWithAdaptiveLayoutGroup(
                        daysContainer.gameObject,
                        portrait: new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 20f, childAlignment = TextAnchor.MiddleCenter },
                        landscape: new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 16f, childAlignment = TextAnchor.MiddleCenter },
                        padding: new RectOffset(0, 0, 0, 0),
                        ctrlW: true, ctrlH: true, expandW: true, expandH: true);

                    SetLandscapePreset(daysContainer,
                        new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 20f), new Vector2(-72f, 380f));

                    SetFlexLayoutElement(daysContainer.Find("Row1"), 4f, 1f);
                    SetFlexLayoutElement(daysContainer.Find("Row2"), 3.35f, 1f);
                }

                SetLandscapePreset(daily.Find("ClaimButton"),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-270f, 44f), new Vector2(500f, 116f));
                SetLandscapePreset(daily.Find("ClaimDoubleButton"),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(270f, 44f), new Vector2(500f, 116f));
                SetLandscapePreset(daily.Find("AllClaimedMessage"),
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 60f), new Vector2(-160f, 80f));
            }

            Transform constellationPanel = safe.Find("ConstellationScreen/ConstellationPanel");
            if (constellationPanel != null)
            {
                SetLandscapePreset(constellationPanel, landscapePanelPreset);

                SetLandscapePreset(constellationPanel.Find("ZoomInButton"),
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(24f, 260f), new Vector2(96f, 96f));
                SetLandscapePreset(constellationPanel.Find("ZoomOutButton"),
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(24f, 152f), new Vector2(96f, 96f));
                SetLandscapePreset(constellationPanel.Find("CenterButton"),
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(24f, 44f), new Vector2(96f, 96f));

                Transform detail = constellationPanel.Find("DetailSheet");
                if (detail != null)
                {
                    // Right-side drawer in Landscape
                    SetLandscapePreset(detail,
                        new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                        new Vector2(-16f, -60f), new Vector2(660f, -152f));

                    SetLandscapePreset(detail.Find("Icon"),
                        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(24f, -24f), new Vector2(104f, 104f));
                    SetLandscapePreset(detail.Find("Title"),
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                        new Vector2(144f, -20f), new Vector2(-220f, 82f));
                    SetLandscapePreset(detail.Find("Type"),
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                        new Vector2(144f, -104f), new Vector2(-168f, 36f));
                    SetLandscapePreset(detail.Find("Status"),
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                        new Vector2(144f, -142f), new Vector2(-168f, 34f));
                    SetLandscapePreset(detail.Find("Description"),
                        new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -34f), new Vector2(-48f, -418f));
                    SetLandscapePreset(detail.Find("UnlockButton"),
                        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 124f), new Vector2(-48f, 92f));
                    SetLandscapePreset(detail.Find("UnlockCoinsButton"),
                        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 20f), new Vector2(-48f, 92f));
                }
            }

            SetLandscapePreset(safe.Find("SettingsPopup/PopupPanel/Window"),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(960f, 800f));

            // Keep Portrait as the saved scene default
            AdaptiveLayoutPreviewMenu.ApplyToOpenScenes(LayoutMode.Portrait);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WireGameplay()
        {
            Scene scene = EditorSceneManager.OpenScene(GAMEPLAY_SCENE, OpenSceneMode.Single);
            Canvas canvas = FindCanvas(scene);
            if (canvas == null)
            {
                Debug.LogError("[Sprint14LandscapeLayoutTool] Gameplay Canvas not found.");
                return;
            }

            AdaptiveLayoutPreviewMenu.ApplyToOpenScenes(LayoutMode.Portrait);

            Transform hud = canvas.transform.Find("SafeAreaHUD");
            if (hud != null)
            {
                // Consolidate HUD widgets onto a single SafeAreaHUD sub-canvas (draw calls <= 10)
                if (hud.GetComponent<Canvas>() == null) hud.gameObject.AddComponent<Canvas>();
                if (hud.GetComponent<GraphicRaycaster>() == null) hud.gameObject.AddComponent<GraphicRaycaster>();
                EditorUtility.SetDirty(hud.gameObject);

                foreach (string widgetName in new[] { "XpBar", "HpBar", "WaveHud", "CoinHud", "KillsHud", "AbilityHud", "UltimateButton", "BossHealthBar", "ComboHud" })
                {
                    Transform child = hud.Find(widgetName);
                    if (child == null) continue;
                    if (child.TryGetComponent<GraphicRaycaster>(out var gr)) Object.DestroyImmediate(gr);
                    if (child.TryGetComponent<Canvas>(out var subCanvas)) Object.DestroyImmediate(subCanvas);
                }

                // HudTopStack: lower topOffset in Landscape
                if (hud.TryGetComponent<HudTopStack>(out var topStack))
                {
                    var soStack = new SerializedObject(topStack);
                    SerializedProperty prop = soStack.FindProperty("landscapeTopOffset");
                    if (prop != null) prop.floatValue = -136f;
                    soStack.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(topStack);
                }

                // Top-left Hero Block (PauseButton + HpBar + XpBar)
                SetLandscapePreset(hud.Find("PauseButton"),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(24f, -20f), new Vector2(84f, 84f));
                SetLandscapePreset(hud.Find("HpBar"),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(124f, -20f), new Vector2(420f, 44f));
                SetLandscapePreset(hud.Find("XpBar"),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(124f, -72f), new Vector2(420f, 32f));

                // Left-side ability columns below Hero Block (keep VerticalLayoutGroup on AbilityHud for Portrait compatibility)
                Transform abilityHud = hud.Find("AbilityHud");
                if (abilityHud != null)
                {
                    if (abilityHud.TryGetComponent<AdaptiveLayoutGroup>(out var oldAdaptive))
                    {
                        Object.DestroyImmediate(oldAdaptive);
                    }
                    var vl = abilityHud.GetComponent<VerticalLayoutGroup>();
                    if (vl == null) vl = abilityHud.gameObject.AddComponent<VerticalLayoutGroup>();
                    vl.spacing = 12f;
                    vl.padding = new RectOffset(0, 0, 0, 0);
                    vl.childAlignment = TextAnchor.LowerLeft;
                    vl.childControlWidth = false;
                    vl.childControlHeight = false;
                    vl.childForceExpandWidth = false;
                    vl.childForceExpandHeight = false;
                    vl.enabled = true;
                    EditorUtility.SetDirty(vl);

                    SetLandscapePreset(abilityHud,
                        new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(24f, -132f), new Vector2(164f, 560f));

                    Transform activeRow = abilityHud.Find("ActiveSlots");
                    if (activeRow != null)
                    {
                        ReplaceWithAdaptiveLayoutGroup(
                            activeRow.gameObject,
                            portrait: new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 8f, childAlignment = TextAnchor.MiddleLeft },
                            landscape: new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 8f, childAlignment = TextAnchor.UpperCenter },
                            padding: new RectOffset(0, 0, 0, 0),
                            ctrlW: false, ctrlH: false, expandW: false, expandH: false);

                        SetLandscapePreset(activeRow,
                            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                            Vector2.zero, new Vector2(76f, 560f));
                    }

                    Transform passiveRow = abilityHud.Find("PassiveSlots");
                    if (passiveRow != null)
                    {
                        ReplaceWithAdaptiveLayoutGroup(
                            passiveRow.gameObject,
                            portrait: new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 8f, childAlignment = TextAnchor.MiddleLeft },
                            landscape: new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 8f, childAlignment = TextAnchor.UpperCenter },
                            padding: new RectOffset(0, 0, 0, 0),
                            ctrlW: false, ctrlH: false, expandW: false, expandH: false);

                        SetLandscapePreset(passiveRow,
                            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(84f, 0f), new Vector2(76f, 560f));
                    }
                }

                // Top-center WaveHud, bottom-right Ultimate, top notification shade & FTUE hints
                SetLandscapePreset(hud.Find("WaveHud"),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -8f), new Vector2(380f, 112f));
                SetLandscapePreset(hud.Find("UltimateButton"),
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-44f, 68f), new Vector2(168f, 168f));
                SetLandscapePreset(hud.Find("BossHealthBar"),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -136f), new Vector2(720f, 104f));
                SetLandscapePreset(hud.Find("WaveEventBanner"),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -136f), new Vector2(720f, 164f));
                SetLandscapePreset(hud.Find("FtueMoveHint"),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 72f), new Vector2(740f, 76f));
                SetLandscapePreset(hud.Find("FtueUltimateHint"),
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-44f, 250f), new Vector2(420f, 68f));
            }

            SetLandscapePreset(canvas.transform.Find("SafeAreaOverlay/AchievementPopup"),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -136f), new Vector2(680f, 128f));

            WirePauseTwoColumns(canvas.transform.Find("PauseMenu/PausePanel/Window") as RectTransform);
            WireGameOverTwoColumns(canvas.transform.Find("GameOverPanel/Content/Layout") as RectTransform);

            SetLandscapePreset(canvas.transform.Find("SettingsPopup/PopupPanel/Window"),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(960f, 800f));

            AdaptiveLayoutPreviewMenu.ApplyToOpenScenes(LayoutMode.Portrait);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WirePauseTwoColumns(RectTransform window)
        {
            if (window == null) return;

            SetLandscapePreset(window,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1520f, 0f));

            Transform body = EnsureContainer(window, "Body");
            ReplaceWithAdaptiveLayoutGroup(
                body.gameObject,
                portrait: new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 18f, childAlignment = TextAnchor.UpperCenter },
                landscape: new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 40f, childAlignment = TextAnchor.UpperCenter },
                padding: new RectOffset(0, 0, 0, 0),
                ctrlW: true, ctrlH: true, expandW: true, expandH: false);

            Transform left = EnsureVerticalColumn(body, "LeftColumn", 20f, 1.15f);
            Transform right = EnsureVerticalColumn(body, "RightColumn", 18f, 0.85f);

            ReparentIfDirectChild(window, left, "Summary", "BuildHeader", "BuildGrid", "SynergySection");
            ReparentIfDirectChild(window, right, "ResumeButton", "SettingsButton", "QuitToMenuButton");

            Transform title = window.Find("Title");
            if (title != null) title.SetSiblingIndex(0);
            body.SetSiblingIndex(title != null ? 1 : 0);

            EditorUtility.SetDirty(window);
        }

        private static void WireGameOverTwoColumns(RectTransform layout)
        {
            if (layout == null) return;

            SetLandscapePreset(layout,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-160f, -64f));

            Transform cols = EnsureContainer(layout, "Columns");
            ReplaceWithAdaptiveLayoutGroup(
                cols.gameObject,
                portrait: new StackModeSettings { axis = LayoutAxis.Vertical, spacing = 16f, childAlignment = TextAnchor.UpperCenter },
                landscape: new StackModeSettings { axis = LayoutAxis.Horizontal, spacing = 36f, childAlignment = TextAnchor.UpperCenter },
                padding: new RectOffset(0, 0, 0, 0),
                ctrlW: true, ctrlH: true, expandW: true, expandH: false);
            SetFlexLayoutElement(cols, 1f, 0f);

            Transform left = EnsureVerticalColumn(cols, "LeftColumn", 14f, 0.95f);
            Transform right = EnsureVerticalColumn(cols, "RightColumn", 14f, 1.05f);

            ReparentIfDirectChild(layout, left, "StatChips", "Rewards", "RunAchievementsSection");
            ReparentIfDirectChild(layout, right, "DamageBreakdownSection");

            // Keep order: Title (0), WaveText (1), BestRow (2), Columns (3), AdButtons (4), NavButtons (5)
            int insertIndex = 0;
            foreach (string topName in new[] { "Title", "WaveText", "BestRow" })
            {
                Transform child = layout.Find(topName);
                if (child != null) child.SetSiblingIndex(insertIndex++);
            }
            cols.SetSiblingIndex(insertIndex);

            // Slightly tighten AdButtons and NavButtons in Landscape so everything fits comfortably at 1080p
            Transform adButtons = layout.Find("AdButtons");
            if (adButtons != null && adButtons.TryGetComponent<LayoutElement>(out var adLe))
            {
                adLe.minHeight = 104f;
                adLe.preferredHeight = 108f;
                EditorUtility.SetDirty(adLe);
            }
            Transform navButtons = layout.Find("NavButtons");
            if (navButtons != null && navButtons.TryGetComponent<LayoutElement>(out var navLe))
            {
                navLe.minHeight = 100f;
                navLe.preferredHeight = 104f;
                EditorUtility.SetDirty(navLe);
            }

            EditorUtility.SetDirty(layout);
        }

        private static Transform EnsureContainer(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Transform EnsureVerticalColumn(Transform parent, string name, float spacing, float flexWidth)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) go.transform.SetParent(parent, false);

            var vl = go.GetComponent<VerticalLayoutGroup>();
            if (vl == null) vl = go.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.padding = new RectOffset(0, 0, 0, 0);
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            SetFlexLayoutElement(go.transform, flexWidth, 0f);
            EditorUtility.SetDirty(go);
            return go.transform;
        }

        private static void ReparentIfDirectChild(Transform oldParent, Transform newParent, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform child = oldParent.Find(names[i]);
                if (child != null && child.parent != newParent)
                {
                    child.SetParent(newParent, false);
                }
            }
        }

        private static void SetFlexLayoutElement(Transform target, float flexW, float flexH)
        {
            if (target == null) return;
            var le = target.GetComponent<LayoutElement>();
            if (le == null) le = target.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = flexW;
            le.flexibleHeight = flexH;
            EditorUtility.SetDirty(le);
        }

        private static void SetLandscapePreset(
            Transform target,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            SetLandscapePreset(target, Preset(anchorMin, anchorMax, pivot, anchoredPos, sizeDelta));
        }

        private static void SetLandscapePreset(Transform target, RectPreset landscape)
        {
            if (target == null) return;
            var rr = target.GetComponent<ResponsiveRect>();
            if (rr == null)
            {
                rr = target.gameObject.AddComponent<ResponsiveRect>();
            }
            if (!rr.HasPreset(LayoutMode.Portrait))
            {
                rr.CapturePreset(LayoutMode.Portrait);
            }
            rr.SetPreset(LayoutMode.Landscape, landscape);
            EditorUtility.SetDirty(rr);
        }

        private static RectPreset Preset(
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            return new RectPreset
            {
                captured = true,
                anchorMin = anchorMin,
                anchorMax = anchorMax,
                pivot = pivot,
                anchoredPosition = anchoredPos,
                sizeDelta = sizeDelta
            };
        }

        private static void ReplaceWithAdaptiveLayoutGroup(
            GameObject go,
            StackModeSettings portrait,
            StackModeSettings landscape,
            RectOffset padding,
            bool ctrlW,
            bool ctrlH,
            bool expandW,
            bool expandH)
        {
            var existingH = go.GetComponent<HorizontalLayoutGroup>();
            if (existingH != null) Object.DestroyImmediate(existingH);
            var existingV = go.GetComponent<VerticalLayoutGroup>();
            if (existingV != null) Object.DestroyImmediate(existingV);

            var adaptive = go.GetComponent<AdaptiveLayoutGroup>();
            if (adaptive == null) adaptive = go.AddComponent<AdaptiveLayoutGroup>();

            adaptive.padding = padding;
            adaptive.childControlWidth = ctrlW;
            adaptive.childControlHeight = ctrlH;
            adaptive.childForceExpandWidth = expandW;
            adaptive.childForceExpandHeight = expandH;
            adaptive.SetSettings(LayoutMode.Portrait, portrait);
            adaptive.SetSettings(LayoutMode.Landscape, landscape);
            adaptive.ApplyLayoutMode(LayoutMode.Portrait);
            EditorUtility.SetDirty(adaptive);
        }

        private static void ConfigureAdaptiveGrid(
            GameObject go,
            GridModeSettings portrait,
            GridModeSettings landscape,
            int ultraWideColumns,
            RectOffset padding)
        {
            var existingV = go.GetComponent<VerticalLayoutGroup>();
            if (existingV != null) Object.DestroyImmediate(existingV);
            var existingH = go.GetComponent<HorizontalLayoutGroup>();
            if (existingH != null) Object.DestroyImmediate(existingH);

            var grid = go.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = go.AddComponent<GridLayoutGroup>();
            grid.padding = padding;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            var adaptive = go.GetComponent<AdaptiveGridLayout>();
            if (adaptive == null) adaptive = go.AddComponent<AdaptiveGridLayout>();
            adaptive.SetSettings(LayoutMode.Portrait, portrait);
            adaptive.SetSettings(LayoutMode.Landscape, landscape);
            adaptive.UltraWideColumns = ultraWideColumns;
            adaptive.FitCellWidthToContainer = true;
            adaptive.ApplyLayoutMode(LayoutMode.Portrait);

            var csf = go.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                EditorUtility.SetDirty(csf);
            }

            EditorUtility.SetDirty(grid);
            EditorUtility.SetDirty(adaptive);
        }

        private static void UpdateCameraConfigMargins()
        {
            var camCfg = AssetDatabase.LoadAssetAtPath<CameraConfig>(CAMERA_CONFIG_PATH);
            if (camCfg == null) return;
            camCfg.landscapeCompassMargins = new CompassMargins
            {
                left = 220f,
                right = 220f,
                top = 136f,
                bottom = 90f
            };
            EditorUtility.SetDirty(camCfg);
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas c = root.GetComponent<Canvas>();
                if (c != null) return c;
            }
            return null;
        }
    }
}
