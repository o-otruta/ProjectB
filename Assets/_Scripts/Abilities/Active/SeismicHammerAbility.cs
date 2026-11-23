using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;

namespace ProjectB.Abilities
{
    public class SeismicHammerAbility : ActiveAbility
    {
        private SeismicHammerAbilityData HammerData => Data as SeismicHammerAbilityData;

        private const float WAVE_ANIM_DURATION = 0.28f;
        private readonly Collider[] hitBuffer = new Collider[128];

        public override void SimulateFrameTick()
        {
            Update();
        }

        private float currentDamage;
        private float currentCooldown;
        private float currentRadius;
        public float EffectiveRadius => ScaleArea(currentRadius);
        private float currentSlowFactor;
        private float currentSlowDuration;

        private float nextFireTime;
        private float waveElapsed = -1f;
        private Transform waveRingVisual;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);

            if (HammerData != null)
            {
                currentDamage = HammerData.baseDamage;
                currentCooldown = HammerData.baseCooldown;
                currentRadius = HammerData.slamRadius;
                currentSlowFactor = HammerData.slowFactor;
                currentSlowDuration = HammerData.slowDuration;
            }

            EnsureShockwaveVisual();
        }

        private void EnsureShockwaveVisual()
        {
            if (waveRingVisual != null) return;

            if (HammerData != null && HammerData.waveVisualPrefab != null)
            {
                var inst = Instantiate(HammerData.waveVisualPrefab, transform, false);
                inst.name = "SeismicShockwave_Visual";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                waveRingVisual = inst.transform;
                var renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderers[i].receiveShadows = false;
                    }
                }
                waveRingVisual.gameObject.SetActive(false);
                return;
            }

            var ringGo = new GameObject("SeismicShockwave_Visual");
            ringGo.transform.SetParent(transform, false);
            waveRingVisual = ringGo.transform;

            const int segments = 12;
            Material mat = HammerData != null ? HammerData.waveMaterial : null;
            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f / segments) * i;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"WaveBlock_{i}";
                cube.transform.SetParent(waveRingVisual, false);
                cube.transform.localPosition = new Vector3(Mathf.Cos(angle), 0.14f, Mathf.Sin(angle));
                cube.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                cube.transform.localScale = new Vector3(0.24f, 0.24f, 0.52f);
                var col = cube.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }

                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (mat != null) mr.sharedMaterial = mat;
                }
            }

            waveRingVisual.gameObject.SetActive(false);
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage:
                    currentDamage += value;
                    break;
                case ModifierType.Radius:
                case ModifierType.Area:
                    currentRadius += value;
                    break;
                case ModifierType.Cooldown:
                    currentCooldown = Mathf.Max(0.8f, currentCooldown - value);
                    break;
                case ModifierType.Duration:
                    currentSlowDuration += value;
                    break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1)
            {
                currentRadius *= 1.2f;
            }
            else if (tier == 2)
            {
                currentCooldown = Mathf.Max(0.8f, currentCooldown * 0.8f);
                currentRadius *= 1.15f;
            }
        }

        public const float SHATTER_NOVA_DAMAGE_MULTIPLIER = 2.5f;
        public const int SHATTER_NOVA_SHARDS = 4;
        public const float SHATTER_NOVA_SHARD_RADIUS = 3.2f;
        public const float SHATTER_NOVA_SHARD_DAMAGE_FACTOR = 0.6f;
        private readonly Collider[] shatterBuffer = new Collider[32];
        private readonly ProjectB.Enemies.EnemyBase[] shardTargets = new ProjectB.Enemies.EnemyBase[SHATTER_NOVA_SHARDS];

        public int ShatterNovaCount { get; private set; }

        /// <summary>
        /// Shatter Nova reaction (Glacial Shatter + Seismic Hammer): a slam on a frozen enemy deals 2.5x damage,
        /// consumes the freeze and bursts the target into 4 ice shards that strike the nearest neighbours.
        /// Returns the number of neighbours hit by shards.
        /// </summary>
        public int TriggerShatterNova(ProjectB.Enemies.EnemyBase frozenEnemy, LayerMask layer)
        {
            if (frozenEnemy == null || frozenEnemy.IsDead) return 0;

            Vector3 pos = frozenEnemy.transform.position;
            frozenEnemy.ClearFreeze();
            int novaDamage = RollScaledDamage(currentDamage * SHATTER_NOVA_DAMAGE_MULTIPLIER, out bool novaCrit);
            DealDamage(frozenEnemy, novaDamage, novaCrit, SynergyIds.ShatterNova, isAoE: true, sourcePosition: pos);
            ShatterNovaCount++;

            int found = Physics.OverlapSphereNonAlloc(pos, ScaleArea(SHATTER_NOVA_SHARD_RADIUS), shatterBuffer, layer);
            int picked = 0;
            for (int s = 0; s < SHATTER_NOVA_SHARDS; s++)
            {
                ProjectB.Enemies.EnemyBase best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < found; i++)
                {
                    if (shatterBuffer[i] == null || !shatterBuffer[i].TryGetComponent<ProjectB.Enemies.EnemyBase>(out var e)) continue;
                    if (e == frozenEnemy || e.IsDead) continue;
                    bool used = false;
                    for (int u = 0; u < picked; u++) if (shardTargets[u] == e) { used = true; break; }
                    if (used) continue;
                    float d = (e.transform.position - pos).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = e; }
                }
                if (best == null) break;
                shardTargets[picked++] = best;
            }

            for (int i = 0; i < picked; i++)
            {
                int dmg = RollScaledDamage(currentDamage * SHATTER_NOVA_SHARD_DAMAGE_FACTOR, out bool crit);
                DealDamage(shardTargets[i], dmg, crit, SynergyIds.ShatterNova, isAoE: true, sourcePosition: pos);
                shardTargets[i] = null;
            }

            eventBus?.Publish(new SynergyReactionVfxEvent(SynergyReactionVfxType.ShatterNova, pos, SHATTER_NOVA_SHARD_RADIUS));
            return picked;
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            UpdateShockwaveVisual();

            if (Time.time >= nextFireTime)
            {
                TriggerSlam();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 0.8f);
            }
        }

        private void UpdateShockwaveVisual()
        {
            if (waveElapsed < 0f || waveRingVisual == null) return;

            waveElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(waveElapsed / WAVE_ANIM_DURATION);
            float scale = Mathf.Lerp(0.35f, EffectiveRadius, 1f - (1f - t) * (1f - t));
            waveRingVisual.localScale = new Vector3(scale, 1f - t * 0.55f, scale);

            if (t >= 1f)
            {
                waveElapsed = -1f;
                waveRingVisual.gameObject.SetActive(false);
            }
        }

        public int TriggerSlam()
        {
            if (HammerData == null) return 0;

            int found = Physics.OverlapSphereNonAlloc(transform.position, EffectiveRadius, hitBuffer, HammerData.targetLayer);
            int enemiesHit = 0;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
                {
                    if (damageable is ProjectB.Enemies.EnemyBase frozenEnemy && frozenEnemy.IsFrozen && HasSynergy(SynergyIds.ShatterNova))
                    {
                        TriggerShatterNova(frozenEnemy, HammerData.targetLayer);
                    }
                    else
                    {
                        int dmg = RollScaledDamage(currentDamage, out bool isCrit);
                        DealDamage(damageable, dmg, isCrit, isAoE: true);
                    }
                    enemiesHit++;
                }

                if (col.TryGetComponent<ISlowable>(out var slowable))
                {
                    slowable.ApplySlow(currentSlowFactor, currentSlowDuration);
                }
            }

            if (waveRingVisual != null)
            {
                waveRingVisual.gameObject.SetActive(true);
                waveRingVisual.localScale = new Vector3(0.35f, 1f, 0.35f);
                waveElapsed = 0f;
            }

            eventBus?.Publish(new SeismicSlamEvent(transform.position, EffectiveRadius));
            return enemiesHit;
        }
    }
}
