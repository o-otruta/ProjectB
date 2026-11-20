using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectB.Arena
{
    /// <summary>
    /// Renders large counts of static arena decorations (e.g., voxel grass) via chunk-culled GPU Instancing
    /// without instantiating thousands of GameObjects. Zero GC allocations in Update.
    /// </summary>
    public class ArenaDecorationRenderer : MonoBehaviour
    {
        private const int MAX_INSTANCES_PER_DRAW = 1023;

        [Header("Rendering Settings")]
        [SerializeField] private float _chunkSize = 25f;
        [SerializeField] private bool _castShadows = false;
        [SerializeField] private bool _receiveShadows = true;

        private struct MeshPart
        {
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 LocalToPrefab;
            public int SubMeshIndex;
            public Bounds LocalBounds;
        }

        private class PrefabRenderData
        {
            public MeshPart[] Parts;
            public Vector3 RootScale;
        }

        private class ChunkBatch
        {
            public Mesh Mesh;
            public int SubMeshIndex;
            public RenderParams RenderParams;
            public Matrix4x4[] Matrices;
        }

        private class Chunk
        {
            public Bounds Bounds;
            public ChunkBatch[] Batches;
        }

        private readonly List<Chunk> _chunks = new List<Chunk>();
        private readonly List<Material> _ownedMaterials = new List<Material>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private Camera _cachedCamera;

        public int TotalInstanceCount { get; private set; }
        public int ChunkCount => _chunks.Count;

        public void Build(
            GameObject[] decorationPrefabs,
            float arenaSize,
            int count,
            System.Random rng,
            float minScale = 0.8f,
            float maxScale = 1.2f)
        {
            Clear();

            if (decorationPrefabs == null || decorationPrefabs.Length == 0 || count <= 0 || arenaSize <= 0f)
            {
                return;
            }

            // 1. Extract meshes & instancing-enabled materials for each prefab
            var materialCache = new Dictionary<Material, Material>();
            var prefabDataList = new List<PrefabRenderData>(decorationPrefabs.Length);

            for (int i = 0; i < decorationPrefabs.Length; i++)
            {
                GameObject prefab = decorationPrefabs[i];
                if (prefab == null) continue;

                PrefabRenderData data = ExtractPrefabData(prefab, materialCache);
                if (data != null && data.Parts.Length > 0)
                {
                    prefabDataList.Add(data);
                }
            }

            if (prefabDataList.Count == 0)
            {
                Debug.LogWarning("[ArenaDecorationRenderer] No renderable MeshRenderers found in DecorationPrefabs.");
                return;
            }

            // 2. Partition arena into 2D grid of chunks
            float chunkSize = Mathf.Max(5f, _chunkSize);
            float halfSize = arenaSize * 0.5f;
            int gridDim = Mathf.Max(1, Mathf.CeilToInt(arenaSize / chunkSize));
            int totalCells = gridDim * gridDim;
            int prefabCount = prefabDataList.Count;

            // Temporary lists per (chunkIndex, prefabIndex, partIndex)
            int totalPartSlots = 0;
            int[] prefabPartOffsets = new int[prefabCount];
            var allParts = new List<MeshPart>();
            for (int p = 0; p < prefabCount; p++)
            {
                prefabPartOffsets[p] = totalPartSlots;
                totalPartSlots += prefabDataList[p].Parts.Length;
                allParts.AddRange(prefabDataList[p].Parts);
            }

            var cellPartMatrices = new List<Matrix4x4>[totalCells][];
            var cellBoundsMin = new Vector3[totalCells];
            var cellBoundsMax = new Vector3[totalCells];
            var cellHasData = new bool[totalCells];

            for (int i = 0; i < count; i++)
            {
                float x = -halfSize + (float)rng.NextDouble() * arenaSize;
                float z = -halfSize + (float)rng.NextDouble() * arenaSize;
                int prefabIdx = rng.Next(0, prefabCount);
                float yRot = (float)rng.NextDouble() * 360f;
                float randomScale = minScale + (float)rng.NextDouble() * (maxScale - minScale);

                int cx = Mathf.Clamp(Mathf.FloorToInt((x + halfSize) / chunkSize), 0, gridDim - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((z + halfSize) / chunkSize), 0, gridDim - 1);
                int cellIdx = cz * gridDim + cx;

                PrefabRenderData pData = prefabDataList[prefabIdx];
                Vector3 pos = new Vector3(x, 0f, z);
                Quaternion rot = Quaternion.Euler(0f, yRot, 0f);
                Vector3 scale = pData.RootScale * randomScale;
                Matrix4x4 instanceMatrix = Matrix4x4.TRS(pos, rot, scale);

                var slotArrays = cellPartMatrices[cellIdx];
                if (slotArrays == null)
                {
                    slotArrays = new List<Matrix4x4>[totalPartSlots];
                    cellPartMatrices[cellIdx] = slotArrays;
                }

                int partOffset = prefabPartOffsets[prefabIdx];
                for (int partIdx = 0; partIdx < pData.Parts.Length; partIdx++)
                {
                    int globalSlot = partOffset + partIdx;
                    var list = slotArrays[globalSlot];
                    if (list == null)
                    {
                        list = new List<Matrix4x4>(32);
                        slotArrays[globalSlot] = list;
                    }

                    Matrix4x4 finalMatrix = instanceMatrix * pData.Parts[partIdx].LocalToPrefab;
                    list.Add(finalMatrix);
                }

                // Expand chunk AABB conservatively to cover voxel height/extents
                const float padXZ = 1.5f;
                const float padY = 2.5f;
                Vector3 minPt = new Vector3(x - padXZ, -0.5f, z - padXZ);
                Vector3 maxPt = new Vector3(x + padXZ, padY, z + padXZ);

                if (!cellHasData[cellIdx])
                {
                    cellHasData[cellIdx] = true;
                    cellBoundsMin[cellIdx] = minPt;
                    cellBoundsMax[cellIdx] = maxPt;
                }
                else
                {
                    cellBoundsMin[cellIdx] = Vector3.Min(cellBoundsMin[cellIdx], minPt);
                    cellBoundsMax[cellIdx] = Vector3.Max(cellBoundsMax[cellIdx], maxPt);
                }
            }

            // 3. Finalize compact chunk arrays for zero-GC rendering
            int layer = gameObject.layer;
            ShadowCastingMode shadowMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            for (int cellIdx = 0; cellIdx < totalCells; cellIdx++)
            {
                if (!cellHasData[cellIdx]) continue;

                var slotArrays = cellPartMatrices[cellIdx];
                var chunkBatches = new List<ChunkBatch>(totalPartSlots);

                Bounds bounds = new Bounds();
                bounds.SetMinMax(cellBoundsMin[cellIdx], cellBoundsMax[cellIdx]);

                for (int slot = 0; slot < totalPartSlots; slot++)
                {
                    var list = slotArrays[slot];
                    if (list == null || list.Count == 0) continue;

                    MeshPart part = allParts[slot];
                    var renderParams = new RenderParams(part.Material)
                    {
                        layer = layer,
                        shadowCastingMode = shadowMode,
                        receiveShadows = _receiveShadows,
                        worldBounds = bounds
                    };

                    chunkBatches.Add(new ChunkBatch
                    {
                        Mesh = part.Mesh,
                        SubMeshIndex = part.SubMeshIndex,
                        RenderParams = renderParams,
                        Matrices = list.ToArray()
                    });
                }

                if (chunkBatches.Count > 0)
                {
                    _chunks.Add(new Chunk
                    {
                        Bounds = bounds,
                        Batches = chunkBatches.ToArray()
                    });
                }
            }

            TotalInstanceCount = count;
        }

        private PrefabRenderData ExtractPrefabData(GameObject prefab, Dictionary<Material, Material> materialCache)
        {
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(false);
            if (renderers == null || renderers.Length == 0) return null;

            Matrix4x4 rootInv = prefab.transform.worldToLocalMatrix;
            var parts = new List<MeshPart>(renderers.Length);

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                Mesh mesh = mf.sharedMesh;
                Material[] sharedMats = mr.sharedMaterials;
                int subMeshCount = Mathf.Min(mesh.subMeshCount, sharedMats.Length);
                Matrix4x4 localToPrefab = rootInv * mr.transform.localToWorldMatrix;

                for (int sub = 0; sub < subMeshCount; sub++)
                {
                    Material srcMat = sharedMats[sub];
                    if (srcMat == null) continue;

                    Material instancedMat = GetOrCreateInstancedMaterial(srcMat, materialCache);
                    parts.Add(new MeshPart
                    {
                        Mesh = mesh,
                        Material = instancedMat,
                        LocalToPrefab = localToPrefab,
                        SubMeshIndex = sub,
                        LocalBounds = mesh.bounds
                    });
                }
            }

            return new PrefabRenderData
            {
                Parts = parts.ToArray(),
                RootScale = prefab.transform.localScale
            };
        }

        private Material GetOrCreateInstancedMaterial(Material source, Dictionary<Material, Material> cache)
        {
            if (source.enableInstancing)
            {
                return source;
            }

            if (cache.TryGetValue(source, out Material cached))
            {
                return cached;
            }

            Material clone = new Material(source)
            {
                name = $"{source.name}_Instanced",
                enableInstancing = true
            };
            cache[source] = clone;
            _ownedMaterials.Add(clone);
            return clone;
        }

        private void Update()
        {
            if (_chunks.Count == 0) return;

            if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
            {
                _cachedCamera = Camera.main;
                if (_cachedCamera == null) return;
            }

            GeometryUtility.CalculateFrustumPlanes(_cachedCamera, _frustumPlanes);

            for (int c = 0; c < _chunks.Count; c++)
            {
                Chunk chunk = _chunks[c];
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, chunk.Bounds))
                {
                    continue;
                }

                ChunkBatch[] batches = chunk.Batches;
                for (int b = 0; b < batches.Length; b++)
                {
                    ChunkBatch batch = batches[b];
                    Matrix4x4[] matrices = batch.Matrices;
                    int total = matrices.Length;
                    int offset = 0;

                    while (offset < total)
                    {
                        int drawCount = Mathf.Min(MAX_INSTANCES_PER_DRAW, total - offset);
                        Graphics.RenderMeshInstanced(
                            batch.RenderParams,
                            batch.Mesh,
                            batch.SubMeshIndex,
                            matrices,
                            drawCount,
                            offset);
                        offset += drawCount;
                    }
                }
            }
        }

        public void Clear()
        {
            _chunks.Clear();
            TotalInstanceCount = 0;

            for (int i = 0; i < _ownedMaterials.Count; i++)
            {
                if (_ownedMaterials[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_ownedMaterials[i]);
                    }
                    else
                    {
                        DestroyImmediate(_ownedMaterials[i]);
                    }
                }
            }
            _ownedMaterials.Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
