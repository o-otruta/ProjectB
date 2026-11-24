using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data.Platform;
using ProjectB.LevelUp;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    /// <summary>
    /// Non-destructive editor wiring for Sprint 14.3 (Multi-Input):
    /// creates <c>Resources/InputConfig.asset</c>, removes template <c>InputSystem_Actions</c>,
    /// binds <c>Resources/ProjectB_InputActions.inputactions</c> to <see cref="InputSystemUIInputModule"/>
    /// in both scenes, adds <see cref="KeyHintBadgeUI"/> pills and wires <c>GameOverUI</c> / <c>VirtualJoystick</c>.
    /// </summary>
    public static class Sprint14InputWiringTool
    {
        public const string INPUT_CONFIG_PATH = "Assets/Resources/InputConfig.asset";
        public const string INPUT_ACTIONS_PATH = "Assets/Resources/ProjectB_InputActions.inputactions";
        public const string LEGACY_INPUT_ACTIONS_PATH = "Assets/InputSystem_Actions.inputactions";
        public const string MAIN_MENU_SCENE = "Assets/_Scenes/MainMenu.unity";
        public const string GAMEPLAY_SCENE = "Assets/_Scenes/Gameplay.unity";

        [MenuItem("ProjectB/Platform/Apply Sprint 14.3 Input Wiring")]
        public static void ApplyAll()
        {
            EnsureInputConfigAsset();
            ReimportProjectInputActions();
            WireScene(MAIN_MENU_SCENE, isGameplay: false);
            WireScene(GAMEPLAY_SCENE, isGameplay: true);
            RemoveLegacyInputActionsAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint14InputWiringTool] Sprint 14.3 input wiring complete.");
        }

        public static void EnsureInputConfigAsset()
        {
            InputConfig existing = AssetDatabase.LoadAssetAtPath<InputConfig>(INPUT_CONFIG_PATH);
            if (existing != null) return;

            var config = ScriptableObject.CreateInstance<InputConfig>();
            AssetDatabase.CreateAsset(config, INPUT_CONFIG_PATH);
            EditorUtility.SetDirty(config);
        }

        private static void ReimportProjectInputActions()
        {
            AssetDatabase.ImportAsset(INPUT_ACTIONS_PATH, ImportAssetOptions.ForceUpdate);
        }

        private static void RemoveLegacyInputActionsAsset()
        {
            if (AssetDatabase.LoadMainAssetAtPath(LEGACY_INPUT_ACTIONS_PATH) != null)
            {
                AssetDatabase.DeleteAsset(LEGACY_INPUT_ACTIONS_PATH);
            }
        }

        private static void WireScene(string scenePath, bool isGameplay)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            try
            {
                WireInputModule(scene);

                if (isGameplay)
                {
                    WireGameplayScene(scene);
                }
                else
                {
                    WireMainMenuScene(scene);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                // Leave scene clean
            }
        }

        private static void WireInputModule(Scene scene)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH);
            if (asset == null)
            {
                Debug.LogError($"[Sprint14InputWiringTool] Missing {INPUT_ACTIONS_PATH}");
                return;
            }

            var refs = new Dictionary<string, InputActionReference>();
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(INPUT_ACTIONS_PATH);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is InputActionReference actionRef && actionRef.action != null)
                {
                    string key = $"{actionRef.action.actionMap?.name}/{actionRef.action.name}";
                    refs[key] = actionRef;
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (InputSystemUIInputModule module in root.GetComponentsInChildren<InputSystemUIInputModule>(true))
                {
                    var so = new SerializedObject(module);
                    so.FindProperty("m_ActionsAsset").objectReferenceValue = asset;
                    SetActionRef(so, "m_PointAction", refs, "UI/Point");
                    SetActionRef(so, "m_LeftClickAction", refs, "UI/Click");
                    SetActionRef(so, "m_MiddleClickAction", refs, "UI/MiddleClick");
                    SetActionRef(so, "m_RightClickAction", refs, "UI/RightClick");
                    SetActionRef(so, "m_ScrollWheelAction", refs, "UI/ScrollWheel");
                    SetActionRef(so, "m_MoveAction", refs, "UI/Navigate");
                    SetActionRef(so, "m_SubmitAction", refs, "UI/Submit");
                    SetActionRef(so, "m_CancelAction", refs, "UI/Cancel");
                    SetActionRef(so, "m_TrackedDevicePositionAction", refs, "UI/TrackedDevicePosition");
                    SetActionRef(so, "m_TrackedDeviceOrientationAction", refs, "UI/TrackedDeviceOrientation");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(module);
                }
            }
        }

        private static void SetActionRef(SerializedObject so, string propName, Dictionary<string, InputActionReference> refs, string key)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null && refs.TryGetValue(key, out InputActionReference actionRef))
            {
                prop.objectReferenceValue = actionRef;
            }
        }

        private static void WireMainMenuScene(Scene scene)
        {
            EventSystem es = null;
            HeroSelectionUI heroSelection = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (es == null) es = root.GetComponentInChildren<EventSystem>(true);
                if (heroSelection == null) heroSelection = root.GetComponentInChildren<HeroSelectionUI>(true);
            }

            if (es != null && heroSelection != null && heroSelection.StartRunButton != null)
            {
                var so = new SerializedObject(es);
                so.FindProperty("m_FirstSelected").objectReferenceValue = heroSelection.StartRunButton.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(es);
            }
        }

        private static void WireGameplayScene(Scene scene)
        {
            VirtualJoystick joystick = null;
            CardSelectionUI cardSelection = null;
            UltimateButtonUI ultimateButton = null;
            PauseMenuUI pauseMenu = null;
            GameOverUI gameOver = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (joystick == null) joystick = root.GetComponentInChildren<VirtualJoystick>(true);
                if (cardSelection == null) cardSelection = root.GetComponentInChildren<CardSelectionUI>(true);
                if (ultimateButton == null) ultimateButton = root.GetComponentInChildren<UltimateButtonUI>(true);
                if (pauseMenu == null) pauseMenu = root.GetComponentInChildren<PauseMenuUI>(true);
                if (gameOver == null) gameOver = root.GetComponentInChildren<GameOverUI>(true);
            }

            if (joystick != null)
            {
                CanvasGroup cg = joystick.GetComponent<CanvasGroup>();
                if (cg == null) cg = joystick.gameObject.AddComponent<CanvasGroup>();
                var so = new SerializedObject(joystick);
                so.FindProperty("canvasGroup").objectReferenceValue = cg;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(joystick);
            }

            if (cardSelection != null)
            {
                var soSel = new SerializedObject(cardSelection);
                SerializedProperty slotsProp = soSel.FindProperty("cardSlots");
                if (slotsProp != null)
                {
                    for (int i = 0; i < slotsProp.arraySize && i < 3; i++)
                    {
                        var card = slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as CardUI;
                        if (card == null) continue;
                        KeyHintBadgeUI badge = EnsureKeyHintBadge(
                            card.transform,
                            (i + 1).ToString(),
                            new Vector2(1f, 1f),
                            new Vector2(1f, 1f),
                            new Vector2(-14f, -14f),
                            new Vector2(46f, 34f),
                            20f);
                        var soCard = new SerializedObject(card);
                        soCard.FindProperty("keyHintBadge").objectReferenceValue = badge;
                        soCard.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(card);
                    }
                }

                var rerollBtn = soSel.FindProperty("rerollButton")?.objectReferenceValue as Button;
                if (rerollBtn != null)
                {
                    KeyHintBadgeUI rerollBadge = EnsureKeyHintBadge(
                        rerollBtn.transform,
                        "R",
                        new Vector2(1f, 1f),
                        new Vector2(1f, 1f),
                        new Vector2(-10f, -8f),
                        new Vector2(40f, 30f),
                        18f);
                    soSel.FindProperty("rerollKeyHint").objectReferenceValue = rerollBadge;
                }
                soSel.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cardSelection);
            }

            if (ultimateButton != null)
            {
                KeyHintBadgeUI ultBadge = EnsureKeyHintBadge(
                    ultimateButton.transform,
                    "SPACE",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, -14f),
                    new Vector2(84f, 28f),
                    15f);
                var soUlt = new SerializedObject(ultimateButton);
                soUlt.FindProperty("keyHintBadge").objectReferenceValue = ultBadge;
                soUlt.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ultimateButton);
            }

            if (pauseMenu != null)
            {
                var soPause = new SerializedObject(pauseMenu);
                var pauseBtn = soPause.FindProperty("pauseButton")?.objectReferenceValue as Button;
                if (pauseBtn != null)
                {
                    KeyHintBadgeUI pauseBadge = EnsureKeyHintBadge(
                        pauseBtn.transform,
                        "ESC",
                        new Vector2(0.5f, 0f),
                        new Vector2(0.5f, 0f),
                        new Vector2(0f, -10f),
                        new Vector2(58f, 26f),
                        15f);
                    soPause.FindProperty("pauseKeyHint").objectReferenceValue = pauseBadge;
                    soPause.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pauseMenu);
                }
            }

            if (gameOver != null)
            {
                var soGo = new SerializedObject(gameOver);
                Button[] buttons = gameOver.GetComponentsInChildren<Button>(true);
                Button restartBtn = null;
                Button menuBtn = null;
                for (int i = 0; i < buttons.Length; i++)
                {
                    string n = buttons[i].name;
                    if (restartBtn == null && n.IndexOf("Restart", System.StringComparison.OrdinalIgnoreCase) >= 0) restartBtn = buttons[i];
                    else if (menuBtn == null && n.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0) menuBtn = buttons[i];
                }
                if (restartBtn != null) soGo.FindProperty("restartButton").objectReferenceValue = restartBtn;
                if (menuBtn != null) soGo.FindProperty("menuButton").objectReferenceValue = menuBtn;
                soGo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gameOver);
            }
        }

        private static KeyHintBadgeUI EnsureKeyHintBadge(
            Transform parent,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            float fontSize)
        {
            Transform existingHost = parent.Find("KeyHintHost");
            if (existingHost != null)
            {
                Object.DestroyImmediate(existingHost.gameObject);
            }

            // Clean up any legacy direct child named KeyHint if present
            Transform oldHint = parent.Find("KeyHint");
            if (oldHint != null)
            {
                Object.DestroyImmediate(oldHint.gameObject);
            }

            var hostGo = new GameObject("KeyHintHost", typeof(RectTransform), typeof(KeyHintBadgeUI));
            hostGo.transform.SetParent(parent, false);
            var hostRt = (RectTransform)hostGo.transform;
            hostRt.anchorMin = anchorMin;
            hostRt.anchorMax = anchorMax;
            hostRt.pivot = new Vector2(anchorMax.x, anchorMax.y);
            hostRt.anchoredPosition = anchoredPos;
            hostRt.sizeDelta = sizeDelta;

            var pillGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            pillGo.transform.SetParent(hostGo.transform, false);
            var pillRt = (RectTransform)pillGo.transform;
            pillRt.anchorMin = Vector2.zero;
            pillRt.anchorMax = Vector2.one;
            pillRt.offsetMin = Vector2.zero;
            pillRt.offsetMax = Vector2.zero;

            var textGo = new GameObject("KeyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(pillGo.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(4f, 2f);
            textRt.offsetMax = new Vector2(-4f, -2f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = fontSize;
            tmp.raycastTarget = false;

            var badge = hostGo.GetComponent<KeyHintBadgeUI>();
            badge.EditorSetup(pillGo, pillGo.GetComponent<Image>(), tmp, label);
            return badge;
        }
    }
}
