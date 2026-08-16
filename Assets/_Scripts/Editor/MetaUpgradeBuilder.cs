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
            // Убедимся, что открыта правильная сцена
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "MainMenu")
            {
                Debug.LogWarning("Please open MainMenu scene to build the UI.");
                return;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found in the scene.");
                return;
            }

            // Ищем MainMenuUI
            MainMenuUI mainMenuUI = Object.FindAnyObjectByType<MainMenuUI>();
            if (mainMenuUI == null)
            {
                Debug.LogError("No MainMenuUI found in the scene.");
                return;
            }

            // 1. Создаем панель MetaUpgradeUI
            GameObject panelObj = new GameObject("MetaUpgradeUI", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Добавляем фон
            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.9f);

            // Добавляем заголовок
            GameObject titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(panelObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(0, -100);
            titleRect.offsetMax = new Vector2(0, 0);
            
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "UPGRADES";
            titleText.fontSize = 60;
            titleText.alignment = TextAlignmentOptions.Center;

            // Кнопка закрытия
            GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
            closeBtnObj.transform.SetParent(panelObj.transform, false);
            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.sizeDelta = new Vector2(80, 80);
            closeRect.anchoredPosition = new Vector2(-50, -50);
            Image closeBg = closeBtnObj.AddComponent<Image>();
            closeBg.color = Color.red;
            Button closeBtn = closeBtnObj.AddComponent<Button>();

            // Контейнер для списка
            GameObject listContainerObj = new GameObject("ListContainer", typeof(RectTransform));
            listContainerObj.transform.SetParent(panelObj.transform, false);
            RectTransform listRect = listContainerObj.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0, 0);
            listRect.anchorMax = new Vector2(1, 1);
            listRect.offsetMin = new Vector2(50, 50);
            listRect.offsetMax = new Vector2(-50, -100);

            VerticalLayoutGroup vlg = listContainerObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            listContainerObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 2. Создаем префаб карточки
            string prefabPath = "Assets/_Prefabs/UI/UpgradeItemUI.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs")) AssetDatabase.CreateFolder("Assets", "_Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs/UI")) AssetDatabase.CreateFolder("Assets/_Prefabs", "UI");

            GameObject itemObj = new GameObject("UpgradeItem", typeof(RectTransform));
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 100);
            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            HorizontalLayoutGroup hlg = itemObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.childControlWidth = false;
            hlg.childForceExpandWidth = false;

            // Иконка
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(itemObj.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(80, 80);
            Image iconImage = iconObj.AddComponent<Image>();

            // Тексты
            GameObject textContainer = new GameObject("TextContainer", typeof(RectTransform));
            textContainer.transform.SetParent(itemObj.transform, false);
            LayoutElement textLe = textContainer.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1;
            VerticalLayoutGroup tvlg = textContainer.AddComponent<VerticalLayoutGroup>();

            GameObject nameObj = new GameObject("NameText", typeof(RectTransform));
            nameObj.transform.SetParent(textContainer.transform, false);
            TextMeshProUGUI nameT = nameObj.AddComponent<TextMeshProUGUI>();
            nameT.text = "Name";
            nameT.fontSize = 32;

            GameObject descObj = new GameObject("LevelText", typeof(RectTransform));
            descObj.transform.SetParent(textContainer.transform, false);
            TextMeshProUGUI levelT = descObj.AddComponent<TextMeshProUGUI>();
            levelT.text = "Lv.0/10";
            levelT.fontSize = 24;

            // Кнопка
            GameObject buyBtnObj = new GameObject("BuyButton", typeof(RectTransform));
            buyBtnObj.transform.SetParent(itemObj.transform, false);
            RectTransform buyRect = buyBtnObj.GetComponent<RectTransform>();
            buyRect.sizeDelta = new Vector2(150, 80);
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

            // Закрытие по кнопке
            UnityEngine.Events.UnityAction action = new UnityEngine.Events.UnityAction(uiComponent.Hide);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(closeBtn.onClick, action);

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
