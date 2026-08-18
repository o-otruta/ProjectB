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

            // 1. Find or Create AchievementsPanel
            GameObject panelGo = null;
            Transform existingPanel = canvas.transform.Find("AchievementsPanel");
            if (existingPanel != null)
            {
                panelGo = existingPanel.gameObject;
                panelGo.transform.SetAsFirstSibling();
                var panelRect = panelGo.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = new Vector2(0, 200);
                panelRect.offsetMax = new Vector2(0, -150);
            }
            else
            {
                panelGo = new GameObject("AchievementsPanel");
                panelGo.transform.SetParent(canvas.transform, false);
                panelGo.transform.SetAsFirstSibling();
                var panelRect = panelGo.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = new Vector2(0, 200);
                panelRect.offsetMax = new Vector2(0, -150);
                
                var panelImg = panelGo.AddComponent<Image>();
                panelImg.color = new Color(0, 0, 0, 0.95f);
            }

            var screenUI = panelGo.GetComponent<AchievementScreenUI>();
            if (screenUI == null) screenUI = panelGo.AddComponent<AchievementScreenUI>();

            // Setup internal elements if missing
            Transform titleTrans = panelGo.transform.Find("Title");
            if (titleTrans == null)
            {
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
            }

            Transform closeTrans = panelGo.transform.Find("CloseButton");
            Button closeBtn = null;
            if (closeTrans == null)
            {
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
                closeBtn = closeGo.AddComponent<Button>();
            }
            else
            {
                closeBtn = closeTrans.GetComponent<Button>();
            }

            Transform scrollTrans = panelGo.transform.Find("ScrollView");
            Transform contentTrans = null;
            if (scrollTrans == null)
            {
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

                contentTrans = content.transform;
            }
            else
            {
                var scrollRectComp = scrollTrans.GetComponent<ScrollRect>();
                if (scrollRectComp != null && scrollRectComp.content != null)
                {
                    contentTrans = scrollRectComp.content;
                }
            }

            // 2. Add RewardPopup to AchievementsPanel if missing
            Transform rewardPopupTrans = panelGo.transform.Find("RewardPopup");
            AchievementRewardPopupUI rewardPopupUI = null;
            if (rewardPopupTrans == null)
            {
                GameObject popupGo = new GameObject("RewardPopup");
                popupGo.transform.SetParent(panelGo.transform, false);
                var popupRect = popupGo.AddComponent<RectTransform>();
                popupRect.anchorMin = Vector2.zero;
                popupRect.anchorMax = Vector2.one;
                popupRect.sizeDelta = Vector2.zero;
                
                var popupImg = popupGo.AddComponent<Image>();
                popupImg.color = new Color(0, 0, 0, 0.8f); // semi-transparent background
                
                rewardPopupUI = popupGo.AddComponent<AchievementRewardPopupUI>();
                
                // Add icon
                GameObject popupIconGo = new GameObject("Icon");
                popupIconGo.transform.SetParent(popupGo.transform, false);
                var popupIconRect = popupIconGo.AddComponent<RectTransform>();
                popupIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                popupIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                popupIconRect.pivot = new Vector2(0.5f, 0.5f);
                popupIconRect.anchoredPosition = new Vector2(0, 50);
                popupIconRect.sizeDelta = new Vector2(150, 150);
                var popupIconImg = popupIconGo.AddComponent<Image>();
                
                // Add title
                GameObject popupTitleGo = new GameObject("Title");
                popupTitleGo.transform.SetParent(popupGo.transform, false);
                var popupTitleRect = popupTitleGo.AddComponent<RectTransform>();
                popupTitleRect.anchorMin = new Vector2(0, 0.5f);
                popupTitleRect.anchorMax = new Vector2(1, 0.5f);
                popupTitleRect.pivot = new Vector2(0.5f, 0.5f);
                popupTitleRect.anchoredPosition = new Vector2(0, 200);
                popupTitleRect.sizeDelta = new Vector2(0, 80);
                var popupTitleText = popupTitleGo.AddComponent<TextMeshProUGUI>();
                popupTitleText.fontSize = 60;
                popupTitleText.alignment = TextAlignmentOptions.Center;
                
                // Add description
                GameObject popupDescGo = new GameObject("Description");
                popupDescGo.transform.SetParent(popupGo.transform, false);
                var popupDescRect = popupDescGo.AddComponent<RectTransform>();
                popupDescRect.anchorMin = new Vector2(0, 0.5f);
                popupDescRect.anchorMax = new Vector2(1, 0.5f);
                popupDescRect.pivot = new Vector2(0.5f, 0.5f);
                popupDescRect.anchoredPosition = new Vector2(0, -100);
                popupDescRect.sizeDelta = new Vector2(0, 100);
                var popupDescText = popupDescGo.AddComponent<TextMeshProUGUI>();
                popupDescText.fontSize = 40;
                popupDescText.alignment = TextAlignmentOptions.Center;
                
                // Add Close Button
                GameObject popupCloseGo = new GameObject("CloseButton");
                popupCloseGo.transform.SetParent(popupGo.transform, false);
                var popupCloseRect = popupCloseGo.AddComponent<RectTransform>();
                popupCloseRect.anchorMin = new Vector2(0.5f, 0.5f);
                popupCloseRect.anchorMax = new Vector2(0.5f, 0.5f);
                popupCloseRect.pivot = new Vector2(0.5f, 0.5f);
                popupCloseRect.anchoredPosition = new Vector2(0, -250);
                popupCloseRect.sizeDelta = new Vector2(300, 80);
                var popupCloseImg = popupCloseGo.AddComponent<Image>();
                popupCloseImg.color = Color.green;
                var popupCloseBtn = popupCloseGo.AddComponent<Button>();
                
                GameObject popupCloseTextGo = new GameObject("Text");
                popupCloseTextGo.transform.SetParent(popupCloseGo.transform, false);
                var popupCloseTextRect = popupCloseTextGo.AddComponent<RectTransform>();
                popupCloseTextRect.anchorMin = Vector2.zero;
                popupCloseTextRect.anchorMax = Vector2.one;
                popupCloseTextRect.sizeDelta = Vector2.zero;
                var popupCloseText = popupCloseTextGo.AddComponent<TextMeshProUGUI>();
                popupCloseText.text = "OK";
                popupCloseText.fontSize = 40;
                popupCloseText.alignment = TextAlignmentOptions.Center;
                popupCloseText.color = Color.black;
                
                var soPopup = new SerializedObject(rewardPopupUI);
                soPopup.FindProperty("popupPanel").objectReferenceValue = popupGo;
                soPopup.FindProperty("rewardIconImage").objectReferenceValue = popupIconImg;
                soPopup.FindProperty("titleText").objectReferenceValue = popupTitleText;
                soPopup.FindProperty("descriptionText").objectReferenceValue = popupDescText;
                soPopup.FindProperty("closeButton").objectReferenceValue = popupCloseBtn;
                soPopup.ApplyModifiedProperties();
                
                popupGo.SetActive(false);
            }
            else
            {
                rewardPopupUI = rewardPopupTrans.GetComponent<AchievementRewardPopupUI>();
            }

            // 3. Update AchievementItem Prefab safely
            string prefabPath = "Assets/_Prefabs/UI/AchievementItem.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject prefabInstance = null;
            
            if (prefabAsset != null)
            {
                // Modify existing prefab
                prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
                var itemUI = prefabInstance.GetComponent<AchievementItemUI>();
                
                if (prefabInstance.GetComponent<Button>() == null)
                {
                    prefabInstance.AddComponent<Button>();
                }
                
                Transform progBgTrans = prefabInstance.transform.Find("ProgressBarBackground");
                if (progBgTrans == null)
                {
                    GameObject progBgGo = new GameObject("ProgressBarBackground");
                    progBgGo.transform.SetParent(prefabInstance.transform, false);
                    var progBgRect = progBgGo.AddComponent<RectTransform>();
                    progBgRect.anchorMin = new Vector2(1, 0);
                    progBgRect.anchorMax = new Vector2(1, 0);
                    progBgRect.pivot = new Vector2(1, 0);
                    progBgRect.anchoredPosition = new Vector2(-50, 20);
                    progBgRect.sizeDelta = new Vector2(300, 30);
                    var progBgImg = progBgGo.AddComponent<Image>();
                    progBgImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                    progBgImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    progBgImg.type = Image.Type.Sliced;
                    progBgTrans = progBgGo.transform;
                }
                
                Transform progFillTrans = progBgTrans.Find("ProgressBarFill");
                if (progFillTrans == null)
                {
                    GameObject progFillGo = new GameObject("ProgressBarFill");
                    progFillGo.transform.SetParent(progBgTrans, false);
                    var progFillRect = progFillGo.AddComponent<RectTransform>();
                    progFillRect.anchorMin = Vector2.zero;
                    progFillRect.anchorMax = Vector2.one;
                    progFillRect.sizeDelta = Vector2.zero;
                    var progFillImg = progFillGo.AddComponent<Image>();
                    progFillImg.color = Color.green;
                    progFillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    progFillImg.type = Image.Type.Filled;
                    progFillImg.fillMethod = Image.FillMethod.Horizontal;
                    progFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                    progFillTrans = progFillGo.transform;
                }
                
                var soItem = new SerializedObject(itemUI);
                soItem.FindProperty("itemButton").objectReferenceValue = prefabInstance.GetComponent<Button>();
                soItem.FindProperty("progressBarBackground").objectReferenceValue = progBgTrans.gameObject;
                soItem.FindProperty("progressBarFill").objectReferenceValue = progFillTrans.GetComponent<Image>();
                soItem.ApplyModifiedProperties();
                
                PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
            
            // Re-load the updated prefab asset
            prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // Update ScreenUI
            var soScreen = new SerializedObject(screenUI);
            if (contentTrans != null)
                soScreen.FindProperty("contentContainer").objectReferenceValue = contentTrans;
                
            if (prefabAsset != null)
            {
                var prefabUI = prefabAsset.GetComponent<AchievementItemUI>();
                soScreen.FindProperty("itemPrefab").objectReferenceValue = prefabUI;
            }
            
            if (rewardPopupUI != null)
            {
                soScreen.FindProperty("rewardPopup").objectReferenceValue = rewardPopupUI;
            }
            
            soScreen.ApplyModifiedProperties();

            // Setup bindings automatically
            var mainMenuUI = canvas.GetComponent<MainMenuUI>();
            if (mainMenuUI != null)
            {
                var soMenu = new SerializedObject(mainMenuUI);
                soMenu.FindProperty("achievementScreenUI").objectReferenceValue = screenUI;
                soMenu.ApplyModifiedProperties();
                
                var btnTrans = canvas.transform.Find("ButtonsContainer/AchievementsButton");
                if (btnTrans != null)
                {
                    var btn = btnTrans.GetComponent<Button>();
                    while (btn.onClick.GetPersistentEventCount() > 0)
                    {
                        UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, 0);
                    }
                    var targetAction = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), mainMenuUI, "OnAchievementsClicked") as UnityEngine.Events.UnityAction;
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, targetAction);
                }
            }

            if (closeBtn != null)
            {
                while (closeBtn.onClick.GetPersistentEventCount() > 0)
                {
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(closeBtn.onClick, 0);
                }
                var targetAction = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<bool>), panelGo, "SetActive") as UnityEngine.Events.UnityAction<bool>;
                UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(closeBtn.onClick, targetAction, false);
            }

            panelGo.SetActive(false);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("Achievements UI Setup Complete!");
        }
    }
}
