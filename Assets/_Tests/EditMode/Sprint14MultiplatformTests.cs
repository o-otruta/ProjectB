using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Core;
using ProjectB.Data.Platform;
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
    }
}
