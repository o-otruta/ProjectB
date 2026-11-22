using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace ProjectB.LevelUp
{
    public class XpManager : MonoBehaviour
    {
        [SerializeField] private GameObject xpCrystalPrefab;
        private Transform heroTarget;
        private HeroExperience heroExperience;

        private IObjectPool<XpCrystal> xpPool;
        private Transform poolContainer;
        private Material fallbackMaterial;
        private int pickupsLayer;

        private readonly HashSet<XpCrystal> activeCrystals = new HashSet<XpCrystal>(256);
        private readonly List<XpCrystal> magnetizeBuffer = new List<XpCrystal>(256);

        public int ActiveCount => activeCrystals.Count;
        public IReadOnlyCollection<XpCrystal> ActiveCrystals => activeCrystals;

        [Inject]
        public void Construct(ProjectB.Player.HeroHealth heroHealth, HeroExperience heroExperience)
        {
            if (heroHealth != null)
            {
                heroTarget = heroHealth.transform;
            }
            this.heroExperience = heroExperience;
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (xpPool != null) return;

            pickupsLayer = LayerMask.NameToLayer("Pickups");
            poolContainer = new GameObject("XpCrystalContainer").transform;
            
            // Safely get the default material by creating a temporary primitive, to avoid Shader.Find crashing in URP/mobile
            GameObject tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
            fallbackMaterial.color = Color.green;
            fallbackMaterial.enableInstancing = true;
            if (Application.isPlaying) Destroy(tempPrimitive);
            else DestroyImmediate(tempPrimitive);

            xpPool = new ObjectPool<XpCrystal>(
                createFunc: () => {
                    GameObject go;
                    if (xpCrystalPrefab != null) {
                        go = Instantiate(xpCrystalPrefab, poolContainer);
                    } else {
                        // Fallback: green diamond (cube rotated)
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.transform.SetParent(poolContainer);
                        go.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                        
                        // Поворот чтобы выглядело как ромб
                        go.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
                        
                        go.GetComponent<Renderer>().sharedMaterial = fallbackMaterial;
                        var col = go.GetComponent<Collider>();
                        col.isTrigger = true;
                    }
                    
                    XpCrystal crystal = go.GetComponent<XpCrystal>();
                    if (crystal == null) crystal = go.AddComponent<XpCrystal>();
                    
                    // Назначаем правильный слой для оптимизации матрицы коллизий
                    if (pickupsLayer != -1) go.layer = pickupsLayer;
                    
                    return crystal;
                },
                actionOnGet: c =>
                {
                    if (c != null)
                    {
                        c.gameObject.SetActive(true);
                        activeCrystals.Add(c);
                    }
                },
                actionOnRelease: c =>
                {
                    if (c != null)
                    {
                        activeCrystals.Remove(c);
                        c.gameObject.SetActive(false);
                    }
                },
                actionOnDestroy: c =>
                {
                    if (c != null)
                    {
                        activeCrystals.Remove(c);
                        if (Application.isPlaying) Destroy(c.gameObject);
                        else DestroyImmediate(c.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 100,
                maxSize: 1000
            );
        }

        public void PrewarmPool(int count = 64)
        {
            EnsureInitialized();
            if (xpPool == null || count <= 0) return;

            magnetizeBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                var c = xpPool.Get();
                if (c != null) magnetizeBuffer.Add(c);
            }
            for (int i = 0; i < magnetizeBuffer.Count; i++)
            {
                xpPool.Release(magnetizeBuffer[i]);
            }
            magnetizeBuffer.Clear();
        }

        public void SpawnXp(Vector3 position, int amount)
        {
            if (heroTarget == null || xpPool == null) return;
            
            XpCrystal crystal = xpPool.Get();
            if (crystal == null) return;
            
            // Немного приподнимаем кристалл над землей
            position.y = 0.25f;
            crystal.transform.position = position;
            
            crystal.Initialize(heroTarget, amount, xpPool, heroExperience);
            activeCrystals.Add(crystal);
        }

        public int MagnetizeAll(float speed = 28f)
        {
            if (activeCrystals.Count == 0) return 0;

            magnetizeBuffer.Clear();
            foreach (var crystal in activeCrystals)
            {
                magnetizeBuffer.Add(crystal);
            }

            int magnetized = 0;
            for (int i = 0; i < magnetizeBuffer.Count; i++)
            {
                var crystal = magnetizeBuffer[i];
                if (crystal == null || !crystal.gameObject.activeInHierarchy)
                {
                    activeCrystals.Remove(crystal);
                    continue;
                }

                crystal.Magnetize(speed);
                magnetized++;
            }

            magnetizeBuffer.Clear();
            return magnetized;
        }

        private void OnDestroy()
        {
            if (poolContainer != null)
            {
                if (Application.isPlaying) Destroy(poolContainer.gameObject);
                else DestroyImmediate(poolContainer.gameObject);
                poolContainer = null;
            }
            if (fallbackMaterial != null)
            {
                if (Application.isPlaying) Destroy(fallbackMaterial);
                else DestroyImmediate(fallbackMaterial);
                fallbackMaterial = null;
            }
        }
    }
}
