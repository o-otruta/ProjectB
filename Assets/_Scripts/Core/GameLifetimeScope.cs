using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Abilities;

using ProjectB.Meta;
using ProjectB.Core.Events;
using ProjectB.Combat;
using ProjectB.Arena;
using ProjectB.Data;
using ProjectB.Visuals;

namespace ProjectB.Core
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register ScriptableObject Configs (single source of truth via DI)
            RegisterResourceConfig<RunRulesConfig>(builder, RunRulesConfig.DEFAULT_RESOURCE_PATH);
            RegisterResourceConfig<ComboConfig>(builder, ComboConfig.DEFAULT_RESOURCE_PATH);
            RegisterResourceConfig<PickupConfig>(builder, PickupConfig.DEFAULT_RESOURCE_PATH);
            RegisterResourceConfig<SecretConfig>(builder, SecretConfig.DEFAULT_RESOURCE_PATH);
            RegisterResourceConfig<CardCatalog>(builder, CardCatalog.DEFAULT_RESOURCE_PATH);

            // Register Core Systems
            builder.Register<RunStatistics>(Lifetime.Scoped);
            builder.Register<RunComboTracker>(Lifetime.Scoped);
            builder.Register<GameEventBus>(Lifetime.Scoped);
            builder.Register<HeroRuntimeStats>(Lifetime.Scoped);
            builder.Register<UpgradeApplier>(Lifetime.Scoped);
            builder.Register<EnemyRuntimeModifiers>(Lifetime.Scoped);
            builder.Register<EnemyProjectilePool>(Lifetime.Scoped);
            builder.RegisterEntryPoint<EnemyDeathHandler>();
            builder.RegisterEntryPoint<MetaRunRewardsHandler>().AsSelf();

            // Register Managers & Environment
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.RegisterComponentInHierarchy<WaveManager>();
            builder.RegisterComponentInHierarchy<WaveEventManager>();
            builder.RegisterComponentInHierarchy<XpManager>();
            builder.RegisterComponentInHierarchy<CoinManager>();
            builder.RegisterComponentInHierarchy<UpgradeManager>();
            builder.RegisterComponentInHierarchy<ArenaGenerator>();
            builder.RegisterComponentInHierarchy<SecretItemSpawner>();
            builder.RegisterComponentInHierarchy<VacuumMagnetSpawner>();
            builder.RegisterComponentInHierarchy<VfxFeedbackManager>();
            builder.RegisterComponentInHierarchy<CameraController>();
            
            // Register UI
            builder.RegisterComponentInHierarchy<VirtualJoystick>();
            builder.RegisterComponentInHierarchy<CardSelectionUI>();
            builder.RegisterComponentInHierarchy<GameOverUI>();
            builder.RegisterComponentInHierarchy<HpBarUI>();
            builder.RegisterComponentInHierarchy<XpBarUI>();
            builder.RegisterComponentInHierarchy<WaveHudUI>();
            builder.RegisterComponentInHierarchy<WaveEventBannerUI>();
            builder.RegisterComponentInHierarchy<CoinHudUI>();
            builder.RegisterComponentInHierarchy<ComboHudUI>();
            builder.RegisterComponentInHierarchy<AbilityHudUI>();
            builder.RegisterComponentInHierarchy<BossHealthBarUI>();
            builder.RegisterComponentInHierarchy<UltimateButtonUI>();
            builder.RegisterComponentInHierarchy<SecretCompassIndicatorUI>();
            builder.RegisterComponentInHierarchy<DamageVignetteUI>();
            builder.RegisterComponentInHierarchy<DebugCheatMenu>();
            builder.RegisterComponentInHierarchy<AchievementPopupUI>();
            builder.RegisterComponentInHierarchy<PauseMenuUI>();
            // Sprint 13: modal HUD fade + shared settings popup (opened from Pause).
            builder.RegisterComponentInHierarchy<HudVisibilityController>();
            builder.RegisterComponentInHierarchy<KillCounterHudUI>();
            builder.RegisterComponentInHierarchy<SettingsPopupUI>();
            builder.RegisterComponentInHierarchy<GameplayFtueUI>();
            
            // Register Player
            builder.RegisterComponentInHierarchy<HeroHealth>();
            builder.RegisterComponentInHierarchy<HeroExperience>();
            builder.RegisterComponentInHierarchy<HeroEconomy>();
            builder.RegisterComponentInHierarchy<HeroMovement>();
            builder.RegisterComponentInHierarchy<HeroAbilities>();
            builder.RegisterComponentInHierarchy<HeroCombat>();
            builder.RegisterComponentInHierarchy<HeroUltimate>();

            // Explicitly inject into all scene components
            // (Removed explicit RegisterBuildCallback to prevent double injection)
        }

        private static void RegisterResourceConfig<T>(IContainerBuilder builder, string resourcePath) where T : ScriptableObject
        {
            T asset = Resources.Load<T>(resourcePath);
            if (asset != null)
            {
                builder.RegisterInstance(asset);
            }
            else
            {
                Debug.LogWarning($"[GameLifetimeScope] Config asset '{resourcePath}' ({typeof(T).Name}) not found in Resources; consumers will use default constants.");
            }
        }
    }
}
