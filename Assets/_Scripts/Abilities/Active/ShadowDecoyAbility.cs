using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    /// <summary>
    /// Shadow Decoy (Epic): deploys a holographic decoy that taunts nearby enemies and explodes after 3.5s.
    /// Synergy "Phantom Legion" (+ Mini Dragon): the detonation releases a ring of 8 dragon fireballs.
    /// </summary>
    public class ShadowDecoyAbility : ActiveAbility
    {
        public const int MAX_DECOYS = 4;

        private ShadowDecoyAbilityData DecoyData => Data as ShadowDecoyAbilityData;

        private readonly Collider[] hitBuffer = new Collider[128];
        private readonly ShadowDecoyEntity[] decoys = new ShadowDecoyEntity[MAX_DECOYS];
        private Transform decoyRoot;
        private IObjectPool<GravityShardProjectile> fireballPool;

        private float currentDamage;
        private float currentCooldown;
        private int currentDecoyCount;
        private float currentLifetime;
        private float currentExplosionRadius;
        private float nextFireTime;

        public float TauntPulseInterval => DecoyData != null ? DecoyData.tauntPulseInterval : 0.4f;
        public int PhantomVolleys { get; private set; }
        public int LastTauntedCount { get; private set; }

        public int ActiveDecoyCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < decoys.Length; i++) if (decoys[i] != null && decoys[i].IsActive) c++;
                return c;
            }
        }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            var d = DecoyData;
            if (d != null)
            {
                currentDamage = d.baseDamage;
                currentCooldown = d.baseCooldown;
                currentDecoyCount = d.decoyCount;
                currentLifetime = d.decoyLifetime;
                currentExplosionRadius = d.explosionRadius;
            }

            decoyRoot = new GameObject("ShadowDecoys").transform;
            for (int i = 0; i < MAX_DECOYS; i++)
            {
                GameObject go = d != null && d.decoyPrefab != null
                    ? Instantiate(d.decoyPrefab, decoyRoot)
                    : new GameObject($"ShadowDecoy_{i}");
                go.name = $"ShadowDecoy_{i}";
                go.transform.SetParent(decoyRoot, false);
                var entity = go.GetComponent<ShadowDecoyEntity>();
                if (entity == null) entity = go.AddComponent<ShadowDecoyEntity>();
                entity.ForceDeactivate();
                decoys[i] = entity;
            }

            fireballPool = new ObjectPool<GravityShardProjectile>(
                CreateFireball,
                p => { if (p != null) p.gameObject.SetActive(true); },
                p => { if (p != null) p.gameObject.SetActive(false); },
                p =>
                {
                    if (p == null) return;
                    if (Application.isPlaying) Destroy(p.gameObject);
                    else DestroyImmediate(p.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 48);
        }

        private GravityShardProjectile CreateFireball()
        {
            var d = DecoyData;
            GameObject go = d != null && d.phantomFireballPrefab != null
                ? Instantiate(d.phantomFireballPrefab, decoyRoot)
                : new GameObject("PhantomFireball");
            go.name = "PhantomFireball";
            go.transform.SetParent(decoyRoot, false);
            var p = go.GetComponent<GravityShardProjectile>();
            if (p == null) p = go.AddComponent<GravityShardProjectile>();
            return p;
        }

        public override void PrewarmPools(int count = 0)
        {
            if (fireballPool == null) return;
            int n = count > 0 ? count : 8;
            var temp = new GravityShardProjectile[n];
            for (int i = 0; i < n; i++) temp[i] = fireballPool.Get();
            for (int i = 0; i < n; i++) if (temp[i] != null) fireballPool.Release(temp[i]);
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDamage += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(2f, currentCooldown - value); break;
                case ModifierType.Count: currentDecoyCount = Mathf.Min(MAX_DECOYS, currentDecoyCount + Mathf.Max(1, Mathf.RoundToInt(value))); break;
                case ModifierType.Duration: currentLifetime += value; break;
                case ModifierType.Radius:
                case ModifierType.Area: currentExplosionRadius += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1) currentExplosionRadius *= 1.2f;
            else if (tier == 2) currentDecoyCount = Mathf.Min(MAX_DECOYS, currentDecoyCount + 1);
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;
            if (Time.time >= nextFireTime && ActiveDecoyCount == 0)
            {
                Deploy();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 2f);
            }
        }

        /// <summary>Deploys decoys around the hero. Returns the number deployed.</summary>
        public int Deploy()
        {
            var d = DecoyData;
            if (d == null) return 0;

            int deployed = 0;
            float angleOffset = Random.Range(0f, Mathf.PI * 2f);
            for (int n = 0; n < currentDecoyCount; n++)
            {
                ShadowDecoyEntity decoy = FindFreeDecoy();
                if (decoy == null) break;

                float angle = angleOffset + n * (Mathf.PI * 2f / Mathf.Max(1, currentDecoyCount));
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * d.deployDistance;
                pos.y = 0f;
                decoy.transform.localScale = Vector3.one * VisualScaleMultiplier;
                decoy.Activate(this, pos, currentLifetime);
                PulseTaunt(decoy);
                eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.DecoyDeploy, pos, d.tauntRadius));
                deployed++;
            }
            return deployed;
        }

        private ShadowDecoyEntity FindFreeDecoy()
        {
            for (int i = 0; i < decoys.Length; i++)
            {
                if (decoys[i] != null && !decoys[i].IsActive) return decoys[i];
            }
            return null;
        }

        /// <summary>Taunts every non-boss enemy within the taunt radius. Returns enemies taunted.</summary>
        public int PulseTaunt(ShadowDecoyEntity decoy)
        {
            var d = DecoyData;
            if (d == null || decoy == null || !decoy.IsActive) return 0;

            int found = Physics.OverlapSphereNonAlloc(decoy.transform.position, ScaleArea(d.tauntRadius), hitBuffer, d.targetLayer);
            int taunted = 0;
            for (int i = 0; i < found; i++)
            {
                if (hitBuffer[i] == null || !hitBuffer[i].TryGetComponent<EnemyBase>(out var enemy) || enemy.IsDead) continue;
                if (enemy.ApplyTaunt(decoy.transform, decoy.Remaining + 0.1f)) taunted++;
            }
            LastTauntedCount = taunted;
            return taunted;
        }

        /// <summary>Explosion + optional Phantom Legion fireball ring. Called by the decoy.</summary>
        public int HandleDecoyDetonated(ShadowDecoyEntity decoy, Vector3 position)
        {
            var d = DecoyData;
            if (d == null) return 0;

            float radius = ScaleArea(currentExplosionRadius);
            int found = Physics.OverlapSphereNonAlloc(position, radius, hitBuffer, d.targetLayer);
            int hits = 0;
            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;
                int dmg = RollScaledDamage(currentDamage, out bool isCrit);
                DealDamage(damageable, dmg, isCrit, isAoE: true, sourcePosition: position);
                hits++;
            }

            eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.DecoyDetonate, position, radius));

            if (HasSynergy(SynergyIds.PhantomLegion))
            {
                ReleasePhantomLegion(position);
            }

            return hits;
        }

        /// <summary>Phantom Legion: ring of dragon fireballs flying outward. Returns fireballs launched.</summary>
        public int ReleasePhantomLegion(Vector3 position)
        {
            var d = DecoyData;
            if (d == null || fireballPool == null) return 0;

            int count = Mathf.Max(1, d.phantomFireballCount);
            float damage = GetScaledDamage(currentDamage * d.phantomFireballDamageFactor);
            Vector3 start = position + Vector3.up * 0.6f;
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                GravityShardProjectile fb = fireballPool.Get();
                if (fb == null) continue;
                fb.Initialize(start, dir, damage, d.targetLayer, runtimeStats, runStatistics, fireballPool);
                fb.Configure(SynergyIds.PhantomLegion, d.phantomFireballSpeed, d.phantomFireballLifetime, ScaleArea(d.phantomFireballRadius), spin: false);
            }

            PhantomVolleys++;
            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.PhantomLegion, position, 2.5f));
            return count;
        }

        private void OnDestroy()
        {
            fireballPool?.Clear();
            if (decoyRoot != null)
            {
                if (Application.isPlaying) Destroy(decoyRoot.gameObject);
                else DestroyImmediate(decoyRoot.gameObject);
            }
        }
    }
}
