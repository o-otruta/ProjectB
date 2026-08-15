using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ProjectB.UI;
using ProjectB.Core;
using UnityEditor.SceneManagement;

namespace ProjectB.EditorScripts
{
    public class MainMenuSetup
    {
        [MenuItem("Tools/Setup Main Menu")]
        public static void Setup()
        {
            // Set up the scene hierarchy
            GameObject goScope = new GameObject("MainMenuLifetimeScope");
            var scope = goScope.AddComponent<MainMenuLifetimeScope>();
            
            // Create Canvas
            GameObject canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            
            // UI Script
            var ui = canvasGo.AddComponent<MainMenuUI>();

            // Top Bar
            GameObject topBar = new GameObject("TopBar");
            topBar.transform.SetParent(canvasGo.transform, false);
            var topBarRect = topBar.AddComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.pivot = new Vector2(0.5f, 1);
            topBarRect.anchoredPosition = new Vector2(0, 0);
            topBarRect.sizeDelta = new Vector2(0, 150);
            var topBarImg = topBar.AddComponent<Image>();
            topBarImg.color = new Color(0, 0, 0, 0.5f);

            // Coins Text
            GameObject coinsObj = new GameObject("CoinsText");
            coinsObj.transform.SetParent(topBar.transform, false);
            var coinsRect = coinsObj.AddComponent<RectTransform>();
            coinsRect.anchorMin = new Vector2(1, 0.5f);
            coinsRect.anchorMax = new Vector2(1, 0.5f);
            coinsRect.pivot = new Vector2(1, 0.5f);
            coinsRect.anchoredPosition = new Vector2(-50, 0);
            coinsRect.sizeDelta = new Vector2(400, 100);
            var coinsText = coinsObj.AddComponent<TextMeshProUGUI>();
            coinsText.text = "0";
            coinsText.fontSize = 64;
            coinsText.alignment = TextAlignmentOptions.Right;
            coinsText.color = Color.yellow;
            
            // Link to UI
            var so = new SerializedObject(ui);
            so.FindProperty("coinsText").objectReferenceValue = coinsText;

            // Buttons Container
            GameObject btnContainer = new GameObject("ButtonsContainer");
            btnContainer.transform.SetParent(canvasGo.transform, false);
            var btnContainerRect = btnContainer.AddComponent<RectTransform>();
            btnContainerRect.anchorMin = new Vector2(0.5f, 0);
            btnContainerRect.anchorMax = new Vector2(0.5f, 0);
            btnContainerRect.pivot = new Vector2(0.5f, 0);
            btnContainerRect.anchoredPosition = new Vector2(0, -300);
            btnContainerRect.sizeDelta = new Vector2(600, 1000);
            
            var vlg = btnContainer.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 30;
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;

            // Create buttons
            CreateButton(btnContainer.transform, "PlayButton", "Играть", ui, "OnPlayClicked");
            CreateButton(btnContainer.transform, "UpgradesButton", "Прокачка", ui, "OnUpgradesClicked");
            CreateButton(btnContainer.transform, "AbilitiesButton", "Способности", ui, "OnAbilitiesClicked");
            
            var achievementsBtn = CreateButton(btnContainer.transform, "AchievementsButton", "Ачивки", ui, "OnAchievementsClicked");
            var bonusBtn = CreateButton(btnContainer.transform, "BonusButton", "Бонус", ui, "OnBonusClicked");
            
            CreateButton(btnContainer.transform, "SettingsButton", "Настройки", ui, "OnSettingsClicked");

            // Badges
            GameObject badgeAch = CreateBadge(achievementsBtn.transform, "Badge");
            so.FindProperty("achievementsBadge").objectReferenceValue = badgeAch;
            
            GameObject badgeBonus = CreateBadge(bonusBtn.transform, "Badge");
            so.FindProperty("bonusBadge").objectReferenceValue = badgeBonus;
            
            so.ApplyModifiedProperties();

            // Event System
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // 3D Background
            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Hero.prefab");
            if (heroPrefab != null)
            {
                GameObject hero = PrefabUtility.InstantiatePrefab(heroPrefab) as GameObject;
                hero.transform.position = new Vector3(0, -2, 5); // In front of camera
                hero.transform.rotation = Quaternion.Euler(0, 180, 0);
                hero.AddComponent<MenuHeroRotator>();
            }
            else
            {
                Debug.LogWarning("Hero prefab not found at Assets/_Prefabs/Hero.prefab");
            }
            
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Main Menu Setup Complete!");
        }

        private static GameObject CreateButton(Transform parent, string name, string textStr, MainMenuUI ui, string methodName)
        {
            GameObject btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600, 120);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var btn = btnGo.AddComponent<Button>();
            
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = textStr;
            text.fontSize = 54;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, 
                (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), ui, methodName));

            return btnGo;
        }

        private static GameObject CreateBadge(Transform parent, string name)
        {
            GameObject badge = new GameObject(name);
            badge.transform.SetParent(parent, false);
            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-20, -20);
            rect.sizeDelta = new Vector2(40, 40);
            
            var img = badge.AddComponent<Image>();
            img.color = Color.red;
            
            badge.SetActive(false);
            return badge;
        }
    }
}
