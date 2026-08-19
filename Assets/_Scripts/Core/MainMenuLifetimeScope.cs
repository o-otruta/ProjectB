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
            Debug.Log("[MainMenuLifetimeScope] Configure started");
            builder.RegisterComponentInHierarchy<MainMenuUI>();
            builder.RegisterComponentInHierarchy<MetaUpgradeUI>();
            builder.RegisterComponentInHierarchy<AchievementScreenUI>();
            builder.RegisterComponentInHierarchy<DailyBonusScreenUI>();
            builder.Register<DailyBonusManager>(Lifetime.Scoped);

            builder.RegisterBuildCallback(resolver =>
            {
                Debug.Log("[MainMenuLifetimeScope] BuildCallback executed");
                var ui = FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
                if (ui != null)
                {
                    Debug.Log("[MainMenuLifetimeScope] Found MainMenuUI, injecting!");
                    resolver.Inject(ui);
                }
                else
                {
                    Debug.LogWarning("[MainMenuLifetimeScope] MainMenuUI NOT found in scene!");
                }

                var metaUi = FindAnyObjectByType<MetaUpgradeUI>(FindObjectsInactive.Include);
                if (metaUi != null)
                {
                    Debug.Log("[MainMenuLifetimeScope] Found MetaUpgradeUI, injecting!");
                    resolver.Inject(metaUi);
                }
                
                var achievementUi = FindAnyObjectByType<AchievementScreenUI>(FindObjectsInactive.Include);
                if (achievementUi != null)
                {
                    Debug.Log("[MainMenuLifetimeScope] Found AchievementScreenUI, injecting!");
                    resolver.Inject(achievementUi);
                }

                var bonusUi = FindAnyObjectByType<DailyBonusScreenUI>(FindObjectsInactive.Include);
                if (bonusUi != null)
                {
                    Debug.Log("[MainMenuLifetimeScope] Found DailyBonusScreenUI, injecting!");
                    resolver.Inject(bonusUi);
                }
            });
        }
    }
}
