#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Abilities;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Core.Telemetry;
using ProjectB.Data;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.Visuals;

namespace ProjectB.Core.BalanceBot
{
    public struct BotRunJob
    {
        public string HeroId;
        public BalanceBotStrategyKind StrategyKind;
        public BalanceBotMetaProfile MetaProfile;
        public int RunIndex;
        public int Seed;
    }

    public sealed class BalanceBotRunner : MonoBehaviour
    {
        private static BalanceBotRunner activeInstance;

        private readonly List<BotRunJob> jobs = new List<BotRunJob>(64);
        private BalanceBotConfig config;
        private BalanceBotSaveIsolationScope saveIsolation;
        private SaveManager saveManager;
        private MetaUpgradeData[] cachedMetaUpgrades;
        private TalentTreeData cachedTalentTree;
        private HeroCatalog cachedHeroCatalog;

        private Coroutine batchCoroutine;
        private GameManager currentGameManager;
        private VfxFeedbackManager currentVfxManager;
        private BalanceBotBrain currentBrain;
        private float activeTimeScale = 1f;
        private bool useFixedStep;
        private float fixedStepDelta = 1f / 60f;
        private bool originalRunInBackground;
        private bool hasCapturedRunInBackground;
        private int originalVSyncCount;
        private bool hasCapturedVSync;
        private double totalGameSeconds;
        private double totalRealSeconds;

        /// <summary>Средняя скорость симуляции за пакет: игровые секунды / реальные секунды.</summary>
        public float EffectiveSpeed => totalRealSeconds > 0.001 ? (float)(totalGameSeconds / totalRealSeconds) : 0f;

        public static BalanceBotRunner ActiveInstance => activeInstance;
        public static bool IsBatchRunning => activeInstance != null && activeInstance.IsRunning;

        public bool IsRunning { get; private set; }
        public int CompletedJobs { get; private set; }
        public int TotalJobs => jobs.Count;
        public string CurrentJobLabel { get; private set; } = string.Empty;
        public string LastOutputDirectory { get; private set; }
        public string LastSummaryPath { get; private set; }
        public string LastError { get; private set; }

        public event Action OnBatchCompleted;

        public static BalanceBotRunner StartBatch(BalanceBotConfig runConfig, string customOutputDirectory = null)
        {
            if (activeInstance != null)
            {
                if (activeInstance.IsRunning)
                {
                    Debug.LogWarning("[BalanceBotRunner] Batch already running.");
                    return activeInstance;
                }
                Destroy(activeInstance.gameObject);
                activeInstance = null;
            }

            var go = new GameObject("BalanceBotRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<BalanceBotRunner>();
            activeInstance = runner;
            runner.BeginBatch(runConfig, customOutputDirectory);
            return runner;
        }

        public static void StopActiveBatch()
        {
            if (activeInstance != null)
            {
                activeInstance.StopBatch();
            }
        }

        public void BeginBatch(BalanceBotConfig runConfig, string customOutputDirectory = null)
        {
            if (IsRunning) return;

            config = runConfig != null ? runConfig : BalanceBotConfig.CreateRuntimeDefault();
            useFixedStep = config.useFixedStepSimulation;
            fixedStepDelta = 1f / Mathf.Clamp(config.simulationFps, 30, 120);
            // В режиме фиксированного шага ускорение даёт не timeScale, а скорость кадров:
            // timeScale > 1 снова укрупнил бы шаг и исказил симуляцию.
            activeTimeScale = useFixedStep ? 1f : Mathf.Clamp(config.timeScale, 1f, 12f);
            totalGameSeconds = 0;
            totalRealSeconds = 0;
            LastError = null;
            LastSummaryPath = null;

            BuildJobQueue();
            if (jobs.Count == 0)
            {
                Debug.LogWarning("[BalanceBotRunner] No jobs to execute.");
                OnBatchCompleted?.Invoke();
                return;
            }

            IsRunning = true;
            CompletedJobs = 0;
            LastOutputDirectory = RunTelemetrySession.BeginBatchSession(customOutputDirectory);

            originalRunInBackground = Application.runInBackground;
            hasCapturedRunInBackground = true;
            Application.runInBackground = true;

            originalVSyncCount = QualitySettings.vSyncCount;
            hasCapturedVSync = true;
            if (useFixedStep)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }

            batchCoroutine = StartCoroutine(RunBatchCoroutine());
        }

