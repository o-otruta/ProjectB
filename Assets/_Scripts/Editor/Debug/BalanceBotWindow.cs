using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectB.Core.BalanceBot;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.EditorScripts
{
    public class BalanceBotWindow : EditorWindow
    {
        private BalanceBotConfig config;
        private readonly List<string> allHeroIds = new List<string>();
        private readonly List<BalanceBotMetaProfile> availableProfiles = new List<BalanceBotMetaProfile>();
        private Vector2 scrollPos;

        [MenuItem("ProjectB/Debug/Balance Bot")]
        public static void OpenWindow()
        {
            var window = GetWindow<BalanceBotWindow>("Balance Bot");
            window.minSize = new Vector2(460f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshCatalogs();
            if (config == null)
            {
                config = AssetDatabase.LoadAssetAtPath<BalanceBotConfig>(BalanceBotEditorStartup.DEFAULT_CONFIG_PATH);
                if (config == null)
                {
                    config = BalanceBotEditorStartup.EnsureDefaultAssetsExist();
                }
            }
        }

        private void OnInspectorUpdate()
        {
            if (BalanceBotRunner.IsBatchRunning)
            {
                Repaint();
            }
        }

        private void RefreshCatalogs()
        {
            allHeroIds.Clear();
            HeroCatalog heroCatalog = Resources.Load<HeroCatalog>(SelectedHeroService.CATALOG_RESOURCE_PATH);
            if (heroCatalog != null && heroCatalog.heroes != null)
            {
                for (int i = 0; i < heroCatalog.heroes.Count; i++)
                {
                    if (heroCatalog.heroes[i] != null && !string.IsNullOrEmpty(heroCatalog.heroes[i].id))
                    {
                        allHeroIds.Add(heroCatalog.heroes[i].id);
                    }
                }
            }
            if (allHeroIds.Count == 0)
            {
                allHeroIds.AddRange(new[] { "knight", "mage", "ranger", "necromancer", "paladin", "engineer" });
            }

            availableProfiles.Clear();
            string[] guids = AssetDatabase.FindAssets("t:BalanceBotMetaProfile", new[] { BalanceBotEditorStartup.ASSET_FOLDER });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prof = AssetDatabase.LoadAssetAtPath<BalanceBotMetaProfile>(path);
                if (prof != null) availableProfiles.Add(prof);
            }
            availableProfiles.Sort((a, b) => a.tier.CompareTo(b.tier));
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("BalanceBot Autoplay & Telemetry Runner", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            config = (BalanceBotConfig)EditorGUILayout.ObjectField("Bot Config SO", config, typeof(BalanceBotConfig), false);
            if (GUILayout.Button("Init Defaults", GUILayout.Width(100f)))
            {
                config = BalanceBotEditorStartup.EnsureDefaultAssetsExist();
                RefreshCatalogs();
            }
            EditorGUILayout.EndHorizontal();

            if (config == null)
            {
                EditorGUILayout.HelpBox("Assign or initialize a BalanceBotConfig asset.", MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            Undo.RecordObject(config, "Edit BalanceBotConfig");

            // Heroes
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Heroes", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All Heroes"))
            {
                config.heroIds.Clear();
                config.heroIds.AddRange(allHeroIds);
                EditorUtility.SetDirty(config);
            }
            if (GUILayout.Button("None"))
            {
                config.heroIds.Clear();
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < allHeroIds.Count; i++)
            {
                string hid = allHeroIds[i];
                bool selected = config.heroIds.Contains(hid);
                bool next = EditorGUILayout.ToggleLeft(hid, selected);
                if (next != selected)
                {
                    if (next) config.heroIds.Add(hid);
                    else config.heroIds.Remove(hid);
                    EditorUtility.SetDirty(config);
                }
            }

            // Strategies
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Strategies", EditorStyles.boldLabel);
            DrawStrategyToggle(BalanceBotStrategyKind.Random);
            DrawStrategyToggle(BalanceBotStrategyKind.GreedyDamage);
            DrawStrategyToggle(BalanceBotStrategyKind.FixedBuild);
            config.fixedBuildPreset = (BalanceBotBuildPreset)EditorGUILayout.ObjectField("FixedBuild Preset", config.fixedBuildPreset, typeof(BalanceBotBuildPreset), false);

            // Meta profiles
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Meta Profiles", EditorStyles.boldLabel);
            for (int i = 0; i < availableProfiles.Count; i++)
            {
                BalanceBotMetaProfile prof = availableProfiles[i];
                bool selected = config.metaProfiles.Contains(prof);
                bool next = EditorGUILayout.ToggleLeft($"{prof.ResolvedProfileId} ({prof.tier})", selected);
                if (next != selected)
                {
                    if (next) config.metaProfiles.Add(prof);
                    else config.metaProfiles.Remove(prof);
                    EditorUtility.SetDirty(config);
                }
            }

            // Batch Settings
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Batch Parameters", EditorStyles.boldLabel);
            config.runsPerCombination = EditorGUILayout.IntSlider("Runs per combo", config.runsPerCombination, 1, 20);
            config.baseSeed = EditorGUILayout.IntField("Base Seed", config.baseSeed);
            config.useFixedStepSimulation = EditorGUILayout.Toggle(
                new GUIContent("Fixed-step simulation", "Каждый кадр = 1/Sim FPS игровых секунд, кадры — максимально быстро. Рекомендуется: результат не зависит от FPS редактора."),
                config.useFixedStepSimulation);
            if (config.useFixedStepSimulation)
            {
                config.simulationFps = EditorGUILayout.IntSlider("Sim FPS", config.simulationFps, 30, 120);
            }
            else
            {
                config.timeScale = EditorGUILayout.Slider("TimeScale (legacy)", config.timeScale, 1f, 12f);
                EditorGUILayout.HelpBox("TimeScale > 1 укрупняет шаг кадра и ослабляет бота (W13 на 6× против W16 на 1×).", MessageType.Warning);
            }
            config.maxWaveLimit = EditorGUILayout.IntSlider("Max Wave Limit", config.maxWaveLimit, 5, 150);
            config.maxRunMinutes = EditorGUILayout.Slider("Max Run Time (min)", config.maxRunMinutes, 1f, 60f);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }

            EditorGUILayout.EndScrollView();

            int totalRuns = Mathf.Max(1, config.heroIds.Count) *
                            Mathf.Max(1, config.strategies.Count) *
                            Mathf.Max(1, config.metaProfiles.Count) *
                            Mathf.Max(1, config.runsPerCombination);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Planned Batch Size: {totalRuns} runs", EditorStyles.miniBoldLabel);

            BalanceBotRunner runner = BalanceBotRunner.ActiveInstance;
            bool isRunning = runner != null && runner.IsRunning;

            if (isRunning)
            {
                float progress = runner.TotalJobs > 0 ? (float)runner.CompletedJobs / runner.TotalJobs : 0f;
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 22f),
                    progress,
                    $"{runner.CompletedJobs}/{runner.TotalJobs} — {runner.CurrentJobLabel}" +
                    (runner.EffectiveSpeed > 0f ? $" ({runner.EffectiveSpeed:F1}x)" : string.Empty));

                if (GUILayout.Button("Stop Batch", GUILayout.Height(30f)))
                {
                    BalanceBotRunner.StopActiveBatch();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(config.heroIds.Count == 0 || config.strategies.Count == 0 || config.metaProfiles.Count == 0);
                if (GUILayout.Button("Run Batch", GUILayout.Height(32f)))
                {
                    AssetDatabase.SaveAssets();
                    if (EditorApplication.isPlaying)
                    {
                        BalanceBotRunner.StartBatch(config);
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(config);
                        BalanceBotEditorStartup.QueuePendingBatchInPlayMode(path);
                        EditorApplication.isPlaying = true;
                    }
                }
                EditorGUI.EndDisabledGroup();
            }

            if (runner != null && !string.IsNullOrEmpty(runner.LastOutputDirectory))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Reports Folder"))
                {
                    if (Directory.Exists(runner.LastOutputDirectory))
                    {
                        EditorUtility.RevealInFinder(runner.LastOutputDirectory);
                    }
                }
                if (!string.IsNullOrEmpty(runner.LastSummaryPath) && File.Exists(runner.LastSummaryPath))
                {
                    if (GUILayout.Button("Open summary.md"))
                    {
                        EditorUtility.OpenWithDefaultApp(runner.LastSummaryPath);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawStrategyToggle(BalanceBotStrategyKind kind)
        {
            bool selected = config.strategies.Contains(kind);
            bool next = EditorGUILayout.ToggleLeft(kind.ToString(), selected);
            if (next != selected)
            {
                if (next) config.strategies.Add(kind);
                else config.strategies.Remove(kind);
                EditorUtility.SetDirty(config);
            }
        }
    }
}
