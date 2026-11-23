using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;

namespace ProjectB.Arena
{
    public enum ShrineState
    {
        Idle = 0,
        Active = 1,
        Completed = 2
    }

    /// <summary>
    /// Challenge Shrine spawned once per arena: stepping inside summons a ring of 6 elite enemies.
    /// Clearing all summoned elites awards +100 coins and a guaranteed Level Up.
    /// </summary>
    public class ChallengeShrine : MonoBehaviour
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        [Header("Challenge Settings")]
        [SerializeField] private float activationRadius = 2.6f;
        [SerializeField] private int eliteCount = 6;
        [SerializeField] private float ringSpawnRadius = 6.5f;
        [SerializeField] private int coinReward = 100;
        [SerializeField] private bool grantLevelUp = true;

        [Header("Visuals")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform runeRingVisual;
        [SerializeField] private GameObject beaconBeam;
        [SerializeField] private Color idleRingColor = new Color(0.68f, 0.25f, 0.96f, 0.68f);
        [SerializeField] private Color activeRingColor = new Color(1f, 0.22f, 0.22f, 0.85f);
        [SerializeField] private Color completedRingColor = new Color(1f, 0.84f, 0.22f, 0.55f);

        private readonly List<EnemyBase> trackedElites = new List<EnemyBase>(8);

        private HeroHealth heroHealth;
        private HeroEconomy heroEconomy;
        private HeroExperience heroExperience;
        private WaveManager waveManager;
        private GameManager gameManager;
        private GameEventBus eventBus;
        private Func<Vector3, EliteArchetype, EnemyBase> customEliteSpawner;

        private MeshRenderer[] runeRingRenderers;
        private MaterialPropertyBlock propBlock;
        private int totalSpawned;
        private float nextValidationTime;

        public ShrineState State { get; private set; } = ShrineState.Idle;
        public int SpawnedEliteCount => totalSpawned;
        public int RemainingEliteCount => trackedElites.Count;
        public int CoinReward => coinReward;

        public void Initialize(
            HeroHealth heroHealth,
            HeroEconomy heroEconomy,
            HeroExperience heroExperience,
            WaveManager waveManager,
            GameManager gameManager = null,
            GameEventBus eventBus = null,
            Func<Vector3, EliteArchetype, EnemyBase> customEliteSpawner = null)
        {
            this.heroHealth = heroHealth;
            this.heroEconomy = heroEconomy;
            this.heroExperience = heroExperience;
            this.waveManager = waveManager;
            this.gameManager = gameManager;
            this.eventBus = eventBus;
            this.customEliteSpawner = customEliteSpawner;

            ClearTrackedElites();
            totalSpawned = 0;
            State = ShrineState.Idle;

            if (beaconBeam != null)
            {
                beaconBeam.SetActive(true);
            }

            ApplyRingColor(idleRingColor);
        }

        private void Awake()
        {
            CacheRingRenderers();
            ApplyRingColor(idleRingColor);
        }

        private void OnDestroy()
        {
            ClearTrackedElites();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;

            if (runeRingVisual != null)
            {
                float spinSpeed = State == ShrineState.Active ? 95f : 28f;
                runeRingVisual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            }

            if (State == ShrineState.Idle)
            {
                if (heroHealth == null || heroHealth.IsDead) return;
                if (gameManager != null && gameManager.CurrentState != GameState.Playing) return;

                Vector3 diff = heroHealth.transform.position - transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude <= activationRadius * activationRadius)
                {
                    ActivateChallenge();
                }
            }
            else if (State == ShrineState.Active && Time.time >= nextValidationTime)
            {
                nextValidationTime = Time.time + 0.25f;
                PruneDeadElites();
            }
        }

