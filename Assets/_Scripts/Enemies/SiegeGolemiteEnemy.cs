using UnityEngine;
using UnityEngine.Pool;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;

namespace ProjectB.Enemies
{
    /// <summary>
    /// W61 Siege Golemite with two combat phases:
    /// - Phase 1 (Stone Shell): absorbs damage into shellHp with flat armor (clamped at >= shellMinDamagePercent).
    /// - Phase 2 (Exposed Core): once shell breaks, swaps child visuals, removes flat armor, and rushes at coreSpeedMultiplier.
    /// </summary>
    public class SiegeGolemiteEnemy : EnemyBase
    {
        private static readonly Color ShellRimColor = new Color(0.55f, 0.78f, 1.00f, 1f);
        private static readonly Color CoreRimColor = new Color(1.00f, 0.42f, 0.12f, 1f);

        [Header("Phase Visuals")]
        [SerializeField] private GameObject shellVisualRoot;
        [SerializeField] private GameObject coreVisualRoot;

        private SiegeGolemiteEnemyData siegeData;
        private int maxShellHp;
        private int currentShellHp;
        private bool isShellBroken;

        public int MaxShellHp => maxShellHp;
        public int CurrentShellHp => currentShellHp;
        public bool IsShellIntact => !isShellBroken && currentShellHp > 0;
        public bool IsShellBroken => isShellBroken;
        public int ShellFlatArmor => siegeData != null ? Mathf.Max(0, siegeData.shellFlatArmor) : 18;
        public float ShellMinDamagePercent => siegeData != null ? Mathf.Clamp(siegeData.shellMinDamagePercent, 0.05f, 0.75f) : 0.15f;
        public float CoreSpeedMultiplier => siegeData != null ? Mathf.Max(1f, siegeData.coreSpeedMultiplier) : 2.0f;

        public override void Initialize(
            EnemyData data,
            Transform heroTarget,
            IObjectPool<EnemyBase> enemyPool,
            GameEventBus bus,
            float difficultyMultiplier = 1f,
            float damageDifficultyMultiplier = -1f)
        {
            base.Initialize(data, heroTarget, enemyPool, bus, difficultyMultiplier, damageDifficultyMultiplier);

            siegeData = data as SiegeGolemiteEnemyData;
            ResolvePhaseVisuals();

            float effectiveHpMult = Mathf.Max(0.01f, difficultyMultiplier);
            int baseShell = siegeData != null ? Mathf.Max(1, siegeData.shellHp) : 130;
            maxShellHp = Mathf.Max(1, Mathf.RoundToInt(baseShell * effectiveHpMult));
            currentShellHp = maxShellHp;
            isShellBroken = false;
            hasCustomRim = true;

            UpdatePhaseVisualVisibility();
            ApplyVisualState(0f);
        }

        public override void MakeElite(
            EliteArchetype archetype,
            float hpMultiplier,
            float damageMultiplier,
            float speedMultiplier = 1f,
            float scaleMultiplier = 1f,
            float pullResistanceOverride = 1f,
            float auraRadius = 3.5f,
            float auraSpeedBonus = 0.2f)
        {
            bool wasElite = IsElite;
            base.MakeElite(archetype, hpMultiplier, damageMultiplier, speedMultiplier, scaleMultiplier, pullResistanceOverride, auraRadius, auraSpeedBonus);
            if (!wasElite && IsElite && IsShellIntact)
            {
                maxShellHp = Mathf.Max(1, Mathf.RoundToInt(maxShellHp * Mathf.Max(0.1f, hpMultiplier)));
                currentShellHp = maxShellHp;
            }
        }

        public override void MakeMiniBoss(float hpMultiplier = 5f, float damageMultiplier = 2f, float scaleMultiplier = 1.85f, string displayName = null)
        {
            bool wasMiniBoss = IsMiniBoss;
            base.MakeMiniBoss(hpMultiplier, damageMultiplier, scaleMultiplier, displayName);
            if (!wasMiniBoss && IsMiniBoss && IsShellIntact)
            {
                maxShellHp = Mathf.Max(1, Mathf.RoundToInt(maxShellHp * Mathf.Max(0.1f, hpMultiplier)));
                currentShellHp = maxShellHp;
            }
        }

        protected override float GetAdditionalSpeedMultiplier()
        {
            return isShellBroken ? CoreSpeedMultiplier : 1f;
        }

        protected override Color GetCustomRimColor()
        {
            return isShellBroken ? CoreRimColor : ShellRimColor;
        }

        protected override int ModifyIncomingDamage(int rawAmount, bool hasSourcePosition, Vector3 sourcePosition, bool isAoE, out bool wasBlocked)
        {
            if (rawAmount <= 0 || !IsShellIntact)
            {
                wasBlocked = false;
                return rawAmount;
            }

            int minDamage = Mathf.Max(1, Mathf.RoundToInt(rawAmount * ShellMinDamagePercent));
            int afterFlat = rawAmount - ShellFlatArmor;
            int mitigated = Mathf.Min(rawAmount, Mathf.Max(minDamage, afterFlat));
            mitigated = Mathf.Max(1, mitigated);
            wasBlocked = mitigated < rawAmount;
            return mitigated;
        }

        protected override int AbsorbIncomingDamage(int finalAmount, bool isCrit, bool wasBlocked)
        {
            if (!IsShellIntact || finalAmount <= 0)
            {
                return finalAmount;
            }

            if (finalAmount < currentShellHp)
            {
                currentShellHp -= finalAmount;
                return 0;
            }

            int overflow = Mathf.Max(0, finalAmount - currentShellHp);
            currentShellHp = 0;
            ShatterShell();
            return overflow;
        }

        private void ShatterShell()
        {
            if (isShellBroken) return;
            isShellBroken = true;

            UpdatePhaseVisualVisibility();
            ApplyVisualState(1f);
            eventBus?.Publish(new SiegeShellBrokenEvent(transform.position, activeScaleMultiplier));
        }

        private void ResolvePhaseVisuals()
        {
            if (shellVisualRoot == null)
            {
                Transform t = transform.Find("Visuals/ShellVisuals");
                if (t != null) shellVisualRoot = t.gameObject;
            }
            if (coreVisualRoot == null)
            {
                Transform t = transform.Find("Visuals/CoreVisuals");
                if (t != null) coreVisualRoot = t.gameObject;
            }
        }

        private void UpdatePhaseVisualVisibility()
        {
            if (shellVisualRoot != null)
            {
                shellVisualRoot.SetActive(!isShellBroken);
            }
            if (coreVisualRoot != null)
            {
                coreVisualRoot.SetActive(isShellBroken);
            }
        }
    }
}
