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
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private List<EnemyData> enemyTypes;
        
        private HeroHealth heroHealth;
        private Transform heroTarget;
        private ProjectB.Meta.AchievementManager achievementManager;
        private GameEventBus eventBus;
        private ArenaGenerator arenaGenerator;
        private EnemyProjectilePool enemyProjectilePool;

        private Dictionary<EnemyData, IObjectPool<EnemyBase>> enemyPools;
        private readonly HashSet<EnemyBase> activeEnemies = new HashSet<EnemyBase>();
        private readonly List<EnemyBase> enemyIterationBuffer = new List<EnemyBase>();

        private WaitForSeconds waveDelayWait;
        private WaitForSeconds spawnDelayWait;
        private WaitForSeconds maxWaveDurationWait;
        private int obstacleMask;

        private int currentWave = 1;
        public int CurrentWave => currentWave;
        public int EnemiesAlive => activeEnemies.Count;
        private bool isSpawning = false;
        private bool tookDamageInCurrentWave = false;
        private int consecutiveNoDamageWaves = 0;
        private Coroutine waveDurationCoroutine;
        private Coroutine spawnWaveCoroutine;
        private Coroutine startWaveDelayCoroutine;

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
            if (waveConfig == null || enemyTypes == null || enemyTypes.Count == 0 || heroTarget == null)
            {
                Debug.LogError("WaveManager missing references or enemy types!");
                return;
            }

            waveDelayWait = new WaitForSeconds(waveConfig.waveDelay);
            spawnDelayWait = new WaitForSeconds(waveConfig.spawnDelay);
            maxWaveDurationWait = waveConfig.maxWaveDuration > 0f ? new WaitForSeconds(waveConfig.maxWaveDuration) : null;
            obstacleMask = LayerMask.GetMask("Obstacles");

            Transform enemyContainer = new GameObject("EnemyContainer").transform;
            enemyPools = new System.Collections.Generic.Dictionary<EnemyData, IObjectPool<EnemyBase>>();

            foreach (var enemyData in enemyTypes)
            {
                if (enemyData == null || enemyPools.ContainsKey(enemyData)) continue;

                var pool = new ObjectPool<EnemyBase>(
                    createFunc: () => {
                        GameObject go;
                        if (enemyData.modelPrefab != null) {
                            go = Instantiate(enemyData.modelPrefab, enemyContainer);
                        } else {
                            // Fallback, if no prefab assigned, create a default capsule
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
                            Debug.LogError($"Enemy prefab '{enemyData.name}' is missing EnemyBase component! Adding default EnemyBase.", go);
                            enemy = go.AddComponent<EnemyBase>();
                        }
                        return enemy;
                    },
                    actionOnGet: e => e.gameObject.SetActive(true),
                    actionOnRelease: e => {
                        e.gameObject.SetActive(false);
                    },
                    actionOnDestroy: e => Destroy(e.gameObject),
                    collectionCheck: false,
                    defaultCapacity: 50,
                    maxSize: 300
                );
                enemyPools.Add(enemyData, pool);
            }

            eventBus?.Subscribe<GameOverEvent>(OnGameOver);
            if (heroHealth != null)
            {
                heroHealth.OnDamageTaken += HandleHeroDamageTaken;
            }
            StartCoroutine(LeashCheckCoroutine());
            startWaveDelayCoroutine = StartCoroutine(StartWaveDelay());
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

            // Unsubscribe from enemy death events to prevent late wave transitions,
            // but keep enemies alive on screen until scene restart/menu
            CopyActiveEnemiesToBuffer();
            for (int i = 0; i < enemyIterationBuffer.Count; i++)
            {
                var enemy = enemyIterationBuffer[i];
                if (enemy != null)
                {
                    enemy.OnDied -= HandleEnemyDied;
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
            int enemiesToSpawn = Mathf.RoundToInt(waveConfig.baseEnemyCount * Mathf.Pow(waveConfig.enemiesPerWaveMultiplier, currentWave - 1));
            enemiesToSpawn = Mathf.Min(enemiesToSpawn, waveConfig.maxEnemiesPerWave);
            
            Debug.Log($"[WaveManager] Spawning Wave {currentWave} with {enemiesToSpawn} enemies.");

            if (maxWaveDurationWait != null)
            {
                if (waveDurationCoroutine != null) StopCoroutine(waveDurationCoroutine);
                waveDurationCoroutine = StartCoroutine(WaveDurationCoroutine(waveNumber));
            }

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                if (heroTarget == null || (heroHealth != null && heroHealth.IsDead) || currentWave != waveNumber)
                {
                    if (currentWave == waveNumber) isSpawning = false;
                    yield break;
                }
                
                SpawnEnemy();
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

        private Vector3 GetValidSpawnPosition()
        {
            float halfArena = arenaGenerator != null ? arenaGenerator.ArenaSize / 2f : 250f;
            float padding = waveConfig != null ? waveConfig.arenaPadding : 3f;
            float maxCoord = Mathf.Max(5f, halfArena - padding);

            Vector3 center = heroTarget != null ? heroTarget.position : Vector3.zero;
            float minRad = waveConfig != null ? waveConfig.spawnRadiusMin : 10f;
            float maxRad = waveConfig != null ? waveConfig.spawnRadiusMax : 15f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(minRad, maxRad);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                candidate.y = 0f;

                candidate.x = Mathf.Clamp(candidate.x, -maxCoord, maxCoord);
                candidate.z = Mathf.Clamp(candidate.z, -maxCoord, maxCoord);

                if (obstacleMask == 0 || !Physics.CheckSphere(candidate + Vector3.up * 0.5f, 0.5f, obstacleMask))
                {
                    return candidate;
                }
            }

            Vector3 fallback = center + (Random.insideUnitSphere * minRad);
            fallback.y = 0f;
            fallback.x = Mathf.Clamp(fallback.x, -maxCoord, maxCoord);
            fallback.z = Mathf.Clamp(fallback.z, -maxCoord, maxCoord);
            return fallback;
        }

        private void SpawnEnemy()
        {
            EnemyData selectedType = enemyTypes[Random.Range(0, enemyTypes.Count)];
            IObjectPool<EnemyBase> pool = enemyPools[selectedType];
            
            EnemyBase enemy = pool.Get();
            Vector3 spawnPos = GetValidSpawnPosition();
            enemy.transform.position = spawnPos;

            if (enemy is ShootingEnemy shootingEnemy)
            {
                shootingEnemy.SetProjectilePool(enemyProjectilePool);
            }
            
            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            enemy.Initialize(selectedType, heroTarget, pool, eventBus, difficultyMultiplier);

            if (currentWave >= waveConfig.minWaveForElites)
            {
                float chance = Mathf.Min(waveConfig.maxEliteChance, (currentWave - waveConfig.minWaveForElites + 1) * waveConfig.eliteChancePerWave);
                if (Random.value < chance)
                {
                    enemy.MakeElite(waveConfig.eliteHpMultiplier, waveConfig.eliteDamageMultiplier);
                }
            }
            
            enemy.OnDied -= HandleEnemyDied;
            enemy.OnDied += HandleEnemyDied;
            
            activeEnemies.Add(enemy);
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            enemy.OnDied -= HandleEnemyDied;
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
    }
}

