using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace ProjectB.LevelUp
{
    public class CoinManager : MonoBehaviour
    {
        [SerializeField] private GameObject coinPrefab;
        private Transform heroTarget;
        private ProjectB.Player.HeroEconomy heroEconomy;

        private IObjectPool<CoinPickup> coinPool;
        private Material fallbackMaterial;
        private int pickupsLayer;

        [Inject]
        public void Construct(ProjectB.Player.HeroHealth heroHealth, ProjectB.Player.HeroEconomy heroEconomy)
        {
            if (heroHealth != null)
            {
                heroTarget = heroHealth.transform;
            }
            this.heroEconomy = heroEconomy;
        }

        private void Start()
        {
            pickupsLayer = LayerMask.NameToLayer("Pickups");
            Transform poolContainer = new GameObject("CoinContainer").transform;
            
            GameObject tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
            fallbackMaterial.color = Color.yellow;
            fallbackMaterial.enableInstancing = true;
            Destroy(tempPrimitive);

            coinPool = new ObjectPool<CoinPickup>(
                createFunc: () => {
                    GameObject go;
                    if (coinPrefab != null) {
                        go = Instantiate(coinPrefab, poolContainer);
                    } else {
                        // Fallback: yellow sphere
                        go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        go.transform.SetParent(poolContainer);
                        go.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                        
                        go.GetComponent<Renderer>().sharedMaterial = fallbackMaterial;
                        var col = go.GetComponent<Collider>();
                        col.isTrigger = true;
                    }
                    
                    CoinPickup pickup = go.GetComponent<CoinPickup>();
                    if (pickup == null) pickup = go.AddComponent<CoinPickup>();
                    
                    if (pickupsLayer != -1) go.layer = pickupsLayer;
                    
                    return pickup;
                },
                actionOnGet: c => c.gameObject.SetActive(true),
                actionOnRelease: c => c.gameObject.SetActive(false),
                actionOnDestroy: c => Destroy(c.gameObject),
                collectionCheck: false,
                defaultCapacity: 100,
                maxSize: 1000
            );
        }

        public void SpawnCoin(Vector3 position, int amount)
        {
            if (heroTarget == null) return;
            
            CoinPickup pickup = coinPool.Get();
            
            position.y = 0.25f;
            pickup.transform.position = position;
            
            pickup.Initialize(heroTarget, amount, coinPool, heroEconomy);
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
