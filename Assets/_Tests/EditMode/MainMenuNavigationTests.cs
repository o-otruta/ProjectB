using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Top-level Constellations tab, TopBar Daily Bonus modal popup, shard affordability badge,
    /// and direct synergy-to-star navigation from AbilitiesScreenUI.
    /// </summary>
    public class MainMenuNavigationTests
    {
        private const string MainMenuPath = "Assets/_Scenes/MainMenu.unity";
        private const string AbilityCatalogPath = "Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset";
        private const string AbilityItemPrefabPath = "Assets/_Prefabs/UI/AbilityCollectionItem.prefab";

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();

        [TearDown]
        public void TearDown()
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();
            disposables.Clear();

            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) created.Add(obj);
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

        private TalentTreeData BuildTestTree()
        {
            var tree = Track(ScriptableObject.CreateInstance<TalentTreeData>());
            TalentNodeData Make(string id, TalentNodeType type, int shards, int coins, int minStars, string[] prereq, string synergy = null)
            {
                var n = Track(ScriptableObject.CreateInstance<TalentNodeData>());
                n.id = id;
                n.displayName = id;
                n.nodeType = type;
                n.shardCost = shards;
                n.coinCost = coins;
                n.minUnlockedStars = minStars;
                n.prerequisiteIds = new List<string>(prereq ?? Array.Empty<string>());
                n.synergyId = synergy;
                tree.nodes.Add(n);
                return n;
            }

            Make("origin", TalentNodeType.Origin, 0, 0, 0, null);
            Make("root_a", TalentNodeType.Stat, 1, 150, 0, null);
            Make("syn_a", TalentNodeType.Synergy, 2, 0, 0, new[] { "root_a" }, SynergyIds.ToxicInferno);
            Make("gated_end", TalentNodeType.Mastery, 1, 0, 5, new[] { "root_a" });
            return tree;
        }

        private static TabBarUI BuildTabBar(Transform parent, out BadgeUI[] badges)
        {
            var barGo = new GameObject("BottomBar", typeof(RectTransform), typeof(TabBarUI));
            barGo.transform.SetParent(parent, false);
            var tabBar = barGo.GetComponent<TabBarUI>();
            var tabs = new BottomTabUI[5];
            badges = new BadgeUI[5];

            for (int i = 0; i < 5; i++)
            {
                var tabGo = new GameObject($"Tab_{i}", typeof(RectTransform), typeof(BottomTabUI));
                tabGo.transform.SetParent(barGo.transform, false);
                tabs[i] = tabGo.GetComponent<BottomTabUI>();

                var badgeGo = new GameObject($"Badge_{i}", typeof(RectTransform), typeof(Image), typeof(BadgeUI));
                badgeGo.transform.SetParent(tabGo.transform, false);
                var countGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                countGo.transform.SetParent(badgeGo.transform, false);
                badges[i] = badgeGo.GetComponent<BadgeUI>();
                badges[i].EditorSetup(badgeGo.GetComponent<Image>(), countGo.GetComponent<TMP_Text>());
                badgeGo.SetActive(false);
            }
            tabBar.EditorSetup(tabs, badges, null);
            return tabBar;
        }

        [Test]
        public void TalentTreeManager_GetAffordableWithShardsCount_CountsOnlyConnectedShardAffordableStars()
        {
            var save = new SaveManager(new InMemorySaveStorage());
            disposables.Add(save);
            var tree = BuildTestTree();
            var mgr = new TalentTreeManager(save, tree, null);
            disposables.Add(mgr);

            save.AddCoins(500, saveImmediately: false);
            Assert.AreEqual(0, mgr.GetAffordableWithShardsCount(), "Coins alone must not trigger shard badge");

            save.AddBossShards(1, saveImmediately: false);
            Assert.AreEqual(1, mgr.GetAffordableWithShardsCount(), "Only root_a is connected and affordable");

            Assert.AreEqual(TalentUnlockResult.Success, mgr.TryUnlock("root_a", payWithCoins: false));
            Assert.AreEqual(0, mgr.GetAffordableWithShardsCount(), "Zero shards left after unlocking root_a");

            save.AddBossShards(2, saveImmediately: false);
            Assert.AreEqual(1, mgr.GetAffordableWithShardsCount(), "syn_a is affordable; gated_end needs 5 stars");
        }

        [Test]
        public void MainMenuUI_DrivesConstellationsNumericBadge_AndTopBarDailyBonusDotBadge()
        {
            var save = new SaveManager(new InMemorySaveStorage());
            disposables.Add(save);
            var bonus = new DailyBonusManager(save);
            var tree = BuildTestTree();
            var talentMgr = new TalentTreeManager(save, tree, null);
            disposables.Add(talentMgr);

            var rootGo = Track(new GameObject("Canvas", typeof(RectTransform), typeof(MainMenuUI)));
            TabBarUI tabBar = BuildTabBar(rootGo.transform, out BadgeUI[] badges);

            var dailyBadgeGo = new GameObject("DailyBadge", typeof(RectTransform), typeof(Image), typeof(BadgeUI));
            dailyBadgeGo.transform.SetParent(rootGo.transform, false);
            var dailyCountGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            dailyCountGo.transform.SetParent(dailyBadgeGo.transform, false);
            var dailyBadge = dailyBadgeGo.GetComponent<BadgeUI>();
            dailyBadge.EditorSetup(dailyBadgeGo.GetComponent<Image>(), dailyCountGo.GetComponent<TMP_Text>());

            var menu = rootGo.GetComponent<MainMenuUI>();
            SetPrivate(menu, "tabBar", tabBar);
            SetPrivate(menu, "dailyBonusBadge", dailyBadge);

            menu.Construct(save, bonus, talentTreeManager: talentMgr);

            Assert.IsFalse(badges[MainMenuUI.TAB_CONSTELLATIONS].IsVisible, "No shards -> no Constellations badge");
            Assert.IsTrue(dailyBadge.IsVisible, "Daily reward available -> TopBar badge visible");

            save.AddBossShards(1, saveImmediately: false);
            Assert.IsTrue(badges[MainMenuUI.TAB_CONSTELLATIONS].IsVisible);
            Assert.AreEqual(1, badges[MainMenuUI.TAB_CONSTELLATIONS].Count);

            Assert.AreEqual(TalentUnlockResult.Success, talentMgr.TryUnlock("root_a", payWithCoins: false));
            Assert.IsFalse(badges[MainMenuUI.TAB_CONSTELLATIONS].IsVisible);

            Assert.IsTrue(bonus.ClaimReward(watchAdForDouble: false));
            Assert.IsFalse(dailyBadge.IsVisible, "Claiming daily bonus hides TopBar dot badge");
        }

        [Test]
        public void MainMenuUI_OpenConstellationNode_SwitchesTabAndSelectsTargetNode()
        {
            var save = new SaveManager(new InMemorySaveStorage());
            disposables.Add(save);
            var bonus = new DailyBonusManager(save);
            var tree = BuildTestTree();
            var talentMgr = new TalentTreeManager(save, tree, null);
            disposables.Add(talentMgr);

            var rootGo = Track(new GameObject("Canvas", typeof(RectTransform), typeof(MainMenuUI)));
            var constRoot = new GameObject("ConstellationScreen", typeof(RectTransform), typeof(ConstellationTreeUI));
            constRoot.transform.SetParent(rootGo.transform, false);
            var constPanel = new GameObject("ConstellationPanel", typeof(RectTransform));
            constPanel.transform.SetParent(constRoot.transform, false);
            constPanel.SetActive(false);
            var nodesRoot = new GameObject("Nodes", typeof(RectTransform));
            nodesRoot.transform.SetParent(constPanel.transform, false);
            var detailSheet = new GameObject("DetailSheet", typeof(RectTransform));
            detailSheet.transform.SetParent(constPanel.transform, false);
            detailSheet.SetActive(false);

            var constUi = constRoot.GetComponent<ConstellationTreeUI>();
            SetPrivate(constUi, "panel", constPanel);
            SetPrivate(constUi, "nodesRoot", (RectTransform)nodesRoot.transform);
            SetPrivate(constUi, "detailSheet", detailSheet);
            constUi.Construct(talentMgr, save);

            var menu = rootGo.GetComponent<MainMenuUI>();
            SetPrivate(menu, "constellationTreeUI", constUi);
            menu.Construct(save, bonus, talentTreeManager: talentMgr);

            menu.OpenConstellationNode("syn_a");

            Assert.AreEqual(MainMenuUI.TAB_CONSTELLATIONS, menu.SelectedTab);
            Assert.IsTrue(constUi.IsOpen);
            Assert.IsTrue(constUi.IsDetailOpen);
            Assert.IsNotNull(constUi.SelectedNode);
            Assert.AreEqual("syn_a", constUi.SelectedNode.id);
        }

        [Test]
        public void AbilitiesScreenUI_SynergyClick_ResolvesStarAndNavigatesToConstellations()
        {
            AbilityCatalog abilityCatalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilityCatalogPath);
            SynergyCatalog synergyCatalog = Resources.Load<SynergyCatalog>("SynergyCatalog");
            TalentTreeData realTree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            GameObject itemPrefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityItemPrefabPath);
            Assert.IsNotNull(abilityCatalog);
            Assert.IsNotNull(synergyCatalog);
            Assert.IsNotNull(realTree);
            Assert.IsNotNull(itemPrefabGo);

            var save = new SaveManager(new InMemorySaveStorage());
            disposables.Add(save);
            var bonus = new DailyBonusManager(save);
            var talentMgr = new TalentTreeManager(save, realTree, null);
            disposables.Add(talentMgr);

            var rootGo = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(MainMenuUI)));
            var constRoot = new GameObject("ConstellationScreen", typeof(RectTransform), typeof(ConstellationTreeUI));
            constRoot.transform.SetParent(rootGo.transform, false);
            var constPanel = new GameObject("ConstellationPanel", typeof(RectTransform));
            constPanel.transform.SetParent(constRoot.transform, false);
            constPanel.SetActive(false);
            var nodesRoot = new GameObject("Nodes", typeof(RectTransform));
            nodesRoot.transform.SetParent(constPanel.transform, false);
            var detailSheet = new GameObject("DetailSheet", typeof(RectTransform));
            detailSheet.transform.SetParent(constPanel.transform, false);
            detailSheet.SetActive(false);

            var constUi = constRoot.GetComponent<ConstellationTreeUI>();
            SetPrivate(constUi, "panel", constPanel);
            SetPrivate(constUi, "nodesRoot", (RectTransform)nodesRoot.transform);
            SetPrivate(constUi, "detailSheet", detailSheet);
            constUi.Construct(talentMgr, save);

            var abilitiesGo = new GameObject("AbilitiesPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(AbilitiesScreenUI));
            abilitiesGo.transform.SetParent(rootGo.transform, false);
            abilitiesGo.SetActive(false);
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(abilitiesGo.transform, false);

            var menu = rootGo.GetComponent<MainMenuUI>();
            SetPrivate(menu, "constellationTreeUI", constUi);
            SetPrivate(menu, "abilitiesScreenUI", abilitiesGo.GetComponent<AbilitiesScreenUI>());
            menu.Construct(save, bonus, talentTreeManager: talentMgr);

            var abilitiesUi = abilitiesGo.GetComponent<AbilitiesScreenUI>();
            SetPrivate(abilitiesUi, "abilityCatalog", abilityCatalog);
            SetPrivate(abilitiesUi, "synergyCatalog", synergyCatalog);
            SetPrivate(abilitiesUi, "contentContainer", contentGo.transform);
            SetPrivate(abilitiesUi, "itemPrefab", itemPrefabGo.GetComponent<AbilityCollectionItemUI>());
            abilitiesUi.Construct(save, null, null, talentMgr, menu);

            menu.OnAbilitiesClicked();
            abilitiesUi.SetFilter(AbilityCollectionFilter.Synergies);

            SynergyData firstSynergy = synergyCatalog.synergies[0];
            TalentNodeData expectedNode = talentMgr.FindSynergyNode(firstSynergy.id);
            Assert.IsNotNull(expectedNode, $"Expected a Constellation node for synergy {firstSynergy.id}");

            AbilityCollectionItemUI activeSynergyCard = null;
            foreach (AbilityCollectionItemUI card in contentGo.GetComponentsInChildren<AbilityCollectionItemUI>(false))
            {
                activeSynergyCard = card;
                break;
            }
            Assert.IsNotNull(activeSynergyCard);
            Button btn = activeSynergyCard.GetComponent<Button>();
            Assert.IsNotNull(btn);
            btn.onClick.Invoke();

            Assert.AreEqual(MainMenuUI.TAB_CONSTELLATIONS, menu.SelectedTab);
            Assert.IsTrue(constUi.IsOpen);
            Assert.AreSame(expectedNode, constUi.SelectedNode);
        }

        [Test]
        public void MainMenuScene_HasFiveTabsWithConstellations_TopBarDailyBonusButton_AndDailyPopup()
        {
            UiTheme theme = UiTheme.Current;
            Assert.IsNotNull(theme.tabConstellations, "UiTheme.tabConstellations must be assigned");
            Assert.IsNotNull(theme.tabBonus, "UiTheme.tabBonus must remain assigned for TopBar DailyBonusButton");

            Scene loaded = SceneManager.GetSceneByPath(MainMenuPath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform safe = null;
                MainMenuUI menu = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Canvas")
                    {
                        safe = root.transform.Find("SafeAreaRoot");
                        menu = root.GetComponent<MainMenuUI>();
                        break;
                    }
                }
                Assert.IsNotNull(safe);
                Assert.IsNotNull(menu);

                // 1. BottomBar order & Constellations tab
                Transform bottomBar = safe.Find("BottomBar");
                var barTabs = new List<BottomTabUI>();
                for (int i = 0; i < bottomBar.childCount; i++)
                {
                    var bt = bottomBar.GetChild(i).GetComponent<BottomTabUI>();
                    if (bt != null) barTabs.Add(bt);
                }
                CollectionAssert.AreEqual(
                    new[] { "UpgradesButton", "ConstellationsButton", "PlayButton", "AbilitiesButton", "AchievementsButton" },
                    barTabs.ConvertAll(t => t.name));

                Transform constTab = bottomBar.Find("ConstellationsButton");
                Assert.AreSame(theme.tabConstellations, constTab.Find("Icon").GetComponent<Image>().sprite);
                Assert.AreEqual("ui.tab.constellations", constTab.Find("Label").GetComponent<LocalizedText>().Key);
                Assert.AreEqual("OnConstellationClicked", constTab.GetComponent<Button>().onClick.GetPersistentMethodName(0));

                // 2. TopBar DailyBonusButton
                Transform dailyBtnT = safe.Find("TopBar/DailyBonusButton");
                Assert.IsNotNull(dailyBtnT, "TopBar/DailyBonusButton must exist");
                Assert.AreSame(theme.tabBonus, dailyBtnT.Find("Icon").GetComponent<Image>().sprite);
                Assert.IsNotNull(dailyBtnT.Find("Badge")?.GetComponent<BadgeUI>());
                Assert.AreEqual("OnBonusClicked", dailyBtnT.GetComponent<Button>().onClick.GetPersistentMethodName(0));
                Assert.AreSame(dailyBtnT.GetComponent<Button>(), GetPrivate(menu, "dailyBonusButton"));
                Assert.AreSame(dailyBtnT.Find("Badge").GetComponent<BadgeUI>(), GetPrivate(menu, "dailyBonusBadge"));

                // 3. DailyBonusPopup modal
                Transform dailyPopup = safe.Find("DailyBonusPopup");
                Transform achPopup = safe.Find("AchievementPopup");
                Assert.IsNotNull(dailyPopup);
                Assert.IsNotNull(achPopup);
                Assert.Less(dailyPopup.GetSiblingIndex(), achPopup.GetSiblingIndex(), "Achievement toasts must render above DailyBonusPopup");

                var dailyUi = dailyPopup.GetComponent<DailyBonusScreenUI>();
                Assert.IsNotNull(dailyUi);
                var popupPanel = (GameObject)GetPrivate(dailyUi, "popupPanel");
                Assert.IsNotNull(popupPanel);
                Assert.IsFalse(popupPanel.activeSelf, "DailyBonusPopup starts closed");
                Assert.IsNotNull(GetPrivate(dailyUi, "closeButton"));
                Assert.IsNotNull(GetPrivate(dailyUi, "backgroundButton"));

                // 4. Obsolete navigation removed
                Assert.IsNull(safe.Find("UpgradesPanel/ConstellationBanner"), "ConstellationBanner removed from UpgradesPanel");
                Assert.IsNull(safe.Find("ConstellationScreen/ConstellationPanel/Header/BackButton"), "BackButton removed from ConstellationPanel");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
