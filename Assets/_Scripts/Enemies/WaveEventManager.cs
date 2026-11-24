using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Schedules and drives dynamic wave events every 3-4 regular waves (skipping Boss, Mini-Boss, and Intro waves):
    /// Swarm Rush, Gold Goblin, Blood Moon, and Elite Ambush. Zero GC allocations during Update.
    /// </summary>
    public class WaveEventManager : MonoBehaviour
    {
        private static readonly WaveEventType[] AllEventTypes =
        {
            WaveEventType.SwarmRush,
            WaveEventType.GoldGoblin,
            WaveEventType.BloodMoon,
            WaveEventType.EliteAmbush
        };

        private static readonly Color BloodMoonLightColor = new Color(1.0f, 0.28f, 0.24f, 1.0f);

        [SerializeField] private WaveConfig overrideConfig;
        [SerializeField] private Light arenaDirectionalLight;

        private WaveManager waveManager;
        private HeroHealth heroHealth;
        private HeroEconomy heroEconomy;
        private CoinManager coinManager;
        private VacuumMagnetSpawner vacuumMagnetSpawner;
        private GameEventBus eventBus;
        private GameManager gameManager;
        private EnemyRuntimeModifiers enemyRuntimeModifiers;

        private readonly List<EnemyBase> ambushElites = new List<EnemyBase>(4);
        private readonly WaveEventType[] eligibleTypesBuffer = new WaveEventType[4];
        private readonly List<WaveMutatorData> eligibleMutatorsBuffer = new List<WaveMutatorData>(8);
        private Action<EnemyBase> cachedGoblinDiedHandler;
        private Action<EnemyBase> cachedAmbushEliteDiedHandler;

        private WaveEventType activeEventType = WaveEventType.None;
        private WaveEventType lastTriggeredEventType = WaveEventType.None;
        private float eventDuration;
        private float remainingTime;
        private int nextScheduledEventWave = 4;
        private int nextCorridorHordeWave = 19;
        private int ambushTotalElites;
        private float nextAmbushPruneTime;
        private FleeingLootEnemy activeGoldGoblin;
        private Vector3 activeCorridorDirection = Vector3.forward;
        private Vector3 activeCorridorPerp = Vector3.right;
        private float corridorSpawnTimer;
        private int corridorSpawnedTotal;
        private bool isSubscribed;

        private WaveMutatorData activeMutator;
        private WaveMutatorType lastTriggeredMutatorType = WaveMutatorType.None;
        private int mutatorWavesRemaining;
        private int mutatorTotalWaves;
        private int nextScheduledMutatorWave = 30;

        private bool hasSearchedForLight;
        private bool hasCapturedLightBase;
        private Color originalLightColor = Color.white;
        private float originalLightIntensity = 1f;
        private float bloodMoonBlend;

        private float fogBlend;
        private bool hasCapturedFogBase;
        private bool originalFogEnabled;
        private FogMode originalFogMode;
        private Color originalFogColor;
        private float originalFogStartDistance;
        private float originalFogEndDistance;
        private float originalFogDensity;
        private WaveMutatorData lastFogVisualMutator;

        public WaveEventType ActiveEventType => activeEventType;
        public bool IsEventActive => activeEventType != WaveEventType.None;
        public bool IsBloodMoonActive => activeEventType == WaveEventType.BloodMoon;
        public float EventDuration => eventDuration;
        public float RemainingTime => remainingTime;
        public float NormalizedRemainingTime => eventDuration > 0.001f ? Mathf.Clamp01(remainingTime / eventDuration) : 1f;
        public int NextScheduledEventWave => nextScheduledEventWave;
        public int NextCorridorHordeWave => nextCorridorHordeWave;
        public int AmbushTotalElites => ambushTotalElites;
        public int AmbushRemainingElites => ambushElites.Count;
        public FleeingLootEnemy ActiveGoldGoblin => activeGoldGoblin;
        public Vector3 ActiveCorridorDirection => activeCorridorDirection;
        public int CorridorSpawnedTotal => corridorSpawnedTotal;
        public Light ArenaDirectionalLight => arenaDirectionalLight;

        public WaveMutatorData ActiveMutator => activeMutator;
        public WaveMutatorType ActiveMutatorType => activeMutator != null ? activeMutator.mutatorType : WaveMutatorType.None;
        public bool IsMutatorActive => activeMutator != null && activeMutator.mutatorType != WaveMutatorType.None;
        public int MutatorWavesRemaining => mutatorWavesRemaining;
        public int MutatorTotalWaves => mutatorTotalWaves;
        public int NextScheduledMutatorWave => nextScheduledMutatorWave;
        public float FogBlend => fogBlend;
        public float FogOverlayTargetAlpha => lastFogVisualMutator != null ? Mathf.Clamp01(lastFogVisualMutator.fogOverlayAlpha) * fogBlend : 0f;

        public float ActiveMutatorXpMultiplier => activeMutator != null ? Mathf.Max(0.1f, activeMutator.xpMultiplier) : 1f;
        public float ActiveMutatorCoinMultiplier => activeMutator != null ? Mathf.Max(0.1f, activeMutator.coinMultiplier) : 1f;
        public float ActiveXpMultiplier => (IsBloodMoonActive ? BloodMoonXpMultiplier : 1f) * ActiveMutatorXpMultiplier;
        public float ActiveCoinMultiplier => (IsBloodMoonActive ? BloodMoonCoinMultiplier : 1f) * ActiveMutatorCoinMultiplier;

        public float BloodMoonXpMultiplier
        {
            get
            {
                WaveConfig cfg = ActiveConfig;
                return cfg != null ? Mathf.Max(1f, cfg.bloodMoonXpMultiplier) : 2f;
            }
        }

        public float BloodMoonCoinMultiplier
        {
            get
            {
                WaveConfig cfg = ActiveConfig;
                return cfg != null ? Mathf.Max(1f, cfg.bloodMoonCoinMultiplier) : 2f;
            }
        }

        private WaveConfig ActiveConfig => overrideConfig != null ? overrideConfig : (waveManager != null ? waveManager.Config : null);

        public static bool CanTriggerEventOnWave(WaveConfig config, int wave, bool isFtueRun = false)
        {
            if (config == null || !config.waveEventsEnabled || wave < Mathf.Max(1, config.firstWaveEventWave))
            {
                return false;
            }

            if (config.IsFtueProtectedWave(wave, isFtueRun))
            {
                return false;
            }

            if (config.bosses != null && config.bosses.Count > 0 && WaveManager.IsBossWave(wave, config.bossInterval))
            {
                return false;
            }

            if (WaveManager.IsMiniBossWave(wave, config.miniBossInterval, config.bossInterval))
            {
                return false;
            }

            if (config.GetIntroEntryForWave(wave) != null)
            {
                return false;
            }

            return true;
        }

        public static bool CanTriggerCorridorHordeOnWave(WaveConfig config, int wave, bool isFtueRun = false)
        {
            if (config == null || !config.corridorHordeEnabled || wave < Mathf.Max(1, config.firstCorridorHordeWave))
            {
                return false;
            }

            if (config.IsFtueProtectedWave(wave, isFtueRun))
            {
                return false;
            }

            if (config.bosses != null && config.bosses.Count > 0 && WaveManager.IsBossWave(wave, config.bossInterval))
            {
                return false;
            }

            if (WaveManager.IsMiniBossWave(wave, config.miniBossInterval, config.bossInterval))
            {
                return false;
            }

            if (config.GetIntroEntryForWave(wave) != null)
            {
                return false;
            }

            return true;
        }

        public static bool CanTriggerMutatorOnWave(WaveConfig config, int wave, bool isFtueRun = false)
        {
            if (config == null || !config.waveMutatorsEnabled || config.waveMutators == null || config.waveMutators.Count == 0)
            {
                return false;
            }

            if (wave < Mathf.Max(1, config.firstWaveMutatorWave))
            {
                return false;
            }

            if (config.IsFtueProtectedWave(wave, isFtueRun))
            {
                return false;
            }

            if (config.bosses != null && config.bosses.Count > 0 && WaveManager.IsBossWave(wave, config.bossInterval))
            {
                return false;
            }

            if (config.GetIntroEntryForWave(wave) != null)
            {
                return false;
            }

            return true;
        }

        [Inject]
        public void Construct(
            WaveManager waveManager,
            HeroHealth heroHealth,
            HeroEconomy heroEconomy = null,
            CoinManager coinManager = null,
            VacuumMagnetSpawner vacuumMagnetSpawner = null,
            GameEventBus eventBus = null,
            GameManager gameManager = null,
            EnemyRuntimeModifiers enemyRuntimeModifiers = null)
        {
            this.waveManager = waveManager;
            this.heroHealth = heroHealth;
            this.heroEconomy = heroEconomy;
            this.coinManager = coinManager;
            this.vacuumMagnetSpawner = vacuumMagnetSpawner;
            this.eventBus = eventBus;
            this.gameManager = gameManager;
            this.enemyRuntimeModifiers = enemyRuntimeModifiers ?? waveManager?.RuntimeModifiers;
            this.waveManager?.SetVacuumMagnetSpawner(vacuumMagnetSpawner);

            WaveConfig cfg = ActiveConfig;
            if (cfg != null)
            {
                nextScheduledEventWave = Mathf.Max(1, cfg.firstWaveEventWave);
                nextCorridorHordeWave = Mathf.Max(1, cfg.firstCorridorHordeWave);
                nextScheduledMutatorWave = Mathf.Max(1, cfg.firstWaveMutatorWave);
            }

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void SetDirectionalLight(Light light)
        {
            arenaDirectionalLight = light;
            hasSearchedForLight = true;
            hasCapturedLightBase = false;
        }

        private EnemyRuntimeModifiers ResolveEnemyModifiers()
        {
            if (enemyRuntimeModifiers == null && waveManager != null)
            {
                enemyRuntimeModifiers = waveManager.RuntimeModifiers;
            }
            return enemyRuntimeModifiers;
        }

        private void Awake()
        {
            cachedGoblinDiedHandler = HandleGoldGoblinDied;
            cachedAmbushEliteDiedHandler = HandleAmbushEliteDied;
            EnemyRuntimeModifiers mods = ResolveEnemyModifiers();
            if (mods != null)
            {
                mods.EventSpeedMultiplier = 1f;
                mods.ResetMutatorModifiers();
            }
        }

        private void Start()
        {
            WaveConfig cfg = ActiveConfig;
            if (cfg != null)
            {
                nextScheduledEventWave = Mathf.Max(1, cfg.firstWaveEventWave);
                nextCorridorHordeWave = Mathf.Max(1, cfg.firstCorridorHordeWave);
                nextScheduledMutatorWave = Mathf.Max(1, cfg.firstWaveMutatorWave);
            }
            ResolveDirectionalLight();
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            EndCurrentMutator(completed: false);
            Unsubscribe();
            EnemyRuntimeModifiers mods = ResolveEnemyModifiers();
            if (mods != null)
            {
                mods.EventSpeedMultiplier = 1f;
                mods.ResetMutatorModifiers();
            }
            RestoreLightImmediate();
            RestoreFogImmediate();
            CleanupTrackedEntities();
        }

        private void Subscribe()
        {
            if (isSubscribed || eventBus == null) return;
            eventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            eventBus.Subscribe<GameOverEvent>(OnGameOver);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || eventBus == null) return;
            eventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            eventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            isSubscribed = false;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (gameManager != null && gameManager.CurrentState != GameState.Playing) return;

            TickEvent(Time.deltaTime);
        }

        public void TickEvent(float deltaTime)
        {
            if (deltaTime < 0f) return;

            UpdateFogVisuals(deltaTime);
            UpdateAtmosphereLighting(deltaTime);

            if (activeEventType == WaveEventType.None) return;

            if (activeEventType == WaveEventType.EliteAmbush)
            {
                nextAmbushPruneTime -= deltaTime;
                if (nextAmbushPruneTime <= 0f)
                {
                    nextAmbushPruneTime = 0.25f;
                    PruneDeadAmbushElites();
                }
                return;
            }

            if (activeEventType == WaveEventType.CorridorHorde)
            {
                WaveConfig cfg = ActiveConfig;
                if (cfg != null)
                {
                    corridorSpawnTimer -= deltaTime;
                    float interval = Mathf.Max(0.05f, cfg.corridorHordeSpawnInterval);
                    while (corridorSpawnTimer <= 0f)
                    {
                        corridorSpawnTimer += interval;
                        SpawnCorridorHordeBatch(cfg);
                    }
                }
            }

            if (eventDuration > 0f)
            {
                remainingTime -= deltaTime;
                if (remainingTime <= 0f)
                {
                    remainingTime = 0f;
                    HandleTimedEventExpired();
                }
            }
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            if (heroHealth != null && heroHealth.IsDead) return;
            WaveConfig cfg = ActiveConfig;
            if (cfg == null) return;

            bool isFtueRun = waveManager != null && waveManager.IsFtueRun;
            bool nextIsBossWave = cfg.bosses != null && cfg.bosses.Count > 0 && WaveManager.IsBossWave(evt.NextWave, cfg.bossInterval);

            if (activeMutator != null)
            {
                mutatorWavesRemaining--;
                if (mutatorWavesRemaining <= 0 || nextIsBossWave)
                {
                    EndCurrentMutator(completed: true);
                }
            }

            if (activeEventType != WaveEventType.None || activeMutator != null) return;

            if (cfg.corridorHordeEnabled && evt.NextWave >= nextCorridorHordeWave)
            {
                if (CanTriggerCorridorHordeOnWave(cfg, evt.NextWave, isFtueRun) && StartEvent(WaveEventType.CorridorHorde))
                {
                    nextCorridorHordeWave = evt.NextWave + Mathf.Max(2, cfg.corridorHordeInterval);
                    nextScheduledEventWave = Mathf.Max(nextScheduledEventWave, evt.NextWave + Mathf.Max(1, cfg.minWavesBetweenEvents));
                    return;
                }
            }

            if (cfg.waveMutatorsEnabled && evt.NextWave >= nextScheduledMutatorWave)
            {
                if (CanTriggerMutatorOnWave(cfg, evt.NextWave, isFtueRun))
                {
                    WaveMutatorData chosenMutator = PickNextMutator(cfg, evt.NextWave);
                    if (chosenMutator != null && StartMutator(chosenMutator, evt.NextWave))
                    {
                        int minGap = Mathf.Max(1, cfg.minWavesBetweenMutators);
                        int maxGap = Mathf.Max(minGap, cfg.maxWavesBetweenMutators);
                        nextScheduledMutatorWave = evt.NextWave + UnityEngine.Random.Range(minGap, maxGap + 1);
                        nextScheduledEventWave = Mathf.Max(nextScheduledEventWave, evt.NextWave + 1);
                        return;
                    }
                }
            }

            if (evt.NextWave < nextScheduledEventWave) return;
            if (!CanTriggerEventOnWave(cfg, evt.NextWave, isFtueRun)) return;

            WaveEventType chosen = PickNextEventType(cfg);
            if (chosen != WaveEventType.None && StartEvent(chosen))
            {
                int minGap = Mathf.Max(1, cfg.minWavesBetweenEvents);
                int maxGap = Mathf.Max(minGap, cfg.maxWavesBetweenEvents);
                nextScheduledEventWave = evt.NextWave + UnityEngine.Random.Range(minGap, maxGap + 1);
            }
        }

        private WaveMutatorData PickNextMutator(WaveConfig cfg, int wave)
        {
            if (cfg == null || cfg.waveMutators == null || cfg.waveMutators.Count == 0) return null;

            bool isMiniBossWave = WaveManager.IsMiniBossWave(wave, cfg.miniBossInterval, cfg.bossInterval);

            eligibleMutatorsBuffer.Clear();
            for (int i = 0; i < cfg.waveMutators.Count; i++)
            {
                WaveMutatorData candidate = cfg.waveMutators[i];
                if (candidate == null || !candidate.IsEligibleForWave(wave)) continue;
                if (candidate.mutatorType == WaveMutatorType.EliteSurge && !isMiniBossWave) continue;
                if (candidate.mutatorType == lastTriggeredMutatorType) continue;
                eligibleMutatorsBuffer.Add(candidate);
            }

            if (eligibleMutatorsBuffer.Count == 0)
            {
                for (int i = 0; i < cfg.waveMutators.Count; i++)
                {
                    WaveMutatorData candidate = cfg.waveMutators[i];
                    if (candidate == null || !candidate.IsEligibleForWave(wave)) continue;
                    if (candidate.mutatorType == WaveMutatorType.EliteSurge && !isMiniBossWave) continue;
                    eligibleMutatorsBuffer.Add(candidate);
                }
            }

            if (eligibleMutatorsBuffer.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < eligibleMutatorsBuffer.Count; i++)
            {
                totalWeight += Mathf.Max(0.01f, eligibleMutatorsBuffer[i].selectionWeight);
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float acc = 0f;
            for (int i = 0; i < eligibleMutatorsBuffer.Count; i++)
            {
                WaveMutatorData m = eligibleMutatorsBuffer[i];
                acc += Mathf.Max(0.01f, m.selectionWeight);
                if (roll <= acc)
                {
                    return m;
                }
            }

            return eligibleMutatorsBuffer[eligibleMutatorsBuffer.Count - 1];
        }

        public bool StartMutator(WaveMutatorType type)
        {
            if (type == WaveMutatorType.None) return false;
            WaveConfig cfg = ActiveConfig;
            WaveMutatorData mutator = cfg != null ? cfg.FindMutatorByType(type) : null;
            if (mutator == null) return false;
            int currentWave = waveManager != null ? waveManager.CurrentWave : Mathf.Max(1, mutator.minWave);
            return StartMutator(mutator, currentWave);
        }

        public bool StartMutator(WaveMutatorData mutator, int startWaveOverride = -1)
        {
            if (mutator == null || mutator.mutatorType == WaveMutatorType.None) return false;

            if (activeEventType != WaveEventType.None)
            {
                EndCurrentEvent(completed: false, Vector3.zero);
            }
            if (activeMutator != null)
            {
                EndCurrentMutator(completed: false);
            }

            activeMutator = mutator;
            lastTriggeredMutatorType = mutator.mutatorType;
            mutatorTotalWaves = Mathf.Max(1, mutator.durationWaves);
            mutatorWavesRemaining = mutatorTotalWaves;

            EnemyRuntimeModifiers mods = ResolveEnemyModifiers();
            if (mods != null)
            {
                mods.MutatorEnemyHpMultiplier = mutator.enemyHpMultiplier;
                mods.MutatorEnemyCountMultiplier = mutator.enemyCountMultiplier;
                mods.MutatorEliteChanceMultiplier = mutator.eliteChanceMultiplier;
                mods.MutatorXpMultiplier = mutator.xpMultiplier;
                mods.MutatorCoinMultiplier = mutator.coinMultiplier;
                mods.MutatorMiniBossShardMultiplier = mutator.miniBossShardMultiplier;
            }

            if (mutator.enableFogVisuals)
            {
                CaptureLightBaseIfNeeded();
                CaptureFogBaseIfNeeded();
                lastFogVisualMutator = mutator;
            }

            int startWave = startWaveOverride > 0
                ? startWaveOverride
                : (waveManager != null ? waveManager.CurrentWave : Mathf.Max(1, mutator.minWave));

            string title = !string.IsNullOrEmpty(mutator.titleKey)
                ? Loc.Get(mutator.titleKey, mutator.titleFallback)
                : mutator.titleFallback;
            string subtitle = !string.IsNullOrEmpty(mutator.subtitleKey)
                ? Loc.Get(mutator.subtitleKey, mutator.subtitleFallback)
                : mutator.subtitleFallback;

            eventBus?.Publish(new WaveMutatorStartedEvent(
                mutator.mutatorType,
                title,
                subtitle,
                mutatorTotalWaves,
                startWave));
            return true;
        }

        public void EndCurrentMutator(bool completed)
        {
            if (activeMutator == null) return;

            WaveMutatorType endedType = activeMutator.mutatorType;
            activeMutator = null;
            mutatorWavesRemaining = 0;
            mutatorTotalWaves = 0;

            ResolveEnemyModifiers()?.ResetMutatorModifiers();
            eventBus?.Publish(new WaveMutatorEndedEvent(endedType, completed));
        }

        private WaveEventType PickNextEventType(WaveConfig cfg)
        {
            int count = 0;
            for (int i = 0; i < AllEventTypes.Length; i++)
            {
                WaveEventType candidate = AllEventTypes[i];
                if (candidate == lastTriggeredEventType) continue;
                if (IsEventAvailable(cfg, candidate))
                {
                    eligibleTypesBuffer[count++] = candidate;
                }
            }

            if (count == 0)
            {
                for (int i = 0; i < AllEventTypes.Length; i++)
                {
                    WaveEventType candidate = AllEventTypes[i];
                    if (IsEventAvailable(cfg, candidate))
                    {
                        eligibleTypesBuffer[count++] = candidate;
                    }
                }
            }

            return count > 0 ? eligibleTypesBuffer[UnityEngine.Random.Range(0, count)] : WaveEventType.None;
        }

        private bool IsEventAvailable(WaveConfig cfg, WaveEventType type)
        {
            if (cfg == null) return false;
            return type switch
            {
                WaveEventType.SwarmRush => ResolveSwarmRushEnemy(cfg) != null,
                WaveEventType.GoldGoblin => cfg.goldGoblinData != null,
                WaveEventType.BloodMoon => true,
                WaveEventType.EliteAmbush => cfg.enemySchedule != null && cfg.enemySchedule.Count > 0,
                WaveEventType.CorridorHorde => ResolveCorridorHordeEnemy(cfg) != null,
                _ => false
            };
        }

        public bool StartEvent(WaveEventType type)
        {
            if (type == WaveEventType.None) return false;
            WaveConfig cfg = ActiveConfig;
            if (cfg == null) return false;

            if (activeEventType != WaveEventType.None)
            {
                EndCurrentEvent(completed: false, Vector3.zero);
            }
            if (activeMutator != null)
            {
                EndCurrentMutator(completed: false);
            }

            switch (type)
            {
                case WaveEventType.SwarmRush:
                    return StartSwarmRush(cfg);
                case WaveEventType.GoldGoblin:
                    return StartGoldGoblin(cfg);
                case WaveEventType.BloodMoon:
                    return StartBloodMoon(cfg);
                case WaveEventType.EliteAmbush:
                    return StartEliteAmbush(cfg);
                case WaveEventType.CorridorHorde:
                    return StartCorridorHorde(cfg);
                default:
                    return false;
            }
        }

        private bool StartSwarmRush(WaveConfig cfg)
        {
            EnemyData swarmEnemy = ResolveSwarmRushEnemy(cfg);
            if (swarmEnemy == null || waveManager == null) return false;

            int minCount = Mathf.Max(1, cfg.swarmRushMinCount);
            int maxCount = Mathf.Max(minCount, cfg.swarmRushMaxCount);
            int count = UnityEngine.Random.Range(minCount, maxCount + 1);
            float radius = Mathf.Max(5f, cfg.swarmRushRadius);
            Vector3 center = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            center.y = 0f;

            float step = (Mathf.PI * 2f) / count;
            float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + step * i;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float offscreenRadius = waveManager.HasCameraSpawnBounds
                    ? waveManager.GetOffscreenSpawnDistance(dir, extraMargin: 1.5f)
                    : 0f;
                float effectiveRadius = Mathf.Max(radius, offscreenRadius);
                Vector3 desired = center + dir * effectiveRadius;
                Vector3 safePos = waveManager.GetValidPositionNear(desired, 0f, 1.5f);
                waveManager.SpawnEventEnemy(swarmEnemy, safePos);
            }

            BeginEventState(
                WaveEventType.SwarmRush,
                cfg.swarmRushDuration,
                Loc.Get("event.swarm_rush.title", "SWARM RUSH!"),
                Loc.Get("event.swarm_rush.subtitle", "SURVIVE THE SURROUNDING HORDE!"));
            return true;
        }

        private bool StartGoldGoblin(WaveConfig cfg)
        {
            if (cfg.goldGoblinData == null || waveManager == null) return false;

            Vector3 center = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            Vector3 spawnPos = waveManager.HasCameraSpawnBounds
                ? waveManager.GetValidSpawnPosition()
                : waveManager.GetValidPositionNear(center, 11f, 15f);
            EnemyBase spawned = waveManager.SpawnEventEnemy(cfg.goldGoblinData, spawnPos);
            activeGoldGoblin = spawned as FleeingLootEnemy;
            if (activeGoldGoblin == null) return false;

            if (cachedGoblinDiedHandler == null) cachedGoblinDiedHandler = HandleGoldGoblinDied;
            activeGoldGoblin.OnDied -= cachedGoblinDiedHandler;
            activeGoldGoblin.OnDied += cachedGoblinDiedHandler;

            BeginEventState(
                WaveEventType.GoldGoblin,
                cfg.goldGoblinDuration,
                Loc.Get("event.gold_goblin.title", "GOLD GOBLIN!"),
                Loc.Get("event.gold_goblin.subtitle", "SLAY IT BEFORE IT ESCAPES!"));
            return true;
        }

        private bool StartBloodMoon(WaveConfig cfg)
        {
            CaptureLightBaseIfNeeded();
            EnemyRuntimeModifiers mods = ResolveEnemyModifiers();
            if (mods != null) mods.EventSpeedMultiplier = Mathf.Max(1f, cfg.bloodMoonEnemySpeedMultiplier);
            BeginEventState(
                WaveEventType.BloodMoon,
                cfg.bloodMoonDuration,
                Loc.Get("event.blood_moon.title", "BLOOD MOON!"),
                Loc.Get("event.blood_moon.subtitle", "x2 XP & COINS  •  +20% ENEMY SPEED"));
            return true;
        }

        private bool StartEliteAmbush(WaveConfig cfg)
        {
            if (waveManager == null) return false;

            CleanupTrackedEntities();
            Vector3 center = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            center.y = 0f;
            float radius = Mathf.Max(5f, cfg.eliteAmbushRadius);
            float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            if (cachedAmbushEliteDiedHandler == null) cachedAmbushEliteDiedHandler = HandleAmbushEliteDied;

            for (int i = 0; i < 3; i++)
            {
                float angle = baseAngle + (Mathf.PI * 2f / 3f) * i;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float offscreenRadius = waveManager.HasCameraSpawnBounds
                    ? waveManager.GetOffscreenSpawnDistance(dir, extraMargin: 1.5f)
                    : 0f;
                float effectiveRadius = Mathf.Max(radius, offscreenRadius);
                Vector3 desired = center + dir * effectiveRadius;
                Vector3 safePos = waveManager.GetValidPositionNear(desired, 0f, 1.5f);
                EliteArchetype archetype = i switch
                {
                    0 => EliteArchetype.Berserker,
                    1 => EliteArchetype.Juggernaut,
                    _ => EliteArchetype.HasteAura
                };

                EnemyBase elite = waveManager.SpawnChallengeElite(safePos, archetype);
                if (elite != null && !elite.IsDead)
                {
                    elite.OnDied -= cachedAmbushEliteDiedHandler;
                    elite.OnDied += cachedAmbushEliteDiedHandler;
                    ambushElites.Add(elite);
                }
            }

            if (ambushElites.Count == 0) return false;

            ambushTotalElites = ambushElites.Count;
            nextAmbushPruneTime = 0.25f;
            BeginEventState(
                WaveEventType.EliteAmbush,
                0f,
                Loc.Get("event.elite_ambush.title", "ELITE AMBUSH!"),
                Loc.Get("event.elite_ambush.subtitle", "DEFEAT 3 ELITES FOR MAGNET & COINS!"));
            return true;
        }

        private bool StartCorridorHorde(WaveConfig cfg)
        {
            EnemyData hordeEnemy = ResolveCorridorHordeEnemy(cfg);
            if (hordeEnemy == null || waveManager == null) return false;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            activeCorridorDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            activeCorridorPerp = new Vector3(-activeCorridorDirection.z, 0f, activeCorridorDirection.x);
            corridorSpawnTimer = Mathf.Max(0.05f, cfg.corridorHordeSpawnInterval);
            corridorSpawnedTotal = 0;

            BeginEventState(
                WaveEventType.CorridorHorde,
                cfg.corridorHordeDuration,
                Loc.Get("event.corridor_horde.title", "HORDE CORRIDOR!"),
                Loc.Get("event.corridor_horde.subtitle", "HOLD THE LINE AGAINST THE ONSLAUGHT!"));

            SpawnCorridorHordeBatch(cfg);
            return true;
        }

        private void SpawnCorridorHordeBatch(WaveConfig cfg)
        {
            if (cfg == null || waveManager == null || !waveManager.CanSpawnMoreEnemies) return;
            EnemyData hordeEnemy = ResolveCorridorHordeEnemy(cfg);
            if (hordeEnemy == null) return;

            Vector3 center = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            center.y = 0f;

            float halfWidth = Mathf.Max(1f, cfg.corridorHordeCorridorHalfWidth);
            float baseDist = Mathf.Max(10f, cfg.spawnRadiusMax > 0f ? cfg.spawnRadiusMax : 15f);
            float offscreenDist = waveManager.HasCameraSpawnBounds
                ? waveManager.GetOffscreenSpawnDistance(activeCorridorDirection, extraMargin: halfWidth + 1.2f)
                : 0f;
            float spawnDist = Mathf.Max(baseDist, offscreenDist);
            int batch = Mathf.Max(1, cfg.corridorHordeBatchSize);
            float hpScale = Mathf.Clamp(cfg.corridorHordeHpMultiplier, 0.2f, 1f);

            for (int i = 0; i < batch; i++)
            {
                if (!waveManager.CanSpawnMoreEnemies) break;
                float lateral = UnityEngine.Random.Range(-halfWidth, halfWidth);
                Vector3 desired = center + activeCorridorDirection * spawnDist + activeCorridorPerp * lateral;
                Vector3 safePos = waveManager.GetValidPositionNear(desired, 0f, 1.2f);
                if (waveManager.SpawnEventEnemy(hordeEnemy, safePos, makeElite: false, hpScaleOverride: hpScale) != null)
                {
                    corridorSpawnedTotal++;
                }
            }
        }

        private void BeginEventState(WaveEventType type, float duration, string title, string subtitle)
        {
            activeEventType = type;
            lastTriggeredEventType = type;
            eventDuration = Mathf.Max(0f, duration);
            remainingTime = eventDuration;
            eventBus?.Publish(new WaveEventStartedEvent(type, title, subtitle, eventDuration));
        }

        private void HandleTimedEventExpired()
        {
            if (activeEventType == WaveEventType.GoldGoblin)
            {
                if (activeGoldGoblin != null && !activeGoldGoblin.IsDead)
                {
                    if (cachedGoblinDiedHandler != null)
                    {
                        activeGoldGoblin.OnDied -= cachedGoblinDiedHandler;
                    }
                    activeGoldGoblin.EscapeAndDespawn();
                    waveManager?.DespawnTrackedEnemy(activeGoldGoblin);
                }
                activeGoldGoblin = null;
                EndCurrentEvent(completed: false, Vector3.zero);
                return;
            }

            EndCurrentEvent(completed: true, heroHealth != null ? heroHealth.transform.position : Vector3.zero);
        }

        private void HandleGoldGoblinDied(EnemyBase enemy)
        {
            if (enemy != activeGoldGoblin || activeEventType != WaveEventType.GoldGoblin) return;

            if (cachedGoblinDiedHandler != null)
            {
                enemy.OnDied -= cachedGoblinDiedHandler;
            }
            Vector3 rewardPos = enemy.transform.position;
            activeGoldGoblin = null;
            EndCurrentEvent(completed: true, rewardPos);
        }

        private void HandleAmbushEliteDied(EnemyBase enemy)
        {
            if (enemy != null)
            {
                if (cachedAmbushEliteDiedHandler != null)
                {
                    enemy.OnDied -= cachedAmbushEliteDiedHandler;
                }
                ambushElites.Remove(enemy);
            }

            if (activeEventType != WaveEventType.EliteAmbush) return;

            if (ambushElites.Count <= 0)
            {
                Vector3 rewardPos = enemy != null ? enemy.transform.position : (heroHealth != null ? heroHealth.transform.position : Vector3.zero);
                GrantEliteAmbushReward(rewardPos);
                EndCurrentEvent(completed: true, rewardPos);
            }
        }

        private void PruneDeadAmbushElites()
        {
            if (activeEventType != WaveEventType.EliteAmbush) return;

            Vector3 lastKnownPos = heroHealth != null ? heroHealth.transform.position : Vector3.zero;
            bool removed = false;
            for (int i = ambushElites.Count - 1; i >= 0; i--)
            {
                EnemyBase elite = ambushElites[i];
                if (elite == null || elite.IsDead || !elite.gameObject.activeInHierarchy)
                {
                    if (elite != null)
                    {
                        lastKnownPos = elite.transform.position;
                        if (cachedAmbushEliteDiedHandler != null) elite.OnDied -= cachedAmbushEliteDiedHandler;
                    }
                    ambushElites.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed && ambushElites.Count <= 0)
            {
                GrantEliteAmbushReward(lastKnownPos);
                EndCurrentEvent(completed: true, lastKnownPos);
            }
        }

        private void GrantEliteAmbushReward(Vector3 rewardPos)
        {
            WaveConfig cfg = ActiveConfig;
            int totalCoins = cfg != null ? Mathf.Max(0, cfg.eliteAmbushCoinReward) : 50;
            int burstCount = cfg != null ? Mathf.Max(1, cfg.eliteAmbushCoinBurstCount) : 10;

            if (totalCoins > 0)
            {
                if (coinManager != null)
                {
                    coinManager.EnsureInitialized();
                    int perPickup = Mathf.Max(1, totalCoins / burstCount);
                    int remainder = totalCoins - perPickup * burstCount;
                    for (int i = 0; i < burstCount; i++)
                    {
                        int amount = perPickup + (i < remainder ? 1 : 0);
                        Vector2 scatter = UnityEngine.Random.insideUnitCircle * 1.6f;
                        coinManager.SpawnCoin(rewardPos + new Vector3(scatter.x, 0f, scatter.y), amount);
                    }
                }
                else
                {
                    heroEconomy?.AddCoin(totalCoins);
                }
            }

            vacuumMagnetSpawner?.SpawnVacuumMagnet(rewardPos);
        }

        public void EndCurrentEvent(bool completed, Vector3 rewardPosition)
        {
            if (activeEventType == WaveEventType.None) return;

            WaveEventType endedType = activeEventType;
            activeEventType = WaveEventType.None;
            eventDuration = 0f;
            remainingTime = 0f;
            ambushTotalElites = 0;
            corridorSpawnTimer = 0f;

            if (endedType == WaveEventType.BloodMoon)
            {
                EnemyRuntimeModifiers mods = ResolveEnemyModifiers();
                if (mods != null) mods.EventSpeedMultiplier = 1f;
            }

            CleanupTrackedEntities();
            eventBus?.Publish(new WaveEventEndedEvent(endedType, completed, rewardPosition));
        }

        private void OnGameOver(GameOverEvent _)
        {
            EndCurrentEvent(completed: false, Vector3.zero);
            EndCurrentMutator(completed: false);
            RestoreLightImmediate();
            RestoreFogImmediate();
        }

        private void CleanupTrackedEntities()
        {
            if (activeGoldGoblin != null)
            {
                if (cachedGoblinDiedHandler != null) activeGoldGoblin.OnDied -= cachedGoblinDiedHandler;
                activeGoldGoblin = null;
            }

            for (int i = 0; i < ambushElites.Count; i++)
            {
                if (ambushElites[i] != null && cachedAmbushEliteDiedHandler != null)
                {
                    ambushElites[i].OnDied -= cachedAmbushEliteDiedHandler;
                }
            }
            ambushElites.Clear();
        }

        private EnemyData ResolveSwarmRushEnemy(WaveConfig cfg)
        {
            if (cfg.swarmRushEnemyData != null) return cfg.swarmRushEnemyData;
            if (cfg.enemySchedule == null || cfg.enemySchedule.Count == 0) return null;

            for (int i = 0; i < cfg.enemySchedule.Count; i++)
            {
                EnemyWaveEntry entry = cfg.enemySchedule[i];
                if (entry != null && entry.enemy != null && entry.enemy.name.Contains("Skeleton"))
                {
                    return entry.enemy;
                }
            }
            return cfg.enemySchedule[0]?.enemy;
        }

        private EnemyData ResolveCorridorHordeEnemy(WaveConfig cfg)
        {
            if (cfg.corridorHordeEnemyData != null) return cfg.corridorHordeEnemyData;
            return ResolveSwarmRushEnemy(cfg);
        }

        private void ResolveDirectionalLight()
        {
            if (arenaDirectionalLight == null && !hasSearchedForLight)
            {
                hasSearchedForLight = true;
                Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        arenaDirectionalLight = lights[i];
                        break;
                    }
                }
            }

            CaptureLightBaseIfNeeded();
        }

        private void CaptureLightBaseIfNeeded()
        {
            if (arenaDirectionalLight != null && !hasCapturedLightBase)
            {
                hasCapturedLightBase = true;
                originalLightColor = arenaDirectionalLight.color;
                originalLightIntensity = arenaDirectionalLight.intensity;
            }
        }

        private void UpdateAtmosphereLighting(float deltaTime)
        {
            if (arenaDirectionalLight == null) return;
            CaptureLightBaseIfNeeded();
            if (!hasCapturedLightBase) return;

            float bloodTarget = activeEventType == WaveEventType.BloodMoon ? 1f : 0f;
            if (!Mathf.Approximately(bloodMoonBlend, bloodTarget))
            {
                bloodMoonBlend = Mathf.MoveTowards(bloodMoonBlend, bloodTarget, deltaTime * 2.5f);
            }

            Color targetColor = originalLightColor;
            float targetIntensity = originalLightIntensity;

            if (fogBlend > 0.001f && lastFogVisualMutator != null)
            {
                targetColor = Color.Lerp(targetColor, lastFogVisualMutator.directionalLightTint, fogBlend);
                targetIntensity = Mathf.Lerp(targetIntensity, originalLightIntensity * lastFogVisualMutator.directionalLightIntensityScale, fogBlend);
            }

            if (bloodMoonBlend > 0.001f)
            {
                targetColor = Color.Lerp(targetColor, BloodMoonLightColor, bloodMoonBlend);
                targetIntensity = Mathf.Lerp(targetIntensity, originalLightIntensity * 0.85f, bloodMoonBlend);
            }

            arenaDirectionalLight.color = targetColor;
            arenaDirectionalLight.intensity = targetIntensity;
        }

        private void RestoreLightImmediate()
        {
            bloodMoonBlend = 0f;
            if (arenaDirectionalLight != null && hasCapturedLightBase)
            {
                arenaDirectionalLight.color = originalLightColor;
                arenaDirectionalLight.intensity = originalLightIntensity;
            }
        }

        private void CaptureFogBaseIfNeeded()
        {
            if (hasCapturedFogBase) return;
            hasCapturedFogBase = true;
            originalFogEnabled = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogStartDistance = RenderSettings.fogStartDistance;
            originalFogEndDistance = RenderSettings.fogEndDistance;
            originalFogDensity = RenderSettings.fogDensity;
        }

        private void UpdateFogVisuals(float deltaTime)
        {
            bool wantFog = activeMutator != null && activeMutator.enableFogVisuals;
            if (wantFog)
            {
                CaptureFogBaseIfNeeded();
                lastFogVisualMutator = activeMutator;
            }

            float target = wantFog ? 1f : 0f;
            if (Mathf.Approximately(fogBlend, target))
            {
                if (!wantFog && hasCapturedFogBase && fogBlend <= 0.0001f)
                {
                    RestoreFogImmediate();
                }
                return;
            }

            fogBlend = Mathf.MoveTowards(fogBlend, target, deltaTime * 2.0f);

            if (fogBlend > 0.001f && lastFogVisualMutator != null)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                Color baseColor = hasCapturedFogBase && originalFogEnabled ? originalFogColor : lastFogVisualMutator.fogColor;
                RenderSettings.fogColor = Color.Lerp(baseColor, lastFogVisualMutator.fogColor, fogBlend);
                float baseStart = hasCapturedFogBase && originalFogEnabled ? originalFogStartDistance : Mathf.Max(lastFogVisualMutator.fogEndDistance * 2.5f, 45f);
                float baseEnd = hasCapturedFogBase && originalFogEnabled ? originalFogEndDistance : Mathf.Max(lastFogVisualMutator.fogEndDistance * 3.5f, 75f);
                RenderSettings.fogStartDistance = Mathf.Lerp(baseStart, Mathf.Max(1f, lastFogVisualMutator.fogStartDistance), fogBlend);
                RenderSettings.fogEndDistance = Mathf.Lerp(baseEnd, Mathf.Max(lastFogVisualMutator.fogStartDistance + 1f, lastFogVisualMutator.fogEndDistance), fogBlend);
            }
            else if (!wantFog && fogBlend <= 0.001f)
            {
                RestoreFogImmediate();
            }
        }

        private void RestoreFogImmediate()
        {
            fogBlend = 0f;
            lastFogVisualMutator = null;
            if (!hasCapturedFogBase) return;
            RenderSettings.fog = originalFogEnabled;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogStartDistance = originalFogStartDistance;
            RenderSettings.fogEndDistance = originalFogEndDistance;
            RenderSettings.fogDensity = originalFogDensity;
            hasCapturedFogBase = false;
        }
    }
}
