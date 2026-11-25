using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Constellation detail sheet: long star names (e.g. "Mythic Ascension: Lv 50 Cap") must shrink instead of
    /// overflowing, and the title must never run under the close (×) button — in both layout modes.
    /// Read-only: the scene is inspected, never modified.
    /// </summary>
    public class ConstellationDetailSheetLayoutTests
    {
        private const string MainMenuScene = "Assets/_Scenes/MainMenu.unity";

        private static T GetField<T>(object target, string field) where T : class
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            return f.GetValue(target) as T;
        }

        /// <summary>Horizontal span [left, right] of a rect preset inside a parent of the given width.</summary>
        private static Vector2 HorizontalSpan(RectPreset p, float parentWidth)
        {
            float width = (p.anchorMax.x - p.anchorMin.x) * parentWidth + p.sizeDelta.x;
            float left = p.anchorMin.x * parentWidth + p.anchoredPosition.x - p.pivot.x * width;
            return new Vector2(left, left + width);
        }

        [Test]
        public void DetailTitle_AutoSizes_AndStaysClearOfCloseButton_InBothLayouts()
        {
            Scene loaded = SceneManager.GetSceneByPath(MainMenuScene);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Additive) : loaded;
            try
            {
                ConstellationTreeUI ui = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ui = root.GetComponentInChildren<ConstellationTreeUI>(true);
                    if (ui != null) break;
                }
                Assert.IsNotNull(ui, "ConstellationTreeUI missing in MainMenu");

                var sheet = GetField<GameObject>(ui, "detailSheet");
                var title = GetField<TextMeshProUGUI>(ui, "detailTitle");
                var close = GetField<Button>(ui, "closeDetailButton");
                Assert.IsNotNull(sheet);
                Assert.IsNotNull(title);
                Assert.IsNotNull(close);

                Assert.IsTrue(title.enableAutoSizing, "Long star names must shrink to fit");
                Assert.LessOrEqual(title.fontSizeMin, title.fontSizeMax * 0.65f);
                Assert.AreNotEqual(TextOverflowModes.Overflow, title.overflowMode, "Title must not spill over the type line");

                var sheetRect = sheet.GetComponent<ResponsiveRect>();
                var titleRect = title.GetComponent<ResponsiveRect>();
                Assert.IsNotNull(sheetRect);
                Assert.IsNotNull(titleRect);

                // The close button is pinned to the sheet's top-right corner in both modes.
                var closeRt = (RectTransform)close.transform;
                Assert.AreEqual(Vector2.one, closeRt.anchorMin);
                Assert.AreEqual(Vector2.one, closeRt.anchorMax);
                float closeInsetFromRight = -closeRt.anchoredPosition.x + closeRt.sizeDelta.x * closeRt.pivot.x;

                var config = Resources.Load<LayoutConfig>(LayoutConfig.RESOURCE_PATH);
                Assert.IsNotNull(config);

                foreach (LayoutMode mode in new[] { LayoutMode.Portrait, LayoutMode.Landscape })
                {
                    RectPreset sheetPreset = mode == LayoutMode.Landscape ? sheetRect.Landscape : sheetRect.Portrait;
                    RectPreset titlePreset = mode == LayoutMode.Landscape ? titleRect.Landscape : titleRect.Portrait;
                    Assert.IsTrue(sheetPreset.captured && titlePreset.captured, $"{mode}: presets not captured");

                    float canvasWidth = config.GetReferenceResolution(mode).x;
                    Vector2 sheetSpan = HorizontalSpan(sheetPreset, canvasWidth);
                    float sheetWidth = sheetSpan.y - sheetSpan.x;
                    Vector2 titleSpan = HorizontalSpan(titlePreset, sheetWidth);
                    float closeLeft = sheetWidth - closeInsetFromRight;

                    Assert.LessOrEqual(titleSpan.y, closeLeft - 8f,
                        $"{mode}: title ends at {titleSpan.y:F0}, close button starts at {closeLeft:F0} (sheet {sheetWidth:F0})");
                    Assert.Greater(titleSpan.y - titleSpan.x, 240f, $"{mode}: title too narrow");
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
