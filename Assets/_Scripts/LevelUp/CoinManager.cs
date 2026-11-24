using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using ProjectB.Data;

namespace ProjectB.LevelUp
{
    public class CoinManager : MonoBehaviour
    {
        [SerializeField] private GameObject coinPrefab;
        private PickupConfig pickupConfig;
        private Transform heroTarget;
        private ProjectB.Player.HeroEconomy heroEconomy;

        private IObjectPool<CoinPickup> coinPool;
        private Transform poolContainer;
        private Material fallbackMaterial;
        private int pickupsLayer;

        private readonly HashSet<CoinPickup> activeCoins = new HashSet<CoinPickup>(256);
        private readonly List<CoinPickup> magnetizeBuffer = new List<CoinPickup>(256);

        public int ActiveCount => activeCoins.Count;
        public int MaxActiveCoins => pickupConfig != null && pickupConfig.maxActiveCoins > 0
            ? pickupConfig.maxActiveCoins
            : PickupConfig.DEFAULT_MAX_ACTIVE_COINS;
        public IReadOnlyCollection<CoinPickup> ActiveCoins => activeCoins;

        public void SetConfig(PickupConfig config)
        {
            pickupConfig = config;
        }

        [Inject]
        public void Construct(ProjectB.Player.HeroHealth heroHealth, ProjectB.Player.HeroEconomy heroEconomy, PickupConfig pickupConfig = null)
        {
            if (heroHealth != null)
            {
                heroTarget = heroHealth.transform;
            }
            this.heroEconomy = heroEconomy;
            if (pickupConfig != null) this.pickupConfig = pickupConfig;
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (coinPool != null) return;

            pickupsLayer = LayerMask.NameToLayer("Pickups");
            poolContainer = new GameObject("CoinContainer").transform;
            
            GameObject tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackMaterial = new Material(tempPrimitive.GetComponent<Renderer>().sharedMaterial);
            fallbackMaterial.color = Color.yellow;
            fallbackMaterial.enableInstancing = true;
            if (Application.isPlaying) Destroy(tempPrimitive);
            else DestroyImmediate(tempPrimitive);

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
                actionOnGet: c =>
                {
                    if (c != null)
                    {
                        c.gameObject.SetActive(true);
                        activeCoins.Add(c);
                    }
                },
                actionOnRelease: c =>
                {
                    if (c != null)
                    {
                        activeCoins.Remove(c);
                        c.gameObject.SetActive(false);
                    }
                },
                actionOnDestroy: c =>
                {
                    if (c != null)
                    {
                        activeCoins.Remove(c);
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
            if (coinPool == null || count <= 0) return;

            magnetizeBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                var c = coinPool.Get();
                if (c != null) magnetizeBuffer.Add(c);
            }
            for (int i = 0; i < magnetizeBuffer.Count; i++)
            {
                coinPool.Release(magnetizeBuffer[i]);
            }
            magnetizeBuffer.Clear();
        }

        public void SpawnCoin(Vector3 position, int amount)
        {
            if (heroTarget == null || coinPool == null) return;

            position.y = 0.25f;

            if (activeCoins.Count >= MaxActiveCoins && TryMergeIntoNearestCoin(position, amount))
            {
                return;
            }
            
            CoinPickup pickup = coinPool.Get();
            if (pickup == null) return;
            
            pickup.transform.position = position;
            
            pickup.Initialize(heroTarget, amount, coinPool, heroEconomy);
            activeCoins.Add(pickup);
        }

        private bool TryMergeIntoNearestCoin(Vector3 position, int amount)
        {
            CoinPickup nearestGrounded = null;
            float nearestGroundedDistSq = float.MaxValue;
            CoinPickup nearestAny = null;
            float nearestAnyDistSq = float.MaxValue;

            foreach (CoinPickup coin in activeCoins)
            {
                if (coin == null || !coin.gameObject.activeInHierarchy) continue;

                Vector3 diff = coin.transform.position - position;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;

                if (!coin.IsMagnetized && distSq < nearestGroundedDistSq)
                {
                    nearestGroundedDistSq = distSq;
                    nearestGrounded = coin;
                }

                if (distSq < nearestAnyDistSq)
                {
                    nearestAnyDistSq = distSq;
                    nearestAny = coin;
                }
            }

            CoinPickup target = nearestGrounded != null ? nearestGrounded : nearestAny;
            if (target == null) return false;

            if (amount > 0)
            {
                float scaleMult = pickupConfig != null
                    ? pickupConfig.EvaluateMergedScaleMultiplier(target.MergeCount + 1)
                    : PickupConfig.EvaluateDefaultMergedScaleMultiplier(target.MergeCount + 1);
                target.AddMergedAmount(amount, scaleMult);
            }
            return true;
        }

        public int MagnetizeAll(float speed = 28f)
        {
            if (activeCoins.Count == 0) return 0;

            magnetizeBuffer.Clear();
            foreach (var coin in activeCoins)
            {
                magnetizeBuffer.Add(coin);
            }

            int magnetized = 0;
            for (int i = 0; i < magnetizeBuffer.Count; i++)
            {
                var coin = magnetizeBuffer[i];
                if (coin == null || !coin.gameObject.activeInHierarchy)
                {
                    activeCoins.Remove(coin);
                    continue;
                }

                coin.Magnetize(speed);
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
