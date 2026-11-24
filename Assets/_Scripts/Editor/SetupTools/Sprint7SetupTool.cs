using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.UI;

namespace ProjectB.EditorScripts
{
    [System.Obsolete("Legacy one-shot setup tool. Scenes and prefabs are the source of truth (Sprint 15.4).")]
    public static class Sprint7SetupTool
    {
        private const string ELITE_AURA_MAT_PATH = "Assets/_Materials/EliteAuraMat.mat";
        private const string WAVE_CONFIG_PATH = "Assets/_ScriptableObjects/WaveConfig.asset";
        private const string GAMEPLAY_SCENE_PATH = "Assets/_Scenes/Gameplay.unity";
        public static void RunAll()
        {
            RemoveDeadAssets();
            ReserializeLegacyAssets();
            UpdateWaveConfig();
            UpdateHeroDataAssets();
            SetupGameplayQuitConfirmation();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint7SetupTool] Sprint 7 assets, scene confirmation dialog, and legacy ScriptableObject hygiene completed.");
        }

        private static void RemoveDeadAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(ELITE_AURA_MAT_PATH) != null)
            {
                AssetDatabase.DeleteAsset(ELITE_AURA_MAT_PATH);
            }
        }

        private static void ReserializeLegacyAssets()
        {
            string[] roots = { "Assets/_ScriptableObjects", "Assets/Resources" };
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", roots);
            var legacyPaths = new List<string>();
            var editorIdRegex = new Regex(@"m_EditorClassIdentifier:\s*Assembly-CSharp::[^\r\n]+");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset")) continue;
                if (!File.Exists(path)) continue;

                string text = File.ReadAllText(path);
                if (!text.Contains("Assembly-CSharp::")) continue;

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;

                System.Type assetType = asset.GetType();
                string modernIdentifier = $"m_EditorClassIdentifier: {assetType.Assembly.GetName().Name}::{assetType.FullName}";
                string updatedText = editorIdRegex.Replace(text, modernIdentifier);
                if (updatedText != text)
                {
                    File.WriteAllText(path, updatedText);
                    legacyPaths.Add(path);
                }
            }

            if (legacyPaths.Count > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ForceReserializeAssets(legacyPaths, ForceReserializeAssetsOptions.ReserializeAssets);
                Debug.Log($"[Sprint7SetupTool] Updated and reserialized {legacyPaths.Count} legacy ScriptableObjects.");
            }
        }

        private static void UpdateWaveConfig()
        {
            var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfig>(WAVE_CONFIG_PATH);
            if (waveConfig == null)
            {
                Debug.LogError($"[Sprint7SetupTool] Missing WaveConfig at {WAVE_CONFIG_PATH}");
                return;
            }

            var slimeData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/SlimeData.asset");
            var goblinData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/GoblinData.asset");
            var skeletonData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/SkeletonData.asset");
            var batData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/BatData.asset");
            var bombData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/BombBugData.asset");
            var orcData = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_ScriptableObjects/OrcChargerData.asset");

            waveConfig.enemySchedule = new List<EnemyWaveEntry>
            {
                new EnemyWaveEntry { enemy = slimeData, minWave = 1, maxWave = 0, spawnWeight = 1.4f, packSize = 1, triggerIntroWave = false, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = goblinData, minWave = 3, maxWave = 0, spawnWeight = 1.0f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = skeletonData, minWave = 6, maxWave = 0, spawnWeight = 0.85f, packSize = 4, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = batData, minWave = 7, maxWave = 0, spawnWeight = 0.9f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = bombData, minWave = 11, maxWave = 0, spawnWeight = 0.7f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
                new EnemyWaveEntry { enemy = orcData, minWave = 14, maxWave = 0, spawnWeight = 0.65f, packSize = 1, triggerIntroWave = true, introSpawnShare = 0.5f },
            };

            waveConfig.hpLinearPerWave = 0.12f;
            waveConfig.hpQuadraticPerWave = 0.002f;
            waveConfig.damageLinearPerWave = 0.05f;
            waveConfig.maxDamageMultiplier = 6.0f;
            waveConfig.hpCurveMultiplier = new AnimationCurve();
            waveConfig.damageCurveMultiplier = new AnimationCurve();

            EditorUtility.SetDirty(waveConfig);
        }

        private static void UpdateHeroDataAssets()
        {
            var knight = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/HeroData.asset");
            if (knight != null)
            {
                knight.ultimateDescription = "Become invulnerable for 5 seconds and unleash a 4.5m shockwave dealing 40 damage and knocking enemies back.";
                knight.shieldPulseRadius = 4.5f;
                knight.shieldPulseDamage = 40f;
                knight.shieldPulseKnockback = 2.5f;
                EditorUtility.SetDirty(knight);
            }

            var mage = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/MageData.asset");
            if (mage != null)
            {
                mage.meteorCount = 6;
                mage.meteorSearchRadius = 7f;
                mage.meteorExplosionRadius = 3.5f;
                mage.meteorBaseDamage = 80f;
                EditorUtility.SetDirty(mage);
            }

            var ranger = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/RangerData.asset");
            if (ranger != null)
            {
                ranger.arrowRainWaveCount = 3;
                ranger.arrowRainRadius = 8f;
                ranger.arrowRainWaveDamage = 50f;
                ranger.arrowRainSlowFactor = 0.5f;
                ranger.arrowRainSlowDuration = 4.0f;
                EditorUtility.SetDirty(ranger);
            }

            var necro = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_ScriptableObjects/NecromancerHeroData.asset");
            if (necro != null)
            {
                necro.soulNovaBoltCount = 12;
                necro.soulNovaBaseDamage = 60f;
                necro.soulNovaRange = 10f;
                necro.soulNovaSpeed = 16f;
                necro.soulNovaHitRadius = 0.42f;
                necro.soulNovaHealPercent = 0.35f;
                EditorUtility.SetDirty(necro);
            }
        }

        private static void SetupGameplayQuitConfirmation()
        {
            Scene alreadyLoaded = SceneManager.GetSceneByPath(GAMEPLAY_SCENE_PATH);
            bool openedHere = !alreadyLoaded.isLoaded;
            Scene scene = openedHere
                ? EditorSceneManager.OpenScene(GAMEPLAY_SCENE_PATH, OpenSceneMode.Additive)
                : alreadyLoaded;

            try
            {
                PauseMenuUI pauseMenu = null;
                TMP_FontAsset defaultFont = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (pauseMenu == null) pauseMenu = root.GetComponentInChildren<PauseMenuUI>(true);
                    if (defaultFont == null)
                    {
                        var tmp = root.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (tmp != null && tmp.font != null) defaultFont = tmp.font;
                    }
                }

                if (pauseMenu == null)
                {
                    Debug.LogError("[Sprint7SetupTool] PauseMenuUI not found in Gameplay.unity");
                    return;
                }

                var so = new SerializedObject(pauseMenu);
                var pausePanelObj = so.FindProperty("pausePanel").objectReferenceValue as GameObject;
                if (pausePanelObj == null)
                {
                    Debug.LogError("[Sprint7SetupTool] PauseMenuUI.pausePanel is null in Gameplay.unity");
                    return;
                }

                Transform pausePanelTf = pausePanelObj.transform;
                Transform confirmTf = pausePanelTf.Find("QuitConfirmPanel");
                GameObject confirmPanelGo = confirmTf != null
                    ? confirmTf.gameObject
                    : new GameObject("QuitConfirmPanel", typeof(RectTransform), typeof(Image));
                if (confirmTf == null)
                {
                    confirmPanelGo.transform.SetParent(pausePanelTf, false);
                }

                var confirmRt = (RectTransform)confirmPanelGo.transform;
                confirmRt.anchorMin = Vector2.zero;
                confirmRt.anchorMax = Vector2.one;
                confirmRt.offsetMin = Vector2.zero;
                confirmRt.offsetMax = Vector2.zero;
                confirmRt.SetAsLastSibling();

                var backdropImg = EnsureComponent<Image>(confirmPanelGo);
                backdropImg.color = new Color(0f, 0f, 0f, 0.82f);
                backdropImg.raycastTarget = true;

                Transform dialogTf = confirmPanelGo.transform.Find("Dialog");
                GameObject dialogGo = dialogTf != null
                    ? dialogTf.gameObject
                    : new GameObject("Dialog", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
                if (dialogTf == null)
                {
                    dialogGo.transform.SetParent(confirmPanelGo.transform, false);
                }

                var dialogRt = (RectTransform)dialogGo.transform;
                dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
                dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
                dialogRt.pivot = new Vector2(0.5f, 0.5f);
                dialogRt.anchoredPosition = Vector2.zero;
                dialogRt.sizeDelta = new Vector2(760f, 440f);

                var dialogImg = EnsureComponent<Image>(dialogGo);
                dialogImg.color = new Color(0.11f, 0.13f, 0.19f, 0.99f);
                var dialogOutline = EnsureComponent<Outline>(dialogGo);
                dialogOutline.effectColor = new Color(0.45f, 0.52f, 0.7f, 0.9f);
                dialogOutline.effectDistance = new Vector2(3f, -3f);

                var vlg = EnsureComponent<VerticalLayoutGroup>(dialogGo);
                vlg.padding = new RectOffset(40, 40, 36, 36);
                vlg.spacing = 24f;
                vlg.childAlignment = TextAnchor.MiddleCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                EnsureLabel(
                    dialogGo.transform,
                    "Title",
                    "QUIT TO MENU?",
                    defaultFont,
                    44f,
                    64f,
                    Color.white);

                TextMeshProUGUI messageTmp = EnsureLabel(
                    dialogGo.transform,
                    "Message",
                    "Return to Main Menu?\nYour earned coins and progress will be saved.",
                    defaultFont,
                    28f,
                    110f,
                    new Color(0.86f, 0.9f, 0.96f, 1f));

                Transform rowTf = dialogGo.transform.Find("ButtonsRow");
                GameObject rowGo = rowTf != null
                    ? rowTf.gameObject
                    : new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                if (rowTf == null)
                {
                    rowGo.transform.SetParent(dialogGo.transform, false);
                }

                var rowLe = EnsureComponent<LayoutElement>(rowGo);
                rowLe.preferredHeight = 96f;
                rowLe.minHeight = 96f;

                var hlg = EnsureComponent<HorizontalLayoutGroup>(rowGo);
                hlg.spacing = 24f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;

                Button cancelBtn = EnsureActionButton(
                    rowGo.transform,
                    "CancelButton",
                    "CANCEL",
                    new Color(0.26f, 0.31f, 0.42f, 1f),
                    defaultFont);

                Button confirmBtn = EnsureActionButton(
                    rowGo.transform,
                    "ConfirmQuitButton",
                    "QUIT & SAVE",
                    new Color(0.72f, 0.22f, 0.24f, 1f),
                    defaultFont);

                confirmPanelGo.SetActive(false);

                so.FindProperty("quitConfirmPanel").objectReferenceValue = confirmPanelGo;
                so.FindProperty("confirmQuitButton").objectReferenceValue = confirmBtn;
                so.FindProperty("cancelQuitButton").objectReferenceValue = cancelBtn;
                so.FindProperty("quitConfirmText").objectReferenceValue = messageTmp;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pauseMenu);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static TextMeshProUGUI EnsureLabel(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            float height,
            Color color)
        {
            Transform tf = parent.Find(name);
            GameObject go = tf != null ? tf.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            if (tf == null) go.transform.SetParent(parent, false);

            var le = EnsureComponent<LayoutElement>(go);
            le.preferredHeight = height;
            le.minHeight = height;

            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button EnsureActionButton(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            TMP_FontAsset font)
        {
            Transform tf = parent.Find(name);
            GameObject go = tf != null ? tf.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            if (tf == null) go.transform.SetParent(parent, false);

            var img = EnsureComponent<Image>(go);
            img.color = bgColor;
            var btn = EnsureComponent<Button>(go);
            btn.targetGraphic = img;

            Transform txtTf = go.transform.Find("Text");
            GameObject txtGo = txtTf != null ? txtTf.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (txtTf == null) txtGo.transform.SetParent(go.transform, false);

            var txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var tmp = EnsureComponent<TextMeshProUGUI>(txtGo);
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 30f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            return comp != null ? comp : go.AddComponent<T>();
        }
    }
}
