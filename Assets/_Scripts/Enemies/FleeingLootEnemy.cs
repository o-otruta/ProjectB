using System;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.UI;

namespace ProjectB.Enemies
{
    /// <summary>
    /// Gold Goblin treasure runner: flees away from the hero, never deals contact damage,
    /// highlights via the golden Multi-POI Compass, spills coins when hit, and explodes into
    /// 40-80 coins + a Vacuum Magnet pickup on death (unless it escapes when the timer ends).
    /// </summary>
    public class FleeingLootEnemy : EnemyBase
    {
        private static readonly Color GoldRimColor = new Color(1.00f, 0.84f, 0.16f, 1.0f);
        private static readonly Color GoldBaseTint = new Color(1.00f, 0.96f, 0.80f, 1.0f);

        private FleeingLootEnemyData lootData;
        private Action<Vector3, int> coinSpawnCallback;
        private Action<Vector3> vacuumSpawnCallback;
        private float arenaMaxCoord = 240f;
        private float nextHitDropTime = float.NegativeInfinity;
        private float orbitDirectionSign = 1f;

        public override bool CanBeLeashed => false;
        public int HitCoinsDropped { get; private set; }
        public int LastDeathCoinsDropped { get; private set; }

        public event Action<FleeingLootEnemy> OnEscaped;

        protected override Color GetCustomBaseTint() => GoldBaseTint;
        protected override Color GetCustomRimColor() => GoldRimColor;

        public void ConfigureLootCallbacks(
            Action<Vector3, int> onSpawnCoin,
            Action<Vector3> onSpawnVacuum,
            float maxArenaCoord)
        {
            coinSpawnCallback = onSpawnCoin;
            vacuumSpawnCallback = onSpawnVacuum;
            if (maxArenaCoord > 5f)
            {
                arenaMaxCoord = maxArenaCoord;
            }
        }

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);

            lootData = data as FleeingLootEnemyData;
            OnEscaped = null;
            HitCoinsDropped = 0;
            LastDeathCoinsDropped = 0;
            nextHitDropTime = float.NegativeInfinity;
            orbitDirectionSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;

            hasCustomRim = true;
            ApplyVisualState(0f);

            float revealDist = lootData != null ? lootData.compassRevealDistance : 80f;
            SecretCompassIndicatorUI.RegisterDynamicPoi(transform, CompassPoiType.GoldGoblin, revealDist);
        }

        protected override void UpdateMovement()
        {
            if (target == null) return;

            Vector3 away = transform.position - target.position;
            away.y = 0f;
            float dist = away.magnitude;
            Vector3 fleeDir = dist > 0.001f ? away / dist : transform.forward;

            float orbitDist = lootData != null ? Mathf.Max(8f, lootData.fleeOrbitDistance) : 20f;
            Vector3 tangent = new Vector3(-fleeDir.z, 0f, fleeDir.x) * orbitDirectionSign;
            Vector3 desiredDir = dist > orbitDist
                ? (fleeDir * 0.35f + tangent * 0.65f).normalized
                : (fleeDir * 0.85f + tangent * 0.15f).normalized;

            if (ObstacleMask != 0 && Physics.SphereCast(
                transform.position + Vector3.up * 0.5f,
                0.45f,
                desiredDir,
                out RaycastHit hit,
                1.0f,
                ObstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 slide = Vector3.ProjectOnPlane(desiredDir, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude < 0.01f)
                {
                    orbitDirectionSign = -orbitDirectionSign;
                    slide = Vector3.Cross(hit.normal, Vector3.up) * orbitDirectionSign;
                }
                desiredDir = slide.normalized;
            }

            Vector3 nextPos = transform.position + desiredDir * (EffectiveMoveSpeed * Time.deltaTime);
            float bound = Mathf.Max(8f, arenaMaxCoord);
            if (Mathf.Abs(nextPos.x) >= bound || Mathf.Abs(nextPos.z) >= bound)
            {
                nextPos.x = Mathf.Clamp(nextPos.x, -bound, bound);
                nextPos.z = Mathf.Clamp(nextPos.z, -bound, bound);
                orbitDirectionSign = -orbitDirectionSign;
            }
            nextPos.y = 0f;

            transform.position = nextPos;
            if (desiredDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(desiredDir);
            }
        }

        protected override void UpdateAttack()
        {
            // Gold Goblin never deals contact damage; it only flees.
        }

        protected override void OnDamageApplied(int finalAmount, bool isCrit, bool wasBlocked)
        {
            base.OnDamageApplied(finalAmount, isCrit, wasBlocked);
            if (finalAmount <= 0 || currentHp <= 0 || lootData == null || lootData.coinsPerHit <= 0) return;

            if (Time.time >= nextHitDropTime)
            {
                nextHitDropTime = Time.time + Mathf.Max(0.05f, lootData.hitDropCooldown);
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.65f;
                Vector3 dropPos = transform.position + new Vector3(jitter.x, 0f, jitter.y);
                coinSpawnCallback?.Invoke(dropPos, lootData.coinsPerHit);
                HitCoinsDropped += lootData.coinsPerHit;
            }
        }

        protected override void Die()
        {
            if (!isAlive) return;

            SecretCompassIndicatorUI.UnregisterDynamicPoi(transform);

            int minCoins = lootData != null ? Mathf.Max(1, lootData.deathCoinMin) : 40;
            int maxCoins = lootData != null ? Mathf.Max(minCoins, lootData.deathCoinMax) : 80;
            int totalCoins = UnityEngine.Random.Range(minCoins, maxCoins + 1);
            LastDeathCoinsDropped = totalCoins;

            int burstCount = lootData != null ? Mathf.Max(1, lootData.deathCoinBurstCount) : 12;
            if (coinSpawnCallback != null)
            {
                int perPickup = Mathf.Max(1, totalCoins / burstCount);
                int remainder = totalCoins - perPickup * burstCount;
                for (int i = 0; i < burstCount; i++)
                {
                    int amount = perPickup + (i < remainder ? 1 : 0);
                    Vector2 scatter = UnityEngine.Random.insideUnitCircle * 1.8f;
                    coinSpawnCallback.Invoke(transform.position + new Vector3(scatter.x, 0f, scatter.y), amount);
                }
            }

            vacuumSpawnCallback?.Invoke(transform.position);
            base.Die();
        }

        public void EscapeAndDespawn()
        {
            if (!isAlive) return;
            isAlive = false;
            SecretCompassIndicatorUI.UnregisterDynamicPoi(transform);
            OnEscaped?.Invoke(this);
            ReturnToPool();
        }

        public override void Despawn()
        {
            SecretCompassIndicatorUI.UnregisterDynamicPoi(transform);
            base.Despawn();
        }

        private void OnDisable()
        {
            SecretCompassIndicatorUI.UnregisterDynamicPoi(transform);
        }

        private void OnDestroy()
        {
            SecretCompassIndicatorUI.UnregisterDynamicPoi(transform);
        }
    }
}
