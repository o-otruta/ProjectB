using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class MenuScreenOptimizationTests
    {
        private const string AbilityCatalogPath = "Assets/_ScriptableObjects/Abilities/AbilityCatalog.asset";
        private const string AbilityItemPrefabPath = "Assets/_Prefabs/UI/AbilityCollectionItem.prefab";
        private const string AchievementItemPrefabPath = "Assets/_Prefabs/UI/AchievementItem.prefab";

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
            if (obj != null) created.Add(obj);
            return obj;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field} missing");
            f.SetValue(target, value);
        }

        [Test]
        public void AbilityCollectionItemPrefab_HasInactiveChipTemplate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityItemPrefabPath);
            Assert.IsNotNull(prefab, "AbilityCollectionItem.prefab must exist");

            Transform chipTemplate = prefab.transform.Find("Info/SynergyChips/ChipTemplate");
            Assert.IsNotNull(chipTemplate, "ChipTemplate must exist in AbilityCollectionItem.prefab");
            Assert.IsFalse(chipTemplate.gameObject.activeSelf, "ChipTemplate should start inactive in prefab to avoid Awake overhead");
        }

        [Test]
        public void AbilitiesScreenUI_ReusesItemsLazilyPerFilter_AndTogglesSubCanvasWithoutDeactivating()
        {
            AbilityCatalog abilityCatalog = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilityCatalogPath);
            SynergyCatalog synergyCatalog = Resources.Load<SynergyCatalog>("SynergyCatalog");
            GameObject itemPrefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityItemPrefabPath);
            Assert.IsNotNull(abilityCatalog);
            Assert.IsNotNull(synergyCatalog);
            Assert.IsNotNull(itemPrefabGo);

            int expectedActive = 0;
            int expectedPassive = 0;
            string lockedAbilityId = null;
            foreach (AbilityData ab in abilityCatalog.abilities)
            {
                if (ab == null) continue;
                if (ab.type == AbilityType.Passive) expectedPassive++;
                else expectedActive++;
                if (lockedAbilityId == null && !ab.unlockedByDefault && ab.type != AbilityType.Passive)
                {
                    lockedAbilityId = ab.id;
                }
            }

            var rootGo = Track(new GameObject("RootCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
            Canvas rootCanvas = rootGo.GetComponent<Canvas>();
            rootCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

            var panelGo = new GameObject("AbilitiesPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(AbilitiesScreenUI));
            panelGo.transform.SetParent(rootGo.transform, false);
            panelGo.SetActive(false);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(panelGo.transform, false);

            var ui = panelGo.GetComponent<AbilitiesScreenUI>();
            SetPrivate(ui, "abilityCatalog", abilityCatalog);
            SetPrivate(ui, "synergyCatalog", synergyCatalog);
            SetPrivate(ui, "contentContainer", contentGo.transform);
            SetPrivate(ui, "itemPrefab", itemPrefabGo.GetComponent<AbilityCollectionItemUI>());

            using var save = new SaveManager(new InMemorySaveStorage());
            var ach = new AchievementManager(save);
            using var slots = new AbilitySlotService(save, new MetaUpgradeManager(save));
            ui.Construct(save, ach, slots, null);

            // First Show: builds ONLY Active items lazily.
            ui.Show();
            Assert.IsTrue(panelGo.activeSelf);
            Assert.IsTrue(ui.IsVisible);
            Assert.AreEqual(expectedActive, contentGo.transform.childCount, "First open builds only Active abilities");

            Canvas subCanvas = panelGo.GetComponent<Canvas>();
            GraphicRaycaster subRaycaster = panelGo.GetComponent<GraphicRaycaster>();
            CanvasGroup group = panelGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(subCanvas, "Nested Canvas added for isolation");
            Assert.IsNotNull(subRaycaster);
            Assert.AreEqual(rootCanvas.additionalShaderChannels, subCanvas.additionalShaderChannels);
            Assert.IsTrue(subCanvas.enabled);
            Assert.IsTrue(subRaycaster.enabled);
            Assert.IsTrue(group.interactable);
            Assert.IsTrue(group.blocksRaycasts);

            var firstActiveItems = new List<AbilityCollectionItemUI>(contentGo.GetComponentsInChildren<AbilityCollectionItemUI>(true));

            // Hide keeps GameObject active and disables sub-Canvas + raycaster + CanvasGroup interaction.
            ui.Hide();
            Assert.IsTrue(panelGo.activeSelf, "Hide keeps panel active so TMP meshes and layout stay cached");
            Assert.IsFalse(ui.IsVisible);
            Assert.IsFalse(subCanvas.enabled);
            Assert.IsFalse(subRaycaster.enabled);
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);

            // Re-show and RefreshUI reuse exact same item instances.
            ui.Show();
            Assert.IsTrue(subCanvas.enabled);
            Assert.AreEqual(expectedActive, contentGo.transform.childCount);
            ui.RefreshUI();
            Assert.AreEqual(expectedActive, contentGo.transform.childCount);
            var afterRefreshItems = contentGo.GetComponentsInChildren<AbilityCollectionItemUI>(true);
            for (int i = 0; i < firstActiveItems.Count; i++)
            {
                Assert.AreSame(firstActiveItems[i], afterRefreshItems[i], "Items must be reused instead of destroyed/reinstantiated");
            }

            // Switch to Passive, then Synergies, then back to Active.
            ui.SetFilter(AbilityCollectionFilter.Passive);
            Assert.AreEqual(expectedActive + expectedPassive, contentGo.transform.childCount);
            ui.SetFilter(AbilityCollectionFilter.Synergies);
            Assert.AreEqual(expectedActive + expectedPassive + synergyCatalog.synergies.Count, contentGo.transform.childCount);
            ui.SetFilter(AbilityCollectionFilter.Active);
            Assert.AreEqual(expectedActive + expectedPassive + synergyCatalog.synergies.Count, contentGo.transform.childCount,
                "Switching filters must reuse existing category pools without spawning extra objects");

            for (int i = 0; i < firstActiveItems.Count; i++)
            {
                Assert.IsTrue(firstActiveItems[i].gameObject.activeSelf);
            }

            // Mutating save state while hidden marks dirty and updates card on next Show().
            if (!string.IsNullOrEmpty(lockedAbilityId))
            {
                ui.Hide();
                save.UnlockAbility(lockedAbilityId, saveImmediately: false);
                ui.Show();

                AbilityCollectionItemUI unlockedCard = null;
                foreach (AbilityCollectionItemUI card in firstActiveItems)
                {
                    if (card != null && card.transform.Find("IconBg/LockBadge") != null)
                    {
                        var nameTmp = card.transform.Find("Info/Name")?.GetComponent<TextMeshProUGUI>();
                        AbilityData targetData = abilityCatalog.abilities.Find(a => a != null && a.id == lockedAbilityId);
                        if (targetData != null && nameTmp != null && nameTmp.text == Loc.Name(targetData))
                        {
                            unlockedCard = card;
                            break;
                        }
                    }
                }
                Assert.IsNotNull(unlockedCard);
                Assert.IsFalse(unlockedCard.transform.Find("IconBg/LockBadge").gameObject.activeSelf,
                    "Unlocking ability while hidden must refresh card on next Show()");
            }
        }

        [Test]
        public void AchievementScreenUI_PoolsItemsAcrossCategorySwitches_AndTogglesSubCanvasWithoutDeactivating()
        {
            GameObject itemPrefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(AchievementItemPrefabPath);
            Assert.IsNotNull(itemPrefabGo);

            var rootGo = Track(new GameObject("RootCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
            Canvas rootCanvas = rootGo.GetComponent<Canvas>();
            rootCanvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

            var panelGo = new GameObject("AchievementsPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(AchievementScreenUI));
            panelGo.transform.SetParent(rootGo.transform, false);
            panelGo.SetActive(false);

            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
            contentGo.transform.SetParent(viewportGo.transform, false);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewportGo.transform;
            scroll.content = (RectTransform)contentGo.transform;

            var ui = panelGo.GetComponent<AchievementScreenUI>();
            SetPrivate(ui, "contentContainer", contentGo.transform);
            SetPrivate(ui, "itemPrefab", itemPrefabGo.GetComponent<AchievementItemUI>());

            using var save = new SaveManager(new InMemorySaveStorage());
            var ach = new AchievementManager(save);
            int totalAchievements = ach.GetAllAchievements().Count;
            Assert.Greater(totalAchievements, 10);

            ui.Construct(ach, save);
            ui.Show();

            Transform categoryBar = scrollGo.transform.Find("CategoryBar");
            Assert.IsNotNull(categoryBar);
            Assert.IsNull(categoryBar.GetComponent<Image>().sprite, "CategoryBar must use an untextured quad to avoid atlas edge bleed");
            Assert.GreaterOrEqual(viewportGo.GetComponent<RectMask2D>().padding.w, 92f, "Viewport RectMask2D must clip below CategoryBar");

            Assert.IsTrue(panelGo.activeSelf);
            Assert.IsTrue(ui.IsVisible);
            Assert.AreEqual(totalAchievements, contentGo.transform.childCount);

            var poolItems = new List<AchievementItemUI>(contentGo.GetComponentsInChildren<AchievementItemUI>(true));

            // Category filter reuses pooled items instead of destroying/recreating.
            ui.SetCategoryFilter(AchievementCategory.Bosses);
            Assert.AreEqual(totalAchievements, contentGo.transform.childCount, "Category switch must reuse pooled items");

            int activeInBosses = 0;
            foreach (AchievementItemUI item in poolItems)
            {
                if (item.gameObject.activeSelf)
                {
                    activeInBosses++;
                    Assert.AreEqual(AchievementCategory.Bosses, item.Data.category);
                }
            }
            Assert.Greater(activeInBosses, 0);
            Assert.Less(activeInBosses, totalAchievements);

            ui.SetCategoryFilter(null);
            Assert.AreEqual(totalAchievements, contentGo.transform.childCount);
            var afterAllItems = contentGo.GetComponentsInChildren<AchievementItemUI>(true);
            for (int i = 0; i < poolItems.Count; i++)
            {
                Assert.AreSame(poolItems[i], afterAllItems[i]);
                Assert.IsTrue(poolItems[i].gameObject.activeSelf);
            }

            // Hide/Show toggles sub-Canvas without deactivating GameObject.
            Canvas subCanvas = panelGo.GetComponent<Canvas>();
            GraphicRaycaster subRaycaster = panelGo.GetComponent<GraphicRaycaster>();
            CanvasGroup group = panelGo.GetComponent<CanvasGroup>();
            ui.Hide();
            Assert.IsTrue(panelGo.activeSelf);
            Assert.IsFalse(subCanvas.enabled);
            Assert.IsFalse(subRaycaster.enabled);
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);

            ui.Show();
            Assert.IsTrue(subCanvas.enabled);
            Assert.IsTrue(subRaycaster.enabled);
            Assert.IsTrue(group.interactable);
            Assert.IsTrue(group.blocksRaycasts);

            // FocusAchievement selects target category and locates item immediately.
            AchievementData target = ach.GetAllAchievements()[0];
            Assert.IsTrue(ui.FocusAchievement(target.id));
            Assert.AreEqual(target.category, ui.SelectedCategory);
            Assert.IsNotNull(ui.FindItem(target.id));
        }
    }
}
