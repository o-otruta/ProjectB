using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Ads;
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
            builder.Register<DailyBonusManager>(Lifetime.Singleton);
            builder.Register<SelectedHeroService>(Lifetime.Singleton);
            builder.Register<AudioManager>(Lifetime.Singleton);
            builder.Register<AdManager>(Lifetime.Singleton);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && Container != null)
            {
                Container.Resolve<SaveManager>()?.SaveIfDirty();
            }
        }
    }
}
