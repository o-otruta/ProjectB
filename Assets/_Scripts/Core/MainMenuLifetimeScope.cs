using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Meta;
using ProjectB.UI;

namespace ProjectB.Core
{
    public class MainMenuLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MainMenuUI>();
            builder.RegisterComponentInHierarchy<MetaUpgradeUI>();
            builder.RegisterComponentInHierarchy<AchievementScreenUI>();
            builder.RegisterComponentInHierarchy<DailyBonusScreenUI>();
            builder.Register<DailyBonusManager>(Lifetime.Scoped);
        }
    }
}
