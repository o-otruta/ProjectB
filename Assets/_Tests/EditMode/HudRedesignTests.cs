using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// UI redesign (variant A), gameplay HUD: wave track blocks, compact "current only" HP label, and the
    /// Gameplay.unity wiring (HP under the hero, XP level tag, wave track, no locked-slot chips).
    /// </summary>
    public class HudRedesignTests
    {
        private const string GameplayPath = "Assets/_Scenes/Gameplay.unity";
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

        private static object GetPrivate(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            return f.GetValue(target);
        }

        [Test]
        public void WaveTrack_BlockEndsOnTheMiniBossWave()
        {
            Assert.AreEqual(1, WaveTrackUI.GetBlockStart(1, 5));
            Assert.AreEqual(1, WaveTrackUI.GetBlockStart(5, 5), "the mini-boss wave closes its block");
            Assert.AreEqual(6, WaveTrackUI.GetBlockStart(6, 5));
            Assert.AreEqual(21, WaveTrackUI.GetBlockStart(25, 5), "boss wave 25 closes block 21..25");
            Assert.AreEqual(1, WaveTrackUI.GetBlockStart(0, 5), "pre-run wave clamps to the first block");
            Assert.AreEqual(4, WaveTrackUI.GetBlockStart(4, 1), "degenerate interval → one wave per block");

            Assert.AreEqual(WaveTrackUI.NodeState.Done, WaveTrackUI.GetNodeState(2, 3));
            Assert.AreEqual(WaveTrackUI.NodeState.Current, WaveTrackUI.GetNodeState(3, 3));
            Assert.AreEqual(WaveTrackUI.NodeState.Upcoming, WaveTrackUI.GetNodeState(4, 3));
        }

        [Test]
        public void ProgressBar_CurrentFormat_ShowsOnlyTheValue()
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(ProgressBarUI));
            created.Add(go);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(go.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);

            var bar = go.GetComponent<ProgressBarUI>();
            bar.EditorSetup(fill, null, label, ProgressLabelFormat.Current, UiColor.HpFill);
            bar.SetValue(176, 200, true);
            Assert.AreEqual("176", label.text);
        }

        [Test]
        public void GameplayHud_HpUnderHero_XpLevelTag_WaveTrack_NoLockedChips()
        {
            Scene loaded = SceneManager.GetSceneByPath(GameplayPath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform hud = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "SafeAreaHUD") hud = t;
                    }
                }
                Assert.IsNotNull(hud);

                var hp = hud.Find("HpBar").GetComponent<HpBarUI>();
                Assert.IsTrue(hp.FollowsHero, "HP bar pinned under the hero");
                Assert.IsNull(GetPrivate(hp, "hpText") as Object, "no 'cur / max' writer on the compact bar");
                Assert.AreEqual(ProgressLabelFormat.Current, GetPrivate(hud.Find("HpBar").GetComponent<ProgressBarUI>(), "labelFormat"));

                var xp = hud.Find("XpBar").GetComponent<XpBarUI>();
                var badge = GetPrivate(xp, "levelBadgeText") as TMP_Text;
                Assert.IsNotNull(badge);
                Assert.AreEqual("LevelTag", badge.transform.parent.name, "level shown in the gold tag");

                var track = hud.Find("WaveHud/WaveTrack").GetComponent<WaveTrackUI>();
                Assert.IsNotNull(track, "wave track replaces the WAVE n label");
                Assert.AreEqual(5, ((Image[])GetPrivate(track, "nodes")).Length);
                Assert.AreEqual(4, ((TMP_Text[])GetPrivate(track, "nodeLabels")).Length);
                Assert.IsNotNull(GetPrivate(track, "bossIcon") as Object);
                Assert.IsFalse(hud.Find("WaveHud/WaveText").gameObject.activeSelf);

                Assert.IsNull(hud.Find("AbilityHud/ActiveSlots/LockedChip"), "no '🔒 +N' chip");
                Assert.IsNull(hud.Find("AbilityHud/PassiveSlots/LockedChip"));
                var abilities = hud.Find("AbilityHud").GetComponent<AbilityHudUI>();
                Assert.IsNull(GetPrivate(abilities, "activeLockedChip") as Object);
                Assert.IsNull(GetPrivate(abilities, "passiveLockedChip") as Object);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
