using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.Abilities
{
    /// <summary>
    /// Glacial Shatter (Rare): an expanding frost wave damages and freezes enemies; shortly after,
    /// every enemy that is still frozen shatters for bonus damage and sprays shrapnel onto neighbours.
    /// Frozen state (<see cref="EnemyBase.IsFrozen"/>) also powers the "Shatter Nova" synergy with Seismic Hammer.
    /// </summary>
    public class GlacialShatterAbility : ActiveAbility
    {
        public const int MAX_TRACKED = 64;
        private const int MAX_SHATTER_VFX_PER_WAVE = 8;
        private const float WAVE_ANIM_DURATION = 0.35f;

        private GlacialShatterAbilityData GlacialData => Data as GlacialShatterAbilityData;

        private readonly Collider[] hitBuffer = new Collider[128];
        private readonly Collider[] shrapnelBuffer = new Collider[32];
        private readonly EnemyBase[] frozenTargets = new EnemyBase[MAX_TRACKED];
        private int frozenCount;
        private float shatterTimer = -1f;

        private float currentDamage;
        private float currentCooldown;
        private float currentRadius;
        private float currentFreezeDuration;
        private float nextFireTime;

        private Transform waveVisual;
        private float waveElapsed = -1f;

        public int FrozenTrackedCount => frozenCount;
        public bool HasPendingShatter => shatterTimer >= 0f;
        public float EffectiveRadius => ScaleArea(currentRadius);
        public float CurrentFreezeDuration => currentFreezeDuration;

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            var d = GlacialData;
            if (d != null)
            {
                currentDamage = d.baseDamage;
                currentCooldown = d.baseCooldown;
                currentRadius = d.waveRadius;
                currentFreezeDuration = d.freezeDuration;

                if (d.waveVisualPrefab != null)
                {
                    var inst = Instantiate(d.waveVisualPrefab, transform, false);
                    inst.name = "GlacialWave_Visual";
                    var renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderers[i].receiveShadows = false;
                    }
                    waveVisual = inst.transform;
                    waveVisual.gameObject.SetActive(false);
                }
            }
        }

        public override void ApplyModifier(ModifierType type, float value)
        {
            switch (type)
            {
                case ModifierType.Damage: currentDamage += value; break;
                case ModifierType.Cooldown: currentCooldown = Mathf.Max(1.0f, currentCooldown - value); break;
                case ModifierType.Radius:
                case ModifierType.Area: currentRadius += value; break;
                case ModifierType.Duration: currentFreezeDuration += value; break;
            }
        }

        protected override void OnOverdriveTierReached(int tier)
        {
            if (tier == 1) currentRadius *= 1.2f;
            else if (tier == 2)
            {
                currentFreezeDuration *= 1.25f;
                currentCooldown = Mathf.Max(1.0f, currentCooldown * 0.85f);
            }
        }

        public override void SimulateFrameTick()
        {
            Update();
        }

        private void Update()
        {
            if (Data == null || Time.deltaTime <= 0f) return;

            UpdateWaveVisual(Time.deltaTime);
            StepShatter(Time.deltaTime);

            if (Time.time >= nextFireTime)
            {
                TriggerFreezeWave();
                nextFireTime = Time.time + GetEffectiveCooldown(currentCooldown, 1.0f);
            }
        }

        /// <summary>Releases the frost wave. Returns enemies frozen.</summary>
        public int TriggerFreezeWave()
        {
            var d = GlacialData;
            if (d == null) return 0;

            float radius = EffectiveRadius;
            int found = Physics.OverlapSphereNonAlloc(transform.position, radius, hitBuffer, d.targetLayer);
            frozenCount = 0;

            for (int i = 0; i < found; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var damageable) || damageable.IsDead) continue;

                int dmg = RollScaledDamage(currentDamage, out bool isCrit);
                DealDamage(damageable, dmg, isCrit, isAoE: true);

                if (col.TryGetComponent<EnemyBase>(out var enemy) && !enemy.IsDead)
                {
                    enemy.ApplyFreeze(currentFreezeDuration, d.frozenSlowFactor);
                    if (frozenCount < MAX_TRACKED)
                    {
                        frozenTargets[frozenCount++] = enemy;
                    }
                }
            }

            shatterTimer = frozenCount > 0 ? currentFreezeDuration * d.shatterAtFraction : -1f;

            if (waveVisual != null)
            {
                waveVisual.gameObject.SetActive(true);
                waveVisual.localScale = new Vector3(0.3f, 1f, 0.3f);
                waveElapsed = 0f;
            }

            eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.GlacialFreeze, transform.position, radius));
            return frozenCount;
        }

        public void StepShatter(float dt)
        {
            if (shatterTimer < 0f) return;
            shatterTimer -= dt;
            if (shatterTimer <= 0f)
            {
                ShatterFrozen();
            }
        }

        /// <summary>Shatters every tracked enemy that is still frozen. Returns enemies shattered.</summary>
        public int ShatterFrozen()
        {
            var d = GlacialData;
            shatterTimer = -1f;
            if (d == null) return 0;

            int shattered = 0;
            int vfxBudget = MAX_SHATTER_VFX_PER_WAVE;
            for (int i = 0; i < frozenCount; i++)
            {
                EnemyBase enemy = frozenTargets[i];
                frozenTargets[i] = null;
                if (enemy == null || enemy.IsDead || !enemy.IsFrozen) continue;

                Vector3 pos = enemy.transform.position;
                int dmg = RollScaledDamage(currentDamage * d.shatterDamageMultiplier, out bool isCrit);
                DealDamage(enemy, dmg, isCrit, isAoE: true, sourcePosition: pos);
                enemy.ClearFreeze();
                shattered++;

                SprayShrapnel(pos, enemy, currentDamage * d.shrapnelDamageFactor, d.shrapnelRadius, d.targetLayer, null);

                if (vfxBudget-- > 0)
                {
                    eventBus?.Publish(new AbilityVfxEvent(AbilityVfxType.GlacialShatter, pos, d.shrapnelRadius));
                }
            }

            frozenCount = 0;
            return shattered;
        }

        private int SprayShrapnel(Vector3 center, EnemyBase origin, float rawDamage, float radius, LayerMask layer, string sourceId)
        {
            if (rawDamage <= 0f) return 0;
            int hits = Physics.OverlapSphereNonAlloc(center, ScaleArea(radius), shrapnelBuffer, layer);
            int count = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider col = shrapnelBuffer[i];
                if (col == null || !col.TryGetComponent<IDamageable>(out var dmgTarget) || dmgTarget.IsDead) continue;
                if (origin != null && ReferenceEquals(dmgTarget, origin)) continue;

                int dmg = RollScaledDamage(rawDamage, out bool isCrit);
                DealDamage(dmgTarget, dmg, isCrit, sourceId, isAoE: true, sourcePosition: center);
                count++;
            }
            return count;
        }

        private void UpdateWaveVisual(float dt)
        {
            if (waveElapsed < 0f || waveVisual == null) return;
            waveElapsed += dt;
            float t = Mathf.Clamp01(waveElapsed / WAVE_ANIM_DURATION);
            float scale = Mathf.Lerp(0.3f, EffectiveRadius, 1f - (1f - t) * (1f - t));
            waveVisual.localScale = new Vector3(scale, 1f, scale);
            if (t >= 1f)
            {
                waveElapsed = -1f;
                waveVisual.gameObject.SetActive(false);
            }
        }
    }
}
