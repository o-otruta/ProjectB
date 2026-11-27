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
using ProjectB.Data.Platform;
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

        [Test]
        public void MainMenuScene_AbilitiesHeaderChips_AreFramelessNonClickableReadouts()
        {
            Scene loaded = SceneManager.GetSceneByPath(MainMenuPath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform safe = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Canvas")
                    {
                        safe = root.transform.Find("SafeAreaRoot");
                        break;
                    }
                }
                Assert.IsNotNull(safe, "Canvas/SafeAreaRoot");

                Transform abilitiesPanel = safe.Find("AbilitiesPanel");
                Assert.IsNotNull(abilitiesPanel, "AbilitiesPanel");
                var abilitiesUi = abilitiesPanel.GetComponent<AbilitiesScreenUI>();
                Assert.IsNotNull(abilitiesUi, "AbilitiesScreenUI");

                Transform headerChips = abilitiesPanel.Find("HeaderChips");
                Assert.IsNotNull(headerChips, "AbilitiesPanel/HeaderChips");

                var expectedFields = new (string chipName, string fieldName)[]
                {
                    ("CollectedChip", "collectedChipText"),
                    ("SynergiesChip", "synergiesChipText"),
                    ("SlotsChip", "slotsChipText")
                };

                foreach (var (chipName, fieldName) in expectedFields)
                {
                    Transform chip = headerChips.Find(chipName);
                    Assert.IsNotNull(chip, $"HeaderChips/{chipName}");
                    Assert.IsNull(chip.GetComponent<Button>(), $"{chipName} must not have a Button");
                    Assert.IsNull(chip.GetComponent<ThemedButton>(), $"{chipName} must not have a ThemedButton");
                    Assert.IsNotNull(chip.Find("Icon"), $"{chipName}/Icon");
                    Transform textT = chip.Find("Text");
                    Assert.IsNotNull(textT, $"{chipName}/Text");
                    var textTmp = textT.GetComponent<TMP_Text>();
                    Assert.AreSame(textTmp, GetPrivate(abilitiesUi, fieldName),
                        $"AbilitiesScreenUI.{fieldName} must remain wired to {chipName}/Text");
                    var textTg = textT.GetComponent<ThemedGraphic>();
                    Assert.IsNotNull(textTg, $"{chipName}/Text ThemedGraphic");
                    Assert.AreEqual(UiColor.TextSecondary, textTg.Color, $"{chipName}/Text must use UiColor.TextSecondary");
                    Assert.AreEqual(UiTheme.Current.textSecondary, textTmp.color, $"{chipName}/Text color must match TextSecondary");

                    var chipImg = chip.GetComponent<Image>();
                    Assert.IsNotNull(chipImg, $"{chipName} Image");
                    Assert.IsFalse(chipImg.raycastTarget, $"{chipName} must not capture raycasts");
                    Assert.LessOrEqual(chipImg.color.a, 0.01f, $"{chipName} must have no button-like frame");
                    Assert.AreNotSame(UiTheme.Current.surfaceMaterial, chipImg.material, $"{chipName} must not use ToySurface material");

                    var tg = chip.GetComponent<ThemedGraphic>();
                    Assert.IsNotNull(tg, $"{chipName} ThemedGraphic");
                    tg.ApplyTheme(UiTheme.Current);
                    Assert.LessOrEqual(chipImg.color.a, 0.01f, $"{chipName} stays frameless after ThemedGraphic.ApplyTheme");
                    Assert.AreNotSame(UiTheme.Current.surfaceMaterial, chipImg.material, $"{chipName} keeps ToySurface cleared after ApplyTheme");
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MainMenuScene_AbilitiesAndAchievements_ShareUnifiedHeaderFilterAndCardStyle()
        {
            UiTheme theme = UiTheme.Current;

            // 1. Card prefabs share clean surface + 116x116 icon frame (no harsh full-card frame or stripe).
            GameObject abilityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityItemPrefabPath);
            GameObject achievementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/UI/AchievementItem.prefab");
            Assert.IsNotNull(abilityPrefab);
            Assert.IsNotNull(achievementPrefab);

            Transform abilityFrame = abilityPrefab.transform.Find("Frame");
            Transform abilityStripe = abilityPrefab.transform.Find("RarityStripe");
            Assert.IsNotNull(abilityFrame);
            Assert.IsNotNull(abilityStripe);
            Assert.IsFalse(abilityFrame.gameObject.activeSelf, "AbilityCollectionItem Frame must be inactive to match AchievementItem");
            Assert.IsFalse(abilityStripe.gameObject.activeSelf, "AbilityCollectionItem RarityStripe must be inactive to match AchievementItem");

            var abilityIconLe = abilityPrefab.transform.Find("IconBg")?.GetComponent<LayoutElement>();
            var achIconLe = achievementPrefab.transform.Find("TopRow/IconFrame")?.GetComponent<LayoutElement>();
            Assert.IsNotNull(abilityIconLe);
            Assert.IsNotNull(achIconLe);
            Assert.AreEqual(achIconLe.preferredWidth, abilityIconLe.preferredWidth, 0.01f);
            Assert.AreEqual(achIconLe.preferredHeight, abilityIconLe.preferredHeight, 0.01f);

            // 2. MainMenu scene alignment & filter pill styling.
            Scene loaded = SceneManager.GetSceneByPath(MainMenuPath);
            bool openedHere = !loaded.isLoaded;
            Scene scene = openedHere ? EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive) : loaded;
            try
            {
                Transform safe = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Canvas")
                    {
                        safe = root.transform.Find("SafeAreaRoot");
                        break;
                    }
                }
                Assert.IsNotNull(safe, "Canvas/SafeAreaRoot");

                Transform abPanel = safe.Find("AbilitiesPanel");
                Transform achPanel = safe.Find("AchievementsPanel");
                Assert.IsNotNull(abPanel);
                Assert.IsNotNull(achPanel);

                foreach (LayoutMode mode in new[] { LayoutMode.Portrait, LayoutMode.Landscape })
                {
                    RectPreset abTitle = abPanel.Find("Title").GetComponent<ResponsiveRect>().GetPreset(mode);
                    RectPreset achTitle = achPanel.Find("Title").GetComponent<ResponsiveRect>().GetPreset(mode);
                    Assert.AreEqual(achTitle.anchoredPosition, abTitle.anchoredPosition, $"Title position must match in {mode}");
                    Assert.AreEqual(achTitle.sizeDelta, abTitle.sizeDelta, $"Title size must match in {mode}");

                    RectPreset abSub = abPanel.Find("HeaderChips").GetComponent<ResponsiveRect>().GetPreset(mode);
                    RectPreset achSub = achPanel.Find("Summary").GetComponent<ResponsiveRect>().GetPreset(mode);
                    Assert.AreEqual(achSub.anchoredPosition, abSub.anchoredPosition, $"Subtitle band position must match in {mode}");
                    Assert.AreEqual(achSub.sizeDelta, abSub.sizeDelta, $"Subtitle band size must match in {mode}");
                }

                Transform filter = abPanel.Find("Filter");
                Assert.IsNotNull(filter);
                var filterImg = filter.GetComponent<Image>();
                Assert.IsNotNull(filterImg);
                Assert.LessOrEqual(filterImg.color.a, 0.01f, "Filter container must be frameless");
                Assert.AreNotSame(theme.surfaceMaterial, filterImg.material, "Filter container must not use ToySurface material");

                RectPreset filterPortrait = filter.GetComponent<ResponsiveRect>().GetPreset(LayoutMode.Portrait);
                RectPreset achScrollPortrait = achPanel.Find("ScrollView").GetComponent<ResponsiveRect>().GetPreset(LayoutMode.Portrait);
                float achCategoryTop = achScrollPortrait.anchoredPosition.y + achScrollPortrait.sizeDelta.y * 0.5f;
                Assert.AreEqual(achCategoryTop, filterPortrait.anchoredPosition.y, 0.01f, "Filter top aligns with Achievements CategoryBar top in Portrait");

                foreach (string chipName in new[] { "FilterActive", "FilterPassive", "FilterSynergies" })
                {
                    Transform chip = filter.Find(chipName);
                    Assert.IsNotNull(chip, chipName);
                    var chipImg = chip.GetComponent<Image>();
                    Assert.IsNotNull(chipImg);
                    Assert.AreSame(theme.pillSprite, chipImg.sprite, $"{chipName} must use pillSprite");
                    Assert.AreNotSame(theme.surfaceMaterial, chipImg.material, $"{chipName} must not use ToySurface material");
                    var chipLe = chip.GetComponent<LayoutElement>();
                    Assert.IsNotNull(chipLe);
                    Assert.AreEqual(72f, chipLe.minHeight, 0.01f, $"{chipName} height matches AchievementScreenUI chips");

                    Transform labelT = chip.Find("Label");
                    Assert.IsNotNull(labelT);
                    Assert.IsNull(labelT.GetComponent<LocalizedText>(), $"{chipName}/Label must not keep LocalizedText over dynamic counts");
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }

            // 3. Runtime AbilitiesScreenUI populates filter chip counts and applies pill colors (on isolated test objects).
            AbilityCatalog abilityCatalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilityCatalogPath);
            SynergyCatalog synergyCatalog = Resources.Load<SynergyCatalog>("SynergyCatalog");
            var save = new SaveManager(new InMemorySaveStorage());
            disposables.Add(save);
            TalentTreeData realTree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            var talentMgr = new TalentTreeManager(save, realTree, null);
            disposables.Add(talentMgr);

            var rootGo = Track(new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas)));
            var testPanelGo = new GameObject("AbilitiesPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(AbilitiesScreenUI));
            testPanelGo.transform.SetParent(rootGo.transform, false);
            testPanelGo.SetActive(false);
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(testPanelGo.transform, false);

            var filterBtns = new Button[3];
            var filterBgs = new Image[3];
            var filterTxts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var chipGo = new GameObject($"Filter_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                chipGo.transform.SetParent(testPanelGo.transform, false);
                var txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(chipGo.transform, false);
                filterBtns[i] = chipGo.GetComponent<Button>();
                filterBgs[i] = chipGo.GetComponent<Image>();
                filterTxts[i] = txtGo.GetComponent<TMP_Text>();
            }

            var abUi = testPanelGo.GetComponent<AbilitiesScreenUI>();
            SetPrivate(abUi, "abilityCatalog", abilityCatalog);
            SetPrivate(abUi, "synergyCatalog", synergyCatalog);
            SetPrivate(abUi, "contentContainer", contentGo.transform);
            SetPrivate(abUi, "itemPrefab", abilityPrefab.GetComponent<AbilityCollectionItemUI>());
            SetPrivate(abUi, "filterButtons", filterBtns);
            SetPrivate(abUi, "filterBackgrounds", filterBgs);
            SetPrivate(abUi, "filterLabels", filterTxts);
            abUi.Construct(save, null, null, talentMgr, null);
            abUi.Show();

            Assert.AreEqual(theme.accent, filterBgs[0].color);
            Assert.AreEqual(theme.surfaceRaised, filterBgs[1].color);
            Assert.AreEqual(theme.surfaceRaised, filterBgs[2].color);
            StringAssert.Contains($"/{abilityCatalog.abilities.FindAll(a => a != null && a.type != AbilityType.Passive).Count}", filterTxts[0].text);
            StringAssert.Contains($"/{abilityCatalog.abilities.FindAll(a => a != null && a.type == AbilityType.Passive).Count}", filterTxts[1].text);
            StringAssert.Contains($"/{synergyCatalog.synergies.Count}", filterTxts[2].text);

            abUi.SetFilter(AbilityCollectionFilter.Passive);
            Assert.AreEqual(theme.surfaceRaised, filterBgs[0].color);
            Assert.AreEqual(theme.accent, filterBgs[1].color);
        }

        [Test]
        public void ScreenTransition_StopAndReset_WhenIdle_DoesNotClobberResponsivePosition()
        {
            var go = Track(new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(ScreenTransition)));
            var rt = go.GetComponent<RectTransform>();
            var transition = go.GetComponent<ScreenTransition>();
            transition.EditorSetup(rt, new Vector2(0f, -48f));

            // Simulate prior Portrait PlayIn capturing restPosition, followed by Landscape ResponsiveRect repositioning while idle.
            SetPrivate(transition, "restPosition", new Vector2(0f, 25f));
            SetPrivate(transition, "hasRestPosition", true);
            rt.anchoredPosition = new Vector2(100f, -60f);

            transition.StopAndReset();

            Assert.AreEqual(new Vector2(100f, -60f), rt.anchoredPosition);
            Assert.IsFalse((bool)GetPrivate(transition, "hasRestPosition"));
        }
    }
}
