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
        private RunStatistics _runStatistics;
        private HeroRuntimeStats _runtimeStats;
        private WaveManager _waveManager;
        private GameManager _gameManager;

        private readonly List<ExplosiveBarrel> _spawnedBarrels = new List<ExplosiveBarrel>();
        private readonly List<GameObject> _spawnedObstacles = new List<GameObject>();
        private Transform _interactablesContainer;
        private bool _isSubscribed;

        public int CurrentSeed { get; private set; }
        public ArenaConfig Config => _config;
        public float ArenaSize => _config != null ? _config.ArenaSize : 100f;
        public IReadOnlyList<ExplosiveBarrel> SpawnedBarrels => _spawnedBarrels;
        public IReadOnlyList<GameObject> SpawnedObstacles => _spawnedObstacles;
        public ChallengeShrine ActiveShrine { get; private set; }
        public ArenaDecorationRenderer DecorationRenderer => _decorationRenderer;

        [Inject]
        public void Construct(
            IObjectResolver resolver = null,
            GameEventBus eventBus = null,
            HeroHealth heroHealth = null,
            HeroEconomy heroEconomy = null,
            HeroExperience heroExperience = null,
            RunStatistics runStatistics = null,
            HeroRuntimeStats runtimeStats = null)
        {
            _runtimeStats = runtimeStats;
            _resolver = resolver;
            _eventBus = eventBus;
            _heroHealth = heroHealth;
            _heroEconomy = heroEconomy;
            _heroExperience = heroExperience;
            _runStatistics = runStatistics;

            SubscribeToEvents();
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
            SubscribeToEvents();
            if (_generateOnStart)
            {
                GenerateArena();
            }
        }

        private void SubscribeToEvents()
        {
            if (_isSubscribed || _eventBus == null) return;
            _eventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            _isSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!_isSubscribed || _eventBus == null) return;
            _eventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            _isSubscribed = false;
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            if (_config == null || _config.DynamicRespawnWaveInterval <= 0) return;
            if (evt.CompletedWave > 0 && evt.CompletedWave % _config.DynamicRespawnWaveInterval == 0)
            {
                RespawnInteractablesNearHero(null, out _);
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
            _spawnedObstacles.Clear();
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
            UnsubscribeFromEvents();
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
            float padding = Mathf.Max(0f, _config.ObstacleClearancePadding);

            int maxAttempts = count * 15;
            int attempts = 0;
            int spawned = 0;
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            int obstacleMask = LayerMask.GetMask("Obstacles");

            float[] prefabClearanceRadii = new float[_config.ObstaclePrefabs.Length];
            for (int i = 0; i < _config.ObstaclePrefabs.Length; i++)
            {
                prefabClearanceRadii[i] = ComputeObstacleClearanceRadius(_config.ObstaclePrefabs[i]) + padding;
            }

            var placedPositions = new List<Vector3>(count);
            var placedRadii = new List<float>(count);

            GameObject obstaclesContainer = new GameObject("Obstacles");
            obstaclesContainer.transform.SetParent(transform);

            Physics.SyncTransforms();

            while (spawned < count && attempts < maxAttempts)
            {
                attempts++;

                float x = Range(-spawnHalfSize, spawnHalfSize);
                float z = Range(-spawnHalfSize, spawnHalfSize);

                // Avoid safe zone
                if (x * x + z * z < safeRadiusSq)
                    continue;

                Vector3 position = new Vector3(x, 0f, z);
                int prefabIdx = Range(0, _config.ObstaclePrefabs.Length);
                GameObject prefab = _config.ObstaclePrefabs[prefabIdx];
                if (prefab == null) continue;

                float clearanceRadius = prefabClearanceRadii[prefabIdx];

                // Check against already-placed obstacles in this pass
                bool overlapsPlaced = false;
                for (int i = 0; i < placedPositions.Count; i++)
                {
                    Vector3 existing = placedPositions[i];
                    float minSep = clearanceRadius + placedRadii[i];
                    float dx = existing.x - position.x;
                    float dz = existing.z - position.z;
                    if (dx * dx + dz * dz < minSep * minSep)
                    {
                        overlapsPlaced = true;
                        break;
                    }
                }
                if (overlapsPlaced) continue;

                // Also verify against existing physics colliders on the Obstacles layer
                if (obstacleMask != 0 && Physics.CheckSphere(position + Vector3.up * 0.75f, clearanceRadius, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                GameObject obstacle = Instantiate(prefab, position, Quaternion.Euler(0, Range(0f, 360f), 0), obstaclesContainer.transform);

                if (obstacleLayer != -1)
                {
                    obstacle.layer = obstacleLayer;
                    foreach (Transform child in obstacle.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = obstacleLayer;
                    }
                }

                _spawnedObstacles.Add(obstacle);
                placedPositions.Add(position);
                placedRadii.Add(clearanceRadius);
                spawned++;
            }

            Physics.SyncTransforms();
        }

        public static float ComputeObstacleClearanceRadius(GameObject prefab)
        {
            if (prefab == null) return 1.2f;

            Vector3 lossy = prefab.transform.localScale;
            float maxScaleXZ = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z), 0.1f);

            var cap = prefab.GetComponentInChildren<CapsuleCollider>(true);
            if (cap != null)
            {
                return Mathf.Max(0.45f, cap.radius * maxScaleXZ);
            }

            var sph = prefab.GetComponentInChildren<SphereCollider>(true);
            if (sph != null)
            {
                return Mathf.Max(0.45f, sph.radius * Mathf.Max(maxScaleXZ, Mathf.Abs(lossy.y)));
            }

            var box = prefab.GetComponentInChildren<BoxCollider>(true);
            if (box != null)
            {
                float halfW = Mathf.Max(Mathf.Abs(box.size.x * lossy.x), Mathf.Abs(box.size.z * lossy.z)) * 0.5f;
                return Mathf.Max(0.45f, halfW);
            }

            return 1.2f;
        }

        private void GenerateDecorations()
        {
            int rawPrimaryCount = (_config.DecorationPrefabs != null && _config.DecorationPrefabs.Length > 0)
                ? Range(_config.MinDecorations, _config.MaxDecorations + 1)
                : 0;
            int primaryCount = rawPrimaryCount > 0
                ? Mathf.Max(0, Mathf.RoundToInt(rawPrimaryCount * _config.ResolveGrassDensityMultiplier()))
                : 0;
            int propCount = (_config.PropDecorationPrefabs != null && _config.PropDecorationPrefabs.Length > 0)
                ? Range(Mathf.Max(0, _config.MinPropDecorations), Mathf.Max(_config.MinPropDecorations, _config.MaxPropDecorations) + 1)
                : 0;

            if (primaryCount <= 0 && propCount <= 0)
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

            _decorationRenderer.BuildWithProps(
                _config.DecorationPrefabs,
                _config.ArenaSize,
                primaryCount,
                _config.PropDecorationPrefabs,
                _config.PropDecorationWeights,
                propCount,
                _rng);
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
            barrel.Initialize(_heroHealth, _eventBus, _runStatistics);
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
            shrine.SetRewardMultiplier(_runtimeStats != null ? _runtimeStats.SecretRewardMultiplier : 1f);
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

        public int RespawnInteractablesNearHero(Vector3? centerOverride, out ChallengeShrine respawnedShrine)
        {
            respawnedShrine = null;
            if (_config == null) return 0;

            if (_rng == null)
            {
                int seed = CurrentSeed != 0 ? CurrentSeed : (_config.Seed != 0 ? _config.Seed : 1337);
                _rng = new System.Random(seed);
            }

            if (_interactablesContainer == null)
            {
                var interactablesGo = new GameObject("Interactables");
                interactablesGo.transform.SetParent(transform, false);
                _interactablesContainer = interactablesGo.transform;
            }

            Vector3 center = centerOverride ?? (_heroHealth != null ? _heroHealth.transform.position : Vector3.zero);
            center.y = 0f;

            float maxBound = Mathf.Max(8f, ArenaSize * 0.5f - 4f);
            float minR = Mathf.Min(Mathf.Max(5f, _config.DynamicRespawnMinRadius), Mathf.Max(5f, maxBound - 2f));
            float maxR = Mathf.Clamp(Mathf.Max(minR + 2f, _config.DynamicRespawnMaxRadius), minR + 1f, maxBound);
            int obstacleMask = LayerMask.GetMask("Obstacles");

            int barrelsSpawned = 0;
            if (_config.ExplosiveBarrelPrefab != null && _config.DynamicRespawnMaxBarrels > 0)
            {
                int minB = Mathf.Max(0, _config.DynamicRespawnMinBarrels);
                int maxB = Mathf.Max(minB, _config.DynamicRespawnMaxBarrels);
                int targetBarrels = Range(minB, maxB + 1) + (_runtimeStats != null ? _runtimeStats.BarrelRespawnBonus : 0);

                for (int b = 0; b < targetBarrels; b++)
                {
                    if (!TryFindClearPointInRing(center, minR, maxR, maxBound, obstacleMask, 1.3f, 6f, out Vector3 candidate))
                    {
                        break;
                    }

                    ExplosiveBarrel recycled = FindDetonatedBarrel();
                    if (recycled != null)
                    {
                        recycled.transform.SetPositionAndRotation(candidate, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                        recycled.Initialize(_heroHealth, _eventBus, _runStatistics);
                        barrelsSpawned++;
                    }
                    else if (SpawnBarrelAt(candidate) != null)
                    {
                        barrelsSpawned++;
                    }
                }
            }

            if (_config.ChallengeShrinePrefab != null && (ActiveShrine == null || ActiveShrine.State != ShrineState.Active))
            {
                if (TryFindClearPointInRing(center, minR, maxR, maxBound, obstacleMask, 1.8f, 5f, out Vector3 shrinePos))
                {
                    if (ActiveShrine != null && ActiveShrine.State == ShrineState.Idle)
                    {
                        GameObject oldGo = ActiveShrine.gameObject;
                        ActiveShrine = null;
                        if (Application.isPlaying) Destroy(oldGo);
                        else DestroyImmediate(oldGo);
                    }

                    respawnedShrine = SpawnShrineAt(shrinePos);
                }
            }

            return barrelsSpawned;
        }

        private ExplosiveBarrel FindDetonatedBarrel()
        {
            for (int i = 0; i < _spawnedBarrels.Count; i++)
            {
                ExplosiveBarrel b = _spawnedBarrels[i];
                if (b != null && (b.IsDead || !b.gameObject.activeSelf))
                {
                    return b;
                }
            }
            return null;
        }

        private bool TryFindClearPointInRing(
            Vector3 center,
            float minR,
            float maxR,
            float maxBound,
            int obstacleMask,
            float obstacleClearance,
            float barrelMinSpacing,
            out Vector3 result)
        {
            float barrelSpacingSq = barrelMinSpacing * barrelMinSpacing;
            for (int attempt = 0; attempt < 25; attempt++)
            {
                float angle = Range(0f, Mathf.PI * 2f);
                float dist = Range(minR, maxR);
                Vector3 candidate = new Vector3(
                    Mathf.Clamp(center.x + Mathf.Cos(angle) * dist, -maxBound, maxBound),
                    0f,
                    Mathf.Clamp(center.z + Mathf.Sin(angle) * dist, -maxBound, maxBound));

                if (obstacleMask != 0 && Physics.CheckSphere(candidate + Vector3.up * 0.75f, obstacleClearance, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                bool tooClose = false;
                for (int i = 0; i < _spawnedBarrels.Count; i++)
                {
                    ExplosiveBarrel b = _spawnedBarrels[i];
                    if (b == null || b.IsDead || !b.gameObject.activeSelf) continue;
                    float dx = b.transform.position.x - candidate.x;
                    float dz = b.transform.position.z - candidate.z;
                    if (dx * dx + dz * dz < barrelSpacingSq)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                result = candidate;
                return true;
            }

            result = new Vector3(
                Mathf.Clamp(center.x + minR, -maxBound, maxBound),
                0f,
                Mathf.Clamp(center.z, -maxBound, maxBound));
            return true;
        }
    }
}
