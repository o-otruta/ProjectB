using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Arena
{
    /// <summary>
    /// Spawns up to 1 secret item per run (Treasure Chest, Ancient Scroll, or Mystery Crystal)
    /// far from the arena center to reward exploration.
    /// </summary>
    public class SecretItemSpawner : MonoBehaviour
    {
        [Header("Secret Prefabs")]
        [SerializeField] private List<InteractableItem> secretItemPrefabs = new List<InteractableItem>();

        [Header("Spawn Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float spawnChancePerRun = 1f;
        [SerializeField] private float minDistanceFromCenter = 28f;
        [SerializeField] private float maxDistanceFromCenter = 60f;
        [SerializeField] private float arenaPadding = 6f;

        [Header("Reward Tuning")]
        [SerializeField] private int chestCoinReward = 100;
        [SerializeField] private int crystalFallbackCoins = 150;

        private ArenaGenerator arenaGenerator;
        private HeroHealth heroHealth;
        private HeroEconomy heroEconomy;
        private HeroExperience heroExperience;
        private UpgradeManager upgradeManager;
        private AchievementManager achievementManager;
        private GameEventBus eventBus;
        private HeroRuntimeStats runtimeStats;

        private bool hasSpawnedThisRun;
        private int obstacleMask;
        private InteractableItem activeItem;
        private int debugSpawnCycleIndex;

        public InteractableItem ActiveItem => activeItem;

        [Inject]
        public void Construct(
            ArenaGenerator arenaGenerator,
            HeroHealth heroHealth,
            HeroEconomy heroEconomy,
            HeroExperience heroExperience,
            UpgradeManager upgradeManager,
            AchievementManager achievementManager = null,
            GameEventBus eventBus = null,
            HeroRuntimeStats runtimeStats = null)
        {
            this.runtimeStats = runtimeStats;
            this.arenaGenerator = arenaGenerator;
            this.heroHealth = heroHealth;
            this.heroEconomy = heroEconomy;
            this.heroExperience = heroExperience;
            this.upgradeManager = upgradeManager;
            this.achievementManager = achievementManager;
            this.eventBus = eventBus;
        }

        private void Start()
        {
            obstacleMask = LayerMask.GetMask("Obstacles");
            TrySpawnSecretForRun();
        }

        public void TrySpawnSecretForRun()
        {
            if (hasSpawnedThisRun) return;
            if (secretItemPrefabs == null || secretItemPrefabs.Count == 0) return;
            if (Random.value > spawnChancePerRun) return;

            var prefab = secretItemPrefabs[Random.Range(0, secretItemPrefabs.Count)];
            if (prefab == null) return;

            Vector3 spawnPos = FindFarSpawnPosition();
            SpawnSpecificItem(prefab, spawnPos);
            hasSpawnedThisRun = true;
        }

        public InteractableItem SpawnRandomNearHero(float distance = 4.5f)
        {
            if (secretItemPrefabs == null || secretItemPrefabs.Count == 0 || heroHealth == null) return null;

            int index = debugSpawnCycleIndex % secretItemPrefabs.Count;
            float angleRad = (debugSpawnCycleIndex * 120f + 30f) * Mathf.Deg2Rad;
            debugSpawnCycleIndex++;

            var prefab = secretItemPrefabs[index];
            if (prefab == null) return null;

            Vector3 pos = GetClampedPositionNear(heroHealth.transform.position, angleRad, distance);
            return SpawnSpecificItem(prefab, pos);
        }

        public void SpawnAllSecretsNearHero(float distance = 5f)
        {
            if (secretItemPrefabs == null || secretItemPrefabs.Count == 0 || heroHealth == null) return;

            int count = secretItemPrefabs.Count;
            float angleStep = (Mathf.PI * 2f) / count;
            Vector3 center = heroHealth.transform.position;

            for (int i = 0; i < count; i++)
            {
                var prefab = secretItemPrefabs[i];
                if (prefab == null) continue;

                float angle = angleStep * i + Mathf.PI * 0.25f;
                Vector3 pos = GetClampedPositionNear(center, angle, distance);
                SpawnSpecificItem(prefab, pos);
            }
        }

        private Vector3 GetClampedPositionNear(Vector3 center, float angleRad, float distance)
        {
            float halfArena = arenaGenerator != null ? arenaGenerator.ArenaSize * 0.5f : 100f;
            float maxAllowed = Mathf.Max(5f, halfArena - arenaPadding);

            Vector3 pos = center + new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * distance;
            pos.y = 0f;
            pos.x = Mathf.Clamp(pos.x, -maxAllowed, maxAllowed);
            pos.z = Mathf.Clamp(pos.z, -maxAllowed, maxAllowed);
            return pos;
        }

        private InteractableItem SpawnSpecificItem(InteractableItem prefab, Vector3 position)
        {
            var instance = Instantiate(prefab, position, Quaternion.identity, transform);
            Transform heroTransform = heroHealth != null ? heroHealth.transform : null;
            instance.Initialize(heroTransform, HandleItemCollected);
            activeItem = instance;
            DevLog.Log($"[SecretItemSpawner] Spawned secret item '{instance.DisplayName}' at {position}");
            return instance;
        }

        private Vector3 FindFarSpawnPosition()
        {
            float halfArena = arenaGenerator != null ? arenaGenerator.ArenaSize * 0.5f : 100f;
            float maxAllowedRadius = Mathf.Max(10f, halfArena - arenaPadding);
            float minDist = Mathf.Min(minDistanceFromCenter, maxAllowedRadius * 0.6f);
            float maxDist = Mathf.Min(maxDistanceFromCenter, maxAllowedRadius);

            for (int attempt = 0; attempt < 20; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(minDist, maxDist);
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                candidate.x = Mathf.Clamp(candidate.x, -maxAllowedRadius, maxAllowedRadius);
                candidate.z = Mathf.Clamp(candidate.z, -maxAllowedRadius, maxAllowedRadius);

                if (obstacleMask == 0 || !Physics.CheckSphere(candidate + Vector3.up * 0.75f, 1.2f, obstacleMask))
                {
                    return candidate;
                }
            }

            return new Vector3(minDist, 0f, 0f);
        }

        private void HandleItemCollected(InteractableItem item)
        {
            if (item == null) return;
            if (activeItem == item) activeItem = null;

            string rewardSummary = ApplySecretReward(item.ItemType);
            achievementManager?.OnSecretItemFound(item.ItemId);
            eventBus?.Publish(new SecretItemCollectedEvent(item.ItemId, item.DisplayName, rewardSummary));
            DevLog.Log($"[SecretItemSpawner] Collected '{item.DisplayName}': {rewardSummary}");
        }

        public string ApplySecretReward(SecretItemType type)
        {
            switch (type)
            {
                case SecretItemType.TreasureChest:
                    int chestCoins = runtimeStats != null ? runtimeStats.ApplySecretRewardMultiplier(chestCoinReward) : chestCoinReward;
                    heroEconomy?.AddCoin(chestCoins);
                    return $"+{chestCoins} Coins";

                case SecretItemType.AncientScroll:
                    heroExperience?.ForceLevelUp();
                    return "Free Level Up!";

                case SecretItemType.MysteryCrystal:
                    AbilityData granted = upgradeManager != null ? upgradeManager.GrantRandomRareAbility() : null;
                    if (granted != null)
                    {
                        return $"Unlocked: {granted.abilityName}";
                    }
                    int crystalCoins = runtimeStats != null ? runtimeStats.ApplySecretRewardMultiplier(crystalFallbackCoins) : crystalFallbackCoins;
                    heroEconomy?.AddCoin(crystalCoins);
                    return $"+{crystalCoins} Coins";

                default:
                    return string.Empty;
            }
        }
    }
}
