using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectB.UI;
using static ProjectB.EditorScripts.UiKit.UiBuilder;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Idempotent UI Kit v4 polish for secondary screens and shared prefabs that sit outside the 3 core
    /// redesign tools: <c>AbilityCollectionItem.prefab</c> rarity stripe, centered <c>SettingsPopup</c> header,
    /// snug <c>GameOver</c> reward pills, and Primary gold <c>Restart</c> CTA.
    /// </summary>
    public static class UiRedesignPolishTool
    {
        public const string ABILITY_ITEM_PREFAB = "Assets/_Prefabs/UI/AbilityCollectionItem.prefab";

        [MenuItem("ProjectB/UI/Redesign/Apply Polish (Abilities, Settings, GameOver)")]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            RestyleAbilityPrefab();

            Scene menu = EditorSceneManager.OpenScene(UiRedesignPlayScreenTool.MAIN_MENU_SCENE, OpenSceneMode.Single);
            ApplyMainMenu(menu);
            EditorSceneManager.SaveScene(menu);

            Scene gameplay = EditorSceneManager.OpenScene(UiRedesignHudTool.GAMEPLAY_SCENE, OpenSceneMode.Single);
            ApplyGameplay(gameplay);
            EditorSceneManager.SaveScene(gameplay);

            Debug.Log("[UiRedesignPolishTool] Polish applied to AbilityCollectionItem.prefab, MainMenu, and Gameplay.");
        }

        public static void ApplyMainMenu(Scene scene)
        {
            CenterSettingsTitle(scene);
            CenterButtonLabels(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void ApplyGameplay(Scene scene)
        {
            CenterSettingsTitle(scene);
            PolishGameOver(scene);
            CenterButtonLabels(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void CenterButtonLabels(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ThemedButton tb in root.GetComponentsInChildren<ThemedButton>(true))
                {
                    if (tb == null || tb.Style == ButtonStyle.Icon || tb.Label == null) continue;
                    string n = tb.name;
                    if (n == "StartButton" || n == "RerollButton") continue;

                    if (tb.Label.transform.parent == tb.transform)
                    {
                        RectTransform rt = Rt(tb.Label);
                        rt.offsetMin = new Vector2(rt.offsetMin.x, 28f);
                        rt.offsetMax = new Vector2(rt.offsetMax.x, -2f);
                        EditorUtility.SetDirty(rt);
                    }
                    else
                    {
                        Transform content = tb.transform.Find("Content");
                        if (content != null && content.TryGetComponent<HorizontalLayoutGroup>(out _))
                        {
                            RectTransform rt = Rt(content);
                            rt.offsetMin = new Vector2(rt.offsetMin.x, 26f);
                            rt.offsetMax = new Vector2(rt.offsetMax.x, -2f);
                            EditorUtility.SetDirty(rt);
                        }
                    }
                }
            }
        }

        public static void RestyleAbilityPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ABILITY_ITEM_PREFAB);
            if (root == null) return;
            try
            {
                Transform frame = root.transform.Find("Frame");
                if (frame != null)
                {
                    Stretch(Rt(frame), 3f, 10f, 3f, 1f);
                }
                Transform stripe = root.transform.Find("RarityStripe");
                if (stripe != null)
                {
                    var img = stripe.GetComponent<Image>();
                    if (img != null && Theme.solidSprite != null)
                    {
                        img.sprite = Theme.solidSprite;
                        img.type = Image.Type.Simple;
                    }
                    RectTransform srt = Rt(stripe);
                    srt.anchorMin = new Vector2(0f, 0.16f);
                    srt.anchorMax = new Vector2(0f, 0.84f);
                    srt.pivot = new Vector2(0f, 0.5f);
                    srt.anchoredPosition = new Vector2(12f, 3f);
                    srt.sizeDelta = new Vector2(6f, 0f);
                }
                Transform nameT = root.transform.Find("Info/Name");
                if (nameT != null)
                {
                    LE(nameT.gameObject, minH: 46f, prefH: 46f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, ABILITY_ITEM_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void CenterSettingsTitle(Scene scene)
        {
            Transform title = FindChildRecursive(scene, "SettingsPopup/PopupPanel/Window/Title");
            if (title == null) return;

            Band(Rt(title), true, 36f, 80f, 120f, 120f);
            if (title.TryGetComponent(out TMP_Text tmp))
            {
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 36f;
                tmp.fontSizeMax = Theme.h1Size;
                EditorUtility.SetDirty(tmp);
            }
        }

        public static void PolishGameOver(Scene scene)
        {
            Transform gameOverRoot = FindTransformByName(scene, "GameOverPanel");
            if (gameOverRoot == null) return;

            Transform rewards = FindDescendantByName(gameOverRoot, "Rewards");
            if (rewards != null && rewards.TryGetComponent(out HorizontalLayoutGroup rhl))
            {
                rhl.spacing = 18f;
                rhl.childAlignment = TextAnchor.MiddleCenter;
                rhl.childControlWidth = false;
                rhl.childControlHeight = false;
                rhl.childForceExpandWidth = false;
                rhl.childForceExpandHeight = false;
                EditorUtility.SetDirty(rhl);
            }

            foreach (string pillName in new[] { "CoinsReward", "ShardsReward" })
            {
                Transform pill = FindDescendantByName(gameOverRoot, pillName);
                if (pill == null) continue;

                Rt(pill).sizeDelta = new Vector2(200f, 92f);
                var csf = GetOrAdd<ContentSizeFitter>(pill.gameObject);
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                LE(pill.gameObject, minW: 200f, prefW: -1f, flexW: -1f, prefH: 92f);

                HL(pill.gameObject, 12f, Pad(18, 28, 6, 8), TextAnchor.MiddleCenter, true, true, false, false);

                Transform amount = pill.Find("Amount");
                if (amount != null)
                {
                    LE(amount.gameObject, prefW: -1f, flexW: -1f);
                    if (amount.TryGetComponent(out TMP_Text tmp))
                    {
                        tmp.alignment = TextAlignmentOptions.MidlineLeft;
                        tmp.textWrappingMode = TextWrappingModes.NoWrap;
                        EditorUtility.SetDirty(tmp);
                    }
                }
                EditorUtility.SetDirty(pill.gameObject);
            }

            Transform restart = FindDescendantByName(gameOverRoot, "RestartButton");
            if (restart != null && restart.TryGetComponent(out ThemedButton tb))
            {
                if (tb.Label != null && tb.Label.TryGetComponent(out ThemedGraphic labelGraphic))
                {
                    Object.DestroyImmediate(labelGraphic);
                }
                tb.SetStyle(ButtonStyle.Primary);
                EditorUtility.SetDirty(tb);
            }
        }

        private static Transform FindChildRecursive(Scene scene, string relativePath)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform direct = root.transform.Find(relativePath);
                if (direct != null) return direct;

                int slash = relativePath.IndexOf('/');
                string head = slash >= 0 ? relativePath.Substring(0, slash) : relativePath;
                string tail = slash >= 0 ? relativePath.Substring(slash + 1) : string.Empty;
                Transform anchor = FindDescendantByName(root.transform, head);
                if (anchor != null)
                {
                    Transform found = string.IsNullOrEmpty(tail) ? anchor : anchor.Find(tail);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static Transform FindTransformByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDescendantByName(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDescendantByName(Transform parent, string targetName)
        {
            if (parent.name == targetName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDescendantByName(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
