using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using ProjectB.Data.Enemies;
using ProjectB.Player;
using ProjectB.Arena;
using ProjectB.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Enemies
{
    public class WaveManager : MonoBehaviour
    {
        private const int MAX_ACTIVE_ENEMIES_FOR_SUMMON = 80;

        [SerializeField] private WaveConfig waveConfig;
        
        private HeroHealth heroHealth;
        private Transform heroTarget;
        private ProjectB.Meta.AchievementManager achievementManager;
        private GameEventBus eventBus;
        private ArenaGenerator arenaGenerator;
        private EnemyProjectilePool enemyProjectilePool;

        private Transform enemyContainer;
        private Dictionary<EnemyData, IObjectPool<EnemyBase>> enemyPools;
        private Dictionary<BossData, IObjectPool<EnemyBase>> bossPools;
        private readonly HashSet<EnemyBase> activeEnemies = new HashSet<EnemyBase>(256);
        private readonly List<EnemyBase> enemyIterationBuffer = new List<EnemyBase>(256);
        private System.Action<EnemyBase> cachedEnemyDiedHandler;
        private System.Action<EnemyData, Vector3> cachedSummonMinionCallback;
        private System.Func<Vector3, float, float, Vector3> cachedSafePositionProvider;

        private WaitForSeconds waveDelayWait;
        private WaitForSeconds spawnDelayWait;
        private WaitForSeconds maxWaveDurationWait;
        private int obstacleMask;

        private int currentWave = 1;
        public int CurrentWave => currentWave;
        public int EnemiesAlive => activeEnemies.Count;
        public IReadOnlyCollection<EnemyBase> ActiveEnemies => activeEnemies;
        public WaveConfig Config => waveConfig;
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
            ArenaGenerator arenaGenerator = null)
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
        }

        private void Start()
        {
            if (waveConfig == null || waveConfig.enemySchedule == null || waveConfig.enemySchedule.Count == 0 || heroTarget == null)
            {
                Debug.LogError("WaveManager missing WaveConfig, enemySchedule, or heroTarget!");
                return;
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

        public void EnsurePoolsInitialized()
        {
            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            if (cachedSummonMinionCallback == null) cachedSummonMinionCallback = SpawnSummonedMinion;
            if (cachedSafePositionProvider == null) cachedSafePositionProvider = GetValidPositionNear;

            if (waveConfig != null)
            {
                waveDelayWait ??= new WaitForSeconds(waveConfig.waveDelay);
                spawnDelayWait ??= new WaitForSeconds(waveConfig.spawnDelay);
                if (maxWaveDurationWait == null && waveConfig.maxWaveDuration > 0f)
                {
                    maxWaveDurationWait = new WaitForSeconds(waveConfig.maxWaveDuration);
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
        }

        private IEnumerator StartWaveDelay()
        {
            yield return waveDelayWait;
            startWaveDelayCoroutine = null;
            if (spawnWaveCoroutine != null) StopCoroutine(spawnWaveCoroutine);
            spawnWaveCoroutine = StartCoroutine(SpawnWaveCoroutine());
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;
            int waveNumber = currentWave;

            bool hasBossesConfigured = waveConfig.bosses != null && waveConfig.bosses.Count > 0;
            bool isBossWave = hasBossesConfigured && IsBossWave(waveNumber, waveConfig.bossInterval);
            bool isMiniBossWave = IsMiniBossWave(waveNumber, waveConfig.miniBossInterval, waveConfig.bossInterval);

            if (isBossWave)
            {
                int bossIndex = GetBossIndexForWave(waveNumber, waveConfig.bossInterval, waveConfig.bosses.Count);
                BossData bossData = waveConfig.bosses[bossIndex];
                Debug.Log($"[WaveManager] Boss Wave {waveNumber}! Spawning {bossData.bossDisplayName}.");

                // Boss wave: no timeout skip, no regular wave mobs — boss + its summons only
                SpawnBoss(bossData);

                if (currentWave == waveNumber)
                {
                    isSpawning = false;
                    spawnWaveCoroutine = null;
                    CheckWaveEnd();
                }
                yield break;
            }

            int enemiesToSpawn = Mathf.RoundToInt(waveConfig.baseEnemyCount * Mathf.Pow(waveConfig.enemiesPerWaveMultiplier, currentWave - 1));
            enemiesToSpawn = Mathf.Min(enemiesToSpawn, waveConfig.maxEnemiesPerWave);
            
            Debug.Log($"[WaveManager] Spawning Wave {currentWave} with {enemiesToSpawn} enemies{(isMiniBossWave ? " + Mini-Boss" : "")}.");

            if (maxWaveDurationWait != null)
            {
                if (waveDurationCoroutine != null) StopCoroutine(waveDurationCoroutine);
                waveDurationCoroutine = StartCoroutine(WaveDurationCoroutine(waveNumber));
            }

            if (isMiniBossWave)
            {
                SpawnMiniBoss();
            }

            int spawnedCount = 0;
            while (spawnedCount < enemiesToSpawn)
            {
                if (heroTarget == null || (heroHealth != null && heroHealth.IsDead) || currentWave != waveNumber)
                {
                    if (currentWave == waveNumber) isSpawning = false;
                    yield break;
                }

                spawnedCount += SpawnEnemyOrPack(enemiesToSpawn - spawnedCount);
                yield return spawnDelayWait;
            }
            
            if (currentWave == waveNumber)
            {
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

            Debug.Log($"[WaveManager] Wave {waveNumber} reached max duration ({waveConfig.maxWaveDuration}s). Advancing to next wave (enemies remain alive).");
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

                float leashDist = waveConfig != null ? waveConfig.leashDistance : 35f;
                float leashDistSq = leashDist * leashDist;
                Vector3 heroPos = heroTarget.position;

                CopyActiveEnemiesToBuffer();

                for (int i = 0; i < enemyIterationBuffer.Count; i++)
                {
                    var enemy = enemyIterationBuffer[i];
                    if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;

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

            return ClampToArena(center + (Random.insideUnitSphere * minRad));
        }

        private Vector3 GetValidSpawnPosition()
        {
            Vector3 center = heroTarget != null ? heroTarget.position : Vector3.zero;
            float minRad = waveConfig != null ? waveConfig.spawnRadiusMin : 10f;
            float maxRad = waveConfig != null ? waveConfig.spawnRadiusMax : 15f;
            return GetValidPositionNear(center, minRad, maxRad);
        }

        private int SpawnEnemyOrPack(int remainingInWave)
        {
            EnemyWaveEntry selectedEntry = waveConfig != null
                ? waveConfig.SelectEnemyEntryForWave(currentWave, Random.value)
                : null;
            if (selectedEntry == null || selectedEntry.enemy == null) return 1;

            int packCount = Mathf.Clamp(selectedEntry.packSize, 1, Mathf.Max(1, remainingInWave));
            Vector3 basePos = GetValidSpawnPosition();
            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            bool spawnedEliteInPack = false;

            for (int i = 0; i < packCount; i++)
            {
                Vector3 spawnPos = i == 0
                    ? basePos
                    : GetValidPositionNear(basePos, 0.9f, 1.8f);

                EnemyBase enemy = SpawnEnemyInstance(selectedEntry.enemy, spawnPos, difficultyMultiplier);

                if (!spawnedEliteInPack && currentWave >= waveConfig.minWaveForElites)
                {
                    float chance = Mathf.Min(
                        waveConfig.maxEliteChance,
                        (currentWave - waveConfig.minWaveForElites + 1) * waveConfig.eliteChancePerWave);
                    if (Random.value < chance)
                    {
                        enemy.MakeElite(PickEliteArchetype(Random.value), waveConfig);
                        spawnedEliteInPack = true;
                    }
                }
            }

            return packCount;
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
            currentWave = Mathf.Max(1, waveNumber);
            float diffPerWave = waveConfig != null ? waveConfig.difficultyPerWave : 0.1f;
            float difficultyMultiplier = 1f + (currentWave - 1) * diffPerWave;
            EnemyBase enemy = SpawnEnemyInstance(selectedType, spawnPos, difficultyMultiplier);
            if (makeElite && enemy != null)
            {
                enemy.MakeElite(eliteArchetype, waveConfig);
            }
            return enemy;
        }

        private EnemyBase SpawnEnemyInstance(EnemyData selectedType, Vector3 spawnPos, float difficultyMultiplier)
        {
            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            EnsureEnemyPool(selectedType);
            IObjectPool<EnemyBase> pool = enemyPools[selectedType];

            EnemyBase enemy = pool.Get();
            enemy.transform.position = ClampToArena(spawnPos);

            if (enemy is ShootingEnemy shootingEnemy)
            {
                shootingEnemy.SetProjectilePool(enemyProjectilePool);
            }

            enemy.Initialize(selectedType, heroTarget, pool, eventBus, difficultyMultiplier);
            enemy.OnDied += cachedEnemyDiedHandler;
            activeEnemies.Add(enemy);
            return enemy;
        }

        private void SpawnMiniBoss()
        {
            if (waveConfig == null) return;

            EnemyData selectedType = waveConfig.SelectMiniBossEnemyForWave(currentWave, Random.value);
            if (selectedType == null) return;

            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            EnemyBase enemy = SpawnEnemyInstance(selectedType, GetValidSpawnPosition(), difficultyMultiplier);
            enemy.MakeMiniBoss(
                waveConfig.miniBossHpMultiplier,
                waveConfig.miniBossDamageMultiplier,
                waveConfig.miniBossScale);
        }

        public EnemyBase SpawnChallengeElite(Vector3 position, EliteArchetype? overrideArchetype = null)
        {
            if (waveConfig == null) return null;

            EnemyData selectedType = waveConfig.SelectMiniBossEnemyForWave(currentWave, Random.value);
            if (selectedType == null) return null;

            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            EnemyBase enemy = SpawnEnemyInstance(selectedType, position, difficultyMultiplier);
            if (enemy != null)
            {
                EliteArchetype archetype = overrideArchetype ?? PickEliteArchetype(Random.value);
                enemy.MakeElite(archetype, waveConfig);
            }
            return enemy;
        }

        private void SpawnBoss(BossData bossData)
        {
            if (bossData == null) return;
            EnsurePoolsInitialized();
            EnsureBossPool(bossData);

            IObjectPool<EnemyBase> pool = bossPools[bossData];
            EnemyBase enemy = pool.Get();
            enemy.transform.position = GetValidSpawnPosition();

            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;

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
                    difficultyMultiplier);
            }
            else
            {
                enemy.Initialize(bossData, heroTarget, pool, eventBus, difficultyMultiplier);
            }

            enemy.OnDied += cachedEnemyDiedHandler;
            activeEnemies.Add(enemy);
        }

        private void SpawnSummonedMinion(EnemyData minionData, Vector3 nearPosition)
        {
            if (heroHealth != null && heroHealth.IsDead) return;
            if (activeEnemies.Count >= MAX_ACTIVE_ENEMIES_FOR_SUMMON) return;

            EnemyData dataToSpawn = minionData != null
                ? minionData
                : waveConfig?.SelectEnemyEntryForWave(currentWave, 0f)?.enemy;
            if (dataToSpawn == null) return;

            if (cachedEnemyDiedHandler == null) cachedEnemyDiedHandler = HandleEnemyDied;
            EnsureEnemyPool(dataToSpawn);
            IObjectPool<EnemyBase> pool = enemyPools[dataToSpawn];

            EnemyBase minion = pool.Get();
            minion.transform.position = ClampToArena(nearPosition);

            if (minion is ShootingEnemy shootingEnemy)
            {
                shootingEnemy.SetProjectilePool(enemyProjectilePool);
            }

            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            minion.Initialize(dataToSpawn, heroTarget, pool, eventBus, difficultyMultiplier);

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

            if (!isSpawning && activeEnemies.Count <= 0)
            {
                AdvanceWave();
            }
        }

        private void AdvanceWave()
        {
            StopWaveCoroutines();

            int completedWave = currentWave;
            if (!tookDamageInCurrentWave)
            {
                consecutiveNoDamageWaves++;
                achievementManager?.OnNoDamageWaves(consecutiveNoDamageWaves);
            }
            tookDamageInCurrentWave = false;

            currentWave++;
            Debug.Log($"[WaveManager] Wave {completedWave} completed!");
            achievementManager?.OnWaveReached(currentWave);
            eventBus?.Publish(new WaveCompletedEvent(completedWave, currentWave));
            startWaveDelayCoroutine = StartCoroutine(StartWaveDelay());
        }

        private void StopWaveCoroutines()
        {
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

            int prev = currentWave;
            currentWave = Mathf.Max(1, targetWave);
            eventBus?.Publish(new WaveCompletedEvent(prev, currentWave));
            spawnWaveCoroutine = StartCoroutine(SpawnWaveCoroutine());
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
                    e.TakeDamage(Mathf.Max(999999, e.CurrentHp + 1));
                }
            }
            enemyIterationBuffer.Clear();
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
            const float safeRadius = 4.5f;
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
                        enemy.Teleport(ClampToArena(heroPos + pushDir * 5.5f));
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
    }
}
