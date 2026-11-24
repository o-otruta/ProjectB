using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>Top HUD "notification shade": boss HP first, events/toasts stack below, hidden entries take no space.</summary>
    public class HudTopStackTests
    {
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

        private (RectTransform slot, GameObject content) MakeEntry(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(800f, height);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            content.SetActive(false);
            return (rt, content);
        }

        [Test]
        public void Stack_PlacesBossFirst_AndEventsTakeBossSlotWhenNoBoss()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            created.Add(root);
            var boss = MakeEntry(root.transform, "Boss", 120f);
            var banner = MakeEntry(root.transform, "Banner", 190f);
            var toast = MakeEntry(root.transform, "Toast", 156f);
            var combo = MakeEntry(root.transform, "Combo", 132f);

            var stack = root.AddComponent<HudTopStack>();
            stack.EditorSetup(new[]
            {
                new HudTopStack.Entry { slot = boss.slot, content = boss.content },
                new HudTopStack.Entry { slot = banner.slot, content = banner.content },
                new HudTopStack.Entry { slot = toast.slot, content = toast.content },
            }, new[] { combo.slot }, -250f, 14f);

            // No boss: the event banner takes the top slot, the toast goes right below it.
            banner.content.SetActive(true);
            toast.content.SetActive(true);
            stack.Relayout(snap: true);
            Assert.AreEqual(-250f, banner.slot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(-250f - 190f - 14f, toast.slot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(-250f, combo.slot.anchoredPosition.y, 0.01f, "Followers sit at the top when there is no boss");

            // Boss appears: always first, everything else shifts down below it.
            boss.content.SetActive(true);
            stack.Relayout(snap: true);
            float belowBoss = -250f - 120f - 14f;
            Assert.AreEqual(-250f, boss.slot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(belowBoss, banner.slot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(belowBoss - 190f - 14f, toast.slot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(belowBoss, combo.slot.anchoredPosition.y, 0.01f, "Combo stays right below the boss bar");

            // Event ends: the toast slides up into the freed slot.
            banner.content.SetActive(false);
            stack.Relayout(snap: true);
            Assert.AreEqual(belowBoss, toast.slot.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void Stack_TreatsFadedOutHudGroupAsHidden()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            created.Add(root);
            var group = root.AddComponent<CanvasGroup>();
            var boss = MakeEntry(root.transform, "Boss", 120f);
            var toast = MakeEntry(root.transform, "Toast", 156f);
            boss.content.SetActive(true);
            toast.content.SetActive(true);

            var stack = root.AddComponent<HudTopStack>();
            stack.EditorSetup(new[]
            {
                new HudTopStack.Entry { slot = boss.slot, content = boss.content, visibilityGroup = group },
                new HudTopStack.Entry { slot = toast.slot, content = toast.content },
            }, null, -250f, 14f);

            group.alpha = 0f; // HUD hidden under Game Over
            stack.Relayout(snap: true);
            Assert.AreEqual(-250f, toast.slot.anchoredPosition.y, 0.01f, "Achievement toast must not leave a gap for a hidden boss bar");
        }

        [Test]
        public void GameplayScene_WiresTopStack_BossFirst()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/Gameplay.unity", OpenSceneMode.Additive);
            try
            {
                HudTopStack stack = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    stack = root.GetComponentInChildren<HudTopStack>(true);
                    if (stack != null) break;
                }
                Assert.IsNotNull(stack, "Gameplay.unity must have a HudTopStack");

                var so = new UnityEditor.SerializedObject(stack);
                var entries = so.FindProperty("entries");
                Assert.GreaterOrEqual(entries.arraySize, 3);
                var first = entries.GetArrayElementAtIndex(0).FindPropertyRelative("slot").objectReferenceValue;
                Assert.IsNotNull(first);
                Assert.AreEqual("BossHealthBar", first.name, "Boss HP always has top priority");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var e = entries.GetArrayElementAtIndex(i);
                    Assert.IsNotNull(e.FindPropertyRelative("slot").objectReferenceValue);
                    Assert.IsNotNull(e.FindPropertyRelative("content").objectReferenceValue);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
