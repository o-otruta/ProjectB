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
        private Material fallbackMaterial;
        private int pickupsLayer;

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
            pickupsLayer = LayerMask.NameToLayer("Pickups");
            Transform poolContainer = new GameObject("XpCrystalContainer").transform;
            
            // Safely get the default material by creating a temporary primitive, to avoid Shader.Find crashing in URP/mobile
            GameObject tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
            fallbackMaterial.color = Color.green;
            fallbackMaterial.enableInstancing = true;
            Destroy(tempPrimitive);

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
                actionOnGet: c => c.gameObject.SetActive(true),
                actionOnRelease: c => c.gameObject.SetActive(false),
                actionOnDestroy: c => Destroy(c.gameObject),
                collectionCheck: false,
                defaultCapacity: 100,
                maxSize: 1000
            );
        }

        public void SpawnXp(Vector3 position, int amount)
        {
            if (heroTarget == null) return;
            
            XpCrystal crystal = xpPool.Get();
            
            // Немного приподнимаем кристалл над землей
            position.y = 0.25f;
            crystal.transform.position = position;
            
            crystal.Initialize(heroTarget, amount, xpPool, heroExperience);
        }

        private void OnDestroy()
        {
            if (fallbackMaterial != null)
            {
                Destroy(fallbackMaterial);
                fallbackMaterial = null;
            }
        }
    }
}