        public void StopBatch()
        {
            if (!IsRunning) return;

            if (batchCoroutine != null)
            {
                StopCoroutine(batchCoroutine);
                batchCoroutine = null;
            }

            FinalizeCleanup();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(activeInstance, this))
            {
                activeInstance = null;
            }
            if (IsRunning)
            {
                FinalizeCleanup();
            }
        }

        private void Update()
        {
            if (!IsRunning) return;

            if (useFixedStep)
            {
                // Каждый кадр = ровно fixedStepDelta игровых секунд (× timeScale игры: пауза/hit-stop работают как обычно).
                if (!Mathf.Approximately(Time.captureDeltaTime, fixedStepDelta)) Time.captureDeltaTime = fixedStepDelta;
                if (Application.targetFrameRate != -1) Application.targetFrameRate = -1;
            }

            if (currentGameManager == null) return;

            if (currentGameManager.CurrentState == GameState.Playing)
            {
                if (activeTimeScale > 1.01f && Application.targetFrameRate != -1)
                {
                    Application.targetFrameRate = -1;
                }

                bool hitStop = currentVfxManager != null && currentVfxManager.IsHitStopActive;
                if (!hitStop && !Mathf.Approximately(Time.timeScale, activeTimeScale))
                {
                    Time.timeScale = activeTimeScale;
                }
            }
        }

        private void BuildJobQueue()
        {
            jobs.Clear();
            var heroIds = config.heroIds != null && config.heroIds.Count > 0
                ? config.heroIds
                : new List<string> { "knight" };
            var strategies = config.strategies != null && config.strategies.Count > 0
                ? config.strategies
                : new List<BalanceBotStrategyKind> { BalanceBotStrategyKind.GreedyDamage };

            List<BalanceBotMetaProfile> profiles = new List<BalanceBotMetaProfile>();
            if (config.metaProfiles != null)
            {
                for (int i = 0; i < config.metaProfiles.Count; i++)
                {
                    if (config.metaProfiles[i] != null) profiles.Add(config.metaProfiles[i]);
                }
            }
            if (profiles.Count == 0)
            {
                var fallbackProfile = ScriptableObject.CreateInstance<BalanceBotMetaProfile>();
                fallbackProfile.hideFlags = HideFlags.DontSave;
                fallbackProfile.profileId = "Fresh";
                fallbackProfile.tier = BalanceBotMetaTier.Fresh;
                profiles.Add(fallbackProfile);
            }

            int runs = Mathf.Max(1, config.runsPerCombination);
            for (int m = 0; m < profiles.Count; m++)
            {
                for (int s = 0; s < strategies.Count; s++)
                {
                    for (int h = 0; h < heroIds.Count; h++)
                    {
                        for (int r = 0; r < runs; r++)
                        {
                            jobs.Add(new BotRunJob
                            {
                                HeroId = heroIds[h],
                                StrategyKind = strategies[s],
                                MetaProfile = profiles[m],
                                RunIndex = r,
                                Seed = config.baseSeed + r
                            });
                        }
                    }
                }
            }
        }

        private IEnumerator RunBatchCoroutine()
        {
            yield return EnsureRootServicesInitialized();

            if (!RootServices.TryResolve(out saveManager) || saveManager == null)
            {
                LastError = "Could not resolve SaveManager from RootLifetimeScope.";
                Debug.LogError($"[BalanceBotRunner] {LastError}");
                FinalizeCleanup();
                yield break;
            }

            saveIsolation = new BalanceBotSaveIsolationScope(saveManager);
            if (RootServices.TryResolve<AudioManager>(out var audioManager))
            {
                audioManager.SetRequiresUserGestureForTesting(true);
            }
            cachedMetaUpgrades = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            cachedTalentTree = Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH);
            cachedHeroCatalog = Resources.Load<HeroCatalog>(SelectedHeroService.CATALOG_RESOURCE_PATH);

            for (int i = 0; i < jobs.Count; i++)
            {
                BotRunJob job = jobs[i];
                CurrentJobLabel = $"{job.HeroId} | {job.StrategyKind} | {job.MetaProfile.ResolvedProfileId} (#{job.RunIndex + 1})";
                yield return ExecuteSingleRunCoroutine(job, i + 1);
                CompletedJobs = i + 1;
            }

            string simulationLabel = useFixedStep
                ? $"fixed step 1/{Mathf.RoundToInt(1f / fixedStepDelta)} s per frame, effective speed {EffectiveSpeed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}x"
                : null;
            LastSummaryPath = BalanceBotReportGenerator.WriteSummaryReport(
                LastOutputDirectory,
                RunTelemetrySession.CompletedRuns,
                RunTelemetrySession.CardStats,
                activeTimeScale,
                simulationLabel);

