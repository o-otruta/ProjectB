#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectB.Core.BalanceBot;
using ProjectB.Core.Telemetry;
using ProjectB.Meta;

namespace ProjectB.Tests.PlayMode
{
    public class BalanceBotPlayModeTests
    {
        [UnityTest]
        [Explicit]
        [Category("BalanceBot")]
        [Timeout(1200000)]
        public IEnumerator RunBalanceBotBatch()
        {
            bool hadSaveBefore = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            string saveBefore = hadSaveBefore ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;

            BalanceBotConfig config = LoadConfigWithEnvOverrides(out string customOutputDir);
            BalanceBotRunner runner = BalanceBotRunner.StartBatch(config, customOutputDir);
            Assert.IsNotNull(runner);

            while (runner != null && runner.IsRunning)
            {
                yield return null;
            }

            Assert.IsTrue(string.IsNullOrEmpty(runner.LastError), $"BalanceBotRunner error: {runner.LastError}");
            Assert.IsFalse(string.IsNullOrEmpty(runner.LastOutputDirectory), "Output directory must be populated");
            Assert.IsTrue(File.Exists(Path.Combine(runner.LastOutputDirectory, "runs.csv")), "runs.csv must exist");
            Assert.IsTrue(File.Exists(Path.Combine(runner.LastOutputDirectory, "waves.csv")), "waves.csv must exist");
            Assert.IsTrue(File.Exists(Path.Combine(runner.LastOutputDirectory, "cards.csv")), "cards.csv must exist");
            Assert.IsTrue(File.Exists(runner.LastSummaryPath), "summary.md must exist");

            bool hadSaveAfter = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            string saveAfter = hadSaveAfter ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            Assert.AreEqual(hadSaveBefore, hadSaveAfter, "SaveData presence in PlayerPrefs must remain unchanged");
            Assert.AreEqual(saveBefore, saveAfter, "SaveData JSON in PlayerPrefs must remain unchanged");

            if (runner != null)
            {
                UnityEngine.Object.Destroy(runner.gameObject);
            }
        }

        [TearDown]
        public void TearDown()
        {
            BalanceBotRunner.StopActiveBatch();
            BalanceBotSaveIsolationScope.RestorePendingBackupIfAny();
            Time.timeScale = 1f;
        }

        private static BalanceBotConfig LoadConfigWithEnvOverrides(out string customOutputDir)
        {
            customOutputDir = Environment.GetEnvironmentVariable("BALANCE_BOT_OUT_DIR");

            BalanceBotConfig source = null;
#if UNITY_EDITOR
            source = UnityEditor.AssetDatabase.LoadAssetAtPath<BalanceBotConfig>(
                "Assets/_ScriptableObjects/BalanceBot/DefaultBalanceBotConfig.asset");
#endif
            BalanceBotConfig cfg = source != null
                ? UnityEngine.Object.Instantiate(source)
                : BalanceBotConfig.CreateRuntimeDefault();
            cfg.hideFlags = HideFlags.DontSave;

            string heroesEnv = Environment.GetEnvironmentVariable("BALANCE_BOT_HEROES");
            if (!string.IsNullOrEmpty(heroesEnv))
            {
                cfg.heroIds.Clear();
                string[] parts = heroesEnv.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string h = parts[i].Trim();
                    if (!string.IsNullOrEmpty(h)) cfg.heroIds.Add(h);
                }
            }

            string stratEnv = Environment.GetEnvironmentVariable("BALANCE_BOT_STRATEGIES");
            if (!string.IsNullOrEmpty(stratEnv))
            {
                cfg.strategies.Clear();
                string[] parts = stratEnv.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (Enum.TryParse(parts[i].Trim(), ignoreCase: true, out BalanceBotStrategyKind kind))
                    {
                        cfg.strategies.Add(kind);
                    }
                }
            }

            string profileEnv = Environment.GetEnvironmentVariable("BALANCE_BOT_PROFILE");
            if (!string.IsNullOrEmpty(profileEnv))
            {
                cfg.metaProfiles.Clear();
                string[] parts = profileEnv.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string name = parts[i].Trim();
                    BalanceBotMetaProfile loaded = null;
#if UNITY_EDITOR
                    loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<BalanceBotMetaProfile>(
                        $"Assets/_ScriptableObjects/BalanceBot/Profile_{name}.asset");
#endif
                    if (loaded == null && Enum.TryParse(name, ignoreCase: true, out BalanceBotMetaTier tier))
                    {
                        loaded = ScriptableObject.CreateInstance<BalanceBotMetaProfile>();
                        loaded.hideFlags = HideFlags.DontSave;
                        loaded.profileId = tier.ToString();
                        loaded.tier = tier;
                    }
                    if (loaded != null) cfg.metaProfiles.Add(loaded);
                }
            }

            if (int.TryParse(Environment.GetEnvironmentVariable("BALANCE_BOT_RUNS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int runs))
            {
                cfg.runsPerCombination = Mathf.Max(1, runs);
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("BALANCE_BOT_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            {
                cfg.baseSeed = seed;
            }
            if (float.TryParse(Environment.GetEnvironmentVariable("BALANCE_BOT_TIMESCALE"), NumberStyles.Float, CultureInfo.InvariantCulture, out float ts))
            {
                cfg.timeScale = Mathf.Clamp(ts, 1f, 12f);
                cfg.useFixedStepSimulation = false; // явный timeScale = legacy-режим
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("BALANCE_BOT_SIM_FPS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int simFps))
            {
                cfg.simulationFps = Mathf.Clamp(simFps, 30, 120);
                cfg.useFixedStepSimulation = true;
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("BALANCE_BOT_MAX_WAVE"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxWave))
            {
                cfg.maxWaveLimit = Mathf.Max(1, maxWave);
            }
            if (float.TryParse(Environment.GetEnvironmentVariable("BALANCE_BOT_MAX_MINUTES"), NumberStyles.Float, CultureInfo.InvariantCulture, out float maxMin))
            {
                cfg.maxRunMinutes = Mathf.Max(0.5f, maxMin);
            }

            return cfg;
        }
    }
}
#endif
