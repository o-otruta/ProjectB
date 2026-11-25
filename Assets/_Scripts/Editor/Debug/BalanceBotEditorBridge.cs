using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Core.BalanceBot;
using ProjectB.Data.Enemies;

namespace ProjectB.EditorScripts
{
    [InitializeOnLoad]
    public static class BalanceBotEditorStartup
    {
        public const string ASSET_FOLDER = "Assets/_ScriptableObjects/BalanceBot";
        public const string DEFAULT_CONFIG_PATH = ASSET_FOLDER + "/DefaultBalanceBotConfig.asset";
        public const string FRESH_PROFILE_PATH = ASSET_FOLDER + "/Profile_Fresh.asset";
        public const string MID_PROFILE_PATH = ASSET_FOLDER + "/Profile_Mid.asset";
        public const string FULL_PROFILE_PATH = ASSET_FOLDER + "/Profile_Full.asset";
        public const string PRESET_PATH = ASSET_FOLDER + "/Preset_FireAndIce.asset";
        private const string PENDING_CONFIG_PATH_KEY = "ProjectB_BalanceBot_PendingConfigPath";

        static BalanceBotEditorStartup()
        {
            BalanceBotEditorBridge.SimulatorComparisonProvider = BuildSimulatorComparisonMarkdown;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void QueuePendingBatchInPlayMode(string configAssetPath)
        {
            SessionState.SetString(PENDING_CONFIG_PATH_KEY, string.IsNullOrEmpty(configAssetPath) ? DEFAULT_CONFIG_PATH : configAssetPath);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(PENDING_CONFIG_PATH_KEY);
                if (BalanceBotSaveIsolationScope.RestorePendingBackupIfAny())
                {
                    Debug.Log("[BalanceBot] Restored backed-up player SaveData after exiting PlayMode.");
                }
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                string pendingPath = SessionState.GetString(PENDING_CONFIG_PATH_KEY, string.Empty);
                if (!string.IsNullOrEmpty(pendingPath))
                {
                    SessionState.EraseString(PENDING_CONFIG_PATH_KEY);
                    BalanceBotConfig cfg = AssetDatabase.LoadAssetAtPath<BalanceBotConfig>(pendingPath);
                    if (cfg == null) cfg = EnsureDefaultAssetsExist();
                    BalanceBotRunner.StartBatch(cfg);
                }
            }
        }

