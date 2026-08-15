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
            builder.Register<SaveManager>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<MainMenuUI>();

            builder.RegisterBuildCallback(resolver =>
            {
                Debug.Log("[MainMenuLifetimeScope] BuildCallback executed");
                var ui = FindAnyObjectByType<MainMenuUI>();
                if (ui != null)
                {
                    Debug.Log("[MainMenuLifetimeScope] Found MainMenuUI, injecting!");
                    resolver.Inject(ui);
                }
                else
                {
                    Debug.LogWarning("[MainMenuLifetimeScope] MainMenuUI NOT found in scene!");
                }
            });
        }
    }
}
