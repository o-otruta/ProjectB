using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.Data.Platform;
using ProjectB.Player;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.LevelUp;

namespace ProjectB.Enemies
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private WaveConfig waveConfig;

        private HeroHealth heroHealth;
        private Transform heroTarget;
        private ProjectB.Meta.AchievementManager achievementManager;
        private GameEventBus eventBus;
        private ArenaGenerator arenaGenerator;
        private EnemyProjectilePool enemyProjectilePool;
        private CoinManager coinManager;
        private VacuumMagnetSpawner vacuumMagnetSpawner;
        private IObjectResolver objectResolver;
        private EnemyRuntimeModifiers enemyRuntimeModifiers;
        private RunRulesConfig runRulesConfig;
        private ProjectB.Meta.SaveManager saveManager;
        private CameraController cameraController;
        private CameraConfig cameraConfig;

        private Transform enemyContainer;
        private Dictionary<EnemyData, IObjectPool<EnemyBase>> enemyPools;
        private Dictionary<BossData, IObjectPool<EnemyBase>> bossPools;
        private readonly HashSet<EnemyBase> activeEnemies = new HashSet<EnemyBase>(256);
        private readonly List<EnemyBase> enemyIterationBuffer = new List<EnemyBase>(256);
        private readonly EnemySeparationSystem separationSystem = new EnemySeparationSystem();
        private System.Action<EnemyBase> cachedEnemyDiedHandler;
        private System.Action<EnemyData, Vector3> cachedSummonMinionCallback;
        private System.Func<Vector3, float, float, Vector3> cachedSafePositionProvider;
        private System.Action<Vector3, int> cachedSpawnCoinCallback;
        private System.Action<Vector3> cachedSpawnVacuumCallback;
        private System.Action<EnemyData, Vector3, float, float> cachedSplitChildCallback;
        private System.Action<Vector3, FrostWispEnemyData> cachedFrostZoneCallback;
        private System.Action<FrostSlowZone> cachedFrostZoneReleasedCallback;

        private const int MAX_ACTIVE_FROST_ZONES = 24;
        private ObjectPool<FrostSlowZone> frostZonePool;
        private readonly List<FrostSlowZone> activeFrostZones = new List<FrostSlowZone>(MAX_ACTIVE_FROST_ZONES);
        private Transform frostZoneContainer;
        private Material cachedFrostZoneMaterial;

        private WaitForSeconds waveDelayWait;
        private WaitForSeconds spawnDelayWait;
        private WaitForSeconds cachedWaveSpawnWait;
        private float cachedWaveSpawnDelay = -1f;
        private WaitForSeconds maxWaveDurationWait;
        private WaitForSeconds[] ftueSpawnDelayWaits;
        private int obstacleMask;
        private int separationFrameCounter;

        private int currentWave = 1;
        private bool isFtueRun;
        private int currentWaveSpawnedCount;
        private bool currentWaveIsBoss;
        public int CurrentWave => currentWave;
        public bool IsFtueRun => isFtueRun;
        public void SetFtueRun(bool value) { isFtueRun = value; }
        public int EnemiesAlive => activeEnemies.Count;
        public int MaxActiveEnemies => waveConfig != null && waveConfig.maxActiveEnemies > 0 ? waveConfig.maxActiveEnemies : 300;
        public int AvailableEnemySlots => Mathf.Max(0, MaxActiveEnemies - activeEnemies.Count);
        public bool CanSpawnMoreEnemies => activeEnemies.Count < MaxActiveEnemies;
        public int PendingWaveEnemies { get; private set; }
        public int CurrentWaveSpawnedCount => currentWaveSpawnedCount;
        public int CurrentWaveOverlapThreshold => waveConfig != null
            ? waveConfig.GetOverlapAliveThreshold(currentWave, currentWaveSpawnedCount, isFtueRun, currentWaveIsBoss)
            : 0;
        public IReadOnlyCollection<EnemyBase> ActiveEnemies => activeEnemies;
        public IReadOnlyList<FrostSlowZone> ActiveFrostZones => activeFrostZones;
        public WaveConfig Config => waveConfig;
        public EnemyRuntimeModifiers RuntimeModifiers => ResolveEnemyModifiers();
        private bool isSpawning = false;
        private bool tookDamageInCurrentWave = false;
        private int consecutiveNoDamageWaves = 0;
        private Coroutine waveDurationCoroutine;
        private Coroutine spawnWaveCoroutine;
        private Coroutine startWaveDelayCoroutine;
        private Coroutine leashCoroutine;

        public static bool IsBossWave(int wave, int bossInterval)
        {
            return bossInterval > 0 && wave > 0 && wave % bossInterval == 0;
        }

        public static bool IsMiniBossWave(int wave, int miniBossInterval, int bossInterval)
        {
            if (miniBossInterval <= 0 || wave <= 0) return false;
            if (IsBossWave(wave, bossInterval)) return false;
            return wave % miniBossInterval == 0;
        }

        public static int GetBossIndexForWave(int wave, int bossInterval, int bossCount)
        {
            if (bossInterval <= 0 || bossCount <= 0 || wave <= 0) return 0;
            int bossEncounterIndex = Mathf.Max(0, (wave / bossInterval) - 1);
            return bossEncounterIndex % bossCount;
        }

        public static EliteArchetype PickEliteArchetype(float normalizedRoll)
        {
            float clamped = Mathf.Clamp01(normalizedRoll);
            if (clamped < 1f / 3f) return EliteArchetype.Berserker;
            if (clamped < 2f / 3f) return EliteArchetype.Juggernaut;
            return EliteArchetype.HasteAura;
        }

        [Inject]
        public void Construct(
            HeroHealth heroHealth, 
            ProjectB.Meta.AchievementManager achievementManager, 
            GameEventBus eventBus,
            EnemyProjectilePool enemyProjectilePool,
            ArenaGenerator arenaGenerator = null,
            CoinManager coinManager = null,
            IObjectResolver resolver = null,
            EnemyRuntimeModifiers enemyRuntimeModifiers = null,
            RunRulesConfig runRulesConfig = null,
            ProjectB.Meta.SaveManager saveManager = null,
            CameraController cameraController = null,
            CameraConfig cameraConfig = null)
        {
            this.heroHealth = heroHealth;
            if (heroHealth != null)
            {
                heroTarget = heroHealth.transform;
            }
            this.achievementManager = achievementManager;
            this.eventBus = eventBus;
            this.enemyProjectilePool = enemyProjectilePool;
            this.arenaGenerator = arenaGenerator;
            this.coinManager = coinManager;
            this.objectResolver = resolver;
            this.enemyRuntimeModifiers = enemyRuntimeModifiers;
            if (runRulesConfig != null) this.runRulesConfig = runRulesConfig;
            this.saveManager = saveManager;
            if (cameraController != null) this.cameraController = cameraController;
            if (cameraConfig != null) this.cameraConfig = cameraConfig;
            isFtueRun = saveManager != null && saveManager.IsFirstRun;
        }

        public void SetCameraController(CameraController controller, CameraConfig config = null)
        {
            cameraController = controller;
            if (config != null) cameraConfig = config;
        }

        public bool HasCameraSpawnBounds => cameraController != null;

        public void SetRunRulesConfig(RunRulesConfig rules)
        {
            runRulesConfig = rules;
        }

        private EnemyRuntimeModifiers ResolveEnemyModifiers()
        {
            if (enemyRuntimeModifiers == null)
            {
                enemyRuntimeModifiers = new EnemyRuntimeModifiers();
            }
            return enemyRuntimeModifiers;
        }

        public void SetVacuumMagnetSpawner(VacuumMagnetSpawner spawner)
        {
            if (spawner != null)
            {
                vacuumMagnetSpawner = spawner;
            }
        }

        private void Start()
        {
            if (waveConfig == null || waveConfig.enemySchedule == null || waveConfig.enemySchedule.Count == 0 || heroTarget == null)
            {
                Debug.LogError("WaveManager missing WaveConfig, enemySchedule, or heroTarget!");
                return;
            }

            if (saveManager != null)
            {
                isFtueRun = saveManager.IsFirstRun;
            }

            EnsurePoolsInitialized();

            eventBus?.Subscribe<GameOverEvent>(OnGameOver);
            if (heroHealth != null)
            {
                heroHealth.OnDamageTaken += HandleHeroDamageTaken;
            }
            leashCoroutine = StartCoroutine(LeashCheckCoroutine());
            startWaveDelayCoroutine = StartCoroutine(StartWaveDelay());
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || activeEnemies.Count < 2) return;

            separationFrameCounter++;
            if ((separationFrameCounter & 1) != 0) return;

            RunSeparationStep();
        }

        public int RunSeparationStep()
        {
            if (activeEnemies.Count < 2) return 0;

            float maxCoord = 0f;
            if (arenaGenerator != null)
            {
                float halfArena = arenaGenerator.ArenaSize * 0.5f;
                float padding = waveConfig != null ? waveConfig.arenaPadding : 3f;
                maxCoord = Mathf.Max(5f, halfArena - padding);
            }

            return separationSystem.Step(activeEnemies, obstacleMask, maxCoord);
        }

        public void EnsurePoolsInitialized()
        {
            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            if (cachedSummonMinionCallback == null) cachedSummonMinionCallback = SpawnSummonedMinion;
            if (cachedSafePositionProvider == null) cachedSafePositionProvider = GetValidPositionNear;
            if (cachedSpawnCoinCallback == null) cachedSpawnCoinCallback = SpawnLootCoin;
            if (cachedSpawnVacuumCallback == null) cachedSpawnVacuumCallback = SpawnLootVacuum;
            if (cachedSplitChildCallback == null) cachedSplitChildCallback = SpawnSplitChild;
            if (cachedFrostZoneCallback == null) cachedFrostZoneCallback = SpawnFrostSlowZone;
            if (cachedFrostZoneReleasedCallback == null) cachedFrostZoneReleasedCallback = HandleFrostZoneReleased;

            if (waveConfig != null)
            {
                waveDelayWait ??= new WaitForSeconds(waveConfig.waveDelay);
                spawnDelayWait ??= new WaitForSeconds(waveConfig.spawnDelay);
                if (maxWaveDurationWait == null && waveConfig.maxWaveDuration > 0f)
                {
                    maxWaveDurationWait = new WaitForSeconds(waveConfig.maxWaveDuration);
                }
                int ftueCount = waveConfig.FtueWaveCount;
                if (ftueCount > 0 && (ftueSpawnDelayWaits == null || ftueSpawnDelayWaits.Length != ftueCount))
                {
                    ftueSpawnDelayWaits = new WaitForSeconds[ftueCount];
                    for (int i = 0; i < ftueCount; i++)
                    {
                        var fw = waveConfig.ftueWaves[i];
                        if (fw != null)
                        {
                            ftueSpawnDelayWaits[i] = new WaitForSeconds(Mathf.Max(0.05f, fw.spawnDelay));
                        }
                    }
                }
            }

            obstacleMask = LayerMask.GetMask("Obstacles");

            if (enemyContainer == null)
            {
                enemyContainer = new GameObject("EnemyContainer").transform;
            }
            enemyPools ??= new Dictionary<EnemyData, IObjectPool<EnemyBase>>();
            bossPools ??= new Dictionary<BossData, IObjectPool<EnemyBase>>();

            if (waveConfig?.enemySchedule != null)
            {
                for (int i = 0; i < waveConfig.enemySchedule.Count; i++)
                {
                    var entry = waveConfig.enemySchedule[i];
                    if (entry != null && entry.enemy != null)
                    {
                        EnsureEnemyPool(entry.enemy);
                        if (entry.enemy is SplitterEnemyData splitter && splitter.splitChildData != null)
                        {
                            EnsureEnemyPool(splitter.splitChildData);
                        }
                    }
                }
            }

            if (waveConfig?.bosses != null)
            {
                for (int i = 0; i < waveConfig.bosses.Count; i++)
                {
                    var bossData = waveConfig.bosses[i];
                    if (bossData == null) continue;
                    EnsureBossPool(bossData);
                    if (bossData.minionData != null)
                    {
                        EnsureEnemyPool(bossData.minionData);
                    }
                }
            }
        }

        public void PrewarmPools(int countPerEnemyType = 24)
        {
            EnsurePoolsInitialized();
            if (enemyPools == null || countPerEnemyType <= 0) return;

            var temp = new List<EnemyBase>(countPerEnemyType);
            foreach (var kvp in enemyPools)
            {
                var pool = kvp.Value;
                if (pool == null) continue;
                temp.Clear();
                for (int i = 0; i < countPerEnemyType; i++)
                {
                    var instance = pool.Get();
                    if (instance != null)
                    {
                        instance.Initialize(kvp.Key, heroTarget, pool, eventBus, 1f);
                        temp.Add(instance);
                    }
                }
                for (int i = 0; i < temp.Count; i++)
                {
                    pool.Release(temp[i]);
                }
            }
            temp.Clear();
        }

        private void EnsureEnemyPool(EnemyData enemyData)
        {
            if (enemyData == null) return;
            enemyPools ??= new Dictionary<EnemyData, IObjectPool<EnemyBase>>();
            if (enemyPools.ContainsKey(enemyData)) return;

            var pool = new ObjectPool<EnemyBase>(
                createFunc: () => CreateEnemyInstance(enemyData),
                actionOnGet: e => e.gameObject.SetActive(true),
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e =>
                {
                    if (e != null)
                    {
                        if (Application.isPlaying) Destroy(e.gameObject);
                        else DestroyImmediate(e.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 30,
                maxSize: 300
            );
            enemyPools.Add(enemyData, pool);
        }

        private void EnsureBossPool(BossData bossData)
        {
            if (bossData == null) return;
            bossPools ??= new Dictionary<BossData, IObjectPool<EnemyBase>>();
            if (bossPools.ContainsKey(bossData)) return;

            var pool = new ObjectPool<EnemyBase>(
                createFunc: () => CreateEnemyInstance(bossData),
                actionOnGet: e => e.gameObject.SetActive(true),
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e =>
                {
                    if (e != null)
                    {
                        if (Application.isPlaying) Destroy(e.gameObject);
                        else DestroyImmediate(e.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 1,
                maxSize: 4
            );
            bossPools.Add(bossData, pool);
        }

        private EnemyBase CreateEnemyInstance(EnemyData data)
        {
            GameObject go;
            if (data.modelPrefab != null)
            {
                go = Instantiate(data.modelPrefab, enemyContainer);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(enemyContainer);
                go.GetComponent<Collider>().isTrigger = false;
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            }

            EnemyBase enemy = go.GetComponent<EnemyBase>();
            if (enemy == null)
            {
                if (data.modelPrefab != null)
                {
                    Debug.LogError($"Enemy prefab '{data.name}' is missing EnemyBase component! Adding default EnemyBase.", go);
                }
                enemy = go.AddComponent<EnemyBase>();
            }
            return enemy;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            }
            if (heroHealth != null)
            {
                heroHealth.OnDamageTaken -= HandleHeroDamageTaken;
            }
            StopAllCoroutines();
            ClearActiveFrostZones();
            if (frostZoneContainer != null)
            {
                if (Application.isPlaying) Destroy(frostZoneContainer.gameObject);
                else DestroyImmediate(frostZoneContainer.gameObject);
                frostZoneContainer = null;
            }
            if (enemyContainer != null)
            {
                if (Application.isPlaying) Destroy(enemyContainer.gameObject);
                else DestroyImmediate(enemyContainer.gameObject);
                enemyContainer = null;
            }
        }

        private void HandleHeroDamageTaken(int amount)
        {
            if (amount <= 0) return;
            tookDamageInCurrentWave = true;
            consecutiveNoDamageWaves = 0;
        }

        private void CopyActiveEnemiesToBuffer()
        {
            enemyIterationBuffer.Clear();
            foreach (var enemy in activeEnemies)
            {
                enemyIterationBuffer.Add(enemy);
            }
        }

        private void OnGameOver(GameOverEvent evt)
        {
            StopAllCoroutines();
            isSpawning = false;
            PendingWaveEnemies = 0;
            currentWaveSpawnedCount = 0;
            currentWaveIsBoss = false;
            waveDurationCoroutine = null;
            spawnWaveCoroutine = null;
            startWaveDelayCoroutine = null;
            leashCoroutine = null;

            CopyActiveEnemiesToBuffer();
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                var enemy = enemyIterationBuffer[i];
                if (enemy != null && cachedEnemyDiedHandler != null)
                {
                    enemy.OnDied -= cachedEnemyDiedHandler;
                }
            }
            enemyIterationBuffer.Clear();
            ClearActiveFrostZones();
        }

        private IEnumerator StartWaveDelay()
        {
            yield return waveDelayWait;
            startWaveDelayCoroutine = null;
            if (spawnWaveCoroutine != null) StopCoroutine(spawnWaveCoroutine);
            spawnWaveCoroutine = StartCoroutine(SpawnWaveCoroutine());
        }

        private WaitForSeconds GetSpawnDelayWaitForWave(int wave)
        {
            if (isFtueRun && ftueSpawnDelayWaits != null && wave >= 1 && wave <= ftueSpawnDelayWaits.Length)
            {
                WaitForSeconds ftueWait = ftueSpawnDelayWaits[wave - 1];
                if (ftueWait != null) return ftueWait;
            }

            if (waveConfig != null)
            {
                float targetDelay = waveConfig.GetWaveSpawnDelay(wave, isFtueRun);
                float countMult = ResolveEnemyModifiers().MutatorEnemyCountMultiplier;
                if (countMult > 1.01f)
                {
                    targetDelay = Mathf.Max(0.08f, targetDelay / countMult);
                }
                if (cachedWaveSpawnWait == null || !Mathf.Approximately(cachedWaveSpawnDelay, targetDelay))
                {
                    cachedWaveSpawnDelay = targetDelay;
                    cachedWaveSpawnWait = new WaitForSeconds(targetDelay);
                }
                return cachedWaveSpawnWait;
            }

            return spawnDelayWait;
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;
            int waveNumber = currentWave;
            currentWaveSpawnedCount = 0;

            bool isFtueWave = waveConfig.IsFtueProtectedWave(waveNumber, isFtueRun);
            bool hasBossesConfigured = waveConfig.bosses != null && waveConfig.bosses.Count > 0;
            bool isBossWave = !isFtueWave && hasBossesConfigured && IsBossWave(waveNumber, waveConfig.bossInterval);
            bool isMiniBossWave = !isFtueWave && IsMiniBossWave(waveNumber, waveConfig.miniBossInterval, waveConfig.bossInterval);
            currentWaveIsBoss = isBossWave;

            if (isBossWave)
            {
                while (!CanSpawnMoreEnemies)
                {
                    if (heroTarget == null || (heroHealth != null && heroHealth.IsDead) || currentWave != waveNumber)
                    {
                        if (currentWave == waveNumber) isSpawning = false;
                        yield break;
                    }
                    yield return spawnDelayWait;
                }

                int bossIndex = GetBossIndexForWave(waveNumber, waveConfig.bossInterval, waveConfig.bosses.Count);
                BossData bossData = waveConfig.bosses[bossIndex];
                DevLog.Log($"[WaveManager] Boss Wave {waveNumber}! Spawning {bossData.bossDisplayName}.");

                // Boss wave: no timeout skip, no regular wave mobs — boss + its summons only
                SpawnBoss(bossData);
                currentWaveSpawnedCount = 1;

                if (currentWave == waveNumber)
                {
                    isSpawning = false;
                    spawnWaveCoroutine = null;
                    CheckWaveEnd();
                }
                yield break;
            }

            int baseEnemiesToSpawn = waveConfig.GetWaveEnemyCount(waveNumber, isFtueRun);
            float countMult = ResolveEnemyModifiers().MutatorEnemyCountMultiplier;
            int enemiesToSpawn = Mathf.Clamp(Mathf.RoundToInt(baseEnemiesToSpawn * countMult), 1, MaxActiveEnemies);
            WaitForSeconds activeSpawnWait = GetSpawnDelayWaitForWave(waveNumber);

            EnemyWaveEntry introEntry = waveConfig.GetIntroEntryForWave(waveNumber);
            int introRemaining = introEntry != null
                ? Mathf.Clamp(Mathf.RoundToInt(enemiesToSpawn * introEntry.introSpawnShare), 0, enemiesToSpawn)
                : 0;

            DevLog.Log($"[WaveManager] Spawning Wave {currentWave} with {enemiesToSpawn} enemies{(isMiniBossWave ? " + Mini-Boss" : "")}{(isFtueWave ? " (FTUE)" : "")}.");

            if (maxWaveDurationWait != null)
            {
                if (waveDurationCoroutine != null) StopCoroutine(waveDurationCoroutine);
                waveDurationCoroutine = StartCoroutine(WaveDurationCoroutine(waveNumber));
            }

            if (isMiniBossWave)
            {
                while (!CanSpawnMoreEnemies)
                {
                    if (heroTarget == null || (heroHealth != null && heroHealth.IsDead) || currentWave != waveNumber)
                    {
                        if (currentWave == waveNumber) isSpawning = false;
                        yield break;
                    }
                    yield return activeSpawnWait;
                }
                SpawnMiniBoss();
            }

            int spawnedCount = 0;
            PendingWaveEnemies = enemiesToSpawn;
            while (spawnedCount < enemiesToSpawn)
            {
                if (heroTarget == null || (heroHealth != null && heroHealth.IsDead) || currentWave != waveNumber)
                {
                    if (currentWave == waveNumber)
                    {
                        isSpawning = false;
                        PendingWaveEnemies = 0;
                    }
                    yield break;
                }

                int slots = AvailableEnemySlots;
                if (slots <= 0)
                {
                    yield return activeSpawnWait;
                    continue;
                }

                int budget = Mathf.Min(enemiesToSpawn - spawnedCount, slots);
                int spawnedNow = SpawnEnemyOrPack(budget, introEntry, ref introRemaining);
                if (spawnedNow <= 0)
                {
                    yield return activeSpawnWait;
                    continue;
                }

                spawnedCount += spawnedNow;
                currentWaveSpawnedCount = spawnedCount;
                PendingWaveEnemies = Mathf.Max(0, enemiesToSpawn - spawnedCount);
                yield return activeSpawnWait;
            }
            
            if (currentWave == waveNumber)
            {
                PendingWaveEnemies = 0;
                isSpawning = false;
                spawnWaveCoroutine = null;
                CheckWaveEnd();
            }
        }

        private IEnumerator WaveDurationCoroutine(int waveNumber)
        {
            yield return maxWaveDurationWait;

            if (currentWave != waveNumber || (heroHealth != null && heroHealth.IsDead))
            {
                yield break;
            }

            DevLog.Log($"[WaveManager] Wave {waveNumber} reached max duration ({waveConfig.maxWaveDuration}s). Advancing to next wave (enemies remain alive).");
            AdvanceWave();
        }

        private IEnumerator LeashCheckCoroutine()
        {
            float interval = waveConfig != null && waveConfig.leashCheckInterval > 0.1f 
                ? waveConfig.leashCheckInterval 
                : 2f;
            var wait = new WaitForSeconds(interval);

            while (true)
            {
                yield return wait;

                if (heroTarget == null || (heroHealth != null && heroHealth.IsDead)) continue;
                if (activeEnemies.Count == 0) continue;

                float configLeash = waveConfig != null ? waveConfig.leashDistance : 35f;
                float minSafeLeash = cameraController != null
                    ? cameraController.CachedSpawnRadiusMax + (cameraConfig != null ? cameraConfig.leashMargin : cameraController.Config.leashMargin)
                    : 0f;
                float leashDist = Mathf.Max(configLeash, minSafeLeash);
                float leashDistSq = leashDist * leashDist;
                Vector3 heroPos = heroTarget.position;

                CopyActiveEnemiesToBuffer();

                for (int i = 0; i < enemyIterationBuffer.Count; i++)
                {
                    var enemy = enemyIterationBuffer[i];
                    if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy || !enemy.CanBeLeashed) continue;

                    Vector3 diff = enemy.transform.position - heroPos;
                    diff.y = 0f;
                    if (diff.sqrMagnitude > leashDistSq)
                    {
                        Vector3 newPos = GetValidSpawnPosition();
                        enemy.Teleport(newPos);
                    }
                }
                enemyIterationBuffer.Clear();
            }
        }

        private Vector3 ClampToArena(Vector3 pos)
        {
            float halfArena = arenaGenerator != null ? arenaGenerator.ArenaSize / 2f : 250f;
            float padding = waveConfig != null ? waveConfig.arenaPadding : 3f;
            float maxCoord = Mathf.Max(5f, halfArena - padding);

            pos.y = 0f;
            pos.x = Mathf.Clamp(pos.x, -maxCoord, maxCoord);
            pos.z = Mathf.Clamp(pos.z, -maxCoord, maxCoord);
            return pos;
        }

        public Vector3 GetValidPositionNear(Vector3 center, float minRad, float maxRad)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(minRad, maxRad);
                Vector3 candidate = ClampToArena(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);

                if (obstacleMask == 0 || !Physics.CheckSphere(candidate + Vector3.up * 0.5f, 0.6f, obstacleMask))
                {
                    return candidate;
                }
            }

            float fallbackAngle = Random.Range(0f, Mathf.PI * 2f);
            float safeMinRad = Mathf.Max(0f, minRad);
            Vector3 ringOffset = new Vector3(Mathf.Cos(fallbackAngle), 0f, Mathf.Sin(fallbackAngle)) * safeMinRad;
            return ClampToArena(center + ringOffset);
        }

        public float GetOffscreenSpawnDistance(Vector3 dirXZ, float extraMargin = 0f)
        {
            if (cameraController != null)
            {
                return cameraController.GetOffscreenSpawnDistance(dirXZ, extraMargin);
            }
            float baseMin = waveConfig != null ? waveConfig.spawnRadiusMin : 10f;
            return baseMin + Mathf.Max(0f, extraMargin);
        }

        public float GetMaxOffscreenSpawnRadius()
        {
            if (cameraController != null)
            {
                return cameraController.CachedSpawnRadiusMax;
            }
            return waveConfig != null ? waveConfig.spawnRadiusMax : 15f;
        }

        public Vector3 GetValidOffscreenPositionAlongDirection(
            Vector3 center,
            Vector3 dirXZ,
            float extraMargin = 0f,
            float bandWidthOverride = -1f)
        {
            Vector3 dir = new Vector3(dirXZ.x, 0f, dirXZ.z);
            if (dir.sqrMagnitude <= 1e-5f) dir = Vector3.forward;
            else dir.Normalize();

            float baseAngle = Mathf.Atan2(dir.z, dir.x);
            float minRad = GetOffscreenSpawnDistance(dir, extraMargin);
            float bandWidth = bandWidthOverride >= 0f
                ? bandWidthOverride
                : (cameraController != null
                    ? cameraController.Config.spawnBandWidth
                    : Mathf.Max(1f, (waveConfig != null ? waveConfig.spawnRadiusMax - waveConfig.spawnRadiusMin : 5f)));
            float maxRad = minRad + Mathf.Max(0f, bandWidth);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float jitter = attempt == 0 ? 0f : Random.Range(-0.15f, 0.15f);
                float angle = baseAngle + jitter;
                Vector3 attemptDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float attemptMin = attempt == 0 ? minRad : GetOffscreenSpawnDistance(attemptDir, extraMargin);
                float attemptMax = attemptMin + Mathf.Max(0f, bandWidth);
                float radius = Random.Range(attemptMin, attemptMax);
                Vector3 candidate = ClampToArena(center + attemptDir * radius);

                if (obstacleMask == 0 || !Physics.CheckSphere(candidate + Vector3.up * 0.5f, 0.6f, obstacleMask))
                {
                    return candidate;
                }
            }

            return ClampToArena(center + dir * Mathf.Max(0f, minRad));
        }

        public Vector3 GetValidSpawnPosition(float extraMargin = 0f)
        {
            Vector3 center = heroTarget != null ? heroTarget.position : Vector3.zero;
            if (cameraController != null)
            {
                float bandWidth = cameraController.Config.spawnBandWidth;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    float minRad = cameraController.GetOffscreenSpawnDistance(dir, extraMargin);
                    float radius = Random.Range(minRad, minRad + bandWidth);
                    Vector3 candidate = ClampToArena(center + dir * radius);

                    if (obstacleMask == 0 || !Physics.CheckSphere(candidate + Vector3.up * 0.5f, 0.6f, obstacleMask))
                    {
                        return candidate;
                    }
                }

                float fallbackAngle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 fallbackDir = new Vector3(Mathf.Cos(fallbackAngle), 0f, Mathf.Sin(fallbackAngle));
                float safeMinRad = cameraController.GetOffscreenSpawnDistance(fallbackDir, extraMargin);
                return ClampToArena(center + fallbackDir * safeMinRad);
            }

            float legacyMin = (waveConfig != null ? waveConfig.spawnRadiusMin : 10f) + Mathf.Max(0f, extraMargin);
            float legacyMax = (waveConfig != null ? waveConfig.spawnRadiusMax : 15f) + Mathf.Max(0f, extraMargin);
            return GetValidPositionNear(center, legacyMin, legacyMax);
        }

        public static EnemyWaveEntry SelectEntryForWaveSpawn(
            WaveConfig config,
            int wave,
            int remainingInWave,
            EnemyWaveEntry introEntry,
            ref int introRemaining,
            float introChoiceRoll,
            float weightedRoll,
            out int packCount)
        {
            if (config == null || remainingInWave <= 0)
            {
                packCount = 1;
                return null;
            }

            if (introEntry != null && introRemaining > 0)
            {
                float introProb = introRemaining >= remainingInWave
                    ? 1f
                    : (float)introRemaining / Mathf.Max(1, remainingInWave);

                if (introChoiceRoll < introProb)
                {
                    int effectiveIntroPack = config.GetEffectivePackSize(introEntry, wave);
                    packCount = Mathf.Clamp(effectiveIntroPack, 1, Mathf.Min(remainingInWave, introRemaining));
                    introRemaining -= packCount;
                    return introEntry;
                }
            }

            EnemyWaveEntry selected = introEntry != null
                ? config.SelectEnemyEntryForWave(wave, weightedRoll, excludeEntry: introEntry)
                : config.SelectEnemyEntryForWave(wave, weightedRoll);

            if (selected == null || selected.enemy == null)
            {
                packCount = 1;
                return null;
            }

            int maxRegularPack = introRemaining > 0
                ? Mathf.Max(1, remainingInWave - introRemaining)
                : Mathf.Max(1, remainingInWave);
            int effectiveSelectedPack = config.GetEffectivePackSize(selected, wave);
            packCount = Mathf.Clamp(effectiveSelectedPack, 1, maxRegularPack);

            if (ReferenceEquals(selected, introEntry) && introRemaining > 0)
            {
                introRemaining = Mathf.Max(0, introRemaining - packCount);
            }

            return selected;
        }

        private void GetWaveDifficultyMultipliers(out float hpMult, out float dmgMult, bool applyMutatorHp = true)
        {
            GetWaveDifficultyMultipliersForWave(currentWave, out hpMult, out dmgMult, applyMutatorHp);
        }

        private void GetWaveDifficultyMultipliersForWave(int wave, out float hpMult, out float dmgMult, bool applyMutatorHp = true)
        {
            int targetWave = Mathf.Max(1, wave);
            if (waveConfig != null)
            {
                hpMult = waveConfig.GetHpDifficultyMultiplier(targetWave, isFtueRun);
                dmgMult = waveConfig.GetDamageDifficultyMultiplier(targetWave, isFtueRun);
            }
            else
            {
                float fallback = 1f + (targetWave - 1) * 0.1f;
                hpMult = fallback;
                dmgMult = fallback;
            }

            if (applyMutatorHp)
            {
                hpMult = Mathf.Max(0.1f, hpMult * ResolveEnemyModifiers().MutatorEnemyHpMultiplier);
            }
        }

        private int SpawnEnemyOrPack(int remainingInWave, EnemyWaveEntry introEntry, ref int introRemaining)
        {
            if (!CanSpawnMoreEnemies || remainingInWave <= 0) return 0;

            EnemyWaveEntry selectedEntry = SelectEntryForWaveSpawn(
                waveConfig,
                currentWave,
                remainingInWave,
                introEntry,
                ref introRemaining,
                Random.value,
                Random.value,
                out int packCount);

            if (selectedEntry == null || selectedEntry.enemy == null) return 1;

            Vector3 basePos = GetValidSpawnPosition(packCount > 1 ? 1.8f : 0f);
            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier);
            bool spawnedEliteInPack = false;
            int actuallySpawned = 0;

            for (int i = 0; i < packCount; i++)
            {
                if (!CanSpawnMoreEnemies) break;

                Vector3 spawnPos = i == 0
                    ? basePos
                    : GetValidPositionNear(basePos, 0.9f, 1.8f);

                EnemyBase enemy = SpawnEnemyInstance(selectedEntry.enemy, spawnPos, hpMultiplier, damageMultiplier);
                if (enemy == null) break;
                actuallySpawned++;

                bool elitesAllowed = waveConfig != null && !waveConfig.IsFtueProtectedWave(currentWave, isFtueRun);
                if (elitesAllowed && !spawnedEliteInPack && currentWave >= waveConfig.minWaveForElites)
                {
                    float eliteMult = ResolveEnemyModifiers().MutatorEliteChanceMultiplier;
                    float chance = Mathf.Clamp01(waveConfig.GetEliteChanceForWave(currentWave) * eliteMult);
                    if (Random.value < chance)
                    {
                        enemy.MakeElite(PickEliteArchetype(Random.value), waveConfig);
                        spawnedEliteInPack = true;
                    }
                }
            }

            if (actuallySpawned < packCount && ReferenceEquals(selectedEntry, introEntry))
            {
                introRemaining += packCount - actuallySpawned;
            }

            return actuallySpawned;
        }

        public EnemyBase SpawnEnemyForWave(
            EnemyData selectedType,
            Vector3 spawnPos,
            int waveNumber,
            bool makeElite = false,
            EliteArchetype eliteArchetype = EliteArchetype.Berserker)
        {
            if (selectedType == null) return null;
            EnsurePoolsInitialized();
            GetWaveDifficultyMultipliersForWave(waveNumber, out float hpMultiplier, out float damageMultiplier);
            EnemyBase enemy = SpawnEnemyInstance(selectedType, spawnPos, hpMultiplier, damageMultiplier);
            if (makeElite && enemy != null)
            {
                enemy.MakeElite(eliteArchetype, waveConfig);
            }
            return enemy;
        }

        public EnemyBase SpawnEventEnemy(
            EnemyData selectedType,
            Vector3 spawnPos,
            bool makeElite = false,
            EliteArchetype eliteArchetype = EliteArchetype.Berserker,
            float hpScaleOverride = 1f)
        {
            if (selectedType == null) return null;
            EnsurePoolsInitialized();
            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier);
            hpMultiplier *= Mathf.Max(0.05f, hpScaleOverride);
            EnemyBase enemy = SpawnEnemyInstance(selectedType, spawnPos, hpMultiplier, damageMultiplier);
            if (makeElite && enemy != null)
            {
                enemy.MakeElite(eliteArchetype, waveConfig);
            }
            return enemy;
        }

        public void DespawnTrackedEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;
            if (cachedEnemyDiedHandler != null)
            {
                enemy.OnDied -= cachedEnemyDiedHandler;
            }
            activeEnemies.Remove(enemy);
            enemy.Despawn();
            CheckWaveEnd();
        }

        private EnemyBase SpawnEnemyInstance(EnemyData selectedType, Vector3 spawnPos, float hpMultiplier, float damageMultiplier)
        {
            if (!CanSpawnMoreEnemies) return null;
            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            EnsureEnemyPool(selectedType);
            IObjectPool<EnemyBase> pool = enemyPools[selectedType];

            EnemyBase enemy = pool.Get();
            enemy.transform.position = ClampToArena(spawnPos);

            ConfigureSpecializedEnemy(enemy);
            enemy.SetRuntimeModifiers(ResolveEnemyModifiers());

            enemy.Initialize(selectedType, heroTarget, pool, eventBus, hpMultiplier, damageMultiplier);
            enemy.OnDied += cachedEnemyDiedHandler;
            activeEnemies.Add(enemy);
            return enemy;
        }

        private void ConfigureSpecializedEnemy(EnemyBase enemy)
        {
            if (enemy is ShootingEnemy shootingEnemy)
            {
                shootingEnemy.SetProjectilePool(enemyProjectilePool);
            }
            else if (enemy is SummonerEnemy summonerEnemy)
            {
                summonerEnemy.SetSummonCallback(cachedSummonMinionCallback);
            }
            else if (enemy is FleeingLootEnemy fleeingEnemy)
            {
                float halfArena = arenaGenerator != null ? arenaGenerator.ArenaSize * 0.5f : 250f;
                float padding = waveConfig != null ? waveConfig.arenaPadding : 3f;
                float maxCoord = Mathf.Max(8f, halfArena - padding);
                fleeingEnemy.ConfigureLootCallbacks(cachedSpawnCoinCallback, cachedSpawnVacuumCallback, maxCoord);
            }
            else if (enemy is SplitterEnemy splitterEnemy)
            {
                splitterEnemy.SetSplitSpawnCallback(cachedSplitChildCallback);
            }
            else if (enemy is FrostWispEnemy frostWispEnemy)
            {
                frostWispEnemy.SetFrostZoneCallback(cachedFrostZoneCallback);
            }
        }

        private void SpawnSplitChild(EnemyData childData, Vector3 position, float hpScale, float dmgScale)
        {
            if (childData == null || !CanSpawnMoreEnemies) return;
            if (heroHealth != null && heroHealth.IsDead) return;

            EnsurePoolsInitialized();
            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier);
            hpMultiplier *= Mathf.Max(0.1f, hpScale);
            damageMultiplier *= Mathf.Max(0.1f, dmgScale);

            SpawnEnemyInstance(childData, position, hpMultiplier, damageMultiplier);
        }

        private void EnsureFrostZonePoolInitialized()
        {
            if (frostZonePool != null) return;
            if (frostZoneContainer == null)
            {
                frostZoneContainer = new GameObject("FrostZoneContainer").transform;
            }

            frostZonePool = new ObjectPool<FrostSlowZone>(
                createFunc: () =>
                {
                    var go = new GameObject("FrostSlowZone");
                    go.transform.SetParent(frostZoneContainer, false);
                    return go.AddComponent<FrostSlowZone>();
                },
                actionOnGet: z => z.gameObject.SetActive(true),
                actionOnRelease: z => z.gameObject.SetActive(false),
                actionOnDestroy: z =>
                {
                    if (z != null)
                    {
                        if (Application.isPlaying) Destroy(z.gameObject);
                        else DestroyImmediate(z.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: MAX_ACTIVE_FROST_ZONES);
        }

        private void SpawnFrostSlowZone(Vector3 position, FrostWispEnemyData wispData)
        {
            if (wispData == null || (heroHealth != null && heroHealth.IsDead)) return;

            EnsureFrostZonePoolInitialized();

            if (activeFrostZones.Count >= MAX_ACTIVE_FROST_ZONES && activeFrostZones[0] != null)
            {
                activeFrostZones[0].ForceExpire();
            }

            if (cachedFrostZoneMaterial == null)
            {
                cachedFrostZoneMaterial = Resources.Load<Material>("FrostWispZoneMat");
                if (cachedFrostZoneMaterial == null)
                {
                    Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (urpUnlit != null)
                    {
                        cachedFrostZoneMaterial = new Material(urpUnlit)
                        {
                            name = "FrostZoneRuntimeMat",
                            enableInstancing = true
                        };
                        if (cachedFrostZoneMaterial.HasProperty("_BaseColor"))
                        {
                            cachedFrostZoneMaterial.SetColor("_BaseColor", new Color(0.45f, 0.90f, 1.0f, 0.50f));
                        }
                    }
                }
            }

            FrostSlowZone zone = frostZonePool.Get();
            ISlowable heroSlowable = heroTarget != null ? heroTarget.GetComponent<ISlowable>() : null;
            Vector3 clampedPos = ClampToArena(position);
            zone.Initialize(
                clampedPos,
                wispData.frostZoneRadius,
                wispData.frostZoneDuration,
                wispData.heroSlowFactor,
                wispData.slowRefreshDuration,
                wispData.zoneTickInterval,
                heroTarget,
                heroSlowable,
                cachedFrostZoneMaterial,
                frostZonePool,
                wispData.frostZonePrefab,
                cachedFrostZoneReleasedCallback);
            activeFrostZones.Add(zone);
        }

        private void HandleFrostZoneReleased(FrostSlowZone zone)
        {
            activeFrostZones.Remove(zone);
        }

        private void ClearActiveFrostZones()
        {
            for (int i = activeFrostZones.Count - 1; i >= 0; i--)
            {
                if (activeFrostZones[i] != null)
                {
                    activeFrostZones[i].ForceExpire();
                }
            }
            activeFrostZones.Clear();
        }

        private void SpawnLootCoin(Vector3 pos, int amount)
        {
            if (coinManager == null || amount <= 0) return;
            coinManager.EnsureInitialized();
            coinManager.SpawnCoin(ClampToArena(pos), amount);
        }

        private void SpawnLootVacuum(Vector3 pos)
        {
            if (vacuumMagnetSpawner == null && objectResolver != null)
            {
                objectResolver.TryResolve(out vacuumMagnetSpawner);
            }
            if (vacuumMagnetSpawner == null) return;
            vacuumMagnetSpawner.SpawnVacuumMagnet(ClampToArena(pos));
        }

        private void SpawnMiniBoss()
        {
            if (waveConfig == null || !CanSpawnMoreEnemies) return;

            EnemyData selectedType = waveConfig.SelectMiniBossEnemyForWave(currentWave, Random.value);
            if (selectedType == null) return;

            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier);
            EnemyBase enemy = SpawnEnemyInstance(selectedType, GetValidSpawnPosition(), hpMultiplier, damageMultiplier);
            if (enemy == null) return;

            enemy.MakeMiniBoss(
                waveConfig.miniBossHpMultiplier,
                waveConfig.miniBossDamageMultiplier,
                waveConfig.miniBossScale);
            enemy.SetShardRewardMultiplier(ResolveEnemyModifiers().MutatorMiniBossShardMultiplier);
        }

        public EnemyBase SpawnChallengeElite(Vector3 position, EliteArchetype? overrideArchetype = null)
        {
            if (waveConfig == null) return null;

            EnemyData selectedType = waveConfig.SelectMiniBossEnemyForWave(currentWave, Random.value);
            if (selectedType == null) return null;

            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier);
            EnemyBase enemy = SpawnEnemyInstance(selectedType, position, hpMultiplier, damageMultiplier);
            if (enemy != null)
            {
                EliteArchetype archetype = overrideArchetype ?? PickEliteArchetype(Random.value);
                enemy.MakeElite(archetype, waveConfig);
            }
            return enemy;
        }

        private void SpawnBoss(BossData bossData)
        {
            if (bossData == null || !CanSpawnMoreEnemies) return;
            SpawnBossAt(bossData, GetValidSpawnPosition());
        }

        private EnemyBase SpawnBossAt(BossData bossData, Vector3 position)
        {
            if (bossData == null || !CanSpawnMoreEnemies) return null;
            EnsurePoolsInitialized();
            EnsureBossPool(bossData);

            IObjectPool<EnemyBase> pool = bossPools[bossData];
            EnemyBase enemy = pool.Get();
            enemy.transform.position = ClampToArena(position);

            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier, applyMutatorHp: false);
            enemy.SetRuntimeModifiers(ResolveEnemyModifiers());

            if (enemy is BossBase boss)
            {
                boss.InitializeBoss(
                    bossData,
                    heroTarget,
                    pool,
                    eventBus,
                    enemyProjectilePool,
                    cachedSummonMinionCallback,
                    cachedSafePositionProvider,
                    hpMultiplier,
                    damageMultiplier);
            }
            else
            {
                enemy.Initialize(bossData, heroTarget, pool, eventBus, hpMultiplier, damageMultiplier);
            }

            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            enemy.OnDied += cachedEnemyDiedHandler;
            activeEnemies.Add(enemy);
            return enemy;
        }

        private void SpawnSummonedMinion(EnemyData minionData, Vector3 nearPosition)
        {
            if (heroHealth != null && heroHealth.IsDead) return;
            int configuredSummonCap = runRulesConfig != null && runRulesConfig.maxActiveEnemiesForSummon > 0
                ? runRulesConfig.maxActiveEnemiesForSummon
                : RunRulesConfig.DEFAULT_MAX_ACTIVE_ENEMIES_FOR_SUMMON;
            int summonCap = Mathf.Min(configuredSummonCap, MaxActiveEnemies);
            if (activeEnemies.Count >= summonCap) return;

            EnemyData dataToSpawn = minionData != null
                ? minionData
                : waveConfig?.SelectEnemyEntryForWave(currentWave, 0f)?.enemy;
            if (dataToSpawn == null) return;

            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            EnsureEnemyPool(dataToSpawn);
            IObjectPool<EnemyBase> pool = enemyPools[dataToSpawn];

            EnemyBase minion = pool.Get();
            Vector3 resolvedPos = ClampToArena(nearPosition);
            CameraConfig cfg = cameraConfig ?? (cameraController != null ? cameraController.Config : null);
            if (cameraController != null && heroTarget != null && cfg != null && cfg.spawnSummonsOffscreen)
            {
                Vector3 heroPos = heroTarget.position;
                Vector3 dir = nearPosition - heroPos;
                dir.y = 0f;
                if (dir.sqrMagnitude <= 0.01f)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                }
                resolvedPos = GetValidOffscreenPositionAlongDirection(heroPos, dir, extraMargin: 0f, bandWidthOverride: 2.0f);
            }
            minion.transform.position = resolvedPos;

            ConfigureSpecializedEnemy(minion);
            minion.SetRuntimeModifiers(ResolveEnemyModifiers());

            GetWaveDifficultyMultipliers(out float hpMultiplier, out float damageMultiplier);
            minion.Initialize(dataToSpawn, heroTarget, pool, eventBus, hpMultiplier, damageMultiplier);

            minion.OnDied += cachedEnemyDiedHandler;
            activeEnemies.Add(minion);
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            if (cachedEnemyDiedHandler != null)
            {
                enemy.OnDied -= cachedEnemyDiedHandler;
            }
            activeEnemies.Remove(enemy);
            CheckWaveEnd();
        }

        private void CheckWaveEnd()
        {
            if (heroHealth != null && heroHealth.IsDead) return;

            enemyIterationBuffer.Clear();
            foreach (var enemy in activeEnemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.IsDead)
                {
                    enemyIterationBuffer.Add(enemy);
                }
            }
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                activeEnemies.Remove(enemyIterationBuffer[i]);
            }
            enemyIterationBuffer.Clear();

            int overlapThreshold = waveConfig != null
                ? waveConfig.GetOverlapAliveThreshold(currentWave, currentWaveSpawnedCount, isFtueRun, currentWaveIsBoss)
                : 0;

            if (!IsWaveFrozen && !isSpawning && activeEnemies.Count <= overlapThreshold)
            {
                AdvanceWave();
            }
        }

        private void AdvanceWave()
        {
            if (IsWaveFrozen) return;
            StopWaveCoroutines();
            currentWaveSpawnedCount = 0;
            currentWaveIsBoss = false;

            int completedWave = currentWave;
            if (!tookDamageInCurrentWave)
            {
                consecutiveNoDamageWaves++;
                achievementManager?.OnNoDamageWaves(consecutiveNoDamageWaves);
            }
            tookDamageInCurrentWave = false;

            currentWave++;
            DevLog.Log($"[WaveManager] Wave {completedWave} completed!");
            achievementManager?.OnWaveReached(currentWave);
            eventBus?.Publish(new WaveCompletedEvent(completedWave, currentWave));
            startWaveDelayCoroutine = StartCoroutine(StartWaveDelay());
        }

        private void StopWaveCoroutines()
        {
            PendingWaveEnemies = 0;
            if (waveDurationCoroutine != null)
            {
                StopCoroutine(waveDurationCoroutine);
                waveDurationCoroutine = null;
            }
            if (spawnWaveCoroutine != null)
            {
                StopCoroutine(spawnWaveCoroutine);
                spawnWaveCoroutine = null;
                isSpawning = false;
            }
            if (startWaveDelayCoroutine != null)
            {
                StopCoroutine(startWaveDelayCoroutine);
                startWaveDelayCoroutine = null;
            }
        }

        public void JumpToWave(int targetWave)
        {
            if (!Application.isPlaying) return;
            StopWaveCoroutines();
            currentWaveSpawnedCount = 0;
            currentWaveIsBoss = false;

            CopyActiveEnemiesToBuffer();
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                var e = enemyIterationBuffer[i];
                if (e != null)
                {
                    if (cachedEnemyDiedHandler != null) e.OnDied -= cachedEnemyDiedHandler;
                    e.Despawn();
                }
            }
            activeEnemies.Clear();
            enemyIterationBuffer.Clear();
            ClearActiveFrostZones();

            int prev = currentWave;
            currentWave = Mathf.Max(1, targetWave);
            eventBus?.Publish(new WaveCompletedEvent(prev, currentWave));
            if (!IsWaveFrozen)
            {
                spawnWaveCoroutine = StartCoroutine(SpawnWaveCoroutine());
            }
        }

        // ---- Debug / test mode (used by DebugCheatMenu) ----

        /// <summary>True while wave spawning and wave advancement are suspended for testing.</summary>
        public bool IsWaveFrozen { get; private set; }

        /// <summary>
        /// Freezes or unfreezes the wave flow. While frozen no wave mobs spawn and the wave never advances;
        /// enemies already on the map (and their summons) keep acting. Unfreezing restarts the current wave.
        /// </summary>
        public void SetWavesFrozen(bool frozen)
        {
            if (!Application.isPlaying || IsWaveFrozen == frozen) return;
            IsWaveFrozen = frozen;
            if (frozen)
            {
                StopWaveCoroutines();
                currentWaveSpawnedCount = 0;
                return;
            }

            currentWaveSpawnedCount = 0;
            currentWaveIsBoss = false;
            if (heroHealth == null || !heroHealth.IsDead)
            {
                spawnWaveCoroutine = StartCoroutine(SpawnWaveCoroutine());
            }
        }

        /// <summary>Removes every active enemy without XP, coin or kill rewards.</summary>
        public int DespawnAllActiveEnemies()
        {
            if (!Application.isPlaying) return 0;
            CopyActiveEnemiesToBuffer();
            int removed = 0;
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                var e = enemyIterationBuffer[i];
                if (e == null) continue;
                if (cachedEnemyDiedHandler != null) e.OnDied -= cachedEnemyDiedHandler;
                e.Despawn();
                removed++;
            }
            activeEnemies.Clear();
            enemyIterationBuffer.Clear();
            ClearActiveFrostZones();
            CheckWaveEnd();
            return removed;
        }

        /// <summary>
        /// Spawns one enemy (or boss) near the hero with the current wave's HP/damage scaling.
        /// Bosses use the full boss initialization (patterns, summons, HP bar).
        /// </summary>
        public EnemyBase DebugSpawnEnemyNearHero(EnemyData data, float distance = 7f, bool makeElite = false)
        {
            if (!Application.isPlaying || data == null || heroTarget == null || !CanSpawnMoreEnemies) return null;
            EnsurePoolsInitialized();

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(1f, distance);
            Vector3 pos = GetValidPositionNear(heroTarget.position + offset, 0f, 2f);

            if (data is BossData bossData)
            {
                return SpawnBossAt(bossData, pos);
            }

            EnemyBase enemy = SpawnEnemyForWave(data, pos, currentWave, makeElite, PickEliteArchetype(Random.value));
            return enemy;
        }

        public void KillAllActiveEnemies()
        {
            if (!Application.isPlaying) return;
            CopyActiveEnemiesToBuffer();
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                var e = enemyIterationBuffer[i];
                if (e != null && !e.IsDead)
                {
                    e.TakeDamageFrom(Mathf.Max(999999, e.CurrentHp + 1), false, e.transform.position, isAoE: true);
                }
            }
            enemyIterationBuffer.Clear();
            ClearActiveFrostZones();
        }

        public void ResumeAfterRevive()
        {
            if (heroHealth != null && heroHealth.IsDead) return;

            enemyIterationBuffer.Clear();
            foreach (var enemy in activeEnemies)
            {
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                {
                    enemyIterationBuffer.Add(enemy);
                }
            }
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                activeEnemies.Remove(enemyIterationBuffer[i]);
            }
            enemyIterationBuffer.Clear();

            Vector3 heroPos = heroTarget != null ? heroTarget.position : Vector3.zero;
            float safeRadius = runRulesConfig != null ? runRulesConfig.reviveSafeRadius : RunRulesConfig.DEFAULT_REVIVE_SAFE_RADIUS;
            float pushDistance = runRulesConfig != null ? runRulesConfig.revivePushDistance : RunRulesConfig.DEFAULT_REVIVE_PUSH_DISTANCE;
            float safeRadiusSq = safeRadius * safeRadius;

            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            CopyActiveEnemiesToBuffer();
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                var enemy = enemyIterationBuffer[i];
                if (enemy == null) continue;

                enemy.OnDied -= cachedEnemyDiedHandler;
                enemy.OnDied += cachedEnemyDiedHandler;

                if (heroTarget != null)
                {
                    Vector3 diff = enemy.transform.position - heroPos;
                    diff.y = 0f;
                    if (diff.sqrMagnitude < safeRadiusSq)
                    {
                        Vector3 pushDir = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector3.forward;
                        enemy.Teleport(ClampToArena(heroPos + pushDir * pushDistance));
                    }
                }
            }
            enemyIterationBuffer.Clear();

            if (leashCoroutine != null)
            {
                StopCoroutine(leashCoroutine);
            }
            leashCoroutine = StartCoroutine(LeashCheckCoroutine());

            CheckWaveEnd();

            bool isBossWave = waveConfig != null
                && waveConfig.bosses != null
                && waveConfig.bosses.Count > 0
                && IsBossWave(currentWave, waveConfig.bossInterval);

            if (!isSpawning && activeEnemies.Count > 0 && maxWaveDurationWait != null && !isBossWave)
            {
                if (waveDurationCoroutine != null) StopCoroutine(waveDurationCoroutine);
                waveDurationCoroutine = StartCoroutine(WaveDurationCoroutine(currentWave));
            }
        }

        [ContextMenu("Debug: Jump To Wave 5 (Mini-Boss)")]
        public void DebugJumpToWave5() => JumpToWave(5);

        [ContextMenu("Debug: Jump To Wave 25 (Golem Boss)")]
        public void DebugJumpToWave25() => JumpToWave(25);

        [ContextMenu("Debug: Jump To Wave 50 (Necromancer Boss)")]
        public void DebugJumpToWave50() => JumpToWave(50);

        [ContextMenu("Debug: Jump To Wave 75 (Frost Lich Boss)")]
        public void DebugJumpToWave75() => JumpToWave(75);

        [ContextMenu("Debug: Jump To Wave 100 (Demon Lord Boss)")]
        public void DebugJumpToWave100() => JumpToWave(100);
    }
}
