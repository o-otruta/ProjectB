using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Data.Enemies;
using ProjectB.Player;

namespace ProjectB.Enemies
{
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private WaveConfig waveConfig;
        [SerializeField] private EnemyData baseEnemyData;
        
        [Tooltip("Цель, за которой будут следовать враги (герой)")]
        [SerializeField] private Transform heroTarget;

        private IObjectPool<EnemyBase> enemyPool;
        private int currentWave = 1;
        public int CurrentWave => currentWave;
        private int enemiesAlive = 0;
        private bool isSpawning = false;

        private void Start()
        {
            if (waveConfig == null || baseEnemyData == null || heroTarget == null)
            {
                Debug.LogError("WaveManager missing references!");
                return;
            }

            Transform enemyContainer = new GameObject("EnemyContainer").transform;

            enemyPool = new ObjectPool<EnemyBase>(
                createFunc: () => {
                    GameObject go;
                    if (baseEnemyData.modelPrefab != null) {
                        go = Instantiate(baseEnemyData.modelPrefab, enemyContainer);
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
            EnemyBase enemy = enemyPool.Get();
            
            // Random point on circle around hero
            float angle = Random.Range(0f, Mathf.PI * 2);
            float radius = Random.Range(waveConfig.spawnRadiusMin, waveConfig.spawnRadiusMax);
            
            Vector3 spawnPos = heroTarget.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            
            // Adjust height if necessary based on your arena, assuming Y=1 is good for a capsule
            spawnPos.y = 1f; 
            enemy.transform.position = spawnPos;
            
            // Re-initialize logic in EnemyBase
            float difficultyMultiplier = 1f + (currentWave - 1) * waveConfig.difficultyPerWave;
            enemy.Initialize(baseEnemyData, heroTarget, enemyPool, difficultyMultiplier);
            
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
                StartCoroutine(StartWaveDelay());
            }
        }
    }
}
