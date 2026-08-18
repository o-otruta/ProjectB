using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;
using ProjectB.UI;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ProjectB.Editor
{
    public class MetaUpgradeBuilder
    {
        [MenuItem("Tools/Build Meta Upgrades System")]
        public static void BuildSystem()
        {
            CreateScriptableObjects();
            CreateUI();
            AssetDatabase.SaveAssets();
            Debug.Log("Meta Upgrades System built successfully!");
        }

        private static void CreateScriptableObjects()
        {
            string folderPath = "Assets/Resources/MetaUpgrades";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Resources", "MetaUpgrades");

            CreateSO(folderPath, "hero_hp", "Hero HP", "Increases max health.", MetaUpgradeEffectType.HeroHP, 100, 1.2f, 20, 10f);
            CreateSO(folderPath, "hero_damage", "Hero Damage", "Increases base damage.", MetaUpgradeEffectType.HeroDamage, 150, 1.3f, 20, 8f);
            CreateSO(folderPath, "hero_speed", "Movement Speed", "Increases movement speed.", MetaUpgradeEffectType.HeroSpeed, 200, 1.4f, 15, 5f);
            CreateSO(folderPath, "magnet_radius", "Magnet Radius", "Increases XP pickup range.", MetaUpgradeEffectType.MagnetRadius, 100, 1.3f, 10, 15f);
            CreateSO(folderPath, "start_level", "Starting Level", "Start with higher level.", MetaUpgradeEffectType.StartLevel, 1000, 2.0f, 3, 1f);
            CreateSO(folderPath, "xp_bonus", "XP Bonus", "Increases XP gained.", MetaUpgradeEffectType.XPBonus, 300, 1.5f, 10, 10f);
            CreateSO(folderPath, "ability_damage", "Ability Damage", "Increases ability damage.", MetaUpgradeEffectType.AbilityDamage, 250, 1.3f, 20, 8f);
        }

        private static void CreateSO(string path, string id, string displayName, string description, MetaUpgradeEffectType type, int baseCost, float multiplier, int maxLevel, float effect)
        {
            string assetPath = $"{path}/{id}.asset";
            MetaUpgradeData data = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MetaUpgradeData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }
            
            data.id = id;
            data.displayName = displayName;
            data.description = description;
            data.effectType = type;
            data.baseCost = baseCost;
            data.costMultiplier = multiplier;
            data.maxLevel = maxLevel;
            data.effectPerLevel = effect;
            
            EditorUtility.SetDirty(data);
        }

        private static void CreateUI()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "MainMenu")
            {
                Debug.LogWarning("Please open MainMenu scene to build the UI.");
                return;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            MainMenuUI mainMenuUI = Object.FindAnyObjectByType<MainMenuUI>();
            if (mainMenuUI == null) return;

            // 1. Создаем панель UpgradesPanel
            GameObject panelObj = new GameObject("UpgradesPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(0, 200);
            panelRect.offsetMax = new Vector2(0, -150);

            // Заголовок
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
            titleObj.transform.SetParent(panelObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(0, -150);
            titleRect.offsetMax = new Vector2(0, 0);
            
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "UPGRADES";
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;

            // Scroll View
            GameObject scrollViewObj = new GameObject("Scroll View", typeof(RectTransform));
            scrollViewObj.transform.SetParent(panelObj.transform, false);
            RectTransform scrollRect = scrollViewObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(50, 50);
            scrollRect.offsetMax = new Vector2(-50, -150);

            GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform));
            viewportObj.transform.SetParent(scrollViewObj.transform, false);
            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            viewportObj.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;

            // Контейнер Content
            GameObject listContainerObj = new GameObject("Content", typeof(RectTransform));
            listContainerObj.transform.SetParent(viewportObj.transform, false);
            RectTransform listRect = listContainerObj.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0, 1);
            listRect.anchorMax = new Vector2(1, 1);
            listRect.pivot = new Vector2(0.5f, 1);
            listRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup vlg = listContainerObj.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 20;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.UpperCenter;
            
            var csf = listContainerObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollViewObj.AddComponent<ScrollRect>();
            sr.content = listRect;
            sr.viewport = viewportRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;

            // 2. Создаем префаб карточки
            string prefabPath = "Assets/_Prefabs/UI/UpgradeItem.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs")) AssetDatabase.CreateFolder("Assets", "_Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs/UI")) AssetDatabase.CreateFolder("Assets/_Prefabs", "UI");

            GameObject itemObj = new GameObject("UpgradeItem", typeof(RectTransform));
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 150);
            
            LayoutElement itemLe = itemObj.AddComponent<LayoutElement>();
            itemLe.preferredHeight = 150;
            itemLe.preferredWidth = 800;
            itemLe.flexibleWidth = 1;

            // Иконка
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(itemObj.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(100, 100);
            iconRect.anchoredPosition = new Vector2(30, 0);
            Image iconImage = iconObj.AddComponent<Image>();

            // Title
            GameObject nameObj = new GameObject("TitleText", typeof(RectTransform));
            nameObj.transform.SetParent(itemObj.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0, 1);
            nameRect.offsetMin = new Vector2(160, -70);
            nameRect.offsetMax = new Vector2(-250, -20);
            TextMeshProUGUI nameT = nameObj.AddComponent<TextMeshProUGUI>();
            nameT.text = "Name";
            nameT.fontSize = 36;
            nameT.alignment = TextAlignmentOptions.TopLeft;
            nameT.enableAutoSizing = false;

            // Description
            GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform));
            descObj.transform.SetParent(itemObj.transform, false);
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0);
            descRect.pivot = new Vector2(0, 0);
            descRect.offsetMin = new Vector2(160, 20);
            descRect.offsetMax = new Vector2(-250, 70);
            TextMeshProUGUI levelT = descObj.AddComponent<TextMeshProUGUI>();
            levelT.text = "Lv.0/10";
            levelT.fontSize = 36;
            levelT.alignment = TextAlignmentOptions.BottomLeft;
            levelT.enableAutoSizing = false;

            // Кнопка
            GameObject buyBtnObj = new GameObject("BuyButton", typeof(RectTransform));
            buyBtnObj.transform.SetParent(itemObj.transform, false);
            RectTransform buyRect = buyBtnObj.GetComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(1, 0.5f);
            buyRect.anchorMax = new Vector2(1, 0.5f);
            buyRect.pivot = new Vector2(1, 0.5f);
            buyRect.sizeDelta = new Vector2(200, 80);
            buyRect.anchoredPosition = new Vector2(-30, 0);
            Image buyBg = buyBtnObj.AddComponent<Image>();
            buyBg.color = Color.green;
            Button buyButton = buyBtnObj.AddComponent<Button>();

            GameObject costObj = new GameObject("CostText", typeof(RectTransform));
            costObj.transform.SetParent(buyBtnObj.transform, false);
            RectTransform costRect = costObj.GetComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero; costRect.anchorMax = Vector2.one;
            costRect.offsetMin = costRect.offsetMax = Vector2.zero;
            TextMeshProUGUI costT = costObj.AddComponent<TextMeshProUGUI>();
            costT.text = "100";
            costT.alignment = TextAlignmentOptions.Center;
            costT.color = Color.black;

            UpgradeItemUI itemComponent = itemObj.AddComponent<UpgradeItemUI>();
            
            // Link references using SerializedObject
            SerializedObject soItem = new SerializedObject(itemComponent);
            soItem.FindProperty("nameText").objectReferenceValue = nameT;
            soItem.FindProperty("levelText").objectReferenceValue = levelT;
            soItem.FindProperty("costText").objectReferenceValue = costT;
            soItem.FindProperty("buyButton").objectReferenceValue = buyButton;
            soItem.FindProperty("iconImage").objectReferenceValue = iconImage;
            soItem.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(itemObj, prefabPath);
            Object.DestroyImmediate(itemObj);

            // 3. Подключаем все к MetaUpgradeUI
            MetaUpgradeUI uiComponent = panelObj.AddComponent<MetaUpgradeUI>();
            SerializedObject soUI = new SerializedObject(uiComponent);
            soUI.FindProperty("panel").objectReferenceValue = panelObj;
            soUI.FindProperty("itemsContainer").objectReferenceValue = listContainerObj.transform;
            soUI.FindProperty("itemPrefab").objectReferenceValue = prefab.GetComponent<UpgradeItemUI>();
            soUI.ApplyModifiedProperties();

            // 4. Подключаем к MainMenuUI
            SerializedObject soMainMenu = new SerializedObject(mainMenuUI);
            soMainMenu.FindProperty("metaUpgradeUI").objectReferenceValue = uiComponent;
            soMainMenu.ApplyModifiedProperties();

            panelObj.SetActive(false); // Прячем панель
            
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
