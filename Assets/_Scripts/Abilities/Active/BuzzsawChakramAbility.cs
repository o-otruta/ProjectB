using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    /// <summary>
    /// Buzzsaw Chakram (Rare): fires ricocheting voxel discs that bounce between enemies.
    /// Synergy "Ricochet Barrage" (+ Critical Lens): every critical hit of the main disc spawns
    /// a shrapnel mini-disc (2 ricochets) and speeds up the main disc.
    /// </summary>
    public class BuzzsawChakramAbility : ActiveAbility
    {
        private const float MAX_DISC_SPEED = 32f;

        private BuzzsawChakramAbilityData ChakramData => Data as BuzzsawChakramAbilityData;

        private readonly Collider[] hitBuffer = new Collider[96];
        private readonly List<Transform> targetBuffer = new List<Transform>(8);
        private IObjectPool<ChakramDisc> discPool;
        private Transform discContainer;

        private float currentDamage;
        private float currentCooldown;
        private int currentDiscCount;
        private int currentRicochets;
        private float currentSpeed;
        private float currentRicochetRange;
        private float nextFireTime;

        public int CurrentRicochets => currentRicochets;
        public int CurrentDiscCount => currentDiscCount;
        public float HitRadius => ChakramData != null ? ChakramData.hitRadius : 0.55f;
        public int MiniDiscsSpawned { get; private set; }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            var d = ChakramData;
            if (d != null)
            {
                currentDamage = d.baseDamage;
                currentCooldown = d.baseCooldown;
                currentDiscCount = d.discCount;
                currentRicochets = d.ricochets;
                currentSpeed = d.discSpeed;
                currentRicochetRange = d.ricochetRange;
            }

            discContainer = new GameObject($"Discs_{(d != null ? d.id : "Chakram")}").transform;
            discPool = new ObjectPool<ChakramDisc>(
                CreateDisc,
                disc => { if (disc != null) disc.gameObject.SetActive(true); },
                disc => { if (disc != null) disc.gameObject.SetActive(false); },
                disc =>
                {
                    if (disc == null) return;
                    if (Application.isPlaying) Destroy(disc.gameObject);
                    else DestroyImmediate(disc.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 6,
                maxSize: 32);

            if (Application.isPlaying) PrewarmPools(4);
        }

        private ChakramDisc CreateDisc()
        {
            GameObject go;
            var d = ChakramData;
            if (d != null && d.discPrefab != null)
            {
                go = Instantiate(d.discPrefab, discContainer);
            }
            else
            {
                go = new GameObject("ChakramDisc");
                go.transform.SetParent(discContainer, false);
            }
            go.name = "ChakramDisc";
            var disc = go.GetComponent<ChakramDisc>();
            if (disc == null) disc = go.AddComponent<ChakramDisc>();
            return disc;
        }

        public override void PrewarmPools(int count = 0)
        {
            if (discPool == null) return;
            int n = count > 0 ? count : 4;
            var temp = new ChakramDisc[n];
            for (int i = 0; i < n; i++) temp[i] = discPool.Get();
            for (int i = 0; i < n; i++) if (temp[i] != null) discPool.Release(temp[i]);
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDamage += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(0.35f, currentCooldown - value); break;
                case ModifierType.Count: currentDiscCount += Mathf.Max(1, Mathf.RoundToInt(value)); break;
                case ModifierType.PierceCount: currentRicochets += Mathf.Max(1, Mathf.RoundToInt(value)); break;
                case ModifierType.Speed: currentSpeed += value; break;
                case ModifierType.Radius:
                case ModifierType.Area: currentRicochetRange += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentSpeed *= 1.2f;
                currentRicochetRange *= 1.2f;
            }
            else if (tier == 2)
            {
                currentDiscCount += 1;
                currentRicochets += 1;
            }
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;
            if (Time.time >= nextFireTime)
            {
                if (Fire() > 0)
                {
                    nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.35f);
                }
                else
                {
                    nextFireTime = Time.time + 0.2f;
                }
            }
        }

        /// <summary>Launches a volley of discs; returns the number launched.</summary>
        public int Fire()
        {
            var d = ChakramData;
            if (d == null || discPool == null) return 0;

            int found = FindClosestTargets(d.searchRadius, d.targetLayer, currentDiscCount);
            if (found == 0) return 0;

            float damage = GetScaledDamage(currentDamage);
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            for (int i = 0; i < currentDiscCount; i++)
            {
                ChakramDisc disc = discPool.Get();
                if (disc == null) continue;
                disc.Launch(this, discPool, origin, targetBuffer[i % found], damage, currentSpeed, currentRicochets, isMini: false, VisualScaleMultiplier);
            }

            eventBus?.Publish(new AbilitySfxEvent(ProjectB.Data.SfxType.ArrowWhoosh, 0.4f));
            return currentDiscCount;
        }

        /// <summary>Called by a disc when it strikes an enemy. Returns true if the hit was critical.</summary>
        public bool HandleDiscHit(ChakramDisc disc, IDamageable victim, Vector3 hitPosition)
        {
            if (victim == null || victim.IsDead) return false;

            int dmg;
            bool isCrit = false;
            if (runtimeStats != null)
            {
                dmg = runtimeStats.RollDamage(disc.Damage, out isCrit);
            }
            else
            {
                dmg = Mathf.Max(1, Mathf.RoundToInt(disc.Damage));
            }

            string source = disc.IsMini ? SynergyIds.RicochetBarrage : null;
            DealDamage(victim, dmg, isCrit, source, isAoE: false, sourcePosition: hitPosition - disc.transform.forward);
            eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.ChakramRicochet, hitPosition, 0.6f));

            if (isCrit && !disc.IsMini && HasSynergy(SynergyIds.RicochetBarrage))
            {
                TriggerRicochetBarrage(disc, hitPosition);
            }

            return isCrit;
        }

        /// <summary>Ricochet Barrage reaction: spawns a mini-disc and accelerates the main disc.</summary>
        public ChakramDisc TriggerRicochetBarrage(ChakramDisc mainDisc, Vector3 hitPosition)
        {
            var d = ChakramData;
            if (d == null || mainDisc == null || discPool == null) return null;

            mainDisc.BoostSpeed(d.critSpeedBoost, MAX_DISC_SPEED);

            Transform next = FindRicochetTarget(hitPosition, mainDisc);
            if (next == null) return null;

            ChakramDisc mini = discPool.Get();
            if (mini == null) return null;

            mini.Launch(this, discPool, hitPosition, next, mainDisc.Damage * d.miniDiscDamageFactor,
                mainDisc.Speed * 1.1f, d.miniDiscRicochets, isMini: true, VisualScaleMultiplier * d.miniDiscScale);
            MiniDiscsSpawned++;
            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.RicochetBarrage, hitPosition, 1.2f));
            return mini;
        }

        /// <summary>Closest living enemy within ricochet range that this disc has not hit yet.</summary>
        public Transform FindRicochetTarget(Vector3 from, ChakramDisc disc)
        {
            var d = ChakramData;
            if (d == null) return null;

            int found = Physics.OverlapSphereNonAlloc(from, ScaleArea(currentRicochetRange), hitBuffer, d.targetLayer);
            Transform best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<IDamageable>(out var dmg) || dmg.IsDead) continue;
                if (!CombatTargetFilter.IsValidAutoAimTarget(col, dmg, d.targetLayer)) continue;
                if (disc != null && disc.HasHit(col.transform)) continue;

                float sqr = (col.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = col.transform;
                }
            }
            return best;
        }

        private int FindClosestTargets(float radius, LayerMask layer, int max)
        {
            targetBuffer.Clear();
            int found = Physics.OverlapSphereNonAlloc(transform.position, radius, hitBuffer, layer);
            for (int pick = 0; pick < max; pick++)
            {
                Transform best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < found; i++)
                {
                    Collider col = hitBuffer[i];
                    if (col == null) continue;
                    if (col.TryGetComponent<IDamageable>(out var dmg) && !CombatTargetFilter.IsValidAutoAimTarget(col, dmg, layer)) continue;
                    if (targetBuffer.Contains(col.transform)) continue;
                    float sqr = (col.transform.position - transform.position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = col.transform;
                    }
                }
                if (best == null) break;
                targetBuffer.Add(best);
            }
            return targetBuffer.Count;
        }

        private void OnDestroy()
        {
            discPool?.Clear();
            if (discContainer != null)
            {
                if (Application.isPlaying) Destroy(discContainer.gameObject);
                else DestroyImmediate(discContainer.gameObject);
            }
        }
    }
}
