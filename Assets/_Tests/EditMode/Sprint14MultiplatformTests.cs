using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Ads;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Data.Meta;
using ProjectB.Data.Platform;
using ProjectB.EditorScripts;
using ProjectB.Enemies;
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
            Assert.AreEqual(0.4f, inputCfg.cardSelectInputDelay, 1e-4f);
        }

        [Test]
        public void GameInputService_FallbackAndProjectAsset_PauseHasEscapeAndP()
        {
            var projectAsset = Resources.Load<InputActionAsset>(GameInputService.INPUT_ACTIONS_RESOURCE_PATH);
            Assert.IsNotNull(projectAsset, "ProjectB_InputActions must exist");
            AssertHasPauseBindings(projectAsset, "ProjectB_InputActions");

            using var fallbackService = new GameInputService(null, cloneAsset: false);
            Assert.IsNotNull(fallbackService.PauseAction, "Fallback PauseAction must exist");
            AssertHasPauseBindings(fallbackService.ActionsAsset, "FallbackAsset");
        }

        private static void AssertHasPauseBindings(InputActionAsset asset, string label)
        {
            InputAction pause = asset.FindActionMap("Gameplay", throwIfNotFound: true).FindAction("Pause", throwIfNotFound: true);
            bool hasEscape = false;
            bool hasP = false;
            for (int i = 0; i < pause.bindings.Count; i++)
            {
                string path = pause.bindings[i].effectivePath;
                if (string.Equals(path, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase)) hasEscape = true;
                else if (string.Equals(path, "<Keyboard>/p", StringComparison.OrdinalIgnoreCase)) hasP = true;
            }
            Assert.IsTrue(hasEscape, $"{label}: Gameplay/Pause must include <Keyboard>/escape");
            Assert.IsTrue(hasP, $"{label}: Gameplay/Pause must include <Keyboard>/p");
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
        public void CardSelectionUI_InputDelay_BlocksEarlySelectionAndReArmsOnReroll_AndSelectsFreeThirdCard()
        {
            bool createdEs = EventSystem.current == null;
            GameObject esGo = null;
            EventSystem es = EventSystem.current;
            if (createdEs)
            {
                esGo = Track(new GameObject("TestEventSystem", typeof(EventSystem)));
                es = esGo.GetComponent<EventSystem>();
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(es, null);
                EventSystem.current = es;
            }

            try
            {
                EventSystem.current.SetSelectedGameObject(null);

                var selRoot = Track(new GameObject("DelayedCardSelection", typeof(RectTransform), typeof(CardSelectionUI)));
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

                var inputCfg = Track(ScriptableObject.CreateInstance<InputConfig>());
                inputCfg.cardSelectInputDelay = 0.4f;

                using var inputMode = new InputModeService(InputMode.KeyboardMouse, listenToInputSystem: false);
                var mockAds = new MockAdProvider { IsRewardedReady = true, SimulateRewardGranted = false };
                var adConfig = Track(ScriptableObject.CreateInstance<AdConfig>());
                adConfig.maxRewardedPerSession = 10;
                adConfig.maxRewardedPerDay = 20;
                var adManager = new AdManager(null, adConfig, mockAds);

                selection.Construct(adManager, null, null, null, null, inputMode, inputCfg);

                float fakeTime = 10f;
                selection.SetUnscaledTimeProviderForTest(() => fakeTime);

                var cardA = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
                cardA.cardName = "CardA";
                var cardB = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
                cardB.cardName = "CardB";
                var cardC = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
                cardC.cardName = "CardC";
                var cardD = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
                cardD.cardName = "CardD";

                CardData chosen = null;
                bool rerolled = false;
                selection.ShowCards(
                    new List<CardData> { cardA, cardB, cardC },
                    c => chosen = c,
                    (_, __) =>
                    {
                        rerolled = true;
                        return new List<CardData> { cardD, cardA, cardC };
                    },
                    freeRerolls: 1);

                // During the 0.4s guard window: no initial focus, and every input type is ignored.
                Assert.IsTrue(selection.IsInputLocked);
                selection.TickInputLock();
                Assert.IsNull(EventSystem.current.currentSelectedGameObject, "Focus must not be placed on Card 0 during the delay");

                fakeTime = 10.35f;
                selection.TickInputLock();
                slots[0].OnPointerClick(null);
                slots[0].OnSubmit(null);
                Assert.IsFalse(selection.TrySelectSlotByIndex(0));
                Assert.IsFalse(selection.TrySelectSlotByIndex(1));
                Assert.IsFalse(selection.TrySelectSlotByIndex(2));
                selection.TriggerReroll();
                Assert.IsNull(chosen, "Early card selection within 0.4s must be ignored");
                Assert.IsFalse(rerolled, "Early reroll within 0.4s must be ignored");
                Assert.IsNull(EventSystem.current.currentSelectedGameObject);

                // After 0.4s expires: focus moves to Card 0 and reroll works.
                fakeTime = 10.45f;
                selection.TickInputLock();
                Assert.IsFalse(selection.IsInputLocked);
                Assert.AreSame(slots[0].gameObject, EventSystem.current.currentSelectedGameObject, "Focus moves to first card once delay expires");

                selection.TriggerReroll();
                Assert.IsTrue(rerolled, "Reroll should succeed after the delay");
                Assert.IsTrue(selection.IsInputLocked, "Reroll must re-arm the 0.4s input guard");
                Assert.IsNull(EventSystem.current.currentSelectedGameObject, "Focus clears on reroll during the new delay");

                // During the post-reroll 0.4s guard window: selection is blocked again.
                fakeTime = 10.80f;
                selection.TickInputLock();
                slots[0].OnSubmit(null);
                slots[1].OnPointerClick(null);
                Assert.IsFalse(selection.TrySelectSlotByIndex(0));
                Assert.IsNull(chosen, "Selection immediately after reroll must be ignored");

                // After post-reroll 0.4s expires: unlocked again, and 3rd card is free (no rewarded ad).
                fakeTime = 10.90f;
                selection.TickInputLock();
                Assert.IsFalse(selection.IsInputLocked);
                Assert.AreSame(slots[0].gameObject, EventSystem.current.currentSelectedGameObject);

                Assert.IsTrue(selection.TrySelectSlotByIndex(2));
                Assert.AreEqual(0, mockAds.RewardedShowCalls, "3rd card is free and must not request a rewarded ad");
                Assert.AreSame(cardC, chosen, "3rd card should be selected immediately without ads");
            }
            finally
            {
                if (createdEs && es != null)
                {
                    typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(es, null);
                }
            }
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

                var dailyGo = Track(new GameObject("DailyBonus", typeof(RectTransform), typeof(DailyBonusScreenUI)));
                var dailyPanel = new GameObject("PopupPanel", typeof(RectTransform));
                dailyPanel.transform.SetParent(dailyGo.transform, false);
                dailyPanel.SetActive(false);
                var daily = dailyGo.GetComponent<DailyBonusScreenUI>();
                typeof(DailyBonusScreenUI).GetField("popupPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(daily, dailyPanel);

                var menu = menuGo.GetComponent<MainMenuUI>();
                typeof(MainMenuUI).GetField("playScreenPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(menu, playPanel);
                typeof(MainMenuUI).GetField("settingsPopupUI", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(menu, settings);
                typeof(MainMenuUI).GetField("dailyBonusScreenUI", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(menu, daily);
                menu.Construct(save, bonus);

                // Switch to Abilities tab
                menu.OnAbilitiesClicked();
                Assert.AreEqual(MainMenuUI.TAB_ABILITIES, menu.SelectedTab);

                // Open Daily Bonus modal -> Cancel closes Daily Bonus without leaving Abilities tab
                menu.OnBonusClicked();
                Assert.IsTrue(daily.IsOpen);
                Assert.AreEqual(MainMenuUI.TAB_ABILITIES, menu.SelectedTab);
                menu.HandleCancelInput();
                Assert.IsFalse(daily.IsOpen);
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
                        var pauseHint = (KeyHintBadgeUI)typeof(PauseMenuUI).GetField("pauseKeyHint", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(pauseUi);
                        Assert.IsNotNull(pauseHint, "Pause keyHint missing");
                        Assert.AreEqual("P", pauseHint.KeyLabel, "Pause keyHint must display P (Esc is caught by browser fullscreen)");
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

        // ───────────────────────── 14.2 Camera & Spawn Ring ─────────────────────────

        [Test]
        public void CameraConfig_ResourceAssetExists_WithValidDefaults()
        {
            var camCfg = Resources.Load<CameraConfig>(CameraConfig.RESOURCE_PATH);
            Assert.IsNotNull(camCfg, "Resources/CameraConfig.asset must exist");
            Assert.AreEqual(35.98f, camCfg.shortSideFov, 0.05f);
            Assert.AreEqual(50f, camCfg.landscapeShortSideFov, 0.05f);
            Assert.AreEqual(21f / 9f, camCfg.maxAspectRatio, 0.01f);
            Assert.Greater(camCfg.spawnMargin, 0.5f);
            Assert.Greater(camCfg.spawnBandWidth, 0.5f);
            Assert.IsFalse(camCfg.spawnSummonsOffscreen, "Summons must spawn near caster by default");
        }

        [Test]
        public void CameraController_ShortSideFov_PreservesPhonePortrait_AndClampsUltrawide()
        {
            var camCfg = Track(ScriptableObject.CreateInstance<CameraConfig>());

            // 9:16 phone portrait must match legacy vFOV = 60° (no shrinkage!)
            float fov9x16 = CameraController.CalculateVerticalFov(9f / 16f, camCfg);
            Assert.AreEqual(60.0f, fov9x16, 0.1f, "9:16 portrait must preserve 60 deg vertical FOV");

            // 9:21 narrow phone preserves horizontal short-side FOV
            float fov9x21 = CameraController.CalculateVerticalFov(9f / 21f, camCfg);
            float hFov9x21 = Camera.VerticalToHorizontalFieldOfView(fov9x21, 9f / 21f);
            Assert.AreEqual(camCfg.shortSideFov, hFov9x21, 0.1f);
            Assert.Greater(fov9x21, fov9x16, "9:21 must expand vertically without losing horizontal view");

            // Landscape 4:3, 16:10, 16:9, 21:9 use landscapeShortSideFov
            Assert.AreEqual(camCfg.landscapeShortSideFov, CameraController.CalculateVerticalFov(4f / 3f, camCfg), 0.05f);
            Assert.AreEqual(camCfg.landscapeShortSideFov, CameraController.CalculateVerticalFov(16f / 10f, camCfg), 0.05f);
            Assert.AreEqual(camCfg.landscapeShortSideFov, CameraController.CalculateVerticalFov(16f / 9f, camCfg), 0.05f);
            Assert.AreEqual(camCfg.landscapeShortSideFov, CameraController.CalculateVerticalFov(21f / 9f, camCfg), 0.05f);

            // 32:9 ultra-wide clamps horizontal FOV to 21:9's horizontal FOV at landscapeShortSideFov
            float fov32x9 = CameraController.CalculateVerticalFov(32f / 9f, camCfg);
            float hFov32x9 = Camera.VerticalToHorizontalFieldOfView(fov32x9, 32f / 9f);
            float hFov21x9 = Camera.VerticalToHorizontalFieldOfView(camCfg.landscapeShortSideFov, 21f / 9f);
            Assert.AreEqual(hFov21x9, hFov32x9, 0.15f, "32:9 must clamp horizontal FOV to 21:9");
        }

        [TestCase(16f / 9f, "16:9")]
        [TestCase(16f / 10f, "16:10")]
        [TestCase(21f / 9f, "21:9")]
        public void CameraController_LandscapeFov_RecalculatesOffscreenSpawn_WhenLandscapeFovChanges(float aspect, string label)
        {
            var camCfg = Track(ScriptableObject.CreateInstance<CameraConfig>());
            var camGo = Track(new GameObject($"LandscapeCam_{label}", typeof(Camera), typeof(CameraController)));
            var cam = camGo.GetComponent<Camera>();
            var ctrl = camGo.GetComponent<CameraController>();
            ctrl.offset = new Vector3(0f, 20f, -12f);
            camGo.transform.position = ctrl.offset;
            camGo.transform.rotation = Quaternion.LookRotation(-ctrl.offset);
            cam.aspect = aspect;

            camCfg.landscapeShortSideFov = 40f;
            ctrl.BindServices(null, camCfg);
            ctrl.RefreshForAspect(aspect, force: true);
            Assert.AreEqual(40f, cam.fieldOfView, 0.05f);
            float spawnMinAt40 = ctrl.CachedSpawnRadiusMin;

            camCfg.landscapeShortSideFov = 55f;
            ctrl.RefreshForAspect(aspect, force: true);
            Assert.AreEqual(55f, cam.fieldOfView, 0.05f);
            Assert.Greater(ctrl.CachedSpawnRadiusMin, spawnMinAt40, $"{label}: larger landscapeShortSideFov must expand off-screen spawn ring");

            for (int deg = 0; deg < 360; deg += 15)
            {
                float rad = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 pos = dir * ctrl.GetOffscreenSpawnDistance(dir);
                Vector3 vp = cam.WorldToViewportPoint(pos);
                bool inside = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                Assert.IsFalse(inside, $"{label} @ 55° angle {deg}: spawn {pos} inside viewport {vp}");
            }
        }

        [TestCase(9f / 21f, "9:21")]
        [TestCase(9f / 16f, "9:16")]
        [TestCase(4f / 3f, "4:3")]
        [TestCase(16f / 10f, "16:10")]
        [TestCase(16f / 9f, "16:9")]
        [TestCase(21f / 9f, "21:9")]
        [TestCase(32f / 9f, "32:9")]
        public void CameraController_SpawnRing_IsStrictlyOffscreen_AcrossAllAspects(float aspect, string label)
        {
            var camCfg = Track(ScriptableObject.CreateInstance<CameraConfig>());
            var camGo = Track(new GameObject($"Cam_{label}", typeof(Camera), typeof(CameraController)));
            var cam = camGo.GetComponent<Camera>();
            var ctrl = camGo.GetComponent<CameraController>();

            ctrl.offset = new Vector3(0f, 20f, -12f);
            camGo.transform.position = ctrl.offset;
            camGo.transform.rotation = Quaternion.LookRotation(-ctrl.offset);
            cam.aspect = aspect;

            ctrl.BindServices(null, camCfg);
            ctrl.RefreshForAspect(aspect, force: true);

            for (int deg = 0; deg < 360; deg += 10)
            {
                float rad = deg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                float dist = ctrl.GetOffscreenSpawnDistance(dir);
                Vector3 worldPos = dir * dist;
                Vector3 vp = cam.WorldToViewportPoint(worldPos);
                bool insideViewport = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                Assert.IsFalse(insideViewport, $"{label} angle {deg}: directional spawn at {worldPos} (vp={vp}) appeared inside viewport!");

                Vector3 ringPos = dir * ctrl.CachedSpawnRadiusMin;
                Vector3 ringVp = cam.WorldToViewportPoint(ringPos);
                bool ringInside = ringVp.z > 0f && ringVp.x >= 0f && ringVp.x <= 1f && ringVp.y >= 0f && ringVp.y <= 1f;
                Assert.IsFalse(ringInside, $"{label} angle {deg}: circular ring spawn at {ringPos} (vp={ringVp}) appeared inside viewport!");
            }
        }

        [Test]
        public void CameraController_CacheRecalculatesOnLayoutModeChangedAndResize_WithZeroGc()
        {
            var camCfg = Track(ScriptableObject.CreateInstance<CameraConfig>());
            var camGo = Track(new GameObject("CachedCam", typeof(Camera), typeof(CameraController)));
            var cam = camGo.GetComponent<Camera>();
            var ctrl = camGo.GetComponent<CameraController>();
            ctrl.offset = new Vector3(0f, 20f, -12f);
            camGo.transform.position = ctrl.offset;
            camGo.transform.rotation = Quaternion.LookRotation(-ctrl.offset);

            var layout = new LayoutModeService(config);
            layout.UpdateMetrics(1080, 1920, Full(1080, 1920));
            ctrl.BindServices(layout, camCfg);

            int v0 = ctrl.FrustumCacheVersion;
            float portraitRadius = ctrl.CachedSpawnRadiusMin;

            // Resize within Portrait (9:21) -> ScreenChanged recalculates cache
            layout.UpdateMetrics(1080, 2520, Full(1080, 2520));
            int v1 = ctrl.FrustumCacheVersion;
            Assert.Greater(v1, v0);
            Assert.Greater(ctrl.CachedSpawnRadiusMin, portraitRadius, "Taller 9:21 frustum needs larger spawn radius");

            // Flip to Landscape (16:9) -> LayoutModeChanged + ScreenChanged recalculates cache
            layout.UpdateMetrics(1920, 1080, Full(1920, 1080));
            int v2 = ctrl.FrustumCacheVersion;
            Assert.Greater(v2, v1);

            // Steady-state directional queries allocate 0 bytes
            Vector3 probeDir = new Vector3(1f, 0f, 0.5f).normalized;
            _ = ctrl.GetOffscreenSpawnDistance(probeDir);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++)
            {
                float d = ctrl.GetOffscreenSpawnDistance(probeDir);
                if (d <= 0f) Assert.Fail();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "GetOffscreenSpawnDistance must allocate 0 bytes");
        }

        [Test]
        public void WaveManager_AndWaveEventManager_OffscreenSpawns_ApplyToWavesCorridorSwarmAndSummons()
        {
            var camCfg = Track(ScriptableObject.CreateInstance<CameraConfig>());
            var camGo = Track(new GameObject("SpawnCam", typeof(Camera), typeof(CameraController)));
            var cam = camGo.GetComponent<Camera>();
            var ctrl = camGo.GetComponent<CameraController>();
            ctrl.offset = new Vector3(0f, 20f, -12f);
            camGo.transform.position = ctrl.offset;
            camGo.transform.rotation = Quaternion.LookRotation(-ctrl.offset);
            cam.aspect = 16f / 9f;
            ctrl.BindServices(null, camCfg);
            ctrl.RefreshForAspect(16f / 9f, force: true);

            var mob = Track(ScriptableObject.CreateInstance<EnemyData>());
            mob.name = "OffscreenMob";
            mob.hp = 50;
            mob.speed = 3f;
            mob.contactDamage = 5;

            var waveCfg = Track(ScriptableObject.CreateInstance<WaveConfig>());
            waveCfg.spawnRadiusMin = 10f;
            waveCfg.spawnRadiusMax = 15f;
            waveCfg.waveEventsEnabled = false;
            waveCfg.swarmRushMinCount = 12;
            waveCfg.swarmRushMaxCount = 12;
            waveCfg.swarmRushRadius = 10f;
            waveCfg.swarmRushEnemyData = mob;
            waveCfg.corridorHordeEnemyData = mob;
            waveCfg.corridorHordeBatchSize = 6;
            waveCfg.corridorHordeCorridorHalfWidth = 3.5f;
            waveCfg.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = mob, minWave = 1, maxWave = 0, spawnWeight = 1f, packSize = 1, triggerIntroWave = false }
            };

            var bus = new GameEventBus();
            var heroGo = Track(new GameObject("SpawnHero"));
            var heroHp = heroGo.AddComponent<HeroHealth>();
            heroHp.Construct(new HeroRuntimeStats(), bus, null);

            var waveGo = Track(new GameObject("OffscreenWaveMgr", typeof(WaveManager)));
            var waveMgr = waveGo.GetComponent<WaveManager>();
            typeof(WaveManager).GetField("waveConfig", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(waveMgr, waveCfg);
            waveMgr.Construct(heroHp, null, bus, null, null, null, null, new EnemyRuntimeModifiers(), null, null, ctrl, camCfg);
            waveMgr.EnsurePoolsInitialized();

            // 1. Regular wave spawn positions (including pack extra margin) must be off-screen on 16:9
            for (int i = 0; i < 40; i++)
            {
                Vector3 pos = waveMgr.GetValidSpawnPosition(extraMargin: 1.8f);
                Vector3 vp = cam.WorldToViewportPoint(pos);
                bool inside = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                Assert.IsFalse(inside, $"Wave spawn #{i} at {pos} (vp={vp}) appeared inside 16:9 viewport");
            }

            // 2. Boss / VoidSummoner summoned minions spawn near caster when spawnSummonsOffscreen = false (default)
            Assert.IsFalse(camCfg.spawnSummonsOffscreen);
            MethodInfo summonMethod = typeof(WaveManager).GetMethod("SpawnSummonedMinion", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(summonMethod);
            Vector3 nearPos = new Vector3(3f, 0f, 2f);
            summonMethod.Invoke(waveMgr, new object[] { mob, nearPos });
            Assert.AreEqual(1, waveMgr.EnemiesAlive);
            foreach (EnemyBase e in waveMgr.ActiveEnemies)
            {
                Assert.Less(Vector3.Distance(e.transform.position, nearPos), 0.01f, $"Summoned minion must appear at nearPosition when spawnSummonsOffscreen=false (got {e.transform.position})");
                waveMgr.DespawnTrackedEnemy(e);
                break;
            }

            // Optional toggle spawnSummonsOffscreen = true still pushes summons off-screen
            camCfg.spawnSummonsOffscreen = true;
            summonMethod.Invoke(waveMgr, new object[] { mob, nearPos });
            Assert.AreEqual(1, waveMgr.EnemiesAlive);
            foreach (EnemyBase e in waveMgr.ActiveEnemies)
            {
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
                bool inside = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                Assert.IsFalse(inside, $"Summoned minion at {e.transform.position} (vp={vp}) spawned inside 16:9 viewport when spawnSummonsOffscreen=true");
                waveMgr.DespawnTrackedEnemy(e);
                break;
            }
            camCfg.spawnSummonsOffscreen = false;

            // 3. CorridorHorde & SwarmRush in WaveEventManager must spawn strictly off-screen
            var evtGo = Track(new GameObject("OffscreenEventMgr", typeof(WaveEventManager)));
            var evtMgr = evtGo.GetComponent<WaveEventManager>();
            typeof(WaveEventManager).GetField("overrideConfig", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(evtMgr, waveCfg);
            evtMgr.Construct(waveMgr, heroHp, null, null, null, bus, null, new EnemyRuntimeModifiers());

            Assert.IsTrue(evtMgr.StartEvent(WaveEventType.CorridorHorde));
            Assert.AreEqual(6, waveMgr.EnemiesAlive);
            foreach (EnemyBase e in waveMgr.ActiveEnemies)
            {
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
                bool inside = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                Assert.IsFalse(inside, $"CorridorHorde enemy at {e.transform.position} (vp={vp}) spawned inside 16:9 viewport");
            }

            waveMgr.KillAllActiveEnemies();
            Assert.IsTrue(evtMgr.StartEvent(WaveEventType.SwarmRush));
            Assert.Greater(waveMgr.EnemiesAlive, 0);
            foreach (EnemyBase e in waveMgr.ActiveEnemies)
            {
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
                bool inside = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                Assert.IsFalse(inside, $"SwarmRush enemy at {e.transform.position} (vp={vp}) spawned inside 16:9 viewport");
            }
        }

        [Test]
        public void SecretCompassIndicatorUI_ClampsToHudZones_ForPortraitAndLandscape()
        {
            var camCfg = Track(ScriptableObject.CreateInstance<CameraConfig>());
            var camGo = Track(new GameObject("CompassCam", typeof(Camera), typeof(CameraController)));
            var cam = camGo.GetComponent<Camera>();
            var ctrl = camGo.GetComponent<CameraController>();
            ctrl.offset = new Vector3(0f, 20f, -12f);
            camGo.transform.position = ctrl.offset;
            camGo.transform.rotation = Quaternion.LookRotation(-ctrl.offset);

            var layout = new LayoutModeService(config);
            layout.UpdateMetrics(1080, 1920, Full(1080, 1920));

            var compassGo = Track(new GameObject("LayoutCompass", typeof(RectTransform), typeof(SecretCompassIndicatorUI)));
            var compass = compassGo.GetComponent<SecretCompassIndicatorUI>();
            compass.BindLayout(layout, camCfg);

            // Portrait: 1080x1920 -> topPadding 240, bottomPadding 260, side 85
            cam.aspect = 9f / 16f;
            ctrl.BindServices(layout, camCfg);
            ctrl.RefreshForAspect(9f / 16f, force: true);
            Assert.IsTrue(compass.EvaluateIndicatorState(Vector3.zero, new Vector3(0f, 0f, 40f), cam, new Vector2(1080f, 1920f), 60f, out Vector2 topPortrait, out _, out _));
            Assert.LessOrEqual(topPortrait.y, 1920f * 0.5f - camCfg.portraitCompassMargins.top + 0.1f);

            // Flip to Landscape 1920x1080 -> left 220, right 200, top 110, bottom 90
            layout.UpdateMetrics(1920, 1080, Full(1920, 1080));
            cam.aspect = 16f / 9f;
            Assert.AreEqual(camCfg.landscapeCompassMargins.left, compass.ActiveMargins.left, 0.01f);

            Assert.IsTrue(compass.EvaluateIndicatorState(Vector3.zero, new Vector3(-40f, 0f, 0f), cam, new Vector2(1920f, 1080f), 60f, out Vector2 leftLand, out _, out _));
            Assert.GreaterOrEqual(leftLand.x, -1920f * 0.5f + camCfg.landscapeCompassMargins.left - 0.1f, "Left HUD zone must be respected in Landscape");

            Assert.IsTrue(compass.EvaluateIndicatorState(Vector3.zero, new Vector3(40f, 0f, 0f), cam, new Vector2(1920f, 1080f), 60f, out Vector2 rightLand, out _, out _));
            Assert.LessOrEqual(rightLand.x, 1920f * 0.5f - camCfg.landscapeCompassMargins.right + 0.1f, "Right HUD zone must be respected in Landscape");

            Assert.IsTrue(compass.EvaluateIndicatorState(Vector3.zero, new Vector3(0f, 0f, -40f), cam, new Vector2(1920f, 1080f), 60f, out Vector2 bottomLand, out _, out _));
            Assert.GreaterOrEqual(bottomLand.y, -1080f * 0.5f + camCfg.landscapeCompassMargins.bottom - 0.1f, "Bottom HUD zone must be respected in Landscape");
        }

        // ───────────────────────── 14.4 Landscape Layouts & HUD Consolidation ─────────────────────────

        [Test]
        public void MainMenu_LandscapeLayouts_HaveLeftNavRail_Grids_DailyRow_AndRightConstellationSheet()
        {
            const string mainMenuScenePath = "Assets/_Scenes/MainMenu.unity";
            Scene loaded = SceneManager.GetSceneByPath(mainMenuScenePath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(mainMenuScenePath, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform safe = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    safe = root.transform.Find("SafeAreaRoot");
                    if (safe != null) break;
                }
                Assert.IsNotNull(safe, "MainMenu SafeAreaRoot missing");

                Transform bottomBar = safe.Find("BottomBar");
                Assert.IsNotNull(bottomBar);
                var bottomAdaptive = bottomBar.GetComponent<AdaptiveLayoutGroup>();
                Assert.IsNotNull(bottomAdaptive, "BottomBar must use AdaptiveLayoutGroup");
                Assert.AreEqual(LayoutAxis.Vertical, bottomAdaptive.GetSettings(LayoutMode.Landscape).axis, "Landscape nav rail must stack vertically");
                var bottomRr = bottomBar.GetComponent<ResponsiveRect>();
                Assert.IsNotNull(bottomRr);
                Assert.IsTrue(bottomRr.HasPreset(LayoutMode.Landscape));
                Assert.LessOrEqual(bottomRr.GetPreset(LayoutMode.Landscape).anchorMax.x, 0.01f, "Left rail anchored on left edge");

                foreach (string panelName in new[] { "UpgradesPanel", "AbilitiesPanel", "AchievementsPanel" })
                {
                    Transform panel = safe.Find(panelName);
                    Assert.IsNotNull(panel, panelName);
                    var panelRr = panel.GetComponent<ResponsiveRect>();
                    Assert.IsNotNull(panelRr, $"{panelName} needs ResponsiveRect");
                    Assert.IsTrue(panelRr.HasPreset(LayoutMode.Landscape));

                    Transform content = panel.Find("ScrollView/Viewport/Content");
                    Assert.IsNotNull(content, $"{panelName} ScrollView/Viewport/Content missing");
                    var adaptiveGrid = content.GetComponent<AdaptiveGridLayout>();
                    Assert.IsNotNull(adaptiveGrid, $"{panelName} Content needs AdaptiveGridLayout");
                    Assert.IsTrue(adaptiveGrid.FitCellWidthToContainer);
                    Assert.AreEqual(1, adaptiveGrid.GetSettings(LayoutMode.Portrait).columns);
                    Assert.AreEqual(2, adaptiveGrid.GetSettings(LayoutMode.Landscape).columns);
                    Assert.AreEqual(3, adaptiveGrid.UltraWideColumns);
                }

                Transform daysContainer = safe.Find("DailyBonusPopup/PopupPanel/Window/DaysContainer");
                Assert.IsNotNull(daysContainer);
                var daysAdaptive = daysContainer.GetComponent<AdaptiveLayoutGroup>();
                Assert.IsNotNull(daysAdaptive);
                Assert.AreEqual(LayoutAxis.Vertical, daysAdaptive.GetSettings(LayoutMode.Portrait).axis);
                Assert.AreEqual(LayoutAxis.Horizontal, daysAdaptive.GetSettings(LayoutMode.Landscape).axis, "Daily 7 days sit in one row in Landscape");
                Assert.IsTrue(safe.Find("DailyBonusPopup/PopupPanel/Window").GetComponent<ResponsiveRect>().HasPreset(LayoutMode.Landscape));

                Transform detailSheet = safe.Find("ConstellationScreen/ConstellationPanel/DetailSheet");
                Assert.IsNotNull(detailSheet);
                var detailRr = detailSheet.GetComponent<ResponsiveRect>();
                Assert.IsNotNull(detailRr);
                Assert.IsTrue(detailRr.HasPreset(LayoutMode.Landscape));
                Assert.GreaterOrEqual(detailRr.GetPreset(LayoutMode.Landscape).anchorMin.x, 0.99f, "Constellation detail sheet docks on the right in Landscape");

                Assert.IsTrue(safe.Find("SettingsPopup/PopupPanel/Window").GetComponent<ResponsiveRect>().HasPreset(LayoutMode.Landscape));
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Gameplay_LandscapeLayouts_HaveTopLeftHeroBlock_ColumnSlots_TwoColumnModals_AndSingleHudCanvas()
        {
            Scene loaded = SceneManager.GetSceneByPath(Sprint14InputWiringTool.GAMEPLAY_SCENE);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(Sprint14InputWiringTool.GAMEPLAY_SCENE, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform canvasTf = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<Canvas>() != null)
                    {
                        canvasTf = root.transform;
                        break;
                    }
                }
                Assert.IsNotNull(canvasTf);

                Transform hud = canvasTf.Find("SafeAreaHUD");
                Assert.IsNotNull(hud);
                Assert.IsNotNull(hud.GetComponent<Canvas>(), "SafeAreaHUD owns one consolidated HUD sub-canvas");
                foreach (string widgetName in new[] { "XpBar", "HpBar", "WaveHud", "CoinHud", "KillsHud", "AbilityHud", "UltimateButton", "BossHealthBar", "ComboHud" })
                {
                    Transform child = hud.Find(widgetName);
                    Assert.IsNotNull(child, widgetName);
                    Assert.IsNull(child.GetComponent<Canvas>(), $"HUD widget {widgetName} must not carry a separate Canvas (keeps draw calls <= 10)");
                }

                var topStack = hud.GetComponent<HudTopStack>();
                Assert.IsNotNull(topStack);
                Assert.Less(topStack.LandscapeTopOffset, 0f);

                // UI redesign: the HP bar follows the hero on screen in both modes (centre anchors, fixed size).
                var hpRr = hud.Find("HpBar").GetComponent<ResponsiveRect>();
                Assert.IsTrue(hpRr.HasPreset(LayoutMode.Landscape));
                Assert.IsTrue(hud.Find("HpBar").GetComponent<HpBarUI>().FollowsHero, "HpBar is pinned under the hero");
                Assert.AreEqual(hpRr.GetPreset(LayoutMode.Landscape).anchorMin, hpRr.GetPreset(LayoutMode.Landscape).anchorMax, "fixed-size bar, not stretched");

                var xpRr = hud.Find("XpBar").GetComponent<ResponsiveRect>();
                Assert.IsTrue(xpRr.HasPreset(LayoutMode.Landscape));
                Assert.Less(xpRr.GetPreset(LayoutMode.Landscape).anchorMax.x, 0.2f, "XpBar sits in top-left hero block in Landscape");

                var abilityVl = hud.Find("AbilityHud").GetComponent<VerticalLayoutGroup>();
                Assert.IsNotNull(abilityVl, "AbilityHud keeps VerticalLayoutGroup for Portrait");
                var abilityUi = hud.Find("AbilityHud").GetComponent<AbilityHudUI>();
                Assert.IsInstanceOf<ILayoutModeConsumer>(abilityUi);
                Assert.AreEqual(LayoutAxis.Vertical, hud.Find("AbilityHud/ActiveSlots").GetComponent<AdaptiveLayoutGroup>().GetSettings(LayoutMode.Landscape).axis);
                Assert.AreEqual(LayoutAxis.Vertical, hud.Find("AbilityHud/PassiveSlots").GetComponent<AdaptiveLayoutGroup>().GetSettings(LayoutMode.Landscape).axis);
                Assert.IsTrue(hud.Find("AbilityHud/ActiveSlots").GetComponent<ResponsiveRect>().HasPreset(LayoutMode.Landscape));
                Assert.IsTrue(hud.Find("AbilityHud/PassiveSlots").GetComponent<ResponsiveRect>().HasPreset(LayoutMode.Landscape));

                Transform pauseBody = canvasTf.Find("PauseMenu/PausePanel/Window/Body");
                Assert.IsNotNull(pauseBody, "Pause window must contain two-column Body");
                var pauseAdaptive = pauseBody.GetComponent<AdaptiveLayoutGroup>();
                Assert.IsNotNull(pauseAdaptive);
                Assert.AreEqual(LayoutAxis.Vertical, pauseAdaptive.GetSettings(LayoutMode.Portrait).axis);
                Assert.AreEqual(LayoutAxis.Horizontal, pauseAdaptive.GetSettings(LayoutMode.Landscape).axis);
                Assert.IsNotNull(pauseBody.Find("LeftColumn/Summary"));
                Assert.IsNotNull(pauseBody.Find("LeftColumn/BuildGrid"));
                Assert.IsNotNull(pauseBody.Find("RightColumn/ResumeButton"));

                Transform goCols = canvasTf.Find("GameOverPanel/Content/Layout/Columns");
                Assert.IsNotNull(goCols, "GameOver layout must contain two-column Columns");
                var goAdaptive = goCols.GetComponent<AdaptiveLayoutGroup>();
                Assert.IsNotNull(goAdaptive);
                Assert.AreEqual(LayoutAxis.Vertical, goAdaptive.GetSettings(LayoutMode.Portrait).axis);
                Assert.AreEqual(LayoutAxis.Horizontal, goAdaptive.GetSettings(LayoutMode.Landscape).axis);
                Assert.IsNotNull(goCols.Find("LeftColumn/StatChips"));
                Assert.IsNotNull(goCols.Find("RightColumn/DamageBreakdownSection"));
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void LayoutModeSwitch_MidLevelUpAndMidPause_PreservesStateAndSwitchesLayoutsCleanly()
        {
            Scene loaded = SceneManager.GetSceneByPath(Sprint14InputWiringTool.GAMEPLAY_SCENE);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(Sprint14InputWiringTool.GAMEPLAY_SCENE, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform canvasTf = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<Canvas>() != null)
                    {
                        canvasTf = root.transform;
                        break;
                    }
                }
                Assert.IsNotNull(canvasTf);

                var cardPanel = canvasTf.Find("CardSelectionPanel").gameObject;
                var cardsContainer = canvasTf.Find("CardSelectionPanel/Content/CardsContainer");
                var cardsGroup = cardsContainer.GetComponent<AdaptiveLayoutGroup>();
                var cardsRr = cardsContainer.GetComponent<ResponsiveRect>();
                var pausePanel = canvasTf.Find("PauseMenu/PausePanel").gameObject;
                var pauseBodyGroup = canvasTf.Find("PauseMenu/PausePanel/Window/Body").GetComponent<AdaptiveLayoutGroup>();

                bool origCardActive = cardPanel.activeSelf;
                bool origPauseActive = pausePanel.activeSelf;
                try
                {
                    cardPanel.SetActive(true);
                    pausePanel.SetActive(true);

                    foreach (LayoutMode mode in new[] { LayoutMode.Landscape, LayoutMode.Portrait, LayoutMode.Landscape })
                    {
                        cardsGroup.ApplyLayoutMode(mode);
                        cardsRr.ApplyLayoutMode(mode);
                        pauseBodyGroup.ApplyLayoutMode(mode);

                        Assert.IsTrue(cardPanel.activeSelf, $"CardSelectionPanel must remain active on {mode}");
                        Assert.IsTrue(pausePanel.activeSelf, $"PausePanel must remain active on {mode}");
                        Assert.AreEqual(mode == LayoutMode.Portrait, cardsGroup.IsVertical);
                        Assert.AreEqual(mode == LayoutMode.Portrait, pauseBodyGroup.IsVertical);
                        Assert.AreEqual(mode, cardsRr.AppliedMode);
                    }
                }
                finally
                {
                    cardsGroup.ApplyLayoutMode(LayoutMode.Portrait);
                    cardsRr.ApplyLayoutMode(LayoutMode.Portrait);
                    pauseBodyGroup.ApplyLayoutMode(LayoutMode.Portrait);
                    cardPanel.SetActive(origCardActive);
                    pausePanel.SetActive(origPauseActive);
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BottomTabUI_AndAdaptiveGridLayout_FitContainer_UpdateCleanlyInLandscape()
        {
            var tabGo = Track(new GameObject("Tab", typeof(RectTransform), typeof(LayoutElement), typeof(Button), typeof(BottomTabUI)));
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(tabGo.transform, false);
            var tab = tabGo.GetComponent<BottomTabUI>();
            var le = tabGo.GetComponent<LayoutElement>();
            tab.layoutElement = le;
            tab.iconRect = (RectTransform)iconGo.transform;

            tab.SetActiveState(true, instant: true);
            tab.ApplyLayoutMode(LayoutMode.Portrait);
            Assert.AreEqual(tab.activeWidth, le.preferredWidth, 0.5f);
            Assert.AreEqual(tab.activeIconY, tab.iconRect.anchoredPosition.y, 0.5f);

            tab.ApplyLayoutMode(LayoutMode.Landscape);
            Assert.AreEqual(tab.landscapeActiveHeight, le.preferredHeight, 0.5f);
            Assert.AreEqual(tab.landscapeActiveIconY, tab.iconRect.anchoredPosition.y, 0.5f);

            var gridGo = Track(new GameObject("FitGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(AdaptiveGridLayout)));
            ((RectTransform)gridGo.transform).sizeDelta = new Vector2(1600f, 900f);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(40, 40, 10, 10);
            var adaptive = gridGo.GetComponent<AdaptiveGridLayout>();
            adaptive.SetSettings(LayoutMode.Portrait, new GridModeSettings { columns = 1, cellSize = new Vector2(1000f, 190f), spacing = new Vector2(20f, 18f) });
            adaptive.SetSettings(LayoutMode.Landscape, new GridModeSettings { columns = 2, cellSize = new Vector2(600f, 190f), spacing = new Vector2(24f, 18f) });
            adaptive.UltraWideColumns = 3;
            adaptive.FitCellWidthToContainer = true;

            // avail = 1600 - 80 - 24 = 1496 -> 748 per column
            adaptive.Apply(LayoutMode.Landscape, 16f / 9f, 2.2f);
            Assert.AreEqual(2, grid.constraintCount);
            Assert.AreEqual(748f, grid.cellSize.x, 1f);
            Assert.AreEqual(190f, grid.cellSize.y, 0.5f);

            // 21:9 -> 3 columns: avail = 1600 - 80 - 48 = 1472 -> floor(1472 / 3) = 490
            adaptive.Apply(LayoutMode.Landscape, 21f / 9f, 2.2f);
            Assert.AreEqual(3, grid.constraintCount);
            Assert.AreEqual(490f, grid.cellSize.x, 1f);
        }

        // ───────────────────────── 14.5 WebGL Basic Launch (No SDK) ─────────────────────────

        private static void SetField(object target, string fieldName, object value)
        {
            var fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(fi, $"Missing field {target.GetType().Name}.{fieldName}");
            fi.SetValue(target, value);
        }

        [Test]
        public void ArenaConfig_WebGrassDensityMultiplier_ScalesTo30To50PercentOnWebOnly()
        {
            ArenaConfig cfg = AssetDatabase.LoadAssetAtPath<ArenaConfig>("Assets/_ScriptableObjects/ArenaConfig.asset");
            Assert.IsNotNull(cfg, "ArenaConfig.asset missing");
            Assert.GreaterOrEqual(cfg.WebGrassDensityMultiplier, 0.3f);
            Assert.LessOrEqual(cfg.WebGrassDensityMultiplier, 0.5f);

            Assert.AreEqual(1f, cfg.ResolveGrassDensityMultiplier("Mobile", false), 0.001f);
            Assert.AreEqual(1f, cfg.ResolveGrassDensityMultiplier("PC", false), 0.001f);
            Assert.AreEqual(cfg.WebGrassDensityMultiplier, cfg.ResolveGrassDensityMultiplier("Web", false), 0.001f);
            Assert.AreEqual(cfg.WebGrassDensityMultiplier, cfg.ResolveGrassDensityMultiplier("Mobile", true), 0.001f);
        }

        [Test]
        public void NullAdProvider_HidesAllFourRewardedUiControls_WhileKeepingFreeCardsAndFreeReroll()
        {
            string prevSave = PlayerPrefs.GetString(SaveManager.SAVE_KEY, null);
            try
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                var save = new SaveManager();
                var adConfig = Resources.Load<AdConfig>(AdManager.CONFIG_RESOURCE_PATH);
                var adManager = new AdManager(save, adConfig, new NullAdProvider());

                Assert.IsFalse(adManager.IsAdsAvailable);
                Assert.IsFalse(adManager.CanShowRewarded());
                Assert.IsFalse(adManager.ShouldShowInterstitial());

                // 1. CardSelectionUI: all 3 free cards stay visible; ad reroll hidden when free rerolls == 0; free reroll shown when > 0
                var csGo = Track(new GameObject("CardSelection", typeof(RectTransform), typeof(CardSelectionUI)));
                var csPanel = new GameObject("Panel", typeof(RectTransform));
                csPanel.transform.SetParent(csGo.transform, false);
                var slots = new CardUI[3];
                for (int i = 0; i < 3; i++)
                {
                    var sGo = new GameObject("Slot" + i, typeof(RectTransform), typeof(Button), typeof(CardUI));
                    sGo.transform.SetParent(csPanel.transform, false);
                    slots[i] = sGo.GetComponent<CardUI>();
                }
                var rerollGo = new GameObject("RerollBtn", typeof(RectTransform), typeof(Button));
                rerollGo.transform.SetParent(csPanel.transform, false);
                var cs = csGo.GetComponent<CardSelectionUI>();
                SetField(cs, "selectionPanel", csPanel);
                SetField(cs, "cardSlots", slots);
                SetField(cs, "rerollButton", rerollGo.GetComponent<Button>());
                cs.Construct(adManager: adManager);

                var c1 = ScriptableObject.CreateInstance<StatUpgradeCardData>();
                var c2 = ScriptableObject.CreateInstance<StatUpgradeCardData>();
                var c3 = ScriptableObject.CreateInstance<StatUpgradeCardData>();
                Track(c1); Track(c2); Track(c3);
                var offer = new List<CardData> { c1, c2, c3 };

                cs.ShowCards(offer, _ => { }, (_, __) => offer, freeRerolls: 1);
                Assert.IsTrue(slots[0].gameObject.activeSelf);
                Assert.IsTrue(slots[1].gameObject.activeSelf);
                Assert.IsTrue(slots[2].gameObject.activeSelf, "3rd card is free and must stay visible when no ad SDK");
                Assert.IsTrue(rerollGo.activeSelf, "Free reroll stays visible");

                cs.ShowCards(offer, _ => { }, (_, __) => offer, freeRerolls: 0);
                Assert.IsFalse(rerollGo.activeSelf, "Ad-only reroll button must be hidden when no ad SDK");

                // 2. GameOverUI: Revive and Double Coins buttons hidden
                var gmGo = Track(new GameObject("GM", typeof(GameManager)));
                var gm = gmGo.GetComponent<GameManager>();
                gm.Construct(null, null, save, new RunStatistics(), null, new GameEventBus(), adManager: adManager);
                Assert.IsFalse(gm.IsAdsAvailable);

                var goUiObj = Track(new GameObject("GameOver", typeof(RectTransform), typeof(GameOverUI)));
                var goPanel = new GameObject("Panel", typeof(RectTransform));
                goPanel.transform.SetParent(goUiObj.transform, false);
                var reviveBtn = new GameObject("ReviveBtn", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                var doubleBtn = new GameObject("DoubleBtn", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                reviveBtn.transform.SetParent(goPanel.transform, false);
                doubleBtn.transform.SetParent(goPanel.transform, false);
                var goUi = goUiObj.GetComponent<GameOverUI>();
                SetField(goUi, "panel", goPanel);
                SetField(goUi, "reviveButton", reviveBtn);
                SetField(goUi, "doubleCoinsButton", doubleBtn);
                goUi.Construct(gm, null, saveManager: save);
                goUi.Show(4, 120);
                Assert.IsFalse(reviveBtn.gameObject.activeSelf, "Revive ad button must be hidden when no ad SDK");
                Assert.IsFalse(doubleBtn.gameObject.activeSelf, "Double coins ad button must be hidden when no ad SDK");

                // 3. DailyBonusScreenUI: Claim stays visible, ClaimDouble hidden
                var bonusManager = new DailyBonusManager(save);
                var dailyGo = Track(new GameObject("Daily", typeof(RectTransform), typeof(DailyBonusScreenUI)));
                var claimBtn = new GameObject("ClaimBtn", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                var claimDoubleBtn = new GameObject("ClaimDoubleBtn", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
                claimBtn.transform.SetParent(dailyGo.transform, false);
                claimDoubleBtn.transform.SetParent(dailyGo.transform, false);
                var dailyUi = dailyGo.GetComponent<DailyBonusScreenUI>();
                SetField(dailyUi, "claimButton", claimBtn);
                SetField(dailyUi, "claimDoubleButton", claimDoubleBtn);
                dailyUi.Construct(bonusManager, adManager);
                typeof(DailyBonusScreenUI).GetMethod("RefreshUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dailyUi, null);
                Assert.IsTrue(claimBtn.gameObject.activeSelf);
                Assert.IsFalse(claimDoubleBtn.gameObject.activeSelf, "Daily double ad button must be hidden when no ad SDK");
            }
            finally
            {
                if (string.IsNullOrEmpty(prevSave)) PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                else PlayerPrefs.SetString(SaveManager.SAVE_KEY, prevSave);
            }
        }

        [Test]
        public void AudioManager_WebGlUserGesture_DefersMusicUntilUnlocked()
        {
            string prevSave = PlayerPrefs.GetString(SaveManager.SAVE_KEY, null);
            try
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                var save = new SaveManager();
                var catalog = Resources.Load<AudioCatalog>(AudioManager.CATALOG_RESOURCE_PATH);
                var audio = new AudioManager(save, catalog);
                audio.SetRequiresUserGestureForTesting(true);
                Assert.IsFalse(audio.IsAudioUnlocked);

                audio.PlayMusic(MusicType.Menu);
                Assert.AreEqual(MusicType.Menu, audio.CurrentMusic);
                Assert.IsFalse(audio.IsAudioUnlocked);

                audio.NotifyUserGestureUnlocked();
                Assert.IsTrue(audio.IsAudioUnlocked);
            }
            finally
            {
                if (string.IsNullOrEmpty(prevSave)) PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                else PlayerPrefs.SetString(SaveManager.SAVE_KEY, prevSave);
            }
        }

        [Test]
        public void WebBrowserLifecycle_HiddenTab_FlushesDirtySave_AndUnlocksAudioOnGesture()
        {
            string prevSave = PlayerPrefs.GetString(SaveManager.SAVE_KEY, null);
            try
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                var save = new SaveManager();
                save.AddCoins(25, saveImmediately: false);
                Assert.IsTrue(save.IsDirty);

                var audio = new AudioManager(save, null);
                audio.SetRequiresUserGestureForTesting(true);

                var gmGo = Track(new GameObject("TestWebLifecycleGM", typeof(GameManager)));
                var gm = gmGo.GetComponent<GameManager>();
                gm.Construct(null, null, save, new RunStatistics(), null, new GameEventBus());

                var lifecycle = new WebBrowserLifecycle(audio, save);
                lifecycle.HandleUserGesture();
                Assert.IsTrue(audio.IsAudioUnlocked);

                lifecycle.HandleVisibilityChanged(true, gm);
                Assert.IsFalse(save.IsDirty, "Hiding tab must flush dirty SaveManager state");
                Assert.IsTrue(gm.IsPaused, "Hiding tab must pause active GameManager");
            }
            finally
            {
                Time.timeScale = 1f;
                if (string.IsNullOrEmpty(prevSave)) PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
                else PlayerPrefs.SetString(SaveManager.SAVE_KEY, prevSave);
            }
        }

        private const string WebBuildProfilePath = "Assets/Settings/Build Profiles/Web.asset";

        [Test]
        public void WebBuildConfiguration_HasBuildProfile_QualityWeb_LinkXml_AndTemplate()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(WebBuildProfilePath);
            Assert.IsNotNull(profile, "Web BuildProfile missing");

            var webRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Web_RPAsset.asset");
            Assert.IsNotNull(webRp, "Web_RPAsset missing");
            Assert.AreEqual(1, webRp.msaaSampleCount, "MSAA must be disabled on Web");
            Assert.LessOrEqual(webRp.shadowDistance, 25f);

            int webQualityIdx = Array.IndexOf(QualitySettings.names, "Web");
            Assert.GreaterOrEqual(webQualityIdx, 0, "Quality level 'Web' missing");

            Assert.IsTrue(File.Exists("Assets/link.xml"), "Assets/link.xml missing");
            string linkXml = File.ReadAllText("Assets/link.xml");
            StringAssert.Contains("VContainer", linkXml);
            StringAssert.Contains("MeshCollider", linkXml);
            Assert.IsTrue(File.Exists("Assets/WebGLTemplates/ProjectB_Web/index.html"), "WebGL template index.html missing");
            Assert.IsTrue(File.Exists("Assets/Plugins/WebGL/ProjectBWebBridge.jslib"), "ProjectBWebBridge.jslib missing");
        }

        [Test]
        public void WebBuildConfiguration_PlayerSettings_Brotli_StrippingLow_NoRunInBackground()
        {
            // The Web profile overrides Player Settings; global PlayerSettings only reflect them while it is active.
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(WebBuildProfilePath);
            Assert.IsNotNull(profile, "Web BuildProfile missing");
            if (BuildProfile.GetActiveBuildProfile() != profile)
            {
                Assert.Ignore("Web Build Profile is not active; switch to it to check WebGL Player Settings.");
            }

            Assert.IsFalse(PlayerSettings.runInBackground, "runInBackground must be false for WebGL");
            Assert.AreEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);
            // Unity 6 Medium (--rule-set=Aggressive) strips VContainer.InjectAttribute : PreserveAttribute even with link.xml; Low (--rule-set=Conservative) preserves it.
            Assert.AreEqual(ManagedStrippingLevel.Low, PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL));
            Assert.AreEqual(Il2CppCodeGeneration.OptimizeSize, PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL));
            Assert.AreEqual("PROJECT:ProjectB_Web", PlayerSettings.WebGL.template);
        }
    }
}