        public static BalanceBotConfig EnsureDefaultAssetsExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "_ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder(ASSET_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets/_ScriptableObjects", "BalanceBot");
            }

            BalanceBotMetaProfile fresh = LoadOrCreateProfile(FRESH_PROFILE_PATH, "Fresh", BalanceBotMetaTier.Fresh);
            LoadOrCreateProfile(MID_PROFILE_PATH, "Mid", BalanceBotMetaTier.Mid);
            LoadOrCreateProfile(FULL_PROFILE_PATH, "Full", BalanceBotMetaTier.Full);
            BalanceBotBuildPreset preset = LoadOrCreatePreset(PRESET_PATH);

            BalanceBotConfig config = AssetDatabase.LoadAssetAtPath<BalanceBotConfig>(DEFAULT_CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BalanceBotConfig>();
                config.fixedBuildPreset = preset;
                config.metaProfiles.Clear();
                config.metaProfiles.Add(fresh);
                AssetDatabase.CreateAsset(config, DEFAULT_CONFIG_PATH);
                EditorUtility.SetDirty(config);
            }
            else
            {
                bool dirty = false;
                if (config.fixedBuildPreset == null && preset != null)
                {
                    config.fixedBuildPreset = preset;
                    dirty = true;
                }
                if (config.metaProfiles == null || config.metaProfiles.Count == 0)
                {
                    config.metaProfiles.Add(fresh);
                    dirty = true;
                }
                if (dirty) EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            return config;
        }

        private static BalanceBotMetaProfile LoadOrCreateProfile(string path, string profileId, BalanceBotMetaTier tier)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BalanceBotMetaProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BalanceBotMetaProfile>();
                profile.profileId = profileId;
                profile.tier = tier;
                profile.initialTotalRuns = 10;
                AssetDatabase.CreateAsset(profile, path);
                EditorUtility.SetDirty(profile);
            }
            return profile;
        }

        private static BalanceBotBuildPreset LoadOrCreatePreset(string path)
        {
            var preset = AssetDatabase.LoadAssetAtPath<BalanceBotBuildPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<BalanceBotBuildPreset>();
                preset.presetId = "FireAndIce";
                preset.useFreeRerollForPreset = true;
                TryAddAbility(preset, "Assets/_ScriptableObjects/Abilities/FireballData.asset");
                TryAddAbility(preset, "Assets/_ScriptableObjects/Abilities/IceAuraData.asset");
                TryAddAbility(preset, "Assets/_ScriptableObjects/Abilities/ChainLightningData.asset");
                TryAddAbility(preset, "Assets/_ScriptableObjects/Abilities/OrbitalSmallSwordData.asset");
                TryAddAbility(preset, "Assets/_ScriptableObjects/Abilities/ChronoTomeData.asset");
                TryAddAbility(preset, "Assets/_ScriptableObjects/Abilities/BattleFuryData.asset");
                AssetDatabase.CreateAsset(preset, path);
                EditorUtility.SetDirty(preset);
            }
            return preset;
        }

        private static void TryAddAbility(BalanceBotBuildPreset preset, string assetPath)
        {
            if (File.Exists(Path.GetFullPath(assetPath)))
            {
                AbilityData ab = AssetDatabase.LoadAssetAtPath<AbilityData>(assetPath);
                if (ab != null && !preset.priorityAbilities.Contains(ab))
                {
                    preset.priorityAbilities.Add(ab);
                }
            }
        }

        private static string BuildSimulatorComparisonMarkdown()
        {
            WaveConfig waveConfig = BalanceCurveSimulator.LoadDefaultWaveConfig();
            if (waveConfig == null)
            {
                return "_WaveConfig asset not found; BalanceCurveSimulator comparison skipped._";
            }

            DpsSimulationSummary dps = BalanceCurveSimulator.SimulateBuildDps(waveConfig);
            EconomySimulationSummary econ = BalanceCurveSimulator.SimulateEconomy(waveConfig);
            XpSimulationSummary xp = BalanceCurveSimulator.SimulateXpCurve(waveConfig, 1.0f);

            var sb = new StringBuilder(1024);
            sb.AppendLine("### Analytical Wall Waves (`BalanceCurveSimulator`)");
            sb.AppendLine();
            sb.AppendLine("| Model Archetype | Assumed AoE DPS | Assumed ST DPS | Predicted Wall Wave | Req DPS at Wall |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            if (dps.Archetypes != null)
            {
                for (int i = 0; i < dps.Archetypes.Length; i++)
                {
                    BuildDpsWallEntry e = dps.Archetypes[i];
                    string wallStr = e.ClearedWave100 ? "Clears W100" : $"W{e.WallWave}";
                    sb.AppendLine(
                        $"| {e.ArchetypeName} | {e.AoeDps.ToString("F0", CultureInfo.InvariantCulture)} | " +
                        $"{e.SingleTargetDps.ToString("F0", CultureInfo.InvariantCulture)} | {wallStr} | " +
                        $"{e.RequiredDpsAtWall.ToString("F0", CultureInfo.InvariantCulture)} |");
                }
            }
            sb.AppendLine();
            sb.AppendLine("### Analytical Economy & XP Forecast");
            sb.AppendLine();
            sb.AppendLine($"- **Predicted Coins/hr (1.25x menu overhead):** W8=`{econ.CoinsPerHourWave8.ToString("F0", CultureInfo.InvariantCulture)}` | W15=`{econ.CoinsPerHourWave15.ToString("F0", CultureInfo.InvariantCulture)}` | W25=`{econ.CoinsPerHourWave25.ToString("F0", CultureInfo.InvariantCulture)}` | W50=`{econ.CoinsPerHourWave50.ToString("F0", CultureInfo.InvariantCulture)}` | W100=`{econ.CoinsPerHourWave100.ToString("F0", CultureInfo.InvariantCulture)}`");
            sb.AppendLine($"- **Predicted Shards/hr (W25):** `{econ.ShardsPerHourWave25.ToString("F1", CultureInfo.InvariantCulture)}`");
            sb.AppendLine($"- **Predicted W30+ LevelUp interval (1.0x XP):** avg `{xp.AvgSecondsPerLevelUpAfter30.ToString("F1", CultureInfo.InvariantCulture)}s` (min `{xp.MinSecondsPerLevelUpAfter30.ToString("F1", CultureInfo.InvariantCulture)}s`)");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