        public bool ActivateChallenge()
        {
            if (State != ShrineState.Idle) return false;

            ClearTrackedElites();
            int countToSpawn = Mathf.Max(1, eliteCount);
            Vector3 center = heroHealth != null ? heroHealth.transform.position : transform.position;
            center.y = 0f;
            float angleStep = (Mathf.PI * 2f) / countToSpawn;

            for (int i = 0; i < countToSpawn; i++)
            {
                float angle = angleStep * i;
                Vector3 desiredPos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringSpawnRadius;
                desiredPos.y = 0f;

                EliteArchetype archetype = (i % 3) switch
                {
                    0 => EliteArchetype.Berserker,
                    1 => EliteArchetype.Juggernaut,
                    _ => EliteArchetype.HasteAura
                };

                EnemyBase elite = null;
                if (customEliteSpawner != null)
                {
                    elite = customEliteSpawner.Invoke(desiredPos, archetype);
                }
                else if (waveManager != null)
                {
                    Vector3 safePos = waveManager.GetValidPositionNear(desiredPos, 0f, 1.5f);
                    elite = waveManager.SpawnChallengeElite(safePos, archetype);
                }

                if (elite != null && !elite.IsDead)
                {
                    elite.OnDied -= HandleEliteDied;
                    elite.OnDied += HandleEliteDied;
                    trackedElites.Add(elite);
                }
            }

            if (trackedElites.Count == 0)
            {
                return false;
            }

            totalSpawned = trackedElites.Count;
            State = ShrineState.Active;
            nextValidationTime = Time.time + 0.25f;
            ApplyRingColor(activeRingColor);

            eventBus?.Publish(new ChallengeShrineEvent(
                ShrineEventType.Started,
                transform.position,
                trackedElites.Count,
                totalSpawned,
                0));

            return true;
        }

        private void HandleEliteDied(EnemyBase enemy)
        {
            if (enemy != null)
            {
                enemy.OnDied -= HandleEliteDied;
                trackedElites.Remove(enemy);
            }

            if (State != ShrineState.Active) return;

            if (trackedElites.Count <= 0)
            {
                CompleteChallenge();
            }
            else
            {
                eventBus?.Publish(new ChallengeShrineEvent(
                    ShrineEventType.Progress,
                    transform.position,
                    trackedElites.Count,
                    totalSpawned,
                    0));
            }
        }

        private void PruneDeadElites()
        {
            if (State != ShrineState.Active) return;

            bool removedAny = false;
            for (int i = trackedElites.Count - 1; i >= 0; i--)
            {
                var elite = trackedElites[i];
                if (elite == null || elite.IsDead || !elite.gameObject.activeInHierarchy)
                {
                    if (elite != null) elite.OnDied -= HandleEliteDied;
                    trackedElites.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (!removedAny) return;

            if (trackedElites.Count <= 0)
            {
                CompleteChallenge();
            }
            else
            {
                eventBus?.Publish(new ChallengeShrineEvent(
                    ShrineEventType.Progress,
                    transform.position,
                    trackedElites.Count,
                    totalSpawned,
                    0));
            }
        }

        private void CompleteChallenge()
        {
            if (State == ShrineState.Completed) return;
            State = ShrineState.Completed;

            ApplyRingColor(completedRingColor);
            if (beaconBeam != null)
            {
                beaconBeam.SetActive(false);
            }

            int finalCoinReward = EffectiveCoinReward;
            if (finalCoinReward > 0)
            {
                heroEconomy?.AddCoin(finalCoinReward);
            }

            if (grantLevelUp)
            {
                heroExperience?.ForceLevelUp();
            }

            eventBus?.Publish(new ChallengeShrineEvent(
                ShrineEventType.Completed,
                transform.position,
                0,
                totalSpawned,
                finalCoinReward));
        }

        private float rewardMultiplier = 1f;

        /// <summary>Constellation "Secret &amp; Shrine" bonus (e.g. 1.3 = +30% coins).</summary>
        public void SetRewardMultiplier(float multiplier)
        {
            rewardMultiplier = Mathf.Max(0f, multiplier);
        }

        public int EffectiveCoinReward => coinReward > 0 ? Mathf.Max(1, Mathf.RoundToInt(coinReward * rewardMultiplier)) : 0;

        private void ClearTrackedElites()
        {
            for (int i = 0; i < trackedElites.Count; i++)
            {
                if (trackedElites[i] != null)
                {
                    trackedElites[i].OnDied -= HandleEliteDied;
                }
            }
            trackedElites.Clear();
        }

        private void CacheRingRenderers()
        {
            if (runeRingVisual != null && runeRingRenderers == null)
            {
                runeRingRenderers = runeRingVisual.GetComponentsInChildren<MeshRenderer>(true);
            }
        }

        private void ApplyRingColor(Color color)
        {
            CacheRingRenderers();
            if (runeRingRenderers == null || runeRingRenderers.Length == 0) return;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            propBlock.SetColor(BaseColorPropertyId, color);
            for (int i = 0; i < runeRingRenderers.Length; i++)
            {
                if (runeRingRenderers[i] != null)
                {
                    runeRingRenderers[i].SetPropertyBlock(propBlock);
                }
            }
        }
    }
}
