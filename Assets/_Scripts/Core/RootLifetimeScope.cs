using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Meta;

namespace ProjectB.Core
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<SaveManager>(Lifetime.Singleton);
            builder.Register<MetaUpgradeManager>(Lifetime.Singleton);
            builder.Register<AchievementManager>(Lifetime.Singleton);
        }
    }
}
