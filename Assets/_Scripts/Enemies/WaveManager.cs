using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using ProjectB.Data.Enemies;
using ProjectB.Player;
using ProjectB.Arena;
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

        private Dictionary<EnemyData, IObjectPool<EnemyBase>> enemyPools;
        private readonly HashSet<EnemyBase> activeEnemies = new HashSet<EnemyBase>();
        private readonly List<EnemyBase> enemyIterationBuffer = new List<EnemyBase>();

        private int currentWave = 1;
        public int CurrentWave => currentWave;
        public int EnemiesAlive => activeEnemies.Count;
        private bool isSpawning = false;
        private Coroutine waveDurationCoroutine;

        [Inject]
        public void Construct(
            HeroHealth heroHealth, 
            ProjectB.Meta.AchievementManager achievementManager, 
            GameEventBus eventBus,
            ArenaGenerator arenaGenerator = null)
        {
            this.heroHealth = heroHealth;
            if (heroHealth != null)
            {
                heroTarget = heroHealth.transform;
            }
            this.achievementManager = achievementManager;
            this.eventBus = eventBus;
            this.arenaGenerator = arenaGenerator;
        }

        private void Start()
        {
            if (waveConfig == null || enemyTypes == null || enemyTypes.Count == 0 || heroTarget == null)
            {
                Debug.LogError("WaveManager missing references or enemy types!");
                return;
            }

            Transform enemyContainer = new GameObject("EnemyContainer").transform;
            enemyPools = new System.Collections.Generic.Dictionary<EnemyData, IObjectPool<EnemyBase>>();

            foreach (var enemyData in enemyTypes)
            {
                var pool = new ObjectPool<EnemyBase>(
                    createFunc: () => {
                        GameObject go;
                        if (enemyData.modelPrefab != null) {
                            go = Instantiate(enemyData.modelPrefab, enemyContainer);
                        } else {
                            // Fallback, if no prefab assigned, create a default capsule
                            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                            go.transform.SetParent(enemyContainer);
                            go.GetComponent<Renderer>().material.color = Color.red;
                            go.GetComponent<Collider>().isTrigger = true; // Enemy needs trigger
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
            StartCoroutine(LeashCheckCoroutine());
            StartCoroutine(StartWaveDelay());
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            }
            StopAllCoroutines();
        }

        private void OnGameOver(GameOverEvent evt)
        {
            StopAllCoroutines();
            isSpawning = false;
            waveDurationCoroutine = null;

            // Unsubscribe from enemy death events to prevent late wave transitions,
            // but keep enemies alive on screen until scene restart/menu
            enemyIterationBuffer.Clear();
            enemyIterationBuffer.AddRange(activeEnemies);
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
            yield return new WaitForSeconds(waveConfig.waveDelay);
            StartCoroutine(SpawnWaveCoroutine());
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;
            int waveNumber = currentWave;
            int enemiesToSpawn = Mathf.RoundToInt(waveConfig.baseEnemyCount * Mathf.Pow(waveConfig.enemiesPerWaveMultiplier, currentWave - 1));
            enemiesToSpawn = Mathf.Min(enemiesToSpawn, waveConfig.maxEnemiesPerWave);
            
            Debug.Log($"[WaveManager] Spawning Wave {currentWave} with {enemiesToSpawn} enemies.");

            if (waveConfig.maxWaveDuration > 0f)
            {
                if (waveDurationCoroutine != null) StopCoroutine(waveDurationCoroutine);
                waveDurationCoroutine = StartCoroutine(WaveDurationCoroutine(waveNumber));
            }

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                if (heroTarget == null || (heroHealth != null && heroHealth.IsDead))
                {
                    isSpawning = false;
                    yield break;
                }
                
                SpawnEnemy();
                yield return new WaitForSeconds(waveConfig.spawnDelay);
            }
            
            isSpawning = false;
            CheckWaveEnd();
        }

        private IEnumerator WaveDurationCoroutine(int waveNumber)
        {
            yield return new WaitForSeconds(waveConfig.maxWaveDuration);

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

                enemyIterationBuffer.Clear();
                enemyIterationBuffer.AddRange(activeEnemies);

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

            int obstacleMask = LayerMask.GetMask("Obstacles");

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

            activeEnemies.RemoveWhere(e => e == null || !e.gameObject.activeInHierarchy || e.IsDead);

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

            int completedWave = currentWave;
            currentWave++;
            Debug.Log($"[WaveManager] Wave {completedWave} completed!");
            achievementManager?.OnWaveReached(currentWave);
            eventBus?.Publish(new WaveCompletedEvent(completedWave, currentWave));
            StartCoroutine(StartWaveDelay());
        }
    }
}

