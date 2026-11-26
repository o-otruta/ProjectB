using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data;
using ProjectB.Data.Platform;
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
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
