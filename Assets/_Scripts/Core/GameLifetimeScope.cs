using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Core;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Player;
using ProjectB.UI;
using ProjectB.Abilities;

namespace ProjectB.Core
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register Managers
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.RegisterComponentInHierarchy<WaveManager>();
            builder.RegisterComponentInHierarchy<XpManager>();
            builder.RegisterComponentInHierarchy<UpgradeManager>();
            
            // Register UI
            builder.RegisterComponentInHierarchy<VirtualJoystick>();
            builder.RegisterComponentInHierarchy<CardSelectionUI>();
            builder.RegisterComponentInHierarchy<GameOverUI>();
            builder.RegisterComponentInHierarchy<HpBarUI>();
            builder.RegisterComponentInHierarchy<XpBarUI>();
            
            // Register Player
            builder.RegisterComponentInHierarchy<HeroHealth>();
            builder.RegisterComponentInHierarchy<HeroExperience>();
            builder.RegisterComponentInHierarchy<HeroMovement>();
            builder.RegisterComponentInHierarchy<HeroAbilities>();

            // Explicitly inject into all scene components
            // (Removed explicit RegisterBuildCallback to prevent double injection)
        }
    }
}
