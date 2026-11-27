using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Data.Platform;
using ProjectB.Meta;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// UI redesign (variant A), Play tab: next wave goal selection, idle sway of the hero preview, per-mode text
    /// alignment and the MainMenu wiring (podium, goal card, ability tiles + detail panel).
    /// </summary>
    public class PlayScreenTests
    {
        private const string MainMenuPath = "Assets/_Scenes/MainMenu.unity";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        private AchievementData Wave(string id, int target, string heroConstraint = "")
        {
            var a = ScriptableObject.CreateInstance<AchievementData>();
            created.Add(a);
            a.id = id;
            a.conditionType = AchievementConditionType.ReachWave;
            a.targetValue = target;
            a.runConstraint = new AchievementRunConstraint { heroId = heroConstraint };
            return a;
        }

        private static object GetPrivate(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            return f.GetValue(target);
        }

        // ───────────────────────── goal card ─────────────────────────

        [Test]
        public void FindNextWaveGoal_PicksNearestOpenUnconstrainedWaveAboveBest()
        {
            var w5 = Wave("w5", 5);
            var w10 = Wave("w10", 10);
            var w20 = Wave("w20", 20);
            var hero15 = Wave("hero15", 15, "mage");
            var kills = ScriptableObject.CreateInstance<AchievementData>();
            created.Add(kills);
            kills.id = "kills";
            kills.conditionType = AchievementConditionType.TotalKills;
            kills.targetValue = 8;

            var all = new List<AchievementData> { w20, hero15, kills, w10, w5 };
            var completed = new HashSet<string> { "w5" };

            Assert.AreSame(w10, PlayGoalCardUI.FindNextWaveGoal(all, completed.Contains, 7), "nearest open wave goal above the record");
            Assert.AreSame(w20, PlayGoalCardUI.FindNextWaveGoal(all, completed.Contains, 12), "constrained (hero) goals are skipped");

            completed.Add("w10");
            completed.Add("w20");
            Assert.IsNull(PlayGoalCardUI.FindNextWaveGoal(all, completed.Contains, 30), "nothing left → card shows the record only");
            Assert.IsNull(PlayGoalCardUI.FindNextWaveGoal(null, completed.Contains, 3));
        }

        [Test]
        public void FindNextWaveGoal_FallsBackToSmallestOpenGoal_WhenRecordAlreadyPassedIt()
        {
            var w10 = Wave("w10", 10);
            var all = new List<AchievementData> { w10 };
            Assert.AreSame(w10, PlayGoalCardUI.FindNextWaveGoal(all, _ => false, 25), "an unclaimed goal below the record is still shown");
        }

        // ───────────────────────── preview + adaptive text ─────────────────────────

        [Test]
        public void MenuHeroSway_StaysAroundAuthoredFacing()
        {
            Assert.AreEqual(180f, MenuHeroRotator.EvaluateSwayYaw(180f, 22f, 5f, 0f), 0.001f);
            Assert.AreEqual(202f, MenuHeroRotator.EvaluateSwayYaw(180f, 22f, 5f, 1.25f), 0.001f, "quarter period → +amplitude");
            Assert.AreEqual(158f, MenuHeroRotator.EvaluateSwayYaw(180f, 22f, 5f, 3.75f), 0.001f, "three quarters → -amplitude");
            for (float t = 0f; t < 10f; t += 0.37f)
            {
                float yaw = MenuHeroRotator.EvaluateSwayYaw(180f, 22f, 5f, t);
                Assert.That(yaw, Is.InRange(158f - 0.001f, 202f + 0.001f), "never turns its back to the camera");
            }
        }

        [Test]
        public void ScrollToCenter_ClampsToContentAndCentresBelowStickyBar()
        {
            var contentGo = new GameObject("Content", typeof(RectTransform));
            created.Add(contentGo);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(1000f, 3000f);

            RectTransform Item(float top)
            {
                var go = new GameObject("Item", typeof(RectTransform));
                go.transform.SetParent(content, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(400f, 200f);
                rt.anchoredPosition = new Vector2(0f, -top);
                return rt;
            }

            Assert.AreEqual(0f, AchievementScreenUI.CalculateScrollToCenter(content, Item(0f), 1000f, 100f), 0.01f, "top item: no scroll");
            Assert.AreEqual(2000f, AchievementScreenUI.CalculateScrollToCenter(content, Item(2800f), 1000f, 100f), 0.01f, "bottom item: clamped to the end");
            // Item centre at 1600 from the top; visible area below the 100 px bar is centred at (1000 + 100) / 2 = 550.
            Assert.AreEqual(1050f, AchievementScreenUI.CalculateScrollToCenter(content, Item(1500f), 1000f, 100f), 0.01f);
        }

        // ───────────────────────── scene wiring ─────────────────────────

        [Test]
        public void MainMenu_PlayTab_HasPodiumGoalCardTilesAndDetailPanel()
        {
            Scene loaded = SceneManager.GetSceneByPath(MainMenuPath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive) : loaded;
            try
            {
                HeroSelectionUI hero = null;
                MenuHeroRotator rotator = null;
                MenuPreviewFraming framing = null;
                GameObject podium = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    hero ??= root.GetComponentInChildren<HeroSelectionUI>(true);
                    rotator ??= root.GetComponentInChildren<MenuHeroRotator>(true);
                    framing ??= root.GetComponentInChildren<MenuPreviewFraming>(true);
                    if (root.name == "HeroPodium") podium = root;
                }
                Assert.IsNotNull(hero);

                Assert.IsNotNull(rotator);
                Assert.AreEqual(PreviewMotion.Sway, rotator.motion, "hero sways instead of spinning its back to the camera");
                Assert.IsNotNull(podium, "voxel podium under the hero");
                Assert.LessOrEqual(podium.transform.position.y, rotator.transform.position.y, "podium sits at the hero's feet");
                foreach (Collider c in podium.GetComponentsInChildren<Collider>(true)) Assert.Fail($"podium has a collider on {c.name}");
                Assert.Greater(framing.transform.eulerAngles.x, 1f, "camera looks slightly down at the island");

                var detail = (GameObject)GetPrivate(hero, "detailPanel");
                Assert.IsNotNull(detail, "HeroSelectionUI.detailPanel");
                Assert.IsFalse(detail.activeSelf, "detail panel starts collapsed");
                Assert.IsNotNull(detail.transform.Find("WeaponText"), "descriptions live in the detail panel");

                Transform block = hero.transform.Find("AbilityBlock");
                Assert.IsNotNull(block, "tiles + detail stack");
                Assert.AreSame(block, detail.transform.parent, "the detail panel stacks right above the tiles");
                Assert.AreEqual(0, detail.transform.GetSiblingIndex(), "detail panel above the tiles");
                Transform card = block.Find("HeroInfoCard");
                Assert.IsNotNull(card);
                Assert.IsNotNull(card.GetComponent<HorizontalLayoutGroup>(), "tiles in a row in both orientations");
                Assert.IsNull(card.GetComponent<AdaptiveLayoutGroup>());
                Assert.IsNull(card.Find("StatChips"), "HP / speed moved to the header chip row");
                var highlights = (GameObject[])GetPrivate(hero, "tileHighlights");
                Assert.AreEqual(3, highlights.Length);
                foreach (GameObject h in highlights) Assert.IsNotNull(h, "each tile has a selection frame");
                Assert.IsNotNull(GetPrivate(hero, "detailPointer"), "bubble tail");
                var name = card.Find("WeaponRow/Name").GetComponent<TMP_Text>();
                Assert.IsFalse(name.enableAutoSizing, "fixed size → stable tile height");
                var nameLayout = name.GetComponent<LayoutElement>();
                Assert.Greater(nameLayout.preferredHeight, 0f, "fixed two-line name box → same tile height for every hero");
                Assert.AreEqual(nameLayout.minHeight, nameLayout.preferredHeight);
                Assert.AreEqual(TextOverflowModes.Ellipsis, name.overflowMode, "longer names end with an ellipsis");
                Assert.IsNotNull(hero.transform.Find("NamePlate/ChipRow/RoleChip"), "role chip in the header");

                var goal = hero.GetComponentInChildren<PlayGoalCardUI>(true);
                Assert.IsNotNull(goal, "goal card");
                foreach (string field in new[] { "bestValueText", "progressFill", "rewardText", "rewardIcon", "cardButton" })
                {
                    Assert.IsNotNull(GetPrivate(goal, field), $"PlayGoalCardUI.{field}");
                }
                Assert.AreEqual(Image.Type.Filled, ((Image)GetPrivate(goal, "progressFill")).type);

                // Top-bar currency readouts: frameless & non-clickable (no Button, no raycast, transparent background, no ToySurface material).
                Transform topBar = hero.transform.parent.Find("TopBar");
                Assert.IsNotNull(topBar, "SafeAreaRoot/TopBar");
                foreach (string pillName in new[] { "CoinsPill", "ShardsPill" })
                {
                    Transform pill = topBar.Find($"Currencies/{pillName}");
                    Assert.IsNotNull(pill, $"TopBar/Currencies/{pillName}");
                    Assert.IsNull(pill.GetComponent<Button>(), $"{pillName} must not have a Button");
                    Assert.IsNotNull(pill.GetComponent<CurrencyLabel>(), $"{pillName} keeps CurrencyLabel");
                    Assert.IsNotNull(pill.Find("Icon"), $"{pillName}/Icon");
                    Assert.IsNotNull(pill.Find("Amount"), $"{pillName}/Amount");

                    var pillImg = pill.GetComponent<Image>();
                    Assert.IsNotNull(pillImg, $"{pillName} Image");
                    Assert.IsFalse(pillImg.raycastTarget, $"{pillName} must not block/capture raycasts");
                    Assert.LessOrEqual(pillImg.color.a, 0.01f, $"{pillName} must have no button-like frame");
                    Assert.AreNotSame(UiTheme.Current.surfaceMaterial, pillImg.material, $"{pillName} must not use ToySurface material");
                }

                // Speed stat chip uses theme.iconSpeed (boot icon) and inline <sprite name=speed> exists.
                Transform speedIconT = hero.transform.Find("NamePlate/ChipRow/SpeedChip/Icon");
                Assert.IsNotNull(speedIconT, "NamePlate/ChipRow/SpeedChip/Icon");
                var speedImg = speedIconT.GetComponent<Image>();
                Assert.IsNotNull(speedImg, "SpeedChip/Icon Image");
                Assert.IsNotNull(UiTheme.Current.iconSpeed, "UiTheme.iconSpeed must be assigned");
                Assert.AreSame(UiTheme.Current.iconSpeed, speedImg.sprite, "SpeedChip/Icon must use UiTheme.iconSpeed");
                Assert.IsNotNull(UiTheme.Current.iconSpriteAsset, "UiTheme.iconSpriteAsset");
                Assert.GreaterOrEqual(UiTheme.Current.iconSpriteAsset.GetSpriteIndexFromName("speed"), 0, "<sprite name=speed> must exist");

                // LockOverlay sits in the bottom CTA slot (not over the hero silhouette) with no button/card chrome.
                var lockOverlay = (GameObject)GetPrivate(hero, "lockOverlay");
                Assert.IsNotNull(lockOverlay, "HeroSelectionUI.lockOverlay");
                Assert.IsNull(lockOverlay.GetComponent<Button>(), "LockOverlay must not have a Button");
                var lockImg = lockOverlay.GetComponent<Image>();
                Assert.IsNotNull(lockImg, "LockOverlay Image");
                Assert.IsFalse(lockImg.raycastTarget, "LockOverlay must not capture raycasts");
                Assert.LessOrEqual(lockImg.color.a, 0.01f, "LockOverlay must be frameless (no button/card background)");
                Assert.AreNotSame(UiTheme.Current.surfaceMaterial, lockImg.material, "LockOverlay must not use ToySurface material");

                var startRect = hero.StartRunButton.GetComponent<ResponsiveRect>();
                var lockRect = lockOverlay.GetComponent<ResponsiveRect>();
                Assert.IsNotNull(startRect);
                Assert.IsNotNull(lockRect);
                foreach (LayoutMode mode in new[] { LayoutMode.Portrait, LayoutMode.Landscape })
                {
                    RectPreset startPreset = startRect.GetPreset(mode);
                    RectPreset lockPreset = lockRect.GetPreset(mode);
                    Assert.IsTrue(lockPreset.captured, $"LockOverlay {mode} preset");
                    Assert.AreEqual(0f, lockPreset.anchorMax.y, 0.001f, $"LockOverlay sits at bottom in {mode}");
                    Assert.AreEqual(startPreset.anchorMin, lockPreset.anchorMin, $"LockOverlay matches StartButton anchorMin in {mode}");
                    Assert.AreEqual(startPreset.anchorMax, lockPreset.anchorMax, $"LockOverlay matches StartButton anchorMax in {mode}");
                    Assert.AreEqual(startPreset.anchoredPosition, lockPreset.anchoredPosition, $"LockOverlay matches StartButton pos in {mode}");
                    Assert.AreEqual(startPreset.sizeDelta, lockPreset.sizeDelta, $"LockOverlay matches StartButton size in {mode}");
                }

                Assert.IsNotNull(GetPrivate(hero, "lockRequirementText"), "HeroSelectionUI.lockRequirementText");
                Assert.IsNotNull(GetPrivate(hero, "lockProgressText"), "HeroSelectionUI.lockProgressText");
                var lockFill = (Image)GetPrivate(hero, "lockProgressFill");
                Assert.IsNotNull(lockFill, "HeroSelectionUI.lockProgressFill");
                Assert.AreEqual(Image.Type.Filled, lockFill.type, "lockProgressFill must be Filled");
                Assert.IsNotNull(GetPrivate(hero, "lockProgressBar"), "HeroSelectionUI.lockProgressBar");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HeroSelectionUI_LockedHero_ShowsBottomUnlockProgress_HidesStart_AndDarkensSilhouette()
        {
            HeroCatalog catalog = Resources.Load<HeroCatalog>("HeroCatalog");
            Assert.IsNotNull(catalog, "Resources/HeroCatalog.asset must exist");

            using var save = new SaveManager(new InMemorySaveStorage());
            save.Data.bestWave = 12;
            var ach = new AchievementManager(save);
            ach.AddProgress("clear_3_shrines", 1);
            var heroService = new SelectedHeroService(save, catalog);

            var root = new GameObject("Root", typeof(RectTransform), typeof(Canvas));
            created.Add(root);
            var panel = new GameObject("PlayScreenPanel", typeof(RectTransform), typeof(HeroSelectionUI));
            panel.transform.SetParent(root.transform, false);
            var ui = panel.GetComponent<HeroSelectionUI>();

            var previewRoot = new GameObject("HeroPreview");
            created.Add(previewRoot);
            var previewCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewCube.transform.SetParent(previewRoot.transform, false);

            var startGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            startGo.transform.SetParent(panel.transform, false);
            var lockGo = new GameObject("LockOverlay", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(panel.transform, false);
            var reqText = new GameObject("Requirement", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            reqText.transform.SetParent(lockGo.transform, false);
            var progText = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            progText.transform.SetParent(lockGo.transform, false);
            var barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(lockGo.transform, false);
            var fillImg = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fillImg.transform.SetParent(barGo.transform, false);
            fillImg.type = Image.Type.Filled;

            void SetField(string name, object val)
            {
                FieldInfo f = typeof(HeroSelectionUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(f, name);
                f.SetValue(ui, val);
            }

            SetField("heroPreviewVisualRoot", previewRoot.transform);
            SetField("startRunButton", startGo.GetComponent<Button>());
            SetField("lockOverlay", lockGo);
            SetField("lockRequirementText", reqText);
            SetField("lockProgressText", progText);
            SetField("lockProgressFill", fillImg);
            SetField("lockProgressBar", barGo);

            ui.Construct(heroService, ach, null);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(HeroSelectionUI).GetMethod("Awake", flags).Invoke(ui, null);
            typeof(HeroSelectionUI).GetMethod("OnEnable", flags).Invoke(ui, null);

            // Knight (unlocked default)
            Assert.IsTrue(startGo.activeSelf, "StartButton shown for unlocked hero");
            Assert.IsFalse(lockGo.activeSelf, "LockOverlay hidden for unlocked hero");
            Renderer ActivePreviewRenderer() => previewRoot.GetComponentInChildren<Renderer>(false);
            var block = new MaterialPropertyBlock();
            Assert.IsNotNull(ActivePreviewRenderer(), "Knight preview renderer");
            Assert.IsFalse(ActivePreviewRenderer().HasPropertyBlock(), "Unlocked hero is not silhouetted");

            // Mage (locked: reach_wave_20, bestWave = 12)
            ui.SelectNext();
            Assert.IsFalse(startGo.activeSelf, "StartButton hidden for locked hero");
            Assert.IsTrue(lockGo.activeSelf, "LockOverlay shown for locked hero");
            StringAssert.Contains("12", progText.text);
            StringAssert.Contains("/ 20", progText.text);
            Assert.AreEqual(12f / 20f, fillImg.fillAmount, 0.001f);
            Assert.IsNotNull(ActivePreviewRenderer(), "Mage preview renderer");
            ActivePreviewRenderer().GetPropertyBlock(block);
            Color actualBase = block.GetColor("_BaseColor");
            Color expectedBase = UiTheme.Current.surfaceSunken;
            Assert.AreEqual(expectedBase.r, actualBase.r, 0.005f, "Locked hero remains darkened (R)");
            Assert.AreEqual(expectedBase.g, actualBase.g, 0.005f, "Locked hero remains darkened (G)");
            Assert.AreEqual(expectedBase.b, actualBase.b, 0.005f, "Locked hero remains darkened (B)");
            Assert.AreEqual(expectedBase.a, actualBase.a, 0.005f, "Locked hero remains darkened (A)");

            // Ranger (locked: clear_3_shrines, progress = 1 / 3)
            ui.SelectNext();
            Assert.IsFalse(startGo.activeSelf);
            Assert.IsTrue(lockGo.activeSelf);
            StringAssert.Contains("1", progText.text);
            StringAssert.Contains("/ 3", progText.text);
            Assert.AreEqual(1f / 3f, fillImg.fillAmount, 0.001f);

            // Back to Knight -> restored
            ui.SelectPrevious();
            ui.SelectPrevious();
            Assert.IsTrue(startGo.activeSelf);
            Assert.IsFalse(lockGo.activeSelf);
            Assert.IsFalse(ActivePreviewRenderer().HasPropertyBlock());
        }
    }
}
