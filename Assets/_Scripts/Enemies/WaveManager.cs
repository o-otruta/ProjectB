using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using ProjectB.Data.Enemies;
using ProjectB.Player;

namespace ProjectB.Enemies
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private System.Collections.Generic.List<EnemyData> enemyTypes;
        
        private Transform heroTarget;
        private ProjectB.LevelUp.XpManager xpManager;
        private ProjectB.LevelUp.CoinManager coinManager;
        private ProjectB.Core.RunStatistics runStatistics;
        private ProjectB.Meta.AchievementManager achievementManager;

        private System.Collections.Generic.Dictionary<EnemyData, IObjectPool<EnemyBase>> enemyPools;
        private int currentWave = 1;
        public int CurrentWave => currentWave;
        private int enemiesAlive = 0;
        private bool isSpawning = false;

        [Inject]
        public void Construct(HeroHealth heroHealth, ProjectB.LevelUp.XpManager xpManager, ProjectB.LevelUp.CoinManager coinManager, ProjectB.Core.RunStatistics runStatistics, ProjectB.Meta.AchievementManager achievementManager)
        {
            if (heroHealth != null)
            {
                heroTarget = heroHealth.transform;
            }
            this.xpManager = xpManager;
            this.coinManager = coinManager;
            this.runStatistics = runStatistics;
            this.achievementManager = achievementManager;
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
                        if (enemy == null) enemy = go.AddComponent<EnemyBase>();
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

            StartCoroutine(StartWaveDelay());
        }

        private IEnumerator StartWaveDelay()
        {
            yield return new WaitForSeconds(waveConfig.waveDelay);
            StartCoroutine(SpawnWaveCoroutine());
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;
            int enemiesToSpawn = Mathf.RoundToInt(waveConfig.baseEnemyCount * Mathf.Pow(waveConfig.enemiesPerWaveMultiplier, currentWave - 1));
            enemiesToSpawn = Mathf.Min(enemiesToSpawn, waveConfig.maxEnemiesPerWave);
            
            Debug.Log($"[WaveManager] Spawning Wave {currentWave} with {enemiesToSpawn} enemies.");

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                if (heroTarget == null || (heroTarget.TryGetComponent<ProjectB.Player.HeroHealth>(out var hp) && hp.IsDead))
                {
                    isSpawning = false;
                    yield break;
                }
                
                SpawnEnemy();
                yield return new WaitForSeconds(waveConfig.spawnDelay);
            }
            
            isSpawning = false;
        }

        private void SpawnEnemy()
        {
            // Randomly select an enemy type
            EnemyData selectedType = enemyTypes[Random.Range(0, enemyTypes.Count)];
            IObjectPool<EnemyBase> pool = enemyPools[selectedType];
            
            EnemyBase enemy = pool.Get();
            
            // Random point on circle around hero
            float angle = Random.Range(0f, Mathf.PI * 2);
            float radius = Random.Range(waveConfig.spawnRadiusMin, waveConfig.spawnRadiusMax);
            
            Vector3 spawnPos = heroTarget.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            
            // Adjust height if necessary based on your arena, assuming Y=1 is good for a capsule
            spawnPos.y = 1f; 
            enemy.transform.position = spawnPos;
            
            // Re-initialize logic in EnemyBase
            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            enemy.Initialize(selectedType, heroTarget, pool, difficultyMultiplier, xpManager, coinManager, runStatistics);

            if (currentWave >= waveConfig.minWaveForElites)
            {
                float chance = Mathf.Min(waveConfig.maxEliteChance, (currentWave - waveConfig.minWaveForElites + 1) * waveConfig.eliteChancePerWave);
                if (Random.value < chance)
                {
                    enemy.MakeElite();
                }
            }
            
            enemy.OnDied -= HandleEnemyDied; // Ensure no duplicate subscription
            enemy.OnDied += HandleEnemyDied;
            
            enemiesAlive++;
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            enemy.OnDied -= HandleEnemyDied;
            enemiesAlive--;
            CheckWaveEnd();
        }

        private void CheckWaveEnd()
        {
            if (!isSpawning && enemiesAlive <= 0)
            {
                currentWave++;
                Debug.Log($"[WaveManager] Wave {currentWave - 1} completed!");
                achievementManager?.OnWaveReached(currentWave);
                StartCoroutine(StartWaveDelay());
            }
        }
    }
}
