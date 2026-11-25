#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.BalanceBot;
using ProjectB.Core.Events;
using ProjectB.Core.Telemetry;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Tests.PlayMode
{
    public class BalanceBotVerificationTests
    {
        [TearDown]
        public void TearDown()
        {
            BalanceBotRunner.StopActiveBatch();
            BalanceBotSaveIsolationScope.RestorePendingBackupIfAny();
            Time.timeScale = 1f;
        }

        [UnityTest]
        [Explicit]
        [Category("BalanceBotVerification")]
        public IEnumerator ManualRunTelemetry_LogsValidCsvs_AndZeroGcInSteadyCombatFrames()
        {
            if (RootServices.TryResolve<SaveManager>(out var existingSave) == false)
            {
                AsyncOperation initOp = SceneManager.LoadSceneAsync("MainMenu");
                while (initOp != null && !initOp.isDone) yield return null;
                yield return null;
            }

            RootServices.TryResolve(out SaveManager saveManager);
            RootServices.TryResolve(out AudioManager audioManager);
            audioManager?.SetRequiresUserGestureForTesting(true);
            using var isolation = new BalanceBotSaveIsolationScope(saveManager);
            var fresh = ScriptableObject.CreateInstance<BalanceBotMetaProfile>();
            fresh.hideFlags = HideFlags.DontSave;
            fresh.tier = BalanceBotMetaTier.Fresh;
            fresh.profileId = "Fresh";
            saveManager.ReplaceDataInMemory(fresh.BuildSaveData("knight"), saveToActiveStorage: true);

            RunTelemetrySession.EnableManualSession();
            string manualSessionDir = RunTelemetrySession.SessionDirectory;
            ArenaGenerator.SetNextRunSeedOverride(2026);

            try
            {
                AsyncOperation loadOp = SceneManager.LoadSceneAsync("Gameplay");
                while (loadOp != null && !loadOp.isDone) yield return null;
                yield return null;

                var scope = UnityEngine.Object.FindAnyObjectByType<GameLifetimeScope>();
                Assert.IsNotNull(scope);
                var c = scope.Container;

                var gameManager = c.Resolve(typeof(GameManager)) as GameManager;
                var heroHealth = c.Resolve(typeof(HeroHealth)) as HeroHealth;
                var heroCombat = c.Resolve(typeof(HeroCombat)) as HeroCombat;
                var heroAbilities = c.Resolve(typeof(HeroAbilities)) as HeroAbilities;
                var heroUltimate = c.Resolve(typeof(HeroUltimate)) as HeroUltimate;
                var runStats = c.Resolve(typeof(RunStatistics)) as RunStatistics;
                var waveManager = c.Resolve(typeof(WaveManager)) as WaveManager;
                var arenaGen = c.Resolve(typeof(ArenaGenerator)) as ArenaGenerator;
                var enemyProjPool = c.Resolve(typeof(EnemyProjectilePool)) as EnemyProjectilePool;
                var cardSelectionUI = c.Resolve(typeof(CardSelectionUI)) as CardSelectionUI;
                var moveInput = c.Resolve(typeof(MoveInputService)) as MoveInputService;
                var eventBus = c.Resolve(typeof(GameEventBus)) as GameEventBus;
                var heroExp = c.Resolve(typeof(HeroExperience)) as HeroExperience;

                waveManager?.SetFtueRun(false);

                // Verify manual session logs to persistentDataPath/BalanceLogs/<timestamp>/
                Assert.IsTrue(manualSessionDir.Contains("BalanceLogs"), $"Expected BalanceLogs path, got {manualSessionDir}");

                // Attach brain for realistic combat/movement + test manual override clearing
                var brainGo = new GameObject("VerifyBrain");
                var brain = brainGo.AddComponent<BalanceBotBrain>();
                var cfg = BalanceBotConfig.CreateRuntimeDefault();
                brain.Initialize(cfg, new GreedyDamageCardPickStrategy(), moveInput, gameManager, heroHealth,
                    heroCombat, heroAbilities, heroUltimate, runStats, waveManager, arenaGen, enemyProjPool,
                    cardSelectionUI, eventBus, 2026);

                // Warm up 10 frames + 1 explicit decision/event cycle so pools and JIT are warm
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                }
                while (gameManager.CurrentState != GameState.Playing && !gameManager.IsGameOver)
                {
                    yield return null;
                }

                brain.StepDecision(0.1f);
                _ = moveInput.Direction;
                eventBus.Publish(new HeroDamagedEvent(1, heroHealth.CurrentHp, heroHealth.MaxHp, "TestSlime"));
                yield return null;

                // Measure exact managed GC bytes allocated by BalanceBotBrain + MoveInputService + RunTelemetry event handlers
                long gcBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 60; i++)
                {
                    brain.StepDecision(0.1f);
                    _ = moveInput.Direction;
                    eventBus.Publish(new HeroDamagedEvent(1, heroHealth.CurrentHp, heroHealth.MaxHp, "TestSlime"));
                }
                long gcAfter = GC.GetAllocatedBytesForCurrentThread();
                long botAndTelemetryGcBytes = gcAfter - gcBefore;

                Assert.AreEqual(0L, botAndTelemetryGcBytes, $"60 ticks of BalanceBotBrain + MoveInputService + RunTelemetry must allocate 0 B, got {botAndTelemetryGcBytes} B");
                DevLog.Log($"[BalanceBotVerification] Zero-GC verified: 60 decision+input+telemetry ticks allocated {botAndTelemetryGcBytes} B");

                // Force a level-up and pick a card, then end the run to verify all 3 CSVs
                heroExp.ForceLevelUp();
                yield return null;
                yield return null;

                heroHealth.SetGodMode(false);
                heroHealth.SetInvulnerable(false);
                heroHealth.DebugKill("VerifyTestKill");
                yield return null;

                brain.Unbind();
                UnityEngine.Object.Destroy(brainGo);

                string wavesPath = Path.Combine(manualSessionDir, "waves.csv");
                string runsPath = Path.Combine(manualSessionDir, "runs.csv");
                string cardsPath = Path.Combine(manualSessionDir, "cards.csv");

                Assert.IsTrue(File.Exists(wavesPath), "waves.csv missing");
                Assert.IsTrue(File.Exists(runsPath), "runs.csv missing");
                Assert.IsTrue(File.Exists(cardsPath), "cards.csv missing");

                string[] runLines = File.ReadAllLines(runsPath);
                Assert.GreaterOrEqual(runLines.Length, 2);
                Assert.IsTrue(runLines[1].Contains("VerifyTestKill"), $"Expected death_cause VerifyTestKill in: {runLines[1]}");
                Assert.IsTrue(File.ReadAllLines(wavesPath).Length >= 2);
                Assert.IsTrue(File.ReadAllLines(cardsPath).Length >= 2);
            }
            finally
            {
                audioManager?.SetRequiresUserGestureForTesting(false);
                RunTelemetrySession.Disable();
                Time.timeScale = 1f;
                if (!string.IsNullOrEmpty(manualSessionDir) && Directory.Exists(manualSessionDir))
                {
                    Directory.Delete(manualSessionDir, recursive: true);
                }
            }
        }

        [UnityTest]
        [Explicit]
        [Category("BalanceBotVerification")]
        [Timeout(2700000)]
        public IEnumerator SpeedupParityCheck_RealTime1x_Vs_FixedStep_ProducesComparableDeathWaves()
        {
            var profile = ScriptableObject.CreateInstance<BalanceBotMetaProfile>();
            profile.hideFlags = HideFlags.DontSave;
            profile.tier = BalanceBotMetaTier.Fresh;
            profile.profileId = "Fresh";

            string tempDir1 = Path.Combine(Path.GetTempPath(), "ProjectB_Ts1_" + Guid.NewGuid().ToString("N"));
            string tempDir6 = Path.Combine(Path.GetTempPath(), "ProjectB_Fixed_" + Guid.NewGuid().ToString("N"));

            try
            {
                var cfg1 = CreateParityConfig(profile, fixedStep: false);
                var runner1 = BalanceBotRunner.StartBatch(cfg1, tempDir1);
                while (runner1.IsRunning) yield return null;
                var runs1 = new List<CompletedRunSummary>(RunTelemetrySession.CompletedRuns);
                UnityEngine.Object.Destroy(runner1.gameObject);

                var cfg6 = CreateParityConfig(profile, fixedStep: true);
                var runner6 = BalanceBotRunner.StartBatch(cfg6, tempDir6);
                while (runner6.IsRunning) yield return null;
                var runs6 = new List<CompletedRunSummary>(RunTelemetrySession.CompletedRuns);
                float fixedSpeed = runner6.EffectiveSpeed;
                UnityEngine.Object.Destroy(runner6.gameObject);

                Assert.AreEqual(3, runs1.Count);
                Assert.AreEqual(3, runs6.Count);

                var waves1 = new List<double>();
                var waves6 = new List<double>();
                for (int i = 0; i < 3; i++)
                {
                    waves1.Add(runs1[i].DeathWave);
                    waves6.Add(runs6[i].DeathWave);
                }

                double med1 = BalanceBotReportGenerator.Percentile(waves1, 0.5);
                double med6 = BalanceBotReportGenerator.Percentile(waves6, 0.5);

                string summary =
                    $"[SpeedupCheck] realtime 1x waves=[{runs1[0].DeathWave},{runs1[1].DeathWave},{runs1[2].DeathWave}] median={med1:F1} | " +
                    $"fixed-step 1/60 waves=[{runs6[0].DeathWave},{runs6[1].DeathWave},{runs6[2].DeathWave}] median={med6:F1} speed={fixedSpeed:F1}x";
                Debug.Log(summary);

                string reportOutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BalanceReports"));
                Directory.CreateDirectory(reportOutDir);
                File.WriteAllText(Path.Combine(reportOutDir, "speedup_check_latest.txt"), summary, CsvUtility.Utf8NoBom);

                // 3 забега на сторону: случайный разброс сам по себе достигает ~2 волн (1x: 16/15/17).
                Assert.LessOrEqual(Math.Abs(med1 - med6), 2.0,
                    $"Fixed-step simulation diverges from real-time play by more than 2 waves. {summary}");
            }
            finally
            {
                if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, recursive: true);
                if (Directory.Exists(tempDir6)) Directory.Delete(tempDir6, recursive: true);
            }
        }

        private static BalanceBotConfig CreateParityConfig(BalanceBotMetaProfile profile, bool fixedStep)
        {
            var cfg = BalanceBotConfig.CreateRuntimeDefault();
            cfg.heroIds = new List<string> { "knight" };
            cfg.strategies = new List<BalanceBotStrategyKind> { BalanceBotStrategyKind.GreedyDamage };
            cfg.metaProfiles = new List<BalanceBotMetaProfile> { profile };
            cfg.runsPerCombination = 3;
            cfg.baseSeed = 1000;
            cfg.useFixedStepSimulation = fixedStep;
            cfg.simulationFps = 60;
            cfg.timeScale = 1f;
            cfg.maxWaveLimit = 25;
            cfg.maxRunMinutes = 8f;
            return cfg;
        }
    }
}
#endif
