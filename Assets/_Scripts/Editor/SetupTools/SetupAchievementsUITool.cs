using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    public class SetupAchievementsUITool
    {
        [MenuItem("ProjectB/Setup/Setup Achievements UI")]
        public static void Setup()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found in the scene.");
                return;
            }

            GameObject panelGo = new GameObject("AchievementsPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.95f);

            var screenUI = panelGo.AddComponent<AchievementScreenUI>();

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 150);
            titleRect.anchoredPosition = new Vector2(0, 0);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "Achievements";
            titleText.fontSize = 72;
            titleText.alignment = TextAlignmentOptions.Center;

            // Close Button
            GameObject closeGo = new GameObject("CloseButton");
            closeGo.transform.SetParent(panelGo.transform, false);
            var closeRect = closeGo.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.sizeDelta = new Vector2(100, 100);
            closeRect.anchoredPosition = new Vector2(-50, -50);
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = Color.red;
            var closeBtn = closeGo.AddComponent<Button>();

            // Scroll View
            GameObject scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.offsetMin = new Vector2(50, 50);
            scrollRect.offsetMax = new Vector2(-50, -150);
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(1, 1, 1, 0.1f);
            var scrollRectComp = scrollGo.AddComponent<ScrollRect>();

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 20;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRectComp.content = contentRect;
            scrollRectComp.viewport = viewportRect;
            scrollRectComp.horizontal = false;
            
            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs"))
                AssetDatabase.CreateFolder("Assets", "_Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/_Prefabs", "UI");

            string prefabPath = "Assets/_Prefabs/UI/AchievementItem.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject itemGo = new GameObject("AchievementItem");
                var itemRect = itemGo.AddComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0, 1);
                itemRect.anchorMax = new Vector2(1, 1);
                itemRect.sizeDelta = new Vector2(0, 150);
                var le = itemGo.AddComponent<LayoutElement>();
                le.preferredHeight = 150;
                le.preferredWidth = 800;
                le.flexibleWidth = 1;
                var itemImg = itemGo.AddComponent<Image>();
                itemImg.color = new Color(0.2f, 0.2f, 0.2f);
                var itemUI = itemGo.AddComponent<AchievementItemUI>();

                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(itemGo.transform, false);
                var iconRect = iconGo.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0, 0.5f);
                iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f);
                iconRect.anchoredPosition = new Vector2(70, 0);
                iconRect.sizeDelta = new Vector2(100, 100);
                var iconComp = iconGo.AddComponent<Image>();

                GameObject titleTextGo = new GameObject("Title");
                titleTextGo.transform.SetParent(itemGo.transform, false);
                var titleTextRect = titleTextGo.AddComponent<RectTransform>();
                titleTextRect.anchorMin = new Vector2(0, 1);
                titleTextRect.anchorMax = new Vector2(1, 1);
                titleTextRect.pivot = new Vector2(0, 1);
                titleTextRect.anchoredPosition = new Vector2(150, -20);
                titleTextRect.sizeDelta = new Vector2(500, 50);
                var titleTextComp = titleTextGo.AddComponent<TextMeshProUGUI>();
                titleTextComp.fontSize = 36;
                titleTextComp.color = Color.white;

                GameObject descTextGo = new GameObject("Desc");
                descTextGo.transform.SetParent(itemGo.transform, false);
                var descTextRect = descTextGo.AddComponent<RectTransform>();
                descTextRect.anchorMin = new Vector2(0, 0);
                descTextRect.anchorMax = new Vector2(1, 0);
                descTextRect.pivot = new Vector2(0, 0);
                descTextRect.anchoredPosition = new Vector2(150, 20);
                descTextRect.sizeDelta = new Vector2(-270, 50);
                var descTextComp = descTextGo.AddComponent<TextMeshProUGUI>();
                descTextComp.fontSize = 24;
                descTextComp.color = Color.gray;

                GameObject progTextGo = new GameObject("ProgressText");
                progTextGo.transform.SetParent(itemGo.transform, false);
                var progTextRect = progTextGo.AddComponent<RectTransform>();
                progTextRect.anchorMin = new Vector2(1, 1);
                progTextRect.anchorMax = new Vector2(1, 1);
                progTextRect.pivot = new Vector2(1, 1);
                progTextRect.anchoredPosition = new Vector2(-50, -20);
                progTextRect.sizeDelta = new Vector2(200, 50);
                var progTextComp = progTextGo.AddComponent<TextMeshProUGUI>();
                progTextComp.fontSize = 32;
                progTextComp.alignment = TextAlignmentOptions.Right;

                GameObject markGo = new GameObject("CompletedMark");
                markGo.transform.SetParent(itemGo.transform, false);
                var markRect = markGo.AddComponent<RectTransform>();
                markRect.anchorMin = new Vector2(1, 0.5f);
                markRect.anchorMax = new Vector2(1, 0.5f);
                markRect.pivot = new Vector2(1, 0.5f);
                markRect.anchoredPosition = new Vector2(-60, 0);
                markRect.sizeDelta = new Vector2(60, 60);
                var markImg = markGo.AddComponent<Image>();
                markImg.color = Color.green;

                var soItem = new SerializedObject(itemUI);
                soItem.FindProperty("iconImage").objectReferenceValue = iconComp;
                soItem.FindProperty("titleText").objectReferenceValue = titleTextComp;
                soItem.FindProperty("descriptionText").objectReferenceValue = descTextComp;
                soItem.FindProperty("progressText").objectReferenceValue = progTextComp;
                soItem.FindProperty("completedMark").objectReferenceValue = markGo;
                soItem.ApplyModifiedProperties();

                prefab = PrefabUtility.SaveAsPrefabAsset(itemGo, prefabPath);
                GameObject.DestroyImmediate(itemGo);
            }

            var soScreen = new SerializedObject(screenUI);
            soScreen.FindProperty("contentContainer").objectReferenceValue = content.transform;
            if (prefab != null)
            {
                var prefabUI = prefab.GetComponent<AchievementItemUI>();
                soScreen.FindProperty("itemPrefab").objectReferenceValue = prefabUI;
            }
            soScreen.ApplyModifiedProperties();

            var mainMenuUI = Object.FindAnyObjectByType<MainMenuUI>();
            if (mainMenuUI != null)
            {
                var soMenu = new SerializedObject(mainMenuUI);
                soMenu.FindProperty("achievementScreenUI").objectReferenceValue = screenUI;
                soMenu.ApplyModifiedProperties();
            }

            var action = (UnityEngine.Events.UnityAction<bool>)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<bool>), panelGo, "SetActive");
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(closeBtn.onClick, action, false);

            panelGo.SetActive(false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("Achievements UI Setup Complete!");
        }
    }
}
