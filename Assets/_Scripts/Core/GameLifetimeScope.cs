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
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Inject(resolver.Resolve<GameManager>());
                resolver.Inject(resolver.Resolve<WaveManager>());
                resolver.Inject(resolver.Resolve<XpManager>());
                resolver.Inject(resolver.Resolve<UpgradeManager>());
                
                resolver.Inject(resolver.Resolve<VirtualJoystick>());
                resolver.Inject(resolver.Resolve<CardSelectionUI>());
                resolver.Inject(resolver.Resolve<GameOverUI>());
                resolver.Inject(resolver.Resolve<HpBarUI>());
                resolver.Inject(resolver.Resolve<XpBarUI>());
                
                resolver.Inject(resolver.Resolve<HeroHealth>());
                resolver.Inject(resolver.Resolve<HeroExperience>());
                resolver.Inject(resolver.Resolve<HeroMovement>());
                resolver.Inject(resolver.Resolve<HeroAbilities>());
            });
        }
    }
}
