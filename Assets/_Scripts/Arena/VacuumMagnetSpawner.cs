using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Arena
{
    /// <summary>
    /// Pooled spawner for VacuumMagnetPickup drops from Mini-Bosses and Bosses.
    /// </summary>
    public class VacuumMagnetSpawner : MonoBehaviour
    {
        [SerializeField] private VacuumMagnetPickup vacuumMagnetPrefab;
        [SerializeField] private float pullSpeed = 28f;

        private HeroHealth heroHealth;
        private XpManager xpManager;
        private CoinManager coinManager;
        private GameEventBus eventBus;
        private WaveManager waveManager;

        private IObjectPool<VacuumMagnetPickup> pickupPool;
        private Transform poolContainer;
        private int pickupsLayer;
        private System.Action<VacuumMagnetPickup> cachedPickupCollectedHandler;

        public float PullSpeed => pullSpeed;

        [Inject]
        public void Construct(
            HeroHealth heroHealth,
            XpManager xpManager,
            CoinManager coinManager,
            GameEventBus eventBus = null,
            WaveManager waveManager = null)
        {
            this.heroHealth = heroHealth;
            this.xpManager = xpManager;
            this.coinManager = coinManager;
            this.eventBus = eventBus;
            this.waveManager = waveManager;
        }

        private void EnsurePool()
        {
            if (pickupPool != null) return;

            cachedPickupCollectedHandler = HandlePickupCollected;
            pickupsLayer = LayerMask.NameToLayer("Pickups");
            poolContainer = new GameObject("VacuumMagnetContainer").transform;

            pickupPool = new ObjectPool<VacuumMagnetPickup>(
                createFunc: CreatePickupInstance,
                actionOnGet: p => { if (p != null) p.gameObject.SetActive(true); },
                actionOnRelease: p => { if (p != null) p.gameObject.SetActive(false); },
                actionOnDestroy: p =>
                {
                    if (p != null)
                    {
                        if (Application.isPlaying) Destroy(p.gameObject);
                        else DestroyImmediate(p.gameObject);
                    }
                },
                collectionCheck: false,
                defaultCapacity: 4,
                maxSize: 16);
        }

        public void PrewarmPool(int count = 4)
        {
            EnsurePool();
            if (pickupPool == null || count <= 0) return;

            var temp = new VacuumMagnetPickup[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = pickupPool.Get();
            }
            for (int i = 0; i < count; i++)
            {
                if (temp[i] != null) pickupPool.Release(temp[i]);
            }
        }

        private VacuumMagnetPickup CreatePickupInstance()
        {
            GameObject go;
            if (vacuumMagnetPrefab != null)
            {
                go = Instantiate(vacuumMagnetPrefab.gameObject, poolContainer);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "VacuumMagnetFallback";
                go.transform.SetParent(poolContainer, false);
                go.transform.localScale = Vector3.one * 0.6f;
                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }

            if (pickupsLayer != -1)
            {
                go.layer = pickupsLayer;
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = pickupsLayer;
                }
            }

            var pickup = go.GetComponent<VacuumMagnetPickup>();
            if (pickup == null)
            {
                pickup = go.AddComponent<VacuumMagnetPickup>();
            }
            return pickup;
        }

        public bool TryDropFromEnemyDeath(Vector3 position, bool wasBoss, bool wasMiniBoss, float roll)
        {
            if (!wasBoss && !wasMiniBoss) return false;

            float bossChance = waveManager != null && waveManager.Config != null
                ? waveManager.Config.bossVacuumDropChance
                : 1.0f;
            float miniBossChance = waveManager != null && waveManager.Config != null
                ? waveManager.Config.miniBossVacuumDropChance
                : 0.25f;

            float chance = wasBoss ? bossChance : miniBossChance;
            if (chance <= 0f) return false;
            if (chance < 1f && roll >= chance) return false;

            return SpawnVacuumMagnet(position) != null;
        }

        public VacuumMagnetPickup SpawnVacuumMagnet(Vector3 position)
        {
            EnsurePool();
            VacuumMagnetPickup pickup = pickupPool.Get();
            if (pickup == null) return null;

            position.y = 0f;
            pickup.transform.position = position;
            Transform heroTransform = heroHealth != null ? heroHealth.transform : null;
            pickup.Initialize(heroTransform, cachedPickupCollectedHandler, pickupPool);
            return pickup;
        }

        public VacuumMagnetPickup SpawnNearHero(float distance = 3.5f)
        {
            if (heroHealth == null) return null;
            Vector3 pos = heroHealth.transform.position + Vector3.forward * distance;
            pos.y = 0f;
            return SpawnVacuumMagnet(pos);
        }

        private void HandlePickupCollected(VacuumMagnetPickup pickup)
        {
            int xpCount = xpManager != null ? xpManager.MagnetizeAll(pullSpeed) : 0;
            int coinCount = coinManager != null ? coinManager.MagnetizeAll(pullSpeed) : 0;
            Vector3 pos = pickup != null ? pickup.transform.position : Vector3.zero;
            eventBus?.Publish(new VacuumMagnetCollectedEvent(pos, xpCount, coinCount));
        }

        private void OnDestroy()
        {
            pickupPool?.Clear();
            if (poolContainer != null)
            {
                if (Application.isPlaying) Destroy(poolContainer.gameObject);
                else DestroyImmediate(poolContainer.gameObject);
            }
        }
    }
}