            DevLog.Log($"[BalanceBotRunner] Batch complete ({CompletedJobs}/{jobs.Count} runs, effective speed {EffectiveSpeed:F1}x). Summary: {LastSummaryPath}");
            FinalizeCleanup();
        }

        private IEnumerator EnsureRootServicesInitialized()
        {
            if (RootServices.TryResolve<SaveManager>(out _))
            {
                yield break;
            }

            if (FindAnyObjectByType<RootLifetimeScope>() == null)
            {
                AsyncOperation loadOp = SceneManager.LoadSceneAsync("MainMenu");
                while (loadOp != null && !loadOp.isDone)
                {
                    yield return null;
                }
                yield return null;
            }
        }

        private IEnumerator ExecuteSingleRunCoroutine(BotRunJob job, int sequenceNumber)
        {
            SaveData profileData = job.MetaProfile.BuildSaveData(
                job.HeroId,
                cachedMetaUpgrades,
                cachedTalentTree,
                null,
                cachedHeroCatalog);
            saveManager.ReplaceDataInMemory(profileData, saveToActiveStorage: true);

            // Re-evaluate TalentTreeManager & AbilitySlotService after replacing SaveData
            if (RootServices.TryResolve<SelectedHeroService>(out var heroSvc))
            {
                heroSvc.TrySelectHero(job.HeroId, saveImmediately: false);
            }

            UnityEngine.Random.InitState(job.Seed);
            ArenaGenerator.SetNextRunSeedOverride(job.Seed);

            string stratLabel = job.StrategyKind == BalanceBotStrategyKind.FixedBuild && config.fixedBuildPreset != null
                ? $"FixedBuild_{config.fixedBuildPreset.presetId}"
                : job.StrategyKind.ToString();

            RunTelemetrySession.ActiveRunIdOverride = $"{job.HeroId}_{stratLabel}_{job.MetaProfile.ResolvedProfileId}_s{job.Seed}_{sequenceNumber:D3}";
            RunTelemetrySession.ActiveStrategyLabel = stratLabel;
            RunTelemetrySession.ActiveMetaProfileLabel = job.MetaProfile.ResolvedProfileId;
            RunTelemetrySession.ActiveSeedOverride = job.Seed;

            AsyncOperation loadOp = SceneManager.LoadSceneAsync("Gameplay");
            while (loadOp != null && !loadOp.isDone)
            {
                yield return null;
            }

            // Wait one frame for VContainer Start() across scene objects
            yield return null;

            GameLifetimeScope gameScope = FindAnyObjectByType<GameLifetimeScope>();
            if (gameScope == null || gameScope.Container == null)
            {
                LastError = "GameLifetimeScope not found in Gameplay scene.";
                Debug.LogError($"[BalanceBotRunner] {LastError}");
                yield break;
            }

            var container = gameScope.Container;
            currentGameManager = container.Resolve(typeof(GameManager)) as GameManager;
            currentVfxManager = container.Resolve(typeof(VfxFeedbackManager)) as VfxFeedbackManager;
            var heroHealth = container.Resolve(typeof(HeroHealth)) as HeroHealth;
            var heroCombat = container.Resolve(typeof(HeroCombat)) as HeroCombat;
            var heroAbilities = container.Resolve(typeof(HeroAbilities)) as HeroAbilities;
            var heroUltimate = container.Resolve(typeof(HeroUltimate)) as HeroUltimate;
            var runStatistics = container.Resolve(typeof(RunStatistics)) as RunStatistics;
            var waveManager = container.Resolve(typeof(WaveManager)) as WaveManager;
            var arenaGenerator = container.Resolve(typeof(ArenaGenerator)) as ArenaGenerator;
            var enemyProjectilePool = container.Resolve(typeof(EnemyProjectilePool)) as EnemyProjectilePool;
            var cardSelectionUI = container.Resolve(typeof(CardSelectionUI)) as CardSelectionUI;
            var moveInput = container.Resolve(typeof(MoveInputService)) as MoveInputService;
            var eventBus = container.Resolve(typeof(GameEventBus)) as GameEventBus;
            var runTelemetry = container.Resolve(typeof(RunTelemetry)) as RunTelemetry;

            // Ensure FTUE is disabled even if WaveManager checked before profile replacement
            waveManager?.SetFtueRun(false);

            if (useFixedStep)
            {
                Time.captureDeltaTime = fixedStepDelta;
                Application.targetFrameRate = -1;
            }
            else if (activeTimeScale > 1.01f)
            {
                Application.targetFrameRate = -1;
            }
            Time.timeScale = activeTimeScale;

            ICardPickStrategy strategy = CreateStrategy(job.StrategyKind, job.Seed);
            var brainGo = new GameObject("BalanceBotBrain");
            currentBrain = brainGo.AddComponent<BalanceBotBrain>();
            currentBrain.Initialize(
                config,
                strategy,
                moveInput,
                currentGameManager,
                heroHealth,
                heroCombat,
                heroAbilities,
                heroUltimate,
                runStatistics,
                waveManager,
                arenaGenerator,
                enemyProjectilePool,
                cardSelectionUI,
                eventBus,
                job.Seed);

            float maxRunSec = Mathf.Max(30f, config.maxRunMinutes * 60f);
            float unscaledStart = Time.realtimeSinceStartup;
            // Фиксированный шаг может идти и медленнее реального времени, если ПК не тянет 60 FPS на поздних волнах.
            float maxUnscaledTimeout = useFixedStep
                ? Mathf.Max(180f, maxRunSec * 2f + 120f)
                : Mathf.Max(180f, maxRunSec / Mathf.Max(1f, activeTimeScale) + 120f);

            while (IsRunning)
            {
                if (currentGameManager == null || currentGameManager.IsGameOver || (heroHealth != null && heroHealth.IsDead))
                {
                    break;
                }

                if (waveManager != null && waveManager.CurrentWave > config.maxWaveLimit)
                {
                    runTelemetry?.FinalizeRun("WaveLimitReached");
                    break;
                }

                if (runStatistics != null && runStatistics.PlayTime >= maxRunSec)
                {
                    runTelemetry?.FinalizeRun("TimeLimitReached");
                    break;
                }

                if (Time.realtimeSinceStartup - unscaledStart > maxUnscaledTimeout)
                {
                    runTelemetry?.FinalizeRun("WatchdogTimeout");
                    break;
                }

                yield return null;
            }

            float realElapsed = Time.realtimeSinceStartup - unscaledStart;
            float gameElapsed = runStatistics != null ? runStatistics.PlayTime : 0f;
            totalRealSeconds += realElapsed;
            totalGameSeconds += gameElapsed;
            DevLog.Log($"[BalanceBotRunner] {CurrentJobLabel}: wave {(waveManager != null ? waveManager.CurrentWave : 0)}, " +
                       $"game {gameElapsed:F0}s / real {realElapsed:F0}s ({(realElapsed > 0.01f ? gameElapsed / realElapsed : 0f):F1}x)");

            if (currentBrain != null)
            {
                currentBrain.Unbind();
                Destroy(currentBrain.gameObject);
                currentBrain = null;
            }

            currentGameManager = null;
            currentVfxManager = null;

            // Give one frame for GameOver telemetry flush
            yield return null;
        }

        private ICardPickStrategy CreateStrategy(BalanceBotStrategyKind kind, int seed)
        {
            return kind switch
            {
                BalanceBotStrategyKind.Random => new RandomCardPickStrategy(seed),
                BalanceBotStrategyKind.FixedBuild => new FixedBuildCardPickStrategy(config.fixedBuildPreset),
                _ => new GreedyDamageCardPickStrategy()
            };
        }

        private void FinalizeCleanup()
        {
            if (currentBrain != null)
            {
                currentBrain.Unbind();
                Destroy(currentBrain.gameObject);
                currentBrain = null;
            }

            currentGameManager = null;
            currentVfxManager = null;
            ArenaGenerator.SetNextRunSeedOverride(null);
            RunTelemetrySession.EndBatchSession();

            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            Application.targetFrameRate = 60;
            if (hasCapturedVSync)
            {
                QualitySettings.vSyncCount = originalVSyncCount;
                hasCapturedVSync = false;
            }
            if (hasCapturedRunInBackground)
            {
                Application.runInBackground = originalRunInBackground;
                hasCapturedRunInBackground = false;
            }

            if (saveIsolation != null)
            {
                saveIsolation.Dispose();
                saveIsolation = null;
            }
            if (RootServices.TryResolve<AudioManager>(out var audioManager))
            {
                audioManager.SetRequiresUserGestureForTesting(false);
            }

            IsRunning = false;
            OnBatchCompleted?.Invoke();
        }
    }
}
#endif
