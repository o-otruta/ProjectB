using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Platform;
using ProjectB.EditorScripts;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Sprint 14 — multiplatform. Covers 14.1 (adaptive foundation): layout-mode hysteresis, canvas scaling by the short
    /// side, ResponsiveRect presets, adaptive stack/grid layouts, orientation policy and input-mode switching.
    /// Later sub-sprints (14.2+: camera, spawn ring, MoveInput, platform providers) extend this file.
    /// </summary>
    public class Sprint14MultiplatformTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private LayoutConfig config;

        [SetUp]
        public void SetUp()
        {
            config = Track(ScriptableObject.CreateInstance<LayoutConfig>());
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        private static Rect Full(int w, int h) => new Rect(0, 0, w, h);

        // ───────────────────────── LayoutModeService ─────────────────────────

        [Test]
        public void LayoutConfig_Defaults_MatchAudit()
        {
            Assert.AreEqual(1.15f, config.landscapeEnterAspect, 1e-4f);
            Assert.AreEqual(0.95f, config.portraitEnterAspect, 1e-4f);
            Assert.AreEqual(new Vector2(1080, 1920), config.portraitReferenceResolution);
            Assert.AreEqual(new Vector2(1920, 1080), config.landscapeReferenceResolution);
        }

        [Test]
        public void LayoutModeService_Hysteresis_BorderlineWindowsDoNotFlicker()
        {
            var service = new LayoutModeService(config);
            int modeEvents = 0;
            service.LayoutModeChanged += _ => modeEvents++;

            service.UpdateMetrics(1080, 1920, Full(1080, 1920));
            Assert.AreEqual(LayoutMode.Portrait, service.Mode);

            // Square and slightly-wide windows stay Portrait while coming from Portrait.
            service.UpdateMetrics(1000, 1000, Full(1000, 1000));
            service.UpdateMetrics(1100, 1000, Full(1100, 1000));
            Assert.AreEqual(LayoutMode.Portrait, service.Mode);
            Assert.AreEqual(0, modeEvents);

            // Crossing 1.15 flips to Landscape.
            service.UpdateMetrics(1150, 1000, Full(1150, 1000));
            Assert.AreEqual(LayoutMode.Landscape, service.Mode);
            Assert.AreEqual(1, modeEvents);

            // Back into the band (4:3 rotated-ish, square): stays Landscape.
            service.UpdateMetrics(1000, 1000, Full(1000, 1000));
            service.UpdateMetrics(960, 1000, Full(960, 1000));
            Assert.AreEqual(LayoutMode.Landscape, service.Mode);
            Assert.AreEqual(1, modeEvents);

            // Crossing 0.95 flips back.
            service.UpdateMetrics(950, 1000, Full(950, 1000));
            Assert.AreEqual(LayoutMode.Portrait, service.Mode);
            Assert.AreEqual(2, modeEvents);
        }

        [TestCase(1080, 1920, LayoutMode.Portrait)]
        [TestCase(1080, 2520, LayoutMode.Portrait)]
        [TestCase(1920, 1080, LayoutMode.Landscape)]
        [TestCase(1920, 1200, LayoutMode.Landscape)]
        [TestCase(2560, 1080, LayoutMode.Landscape)]
        [TestCase(1024, 768, LayoutMode.Landscape)]   // 4:3 is above 1.15
        [TestCase(1000, 1000, LayoutMode.Portrait)]   // square, no history → Portrait
        [TestCase(1100, 1000, LayoutMode.Landscape)]  // in the band, no history → by the longer side
        public void LayoutModeService_InitialMode(int w, int h, LayoutMode expected)
        {
            var service = new LayoutModeService(config);
            service.UpdateMetrics(w, h, Full(w, h));
            Assert.AreEqual(expected, service.Mode);
        }

        [Test]
        public void LayoutModeService_SafeAreaChange_RaisesScreenChangedOnly()
        {
            var service = new LayoutModeService(config);
            service.UpdateMetrics(1080, 2340, Full(1080, 2340));
            int screenEvents = 0, modeEvents = 0;
            service.ScreenChanged += () => screenEvents++;
            service.LayoutModeChanged += _ => modeEvents++;

            Assert.IsFalse(service.UpdateMetrics(1080, 2340, Full(1080, 2340)), "Unchanged metrics must be a no-op.");
            Assert.IsTrue(service.UpdateMetrics(1080, 2340, new Rect(0, 90, 1080, 2160)));
            Assert.AreEqual(1, screenEvents);
            Assert.AreEqual(0, modeEvents);
            Assert.AreEqual(new Rect(0, 90, 1080, 2160), service.SafeArea);
        }

        [Test]
        public void LayoutModeService_IgnoresInvalidSizes()
        {
            var service = new LayoutModeService(config);
            Assert.IsFalse(service.UpdateMetrics(0, 1920, Rect.zero));
            Assert.IsFalse(service.UpdateMetrics(1080, -1, Rect.zero));
        }

        [Test]
        public void LayoutModeService_PollingAndModeFlips_AreZeroGc()
        {
            var service = new LayoutModeService(config);
            int counter = 0;
            Action<LayoutMode> modeHandler = _ => counter++;
            Action screenHandler = () => counter++;
            service.LayoutModeChanged += modeHandler;
            service.ScreenChanged += screenHandler;

            Rect portraitSafe = Full(1080, 1920);
            Rect landscapeSafe = Full(1920, 1080);
            service.UpdateMetrics(1080, 1920, portraitSafe);
            service.UpdateMetrics(1920, 1080, landscapeSafe); // warm-up

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                service.UpdateMetrics(1920, 1080, landscapeSafe); // unchanged → early out
                service.UpdateMetrics(1080, 1920, portraitSafe);
                service.UpdateMetrics(1920, 1080, landscapeSafe);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "LayoutModeService must not allocate while polling or flipping modes.");
            Assert.Greater(counter, 0);
        }

        // ───────────────────────── AdaptiveCanvasScaler ─────────────────────────

        [TestCase(1080, 1920, LayoutMode.Portrait, 0f)]   // 9:16 — width
        [TestCase(1080, 2520, LayoutMode.Portrait, 0f)]   // 9:21 — width (taller phones get more vertical room)
        [TestCase(1536, 2048, LayoutMode.Portrait, 1f)]   // 3:4 tablet — height, so 1920 units still fit
        [TestCase(1920, 1080, LayoutMode.Landscape, 1f)]  // 16:9 — height
        [TestCase(2560, 1080, LayoutMode.Landscape, 1f)]  // 21:9 — height
        [TestCase(1920, 1200, LayoutMode.Landscape, 0f)]  // 16:10 — width, so 1920 units still fit
        public void AdaptiveLayoutMath_MatchWidthOrHeight_ByShortSideWithFitGuard(int w, int h, LayoutMode mode, float expected)
        {
            Vector2 reference = config.GetReferenceResolution(mode);
            Assert.AreEqual(expected, AdaptiveLayoutMath.GetMatchWidthOrHeight(mode, w, h, reference), 1e-4f);
        }

        [Test]
        public void AdaptiveLayoutMath_ScaledReferenceAreaAlwaysFits()
        {
            int[,] screens = { { 1080, 1920 }, { 1080, 2520 }, { 1536, 2048 }, { 1920, 1080 }, { 1920, 1200 }, { 2560, 1080 }, { 1024, 768 }, { 960, 600 } };
            for (int i = 0; i < screens.GetLength(0); i++)
            {
                float w = screens[i, 0], h = screens[i, 1];
                LayoutMode mode = AdaptiveLayoutMath.EvaluateInitial(w / h, config);
                Vector2 reference = config.GetReferenceResolution(mode);
                float match = AdaptiveLayoutMath.GetMatchWidthOrHeight(mode, w, h, reference);
                float scale = match < 0.5f ? w / reference.x : h / reference.y;
                Assert.LessOrEqual(reference.x * scale, w + 0.5f, $"{w}x{h}: reference width overflows");
                Assert.LessOrEqual(reference.y * scale, h + 0.5f, $"{w}x{h}: reference height overflows");
            }
        }

        [Test]
        public void AdaptiveCanvasScaler_AppliesReferenceAndMatchFromService()
        {
            var go = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
            var adaptive = go.AddComponent<AdaptiveCanvasScaler>();
            var scaler = go.GetComponent<CanvasScaler>();

            var service = new LayoutModeService(config);
            service.UpdateMetrics(1080, 1920, Full(1080, 1920));
            adaptive.Bind(service);

            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
            Assert.AreEqual(new Vector2(1080, 1920), scaler.referenceResolution);
            Assert.AreEqual(0f, scaler.matchWidthOrHeight, 1e-4f);

            service.UpdateMetrics(2560, 1080, Full(2560, 1080));
            Assert.AreEqual(LayoutMode.Landscape, adaptive.AppliedMode);
            Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
            Assert.AreEqual(1f, scaler.matchWidthOrHeight, 1e-4f);

            // 16:10 stays Landscape but the fit guard flips the match to width.
            service.UpdateMetrics(1920, 1200, Full(1920, 1200));
            Assert.AreEqual(0f, scaler.matchWidthOrHeight, 1e-4f);
        }

        // ───────────────────────── ResponsiveRect ─────────────────────────

        [Test]
        public void ResponsiveRect_CapturesAndAppliesPresets()
        {
            var go = Track(new GameObject("Panel", typeof(RectTransform)));
            var rt = (RectTransform)go.transform;
            var responsive = go.AddComponent<ResponsiveRect>();

            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.2f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(-48f, 0f);
            responsive.CapturePreset(LayoutMode.Portrait);

            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.12f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(24f, 0f);
            rt.sizeDelta = new Vector2(0f, -96f);
            responsive.CapturePreset(LayoutMode.Landscape);

            responsive.ApplyLayoutMode(LayoutMode.Portrait);
            Assert.AreEqual(new Vector2(1f, 0.2f), rt.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0f), rt.pivot);
            Assert.AreEqual(new Vector2(0f, 40f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(-48f, 0f), rt.sizeDelta);

            responsive.ApplyLayoutMode(LayoutMode.Landscape);
            Assert.AreEqual(new Vector2(0.12f, 1f), rt.anchorMax);
            Assert.AreEqual(new Vector2(0f, 0.5f), rt.pivot);
            Assert.AreEqual(new Vector2(24f, 0f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(0f, -96f), rt.sizeDelta);
            Assert.AreEqual(LayoutMode.Landscape, responsive.AppliedMode);
        }

        [Test]
        public void ResponsiveRect_ModeWithoutPreset_LeavesRectUntouched()
        {
            var go = Track(new GameObject("Panel", typeof(RectTransform)));
            var rt = (RectTransform)go.transform;
            var responsive = go.AddComponent<ResponsiveRect>();
            responsive.SetPreset(LayoutMode.Portrait, new RectPreset { anchorMin = Vector2.zero, anchorMax = Vector2.one, pivot = new Vector2(0.5f, 0.5f) });

            rt.anchoredPosition = new Vector2(123f, 45f);
            Assert.IsFalse(responsive.HasPreset(LayoutMode.Landscape));
            responsive.ApplyLayoutMode(LayoutMode.Landscape);
            Assert.AreEqual(new Vector2(123f, 45f), rt.anchoredPosition);
        }

        // ───────────────────────── AdaptiveLayoutGroup / AdaptiveGridLayout ─────────────────────────

        [Test]
        public void AdaptiveLayoutGroup_SwitchesAxisByMode()
        {
            var root = Track(new GameObject("Stack", typeof(RectTransform)));
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(1000f, 1000f);
            var group = root.AddComponent<AdaptiveLayoutGroup>();
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            var a = CreateChild(rootRt, "A");
            var b = CreateChild(rootRt, "B");

            group.ApplyLayoutMode(LayoutMode.Portrait);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            Assert.IsTrue(group.IsVertical);
            Assert.AreEqual(a.anchoredPosition.x, b.anchoredPosition.x, 0.01f, "Portrait: children share a column");
            Assert.Greater(Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y), 50f);

            group.ApplyLayoutMode(LayoutMode.Landscape);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
            Assert.IsFalse(group.IsVertical);
            Assert.AreEqual(a.anchoredPosition.y, b.anchoredPosition.y, 0.01f, "Landscape: children share a row");
            Assert.Greater(Mathf.Abs(a.anchoredPosition.x - b.anchoredPosition.x), 50f);
            Assert.AreEqual(group.GetSettings(LayoutMode.Landscape).spacing, group.spacing, 1e-4f);
        }

        private RectTransform CreateChild(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)child.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(200f, 200f);
            return rt;
        }

        [Test]
        public void AdaptiveGridLayout_ChangesColumnsByModeAndUltraWide()
        {
            var go = Track(new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)));
            var adaptive = go.AddComponent<AdaptiveGridLayout>();
            adaptive.SetSettings(LayoutMode.Portrait, new GridModeSettings { columns = 1, cellSize = new Vector2(960, 160), spacing = new Vector2(10, 20) });
            adaptive.SetSettings(LayoutMode.Landscape, new GridModeSettings { columns = 2, cellSize = new Vector2(800, 160), spacing = new Vector2(30, 20) });
            adaptive.UltraWideColumns = 3;
            var grid = go.GetComponent<GridLayoutGroup>();

            adaptive.Apply(LayoutMode.Portrait, 9f / 16f, config.ultraWideAspect);
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
            Assert.AreEqual(1, grid.constraintCount);
            Assert.AreEqual(new Vector2(960, 160), grid.cellSize);

            adaptive.Apply(LayoutMode.Landscape, 16f / 9f, config.ultraWideAspect);
            Assert.AreEqual(2, grid.constraintCount);
            Assert.AreEqual(new Vector2(30, 20), grid.spacing);

            adaptive.Apply(LayoutMode.Landscape, 21f / 9f, config.ultraWideAspect);
            Assert.AreEqual(3, grid.constraintCount);

            adaptive.UltraWideColumns = 0;
            adaptive.Apply(LayoutMode.Landscape, 21f / 9f, config.ultraWideAspect);
            Assert.AreEqual(2, grid.constraintCount, "0 ultra-wide columns falls back to the Landscape count");
        }

        // ───────────────────────── OrientationPolicy ─────────────────────────

        [Test]
        public void OrientationPolicy_GooglePlay_PhoneLockedTabletRotates()
        {
            var policy = Track(ScriptableObject.CreateInstance<OrientationPolicy>());
            policy.phone = OrientationRules.PortraitOnly;
            policy.tablet = OrientationRules.All;

            // 6.8" phablet 1440x3088 @ 500 dpi: diagonal ≈ 6.8", short side 2.9" → phone.
            Assert.IsFalse(policy.IsTablet(500f, 1440, 3088));
            OrientationRules phone = policy.Resolve(500f, 1440, 3088);
            Assert.AreEqual(1, phone.AllowedCount);
            Assert.IsTrue(phone.portrait);

            // 10" tablet 1920x1200 @ 224 dpi: diagonal ≈ 10.1", short side 5.4" → tablet.
            Assert.IsTrue(policy.IsTablet(224f, 1920, 1200));
            Assert.IsTrue(policy.Resolve(224f, 1920, 1200).AllowsLandscape);

            // Unknown DPI is treated as a phone (safe default keeps the portrait lock).
            Assert.IsFalse(policy.IsTablet(0f, 1920, 1200));
        }

        [TestCase(OrientationPolicy.GOOGLE_PLAY_RESOURCE_PATH, false)]
        [TestCase(OrientationPolicy.AMAZON_RESOURCE_PATH, true)]
        [TestCase(OrientationPolicy.WEB_RESOURCE_PATH, true)]
        public void OrientationPolicy_ResourceAssetsExist(string path, bool phoneAllowsLandscape)
        {
            var policy = Resources.Load<OrientationPolicy>(path);
            Assert.IsNotNull(policy, $"Missing Resources/{path}.asset — run ProjectB/Setup/Run Sprint 14.1 Adaptive Foundation Setup");
            Assert.AreEqual(phoneAllowsLandscape, policy.phone.AllowsLandscape);
            Assert.IsTrue(policy.tablet.AllowsLandscape);
        }

        [Test]
        public void LayoutConfig_ResourceAssetExists()
        {
            Assert.IsNotNull(Resources.Load<LayoutConfig>(LayoutConfig.RESOURCE_PATH));
        }

        // ───────────────────────── InputModeService ─────────────────────────

        [Test]
        public void InputModeService_SwitchesByLastDeviceAndRaisesEventOnce()
        {
            using var service = new InputModeService(InputMode.KeyboardMouse, listenToInputSystem: false);
            var events = new List<InputMode>();
            service.InputModeChanged += events.Add;

            Assert.IsTrue(service.RegisterActivity(InputMode.Gamepad, 1.0));
            Assert.IsFalse(service.RegisterActivity(InputMode.Gamepad, 1.1));
            Assert.IsTrue(service.RegisterActivity(InputMode.Touch, 2.0));
            Assert.IsTrue(service.RegisterActivity(InputMode.Gamepad, 2.1), "Gamepad is never suppressed by a recent touch");

            CollectionAssert.AreEqual(new[] { InputMode.Gamepad, InputMode.Touch, InputMode.Gamepad }, events);
        }

        [Test]
        public void InputModeService_SuppressesSyntheticMouseRightAfterTouch()
        {
            using var service = new InputModeService(InputMode.Touch, listenToInputSystem: false);
            service.RegisterActivity(InputMode.Touch, 10.0);

            Assert.IsFalse(service.RegisterActivity(InputMode.KeyboardMouse, 10.2), "Mouse within the suppress window is a synthesized touch");
            Assert.AreEqual(InputMode.Touch, service.Mode);

            Assert.IsTrue(service.RegisterActivity(InputMode.KeyboardMouse, 10.0 + InputModeService.MOUSE_AFTER_TOUCH_SUPPRESS_SECONDS + 0.01));
            Assert.AreEqual(InputMode.KeyboardMouse, service.Mode);
        }

        // ───────────────────────── 14.3 Multi-Input ─────────────────────────

        [Test]
        public void InputActionsAsset_Exists_WithRequiredGameplayAndUiActions()
        {
            var asset = Resources.Load<InputActionAsset>(GameInputService.INPUT_ACTIONS_RESOURCE_PATH);
            Assert.IsNotNull(asset, "Resources/ProjectB_InputActions.inputactions must exist");

            InputActionMap gameplay = asset.FindActionMap("Gameplay", throwIfNotFound: false);
            Assert.IsNotNull(gameplay, "Gameplay action map missing");
            Assert.IsNotNull(gameplay.FindAction("Move"), "Gameplay/Move missing");
            Assert.IsNotNull(gameplay.FindAction("Ultimate"), "Gameplay/Ultimate missing");
            Assert.IsNotNull(gameplay.FindAction("Pause"), "Gameplay/Pause missing");

            InputActionMap ui = asset.FindActionMap("UI", throwIfNotFound: false);
            Assert.IsNotNull(ui, "UI action map missing");
            foreach (string actionName in new[] { "Navigate", "Submit", "Cancel", "Point", "Click", "Card1", "Card2", "Card3", "Reroll" })
            {
                Assert.IsNotNull(ui.FindAction(actionName), $"UI/{actionName} missing");
            }
        }

        [Test]
        public void InputConfig_ResourceAssetExists_WithValidDeadzone()
        {
            var inputCfg = Resources.Load<InputConfig>(InputConfig.RESOURCE_PATH);
            Assert.IsNotNull(inputCfg, "Resources/InputConfig.asset must exist");
            Assert.GreaterOrEqual(inputCfg.moveDeadzone, 0f);
            Assert.LessOrEqual(inputCfg.moveDeadzone, 0.5f);
            Assert.IsTrue(inputCfg.floatingJoystickInLandscape);
        }

        [Test]
        public void MoveInputService_AggregatesNormalizesAppliesDeadzone_AndIsZeroGc()
        {
            const float dz = 0.15f;

            // Within deadzone -> zero
            Assert.AreEqual(Vector2.zero, MoveInputService.Aggregate(new Vector2(0.08f, 0.08f), Vector2.zero, dz));

            // Full diagonal -> clamped to unit length
            Vector2 diag = MoveInputService.Aggregate(new Vector2(1f, 1f), Vector2.zero, dz);
            Assert.AreEqual(1f, diag.magnitude, 1e-4f);
            Assert.AreEqual(diag.x, diag.y, 1e-4f);

            // Combining keyboard + joystick never exceeds unit length
            Vector2 combined = MoveInputService.Aggregate(new Vector2(1f, 0f), new Vector2(0.8f, 0.8f), dz);
            Assert.LessOrEqual(combined.magnitude, 1.0001f);

            // Joystick-only passes through cleanly even when action input is inside deadzone
            Vector2 joyOnly = MoveInputService.Aggregate(new Vector2(0.05f, 0f), new Vector2(0f, 0.6f), dz);
            Assert.AreEqual(0f, joyOnly.x, 1e-4f);
            Assert.AreEqual(0.6f, joyOnly.y, 1e-4f);

            // Zero-GC check on steady-state MoveInputService.Direction
            using var gameInput = new GameInputService(null, cloneAsset: false);
            var inputCfg = Track(ScriptableObject.CreateInstance<InputConfig>());
            var moveService = new MoveInputService(gameInput, null, inputCfg);

            // Warm-up
            Vector2 warm = moveService.Direction;
            Assert.AreEqual(Vector2.zero, warm);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++)
            {
                Vector2 d = moveService.Direction;
                if (d.sqrMagnitude < 0f) Assert.Fail();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "MoveInputService.Direction must allocate 0 bytes");
        }

        [Test]
        public void VirtualJoystick_HidesOnKeyboardMouse_ShowsOnTouch_AndForcesFloatingInLandscape()
        {
            var go = Track(new GameObject("Joystick", typeof(RectTransform), typeof(CanvasGroup), typeof(VirtualJoystick)));
            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(bg.transform, false);

            var joystick = go.GetComponent<VirtualJoystick>();
            typeof(VirtualJoystick).GetField("background", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(joystick, bg.GetComponent<RectTransform>());
            typeof(VirtualJoystick).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(joystick, handle.GetComponent<RectTransform>());
            typeof(VirtualJoystick).GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(joystick, go.GetComponent<CanvasGroup>());
            typeof(VirtualJoystick).GetField("isFloating", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(joystick, false);

            using var inputMode = new InputModeService(InputMode.Touch, listenToInputSystem: false);
            var layout = new LayoutModeService(config);
            layout.UpdateMetrics(1080, 1920, Full(1080, 1920));
            var inputCfg = Track(ScriptableObject.CreateInstance<InputConfig>());

            joystick.BindServices(inputMode, layout, inputCfg);
            var cg = go.GetComponent<CanvasGroup>();

            Assert.IsTrue(joystick.IsVisibleForTouch);
            Assert.IsFalse(joystick.IsFloating, "Portrait with isFloating=false stays fixed");
            Assert.AreEqual(1f, cg.alpha, 1e-4f);
            Assert.IsTrue(cg.blocksRaycasts);

            // Switch to Landscape -> forces floating
            layout.UpdateMetrics(1920, 1080, Full(1920, 1080));
            Assert.IsTrue(joystick.IsFloating, "Landscape touch forces floating joystick");
            Assert.IsFalse(bg.activeSelf, "Floating joystick hides background until touched");

            // Switch to KeyboardMouse -> hidden & blocksRaycasts=false, GO remains active for future touch
            inputMode.RegisterActivity(InputMode.KeyboardMouse, 5.0);
            Assert.IsFalse(joystick.IsVisibleForTouch);
            Assert.AreEqual(0f, cg.alpha, 1e-4f);
            Assert.IsFalse(cg.blocksRaycasts);
            Assert.IsTrue(go.activeSelf);

            // Touch again -> visible
            inputMode.RegisterActivity(InputMode.Touch, 6.0);
            Assert.IsTrue(joystick.IsVisibleForTouch);
            Assert.AreEqual(1f, cg.alpha, 1e-4f);
            Assert.IsTrue(cg.blocksRaycasts);
        }

        [Test]
        public void KeyHintBadgeUI_VisibleOnlyInKeyboardMouseMode()
        {
            var host = Track(new GameObject("KeyHintHost", typeof(RectTransform), typeof(KeyHintBadgeUI)));
            var pill = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(host.transform, false);
            var txt = new GameObject("KeyLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            txt.transform.SetParent(pill.transform, false);

            var badge = host.GetComponent<KeyHintBadgeUI>();
            badge.EditorSetup(pill, pill.GetComponent<Image>(), txt, "1");

            using var inputMode = new InputModeService(InputMode.Touch, listenToInputSystem: false);
            badge.Bind(inputMode);
            Assert.IsFalse(badge.IsHintVisible, "Hidden in Touch mode");

            inputMode.RegisterActivity(InputMode.KeyboardMouse, 1.0);
            Assert.IsTrue(badge.IsHintVisible, "Visible in KeyboardMouse mode");

            badge.SetAllowed(false);
            Assert.IsFalse(badge.IsHintVisible, "Hidden when externally disallowed (e.g., disabled card)");

            badge.SetAllowed(true);
            inputMode.RegisterActivity(InputMode.Gamepad, 2.0);
            Assert.IsFalse(badge.IsHintVisible, "Hidden in Gamepad mode");
        }

        [Test]
        public void CardSelectionUI_Hotkeys123AndR_SelectCardsAndTriggerReroll()
        {
            var selRoot = Track(new GameObject("CardSelection", typeof(RectTransform), typeof(CardSelectionUI)));
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(selRoot.transform, false);

            var slots = new CardUI[3];
            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardUI));
                cardGo.transform.SetParent(panel.transform, false);
                slots[i] = cardGo.GetComponent<CardUI>();
            }

            var rerollGo = new GameObject("Reroll", typeof(RectTransform), typeof(Image), typeof(Button));
            rerollGo.transform.SetParent(panel.transform, false);

            var selection = selRoot.GetComponent<CardSelectionUI>();
            typeof(CardSelectionUI).GetField("selectionPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(selection, panel);
            typeof(CardSelectionUI).GetField("cardSlots", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(selection, slots);
            typeof(CardSelectionUI).GetField("rerollButton", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(selection, rerollGo.GetComponent<Button>());

            var cardA = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            cardA.cardName = "A";
            var cardB = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            cardB.cardName = "B";
            var cardC = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            cardC.cardName = "C";

            CardData chosen = null;
            bool rerolled = false;
            selection.ShowCards(
                new List<CardData> { cardA, cardB },
                c => chosen = c,
                (_, __) =>
                {
                    rerolled = true;
                    return new List<CardData> { cardC, cardA };
                },
                freeRerolls: 1);

            Assert.IsTrue(selection.CanRerollCurrentOffer);
            selection.TriggerReroll();
            Assert.IsTrue(rerolled, "Reroll action should invoke reroll callback");

            Assert.IsTrue(selection.TrySelectSlotByIndex(1), "Slot 2 hotkey should select second card");
            Assert.AreSame(cardA, chosen);
        }

        [Test]
        public void CardUI_SubmitHandler_TriggersSelection()
        {
            var go = Track(new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardUI)));
            var cardUi = go.GetComponent<CardUI>();
            var data = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            data.cardName = "MaxHP";

            CardData picked = null;
            cardUi.Setup(data, c => picked = c, requiresAd: false, canWatchAd: true);
            cardUi.OnSubmit(null);
            Assert.AreSame(data, picked);
        }

        [Test]
        public void ThemedButton_HoverSelectAndDisable_UpdatesScaleCleanly()
        {
            var go = Track(new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ThemedButton)));
            var btn = go.GetComponent<Button>();
            var themed = go.GetComponent<ThemedButton>();

            themed.OnPointerEnter(null);
            Assert.IsTrue(themed.IsHovered);
            Assert.Greater(go.transform.localScale.x, 1.01f);

            themed.OnPointerExit(null);
            Assert.IsFalse(themed.IsHovered);
            Assert.AreEqual(1f, go.transform.localScale.x, 1e-4f);

            themed.OnSelect(null);
            Assert.IsTrue(themed.IsSelected);
            Assert.Greater(go.transform.localScale.x, 1.01f);

            btn.interactable = false;
            themed.OnDeselect(null);
            Assert.AreEqual(1f, go.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void PauseMenuUI_CancelAndPauseAction_ClosesModalsAndTogglesPause()
        {
            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("Hero"));
            var heroHp = heroGo.AddComponent<HeroHealth>();
            heroHp.Construct(new HeroRuntimeStats(), bus, null);

            var gmGo = Track(new GameObject("GM", typeof(GameManager)));
            var gm = gmGo.GetComponent<GameManager>();
            gm.Construct(heroHp, null, null, null, null, bus);
            typeof(GameManager).GetField("currentState", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gm, GameState.Playing);

            var pauseRoot = Track(new GameObject("PauseUI", typeof(RectTransform), typeof(PauseMenuUI)));
            var pausePanel = new GameObject("PausePanel", typeof(RectTransform));
            pausePanel.transform.SetParent(pauseRoot.transform, false);
            pausePanel.SetActive(false);

            var quitPanel = new GameObject("QuitConfirm", typeof(RectTransform));
            quitPanel.transform.SetParent(pausePanel.transform, false);
            quitPanel.SetActive(false);

            var settingsGo = Track(new GameObject("Settings", typeof(RectTransform), typeof(SettingsPopupUI)));
            var settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            settingsPanel.transform.SetParent(settingsGo.transform, false);
            settingsPanel.SetActive(false);
            var settings = settingsGo.GetComponent<SettingsPopupUI>();
            typeof(SettingsPopupUI).GetField("popupPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(settings, settingsPanel);

            var pauseUi = pauseRoot.GetComponent<PauseMenuUI>();
            typeof(PauseMenuUI).GetField("pausePanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pauseUi, pausePanel);
            typeof(PauseMenuUI).GetField("quitConfirmPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pauseUi, quitPanel);
            typeof(PauseMenuUI).GetField("settingsPopup", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pauseUi, settings);
            pauseUi.Construct(gm);

            // Esc in Playing -> opens Pause
            pauseUi.HandlePauseOrBackInput();
            Assert.IsTrue(gm.IsPaused);
            Assert.IsTrue(pauseUi.IsPanelOpen);

            // Open Settings -> Esc closes Settings first, keeps Pause open
            pauseUi.OnSettingsClicked();
            Assert.IsTrue(settings.IsOpen);
            pauseUi.HandlePauseOrBackInput();
            Assert.IsFalse(settings.IsOpen);
            Assert.IsTrue(pauseUi.IsPanelOpen);

            // Open QuitConfirm -> Esc closes QuitConfirm first
            pauseUi.OnQuitToMenuClicked();
            Assert.IsTrue(pauseUi.IsQuitConfirmOpen);
            pauseUi.HandlePauseOrBackInput();
            Assert.IsFalse(pauseUi.IsQuitConfirmOpen);
            Assert.IsTrue(pauseUi.IsPanelOpen);

            // Esc in Pause -> resumes game
            pauseUi.HandlePauseOrBackInput();
            Assert.IsFalse(gm.IsPaused);
            Assert.IsFalse(pauseUi.IsPanelOpen);
        }

        [Test]
        public void MainMenuUI_CancelAction_ClosesPopupsAndReturnsToPlayTab()
        {
            bool hadSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            string origSave = hadSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);

            try
            {
                using var save = new SaveManager();
                var bonus = new DailyBonusManager(save);

                var menuGo = Track(new GameObject("MainMenu", typeof(RectTransform), typeof(MainMenuUI)));
                var playPanel = new GameObject("PlayPanel", typeof(RectTransform));
                playPanel.transform.SetParent(menuGo.transform, false);

                var settingsGo = Track(new GameObject("Settings", typeof(RectTransform), typeof(SettingsPopupUI)));
                var settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
                settingsPanel.transform.SetParent(settingsGo.transform, false);
                settingsPanel.SetActive(false);
                var settings = settingsGo.GetComponent<SettingsPopupUI>();
                typeof(SettingsPopupUI).GetField("popupPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(settings, settingsPanel);

                var menu = menuGo.GetComponent<MainMenuUI>();
                typeof(MainMenuUI).GetField("playScreenPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(menu, playPanel);
                typeof(MainMenuUI).GetField("settingsPopupUI", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(menu, settings);
                menu.Construct(save, bonus);

                // Switch to Abilities tab
                menu.OnAbilitiesClicked();
                Assert.AreEqual(MainMenuUI.TAB_ABILITIES, menu.SelectedTab);

                // Open settings -> Cancel closes settings first
                menu.OnSettingsClicked();
                Assert.IsTrue(settings.IsOpen);
                menu.HandleCancelInput();
                Assert.IsFalse(settings.IsOpen);
                Assert.AreEqual(MainMenuUI.TAB_ABILITIES, menu.SelectedTab);

                // Cancel on non-Play tab -> returns to Play tab
                menu.HandleCancelInput();
                Assert.AreEqual(MainMenuUI.TAB_PLAY, menu.SelectedTab);
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(SaveManager.SAVE_KEY, origSave);
                else PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void Scenes_GameplayAndMainMenu_HaveInputModuleAndKeyHintsWired()
        {
            var expectedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Sprint14InputWiringTool.INPUT_ACTIONS_PATH);
            Assert.IsNotNull(expectedAsset, "ProjectB_InputActions must exist");

            foreach (string scenePath in new[] { Sprint14InputWiringTool.MAIN_MENU_SCENE, Sprint14InputWiringTool.GAMEPLAY_SCENE })
            {
                Scene loaded = SceneManager.GetSceneByPath(scenePath);
                bool openedHere = !loaded.isLoaded;
                Scene scene = openedHere ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive) : loaded;
                try
                {
                    InputSystemUIInputModule module = null;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        module = root.GetComponentInChildren<InputSystemUIInputModule>(true);
                        if (module != null) break;
                    }
                    Assert.IsNotNull(module, $"InputSystemUIInputModule missing in {scenePath}");
                    Assert.AreSame(expectedAsset, module.actionsAsset, $"UI InputModule must use ProjectB_InputActions in {scenePath}");
                    Assert.IsNotNull(module.point?.action, $"UI/Point unbound in {scenePath}");
                    Assert.IsNotNull(module.leftClick?.action, $"UI/Click unbound in {scenePath}");
                    Assert.IsNotNull(module.move?.action, $"UI/Navigate unbound in {scenePath}");
                    Assert.IsNotNull(module.submit?.action, $"UI/Submit unbound in {scenePath}");
                    Assert.IsNotNull(module.cancel?.action, $"UI/Cancel unbound in {scenePath}");

                    if (scenePath == Sprint14InputWiringTool.GAMEPLAY_SCENE)
                    {
                        VirtualJoystick joystick = null;
                        CardSelectionUI cardSel = null;
                        UltimateButtonUI ultBtn = null;
                        PauseMenuUI pauseUi = null;
                        GameOverUI gameOver = null;

                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            if (joystick == null) joystick = root.GetComponentInChildren<VirtualJoystick>(true);
                            if (cardSel == null) cardSel = root.GetComponentInChildren<CardSelectionUI>(true);
                            if (ultBtn == null) ultBtn = root.GetComponentInChildren<UltimateButtonUI>(true);
                            if (pauseUi == null) pauseUi = root.GetComponentInChildren<PauseMenuUI>(true);
                            if (gameOver == null) gameOver = root.GetComponentInChildren<GameOverUI>(true);
                        }

                        Assert.IsNotNull(joystick.GetComponent<CanvasGroup>(), "VirtualJoystick must have CanvasGroup");
                        Assert.IsNotNull(typeof(UltimateButtonUI).GetField("keyHintBadge", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ultBtn), "Ultimate keyHintBadge missing");
                        Assert.IsNotNull(typeof(PauseMenuUI).GetField("pauseKeyHint", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(pauseUi), "Pause keyHint missing");
                        Assert.IsNotNull(typeof(CardSelectionUI).GetField("rerollKeyHint", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(cardSel), "Reroll keyHint missing");
                        Assert.IsNotNull(typeof(GameOverUI).GetField("restartButton", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(gameOver), "GameOver restartButton missing");
                        Assert.IsNotNull(typeof(GameOverUI).GetField("menuButton", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(gameOver), "GameOver menuButton missing");

                        var slots = (CardUI[])typeof(CardSelectionUI).GetField("cardSlots", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(cardSel);
                        Assert.AreEqual(3, slots.Length);
                        for (int i = 0; i < slots.Length; i++)
                        {
                            Assert.IsNotNull(typeof(CardUI).GetField("keyHintBadge", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(slots[i]), $"Card {i} keyHintBadge missing");
                        }
                    }
                }
                finally
                {
                    if (openedHere) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
