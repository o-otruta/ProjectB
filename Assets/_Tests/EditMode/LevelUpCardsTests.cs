using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.LevelUp;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// UI redesign (variant A), LevelUp cards: rarity band label, level pips, numeric highlighting in descriptions,
    /// the level-progress model behind the pips and the "Your build" strip wiring in Gameplay.unity.
    /// </summary>
    public class LevelUpCardsTests
    {
        private const string GameplayPath = "Assets/_Scenes/Gameplay.unity";
        private const string Green = "45D483";

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

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            f.SetValue(target, value);
        }

        private static object GetPrivate(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            return f.GetValue(target);
        }

        // ───────────────────────── description highlighting ─────────────────────────

        [Test]
        public void HighlightNumbers_WrapsSignedPercentAndDecimalTokens()
        {
            Assert.AreEqual($"<color=#{Green}>+12%</color> global radius", CardUI.HighlightNumbers("+12% global radius", Green));
            Assert.AreEqual($"distance by <color=#{Green}>+0.5</color>m.", CardUI.HighlightNumbers("distance by +0.5m.", Green));
            Assert.AreEqual($"health by <color=#{Green}>20</color>.", CardUI.HighlightNumbers("health by 20.", Green));
            Assert.AreEqual($"damage <color=#{Green}>-10%</color>", CardUI.HighlightNumbers("damage -10%", Green));
            Assert.AreEqual($"<color=#{Green}>x2</color> coins", CardUI.HighlightNumbers("x2 coins", Green));
            Assert.AreEqual($"crit <color=#{Green}>0,25×</color>", CardUI.HighlightNumbers("crit 0,25×", Green));
        }

        [Test]
        public void HighlightNumbers_LeavesTagsWordsAndEmptyTextAlone()
        {
            Assert.AreEqual("<size=70%>Lv2</size>", CardUI.HighlightNumbers("<size=70%>Lv2</size>", Green), "numbers inside tags and glued to words stay");
            Assert.AreEqual("<sprite name=\"ad\"> box2", CardUI.HighlightNumbers("<sprite name=\"ad\"> box2", Green));
            Assert.AreEqual("No numbers here", CardUI.HighlightNumbers("No numbers here", Green));
            Assert.AreEqual(string.Empty, CardUI.HighlightNumbers(string.Empty, Green));
            Assert.IsNull(CardUI.HighlightNumbers(null, Green));
        }

        // ───────────────────────── level progress & band ─────────────────────────

        [Test]
        public void GetLevelProgress_NewAbilityStartsAtOne_StatCardsHaveNoLevels()
        {
            var stat = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            CardSelectionUI.GetLevelProgress(stat, null, out int cur, out int next, out int cap);
            Assert.AreEqual((0, 0, 0), (cur, next, cap), "stat cards carry no ability level");

            var weapon = Track(ScriptableObject.CreateInstance<WeaponUpgradeCardData>());
            CardSelectionUI.GetLevelProgress(weapon, null, out cur, out next, out cap);
            Assert.AreEqual(0, cap, "weapon cards carry no ability level");

            var ability = Track(ScriptableObject.CreateInstance<AbilityCardData>());
            CardSelectionUI.GetLevelProgress(ability, null, out cur, out next, out cap);
            Assert.AreEqual(0, cur);
            Assert.AreEqual(1, next, "a new ability grants level 1");

            var mod = Track(ScriptableObject.CreateInstance<AbilityModifierCardData>());
            mod.targetAbilityId = "missing";
            CardSelectionUI.GetLevelProgress(mod, null, out cur, out next, out cap);
            Assert.AreEqual((0, 0, 0), (cur, next, cap), "a modifier for an unowned ability shows no pips");
        }

        [Test]
        public void FormatBandLabel_CombinesUppercaseRarityAndCategory()
        {
            var stat = Track(ScriptableObject.CreateInstance<StatUpgradeCardData>());
            stat.rarity = CardRarity.Epic;
            string label = CardUI.FormatBandLabel(stat);
            StringAssert.Contains(" · ", label);
            Assert.AreEqual(label.ToUpperInvariant(), label, "band label is uppercase");
            StringAssert.EndsWith(CardUI.FormatCardTag(stat), label);
        }

        [Test]
        public void SetLevelProgress_ShowsCapPips_FilledNextAndEmpty_AndHidesAboveCapacity()
        {
            UiTheme theme = UiTheme.Current;
            var go = Track(new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardUI)));
            var card = go.GetComponent<CardUI>();
            var pips = new Image[10];
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = new GameObject($"Pip_{i}", typeof(RectTransform), typeof(Image));
                pip.transform.SetParent(go.transform, false);
                pips[i] = pip.GetComponent<Image>();
            }
            SetPrivate(card, "rarityBand", true);
            SetPrivate(card, "levelPips", pips);

            card.SetLevelProgress(2, 3, 5);
            Assert.AreEqual(5, card.VisibleLevelPips, "one pip per level up to the cap");
            Assert.AreEqual(theme.background, pips[0].color, "owned level: solid dark");
            Assert.AreEqual(theme.white, pips[2].color, "granted level: bright");
            Assert.Less(pips[4].color.a, 0.5f, "future level: faint");

            card.SetLevelProgress(12, 13, 20);
            Assert.AreEqual(0, card.VisibleLevelPips, "Path of Mastery caps above the pip count fall back to the text badge");

            card.SetLevelProgress(0, 0, 0);
            Assert.AreEqual(0, card.VisibleLevelPips);
        }

        // ───────────────────────── scene wiring ─────────────────────────

        [Test]
        public void GameplayScene_CardsUseRarityBand_AndBuildStripIsWired()
        {
            Scene loaded = SceneManager.GetSceneByPath(GameplayPath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Additive) : loaded;
            try
            {
                CardSelectionUI selection = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    selection = root.GetComponentInChildren<CardSelectionUI>(true);
                    if (selection != null) break;
                }
                Assert.IsNotNull(selection);

                var cards = (CardUI[])GetPrivate(selection, "cardSlots");
                Assert.AreEqual(3, cards.Length);
                foreach (CardUI card in cards)
                {
                    Assert.IsTrue((bool)GetPrivate(card, "rarityBand"), $"{card.name}: rarity band on");
                    var pips = (Image[])GetPrivate(card, "levelPips");
                    Assert.AreEqual(10, pips.Length, $"{card.name}: 10 level pips (Path of Mastery Lv 10)");
                    foreach (Image pip in pips) Assert.IsNotNull(pip);
                    Assert.IsNotNull(GetPrivate(card, "newTag"), $"{card.name}: NEW! sticker");
                    var badge = (GameObject)GetPrivate(card, "levelBadgeObject");
                    Assert.AreSame(((TMP_Text)GetPrivate(card, "tagText")).transform.parent, badge.transform.parent, "level badge lives inside the band");
                }

                var bar = (LevelUpBuildBarUI)GetPrivate(selection, "buildBar");
                Assert.IsNotNull(bar, "CardSelectionUI.buildBar");
                foreach (string field in new[] { "slotPrefab", "activeContainer", "passiveContainer" })
                {
                    Assert.IsNotNull(GetPrivate(bar, field), $"LevelUpBuildBarUI.{field}");
                }
                var label = bar.GetComponentInChildren<LocalizedText>(true);
                Assert.IsNotNull(label, "build strip label is localized");
                Assert.AreEqual("ui.levelup.your_build", label.Key);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BuildBar_HidesWithoutHero()
        {
            var go = Track(new GameObject("BuildBar", typeof(RectTransform), typeof(LevelUpBuildBarUI)));
            go.GetComponent<LevelUpBuildBarUI>().Refresh(null);
            Assert.IsFalse(go.activeSelf);
        }
    }
}
