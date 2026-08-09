using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.LevelUp
{
    public class XpManager : MonoBehaviour
    {
        public static XpManager Instance { get; private set; }

        [SerializeField] private GameObject xpCrystalPrefab;
        [SerializeField] private Transform heroTarget;

        private IObjectPool<XpCrystal> xpPool;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (heroTarget == null)
            {
                // Попробуем найти героя по тегу, если не задан
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    heroTarget = player.transform;
                }
            }

            Transform poolContainer = new GameObject("XpCrystalContainer").transform;
            Material fallbackMaterial = new Material(Shader.Find("Standard"));
            fallbackMaterial.color = Color.green;

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
            position.y = 0.5f;
            crystal.transform.position = position;
            
            crystal.Initialize(heroTarget, amount, xpPool);
        }
    }
}
