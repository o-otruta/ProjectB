using UnityEngine;
using ProjectB.Combat;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public abstract class ActiveAbility : AbilityBase
    {
        public const int OVERDRIVE_LEVEL_INTERVAL = 10;

        public ActiveAbilityData ActiveData => Data as ActiveAbilityData;
        protected HeroRuntimeStats runtimeStats;
        protected GameEventBus eventBus;
        protected HeroAbilities heroAbilities;
        protected RunStatistics runStatistics;
        protected string cachedAbilityId;

        public int OverdriveTier { get; private set; }
        public float VisualScaleMultiplier { get; private set; } = 1f;
        public float OverdriveDamageMultiplier { get; private set; } = 1f;

        public static int CalculateOverdriveTier(int level)
        {
            return level >= OVERDRIVE_LEVEL_INTERVAL ? level / OVERDRIVE_LEVEL_INTERVAL : 0;
        }

        public static string GetOverdriveTierLabel(int tier)
        {
            return tier switch
            {
                <= 0 => string.Empty,
                1 => "OVERDRIVE!",
                2 => "AWAKENED!",
                3 => "MYTHIC!",
                _ => $"MYTHIC +{tier - 2}!"
            };
        }

        public override void Initialize(AbilityData data)
        {
            base.Initialize(data);
            cachedAbilityId = data != null ? data.id : string.Empty;
            OverdriveTier = 0;
            VisualScaleMultiplier = 1f;
            OverdriveDamageMultiplier = 1f;
        }

        public void SetRuntimeStats(HeroRuntimeStats stats)
        {
            runtimeStats = stats;
        }

        public void SetEventBus(GameEventBus bus)
        {
            eventBus = bus;
        }

        public void SetHeroAbilities(HeroAbilities abilities)
        {
            heroAbilities = abilities;
        }

        public void SetRunStatistics(RunStatistics stats)
        {
            runStatistics = stats;
        }

        protected bool HasSynergy(string synergyId)
        {
            return heroAbilities != null && heroAbilities.HasSynergy(synergyId);
        }

        public virtual void OnSynergyActivated(string synergyId)
        {
        }

        public override void IncrementLevel()
        {
            base.IncrementLevel();

            int targetTier = CalculateOverdriveTier(Level);
            while (OverdriveTier < targetTier)
            {
                OverdriveTier++;
                if (OverdriveTier == 1)
                {
                    VisualScaleMultiplier = 1.15f;
                }
                else if (OverdriveTier == 2)
                {
                    VisualScaleMultiplier = 1.25f;
                }
                else
                {
                    VisualScaleMultiplier = 1.25f;
                    OverdriveDamageMultiplier += 0.25f;
                }

                OnOverdriveTierReached(OverdriveTier);
                string abilityName = Data != null && !string.IsNullOrEmpty(Data.abilityName) ? Data.abilityName : name;
                eventBus?.Publish(new AbilityOverdriveEvent(
                    cachedAbilityId,
                    abilityName,
                    Level,
                    OverdriveTier,
                    GetOverdriveTierLabel(OverdriveTier)));
            }
        }

        protected virtual void OnOverdriveTierReached(int tier)
        {
        }

        public virtual void PrewarmPools(int count = 0)
        {
        }

        public virtual void SimulateFrameTick()
        {
        }

        protected float GetScaledDamage(float rawDamage)
        {
            float multiplier = runtimeStats != null ? runtimeStats.AbilityDamageMultiplier : 1f;
            return rawDamage * multiplier * OverdriveDamageMultiplier;
        }

        /// <summary>Global radius/area multiplier (Area Tome, Engineer passive, Constellation nodes).</summary>
        public float AreaMultiplier => runtimeStats != null ? runtimeStats.GlobalAreaMultiplier : 1f;

        protected float ScaleArea(float baseRadius) => baseRadius * AreaMultiplier;

        protected float GetEffectiveCooldown(float baseCooldown, float minCooldown = 0.1f)
        {
            return runtimeStats != null
                ? runtimeStats.GetEffectiveCooldown(baseCooldown, minCooldown)
                : Mathf.Max(minCooldown, baseCooldown);
        }

        protected int RollScaledDamage(float rawDamage, out bool isCrit)
        {
            float scaled = GetScaledDamage(rawDamage);
            if (runtimeStats != null)
            {
                return runtimeStats.RollDamage(scaled, out isCrit);
            }

            isCrit = false;
            return scaled > 0f ? Mathf.Max(1, Mathf.RoundToInt(scaled)) : 0;
        }

        protected void DealDamage(
            IDamageable target,
            int damage,
            bool isCrit,
            string sourceIdOverride = null,
            bool isAoE = false,
            Vector3? sourcePosition = null)
        {
            if (target == null || target.IsDead || damage <= 0) return;

            if (target is EnemyBase enemy)
            {
                Vector3 origin = sourcePosition ?? transform.position;
                int dealt = enemy.TakeDamageFrom(damage, isCrit, origin, isAoE);
                string sourceId = !string.IsNullOrEmpty(sourceIdOverride) ? sourceIdOverride : cachedAbilityId;
                if (!string.IsNullOrEmpty(sourceId) && dealt > 0)
                {
                    runStatistics?.RecordDamage(sourceId, dealt);
                }
            }
            else
            {
                target.TakeDamage(damage);
            }
        }
    }
}
