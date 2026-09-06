using UnityEngine;
using ProjectB.Data;

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
        public int CurrentSeed { get; private set; }
        public ArenaConfig Config => _config;
        public float ArenaSize => _config != null ? _config.ArenaSize : 100f;

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
        }

        public void ClearArena()
        {
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

            if (_floorMaterial != null)
            {
                var renderer = floor.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.material = _floorMaterial;
            }
            else
            {
                var renderer = floor.GetComponent<MeshRenderer>();
                if (renderer != null) 
                {
                    // Явно создаем URP материал, так как стандартный (от CreatePrimitive) будет фиолетовым
                    Material urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    urpMaterial.color = new Color(0.2f, 0.5f, 0.2f);
                    renderer.material = urpMaterial;
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
                        renderer.material = _wallMaterial;
                    }
                    else
                    {
                        // Фолбэк для стен, чтобы они не были фиолетовыми в URP
                        Material urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        urpMaterial.color = Color.gray;
                        renderer.material = urpMaterial;
                    }
                }
            }
        }

        private void GenerateObstacles()
        {
            if (_config.ObstaclePrefabs == null || _config.ObstaclePrefabs.Length == 0)
            {
                return;
            }

            int count = Range(_config.MinObstacles, _config.MaxObstacles + 1);
            float halfSize = _config.ArenaSize / 2f;
            float safeRadiusSq = _config.SafeZoneRadius * _config.SafeZoneRadius;
            
            // To prevent infinite loops in edge cases
            int maxAttempts = count * 10; 
            int attempts = 0;
            int spawned = 0;

            GameObject obstaclesContainer = new GameObject("Obstacles");
            obstaclesContainer.transform.SetParent(transform);

            while (spawned < count && attempts < maxAttempts)
            {
                attempts++;
                
                float x = Range(-halfSize, halfSize);
                float z = Range(-halfSize, halfSize);
                
                // Avoid safe zone
                if (x * x + z * z < safeRadiusSq)
                    continue;

                Vector3 position = new Vector3(x, 0f, z);
                
                // Optional: Check if we are overlapping existing obstacles using physics (Physics.CheckSphere).
                // Skipping for MVP to keep it simple, or we can add it if requested.

                GameObject prefab = _config.ObstaclePrefabs[Range(0, _config.ObstaclePrefabs.Length)];
                GameObject obstacle = Instantiate(prefab, position, Quaternion.Euler(0, Range(0f, 360f), 0), obstaclesContainer.transform);
                
                int obstacleLayer = LayerMask.NameToLayer("Obstacles");
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
            float halfSize = _config.ArenaSize / 2f;
            float safeRadiusSq = _config.SafeZoneRadius * _config.SafeZoneRadius;

            GameObject decorationsContainer = new GameObject("Decorations");
            decorationsContainer.transform.SetParent(transform);

            for (int i = 0; i < count; i++)
            {
                float x = Range(-halfSize, halfSize);
                float z = Range(-halfSize, halfSize);
                
                // Allow decorations inside the safe zone or not? 
                // Grass looks good anywhere, so we don't avoid the safe zone strictly,
                // but if we want to, we can uncomment the next lines:
                // if (x * x + z * z < safeRadiusSq) continue;

                Vector3 position = new Vector3(x, 0f, z);
                GameObject prefab = _config.DecorationPrefabs[Range(0, _config.DecorationPrefabs.Length)];
                
                // Random scale and rotation for variety
                Quaternion rotation = Quaternion.Euler(0, Range(0f, 360f), 0);
                GameObject decoration = Instantiate(prefab, position, rotation, decorationsContainer.transform);
                
                // Optionally random scale
                float randomScale = Range(0.8f, 1.2f);
                decoration.transform.localScale = new Vector3(randomScale, randomScale, randomScale);

                // Note: We deliberately do NOT set the "Obstacles" layer here.
            }
        }
    }
}
