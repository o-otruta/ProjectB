using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Arena
{
    public class ArenaGenerator : MonoBehaviour
    {
        [SerializeField] private ArenaConfig _config;

        [Header("Materials (Optional)")]
        [SerializeField] private Material _floorMaterial;
        [SerializeField] private Material _wallMaterial;

        [Header("Settings")]
        [SerializeField] private bool _generateOnStart = true;
        [SerializeField] private bool _visibleWalls = false;

        private System.Random _rng;
        private ArenaDecorationRenderer _decorationRenderer;
        private Material _fallbackFloorMaterial;
        private Material _fallbackWallMaterial;

        private IObjectResolver _resolver;
        private GameEventBus _eventBus;
        private HeroHealth _heroHealth;
        private HeroEconomy _heroEconomy;
        private HeroExperience _heroExperience;
        private WaveManager _waveManager;
        private GameManager _gameManager;

        private readonly List<ExplosiveBarrel> _spawnedBarrels = new List<ExplosiveBarrel>();
        private Transform _interactablesContainer;

        public int CurrentSeed { get; private set; }
        public ArenaConfig Config => _config;
        public float ArenaSize => _config != null ? _config.ArenaSize : 100f;
        public IReadOnlyList<ExplosiveBarrel> SpawnedBarrels => _spawnedBarrels;
        public ChallengeShrine ActiveShrine { get; private set; }

        [Inject]
        public void Construct(
            IObjectResolver resolver = null,
            GameEventBus eventBus = null,
            HeroHealth heroHealth = null,
            HeroEconomy heroEconomy = null,
            HeroExperience heroExperience = null)
        {
            _resolver = resolver;
            _eventBus = eventBus;
            _heroHealth = heroHealth;
            _heroEconomy = heroEconomy;
            _heroExperience = heroExperience;
        }

        private void ResolveDeferredDependencies()
        {
            if (_resolver == null) return;

            if (_waveManager == null)
            {
                _resolver.TryResolve(out _waveManager);
            }

            if (_gameManager == null)
            {
                _resolver.TryResolve(out _gameManager);
            }
        }

        private void Start()
        {
            if (_generateOnStart)
            {
                GenerateArena();
            }
        }

        public void GenerateArena(int? overrideSeed = null)
        {
            if (_config == null)
            {
                Debug.LogError("ArenaConfig is missing on ArenaGenerator!");
                return;
            }

            int seedToUse = 0;
            if (overrideSeed.HasValue && overrideSeed.Value != 0)
            {
                seedToUse = overrideSeed.Value;
            }
            else if (_config.Seed != 0)
            {
                seedToUse = _config.Seed;
            }
            else
            {
                seedToUse = new System.Random().Next(1, int.MaxValue);
            }

            CurrentSeed = seedToUse;
            _rng = new System.Random(CurrentSeed);

            ClearArena();

            GenerateFloor();
            GenerateBoundaries();
            GenerateObstacles();
            GenerateDecorations();
            GenerateInteractables();
        }

        public void ClearArena()
        {
            _spawnedBarrels.Clear();
            ActiveShrine = null;
            _interactablesContainer = null;

            if (_decorationRenderer != null)
            {
                _decorationRenderer.Clear();
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            if (_fallbackFloorMaterial != null)
            {
                Destroy(_fallbackFloorMaterial);
                _fallbackFloorMaterial = null;
            }
            if (_fallbackWallMaterial != null)
            {
                Destroy(_fallbackWallMaterial);
                _fallbackWallMaterial = null;
            }
        }

        private int Range(int minInclusive, int maxExclusive)
        {
            return _rng.Next(minInclusive, maxExclusive);
        }

        private float Range(float minInclusive, float maxInclusive)
        {
            return minInclusive + (float)_rng.NextDouble() * (maxInclusive - minInclusive);
        }

        private void GenerateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Arena_Floor";
            floor.transform.SetParent(transform);
            floor.transform.position = Vector3.zero;

            // A standard Unity Plane is 10x10 units. We scale it accordingly.
            float scale = _config.ArenaSize / 10f;
            floor.transform.localScale = new Vector3(scale, 1f, scale);

            var renderer = floor.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (_floorMaterial != null)
                {
                    renderer.sharedMaterial = _floorMaterial;
                }
                else
                {
                    if (_fallbackFloorMaterial == null)
                    {
                        _fallbackFloorMaterial = CreateFallbackMaterial(renderer.sharedMaterial, new Color(0.2f, 0.5f, 0.2f));
                    }
                    if (_fallbackFloorMaterial != null)
                    {
                        renderer.sharedMaterial = _fallbackFloorMaterial;
                    }
                }
            }
        }

        private void GenerateBoundaries()
        {
            float halfSize = _config.ArenaSize / 2f;
            float wallThickness = 2f;
            float wallHeight = 10f;

            // Top, Bottom, Left, Right
            CreateWall("Wall_Top", new Vector3(0, wallHeight / 2, halfSize + wallThickness / 2), new Vector3(_config.ArenaSize + wallThickness * 2, wallHeight, wallThickness));
            CreateWall("Wall_Bottom", new Vector3(0, wallHeight / 2, -halfSize - wallThickness / 2), new Vector3(_config.ArenaSize + wallThickness * 2, wallHeight, wallThickness));
            CreateWall("Wall_Left", new Vector3(-halfSize - wallThickness / 2, wallHeight / 2, 0), new Vector3(wallThickness, wallHeight, _config.ArenaSize));
            CreateWall("Wall_Right", new Vector3(halfSize + wallThickness / 2, wallHeight / 2, 0), new Vector3(wallThickness, wallHeight, _config.ArenaSize));
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer != -1) wall.layer = obstacleLayer;

            if (!_visibleWalls)
            {
                var renderer = wall.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
            else
            {
                var renderer = wall.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    if (_wallMaterial != null)
                    {
                        renderer.sharedMaterial = _wallMaterial;
                    }
                    else
                    {
                        if (_fallbackWallMaterial == null)
                        {
                            _fallbackWallMaterial = CreateFallbackMaterial(renderer.sharedMaterial, Color.gray);
                        }
                        if (_fallbackWallMaterial != null)
                        {
                            renderer.sharedMaterial = _fallbackWallMaterial;
                        }
                    }
                }
            }
        }

        private static Material CreateFallbackMaterial(Material existingDefault, Color color)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = urpLit != null
                ? new Material(urpLit)
                : (existingDefault != null ? new Material(existingDefault) : null);

            if (mat != null)
            {
                mat.color = color;
                mat.enableInstancing = true;
            }

            return mat;
        }

        private void GenerateObstacles()
        {
            if (_config.ObstaclePrefabs == null || _config.ObstaclePrefabs.Length == 0)
            {
                return;
            }

            int count = Range(_config.MinObstacles, _config.MaxObstacles + 1);
            float spawnHalfSize = Mathf.Max(0f, _config.ArenaSize / 2f - 2f);
            float safeRadiusSq = _config.SafeZoneRadius * _config.SafeZoneRadius;
            
            // To prevent infinite loops in edge cases
            int maxAttempts = count * 10; 
            int attempts = 0;
            int spawned = 0;
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");

            GameObject obstaclesContainer = new GameObject("Obstacles");
            obstaclesContainer.transform.SetParent(transform);

            while (spawned < count && attempts < maxAttempts)
            {
                attempts++;
                
                float x = Range(-spawnHalfSize, spawnHalfSize);
                float z = Range(-spawnHalfSize, spawnHalfSize);
                
                // Avoid safe zone
                if (x * x + z * z < safeRadiusSq)
                    continue;

                Vector3 position = new Vector3(x, 0f, z);
                
                // Optional: Check if we are overlapping existing obstacles using physics (Physics.CheckSphere).
                // Skipping for MVP to keep it simple, or we can add it if requested.

                GameObject prefab = _config.ObstaclePrefabs[Range(0, _config.ObstaclePrefabs.Length)];
                GameObject obstacle = Instantiate(prefab, position, Quaternion.Euler(0, Range(0f, 360f), 0), obstaclesContainer.transform);
                
                if (obstacleLayer != -1)
                {
                    obstacle.layer = obstacleLayer;
                    foreach (Transform child in obstacle.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = obstacleLayer;
                    }
                }
                
                spawned++;
            }
        }
        private void GenerateDecorations()
        {
            if (_config.DecorationPrefabs == null || _config.DecorationPrefabs.Length == 0)
            {
                return;
            }

            int count = Range(_config.MinDecorations, _config.MaxDecorations + 1);
            if (count <= 0)
            {
                return;
            }

            if (_decorationRenderer == null)
            {
                _decorationRenderer = GetComponent<ArenaDecorationRenderer>();
                if (_decorationRenderer == null)
                {
                    _decorationRenderer = gameObject.AddComponent<ArenaDecorationRenderer>();
                }
            }

            _decorationRenderer.Build(_config.DecorationPrefabs, _config.ArenaSize, count, _rng);
        }

        private void GenerateInteractables()
        {
            float halfArena = _config.ArenaSize * 0.5f;
            float maxBound = Mathf.Max(8f, halfArena - 4f);
            int obstacleMask = LayerMask.GetMask("Obstacles");

            var interactablesGo = new GameObject("Interactables");
            interactablesGo.transform.SetParent(transform, false);
            _interactablesContainer = interactablesGo.transform;

            if (_config.ExplosiveBarrelPrefab != null && _config.MaxExplosiveBarrels > 0)
            {
                int minBarrels = Mathf.Max(0, _config.MinExplosiveBarrels);
                int maxBarrels = Mathf.Max(minBarrels, _config.MaxExplosiveBarrels);
                int targetCount = Range(minBarrels, maxBarrels + 1);

                float minR = Mathf.Min(_config.BarrelMinDistanceFromCenter, maxBound * 0.5f);
                float maxR = Mathf.Clamp(_config.BarrelMaxDistanceFromCenter, minR + 1f, maxBound);

                int maxAttempts = targetCount * 20;
                int attempts = 0;
                while (_spawnedBarrels.Count < targetCount && attempts < maxAttempts)
                {
                    attempts++;
                    float angle = Range(0f, Mathf.PI * 2f);
                    float dist = Range(minR, maxR);
                    Vector3 candidate = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    candidate.x = Mathf.Clamp(candidate.x, -maxBound, maxBound);
                    candidate.z = Mathf.Clamp(candidate.z, -maxBound, maxBound);

                    if (obstacleMask != 0 && Physics.CheckSphere(candidate + Vector3.up * 0.7f, 1.3f, obstacleMask))
                    {
                        continue;
                    }

                    bool tooCloseToBarrel = false;
                    for (int i = 0; i < _spawnedBarrels.Count; i++)
                    {
                        if (_spawnedBarrels[i] == null) continue;
                        Vector3 diff = _spawnedBarrels[i].transform.position - candidate;
                        diff.y = 0f;
                        if (diff.sqrMagnitude < 36f)
                        {
                            tooCloseToBarrel = true;
                            break;
                        }
                    }
                    if (tooCloseToBarrel) continue;

                    SpawnBarrelAt(candidate);
                }
            }

            if (_config.ChallengeShrinePrefab != null)
            {
                float minR = Mathf.Min(_config.ShrineMinDistanceFromCenter, maxBound * 0.6f);
                float maxR = Mathf.Clamp(_config.ShrineMaxDistanceFromCenter, minR + 1f, maxBound);

                Vector3 shrinePos = new Vector3(minR, 0f, 0f);
                for (int attempt = 0; attempt < 25; attempt++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    float dist = Range(minR, maxR);
                    Vector3 candidate = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    candidate.x = Mathf.Clamp(candidate.x, -maxBound, maxBound);
                    candidate.z = Mathf.Clamp(candidate.z, -maxBound, maxBound);

                    if (obstacleMask != 0 && Physics.CheckSphere(candidate + Vector3.up * 0.8f, 1.8f, obstacleMask))
                    {
                        continue;
                    }

                    bool overlapsBarrel = false;
                    for (int i = 0; i < _spawnedBarrels.Count; i++)
                    {
                        if (_spawnedBarrels[i] == null) continue;
                        Vector3 diff = _spawnedBarrels[i].transform.position - candidate;
                        diff.y = 0f;
                        if (diff.sqrMagnitude < 25f)
                        {
                            overlapsBarrel = true;
                            break;
                        }
                    }
                    if (!overlapsBarrel)
                    {
                        shrinePos = candidate;
                        break;
                    }
                }

                SpawnShrineAt(shrinePos);
            }
        }

        private ExplosiveBarrel SpawnBarrelAt(Vector3 position)
        {
            if (_config == null || _config.ExplosiveBarrelPrefab == null) return null;
            Transform parent = _interactablesContainer != null ? _interactablesContainer : transform;
            GameObject go = Instantiate(_config.ExplosiveBarrelPrefab, position, Quaternion.Euler(0f, Range(0f, 360f), 0f), parent);
            var barrel = go.GetComponent<ExplosiveBarrel>();
            if (barrel == null) barrel = go.AddComponent<ExplosiveBarrel>();
            barrel.Initialize(_heroHealth, _eventBus);
            _spawnedBarrels.Add(barrel);
            return barrel;
        }

        private ChallengeShrine SpawnShrineAt(Vector3 position)
        {
            if (_config == null || _config.ChallengeShrinePrefab == null) return null;
            ResolveDeferredDependencies();
            Transform parent = _interactablesContainer != null ? _interactablesContainer : transform;
            GameObject go = Instantiate(_config.ChallengeShrinePrefab, position, Quaternion.identity, parent);
            var shrine = go.GetComponent<ChallengeShrine>();
            if (shrine == null) shrine = go.AddComponent<ChallengeShrine>();
            shrine.Initialize(_heroHealth, _heroEconomy, _heroExperience, _waveManager, _gameManager, _eventBus);
            ActiveShrine = shrine;
            return shrine;
        }

        public ExplosiveBarrel SpawnBarrelNearHero(float distance = 4.5f)
        {
            if (_heroHealth == null || _config == null || _config.ExplosiveBarrelPrefab == null) return null;
            Vector3 pos = _heroHealth.transform.position + new Vector3(distance, 0f, 0f);
            pos.y = 0f;
            float maxBound = Mathf.Max(5f, ArenaSize * 0.5f - 3f);
            pos.x = Mathf.Clamp(pos.x, -maxBound, maxBound);
            pos.z = Mathf.Clamp(pos.z, -maxBound, maxBound);
            return SpawnBarrelAt(pos);
        }

        public ChallengeShrine SpawnShrineNearHero(float distance = 5.5f)
        {
            if (_heroHealth == null || _config == null || _config.ChallengeShrinePrefab == null) return null;
            Vector3 pos = _heroHealth.transform.position + new Vector3(-distance, 0f, 0f);
            pos.y = 0f;
            float maxBound = Mathf.Max(5f, ArenaSize * 0.5f - 3f);
            pos.x = Mathf.Clamp(pos.x, -maxBound, maxBound);
            pos.z = Mathf.Clamp(pos.z, -maxBound, maxBound);
            return SpawnShrineAt(pos);
        }
    }
}
