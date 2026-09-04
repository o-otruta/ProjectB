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

namespace ProjectB.Core
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register Core Systems
            builder.Register<RunStatistics>(Lifetime.Scoped);
            builder.Register<GameEventBus>(Lifetime.Scoped);
            builder.RegisterEntryPoint<EnemyDeathHandler>();

            // Register Managers & Environment
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.RegisterComponentInHierarchy<WaveManager>();
            builder.RegisterComponentInHierarchy<XpManager>();
            builder.RegisterComponentInHierarchy<CoinManager>();
            builder.RegisterComponentInHierarchy<UpgradeManager>();
            builder.RegisterComponentInHierarchy<ArenaGenerator>();
            builder.RegisterComponentInHierarchy<CameraController>();
            
            // Register UI
            builder.RegisterComponentInHierarchy<VirtualJoystick>();
            builder.RegisterComponentInHierarchy<CardSelectionUI>();
            builder.RegisterComponentInHierarchy<GameOverUI>();
            builder.RegisterComponentInHierarchy<HpBarUI>();
            builder.RegisterComponentInHierarchy<XpBarUI>();
            
            // Register Player
            builder.RegisterComponentInHierarchy<HeroHealth>();
            builder.RegisterComponentInHierarchy<HeroExperience>();
            builder.RegisterComponentInHierarchy<HeroEconomy>();
            builder.RegisterComponentInHierarchy<HeroMovement>();
            builder.RegisterComponentInHierarchy<HeroAbilities>();
            builder.RegisterComponentInHierarchy<HeroCombat>();

            // Explicitly inject into all scene components
            // (Removed explicit RegisterBuildCallback to prevent double injection)
        }
    }
}
