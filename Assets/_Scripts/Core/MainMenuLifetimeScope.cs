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
            builder.RegisterComponentInHierarchy<HeroSelectionUI>();
            builder.RegisterComponentInHierarchy<MetaUpgradeUI>();
            builder.RegisterComponentInHierarchy<AbilitiesScreenUI>();
            builder.RegisterComponentInHierarchy<AchievementScreenUI>();
            builder.RegisterComponentInHierarchy<AchievementPopupUI>();
            builder.RegisterComponentInHierarchy<DailyBonusScreenUI>();
            builder.RegisterComponentInHierarchy<SettingsPopupUI>();
            builder.RegisterComponentInHierarchy<ConstellationTreeUI>();
        }
    }
}
