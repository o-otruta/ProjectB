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
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<SaveManager>(Lifetime.Singleton);
            builder.Register<MetaUpgradeManager>(Lifetime.Singleton);
            builder.Register<AbilitySlotService>(Lifetime.Singleton);
            builder.Register<AchievementManager>(Lifetime.Singleton);
            builder.Register<DailyBonusManager>(Lifetime.Singleton);
            builder.Register<SelectedHeroService>(Lifetime.Singleton);
            // Sprint 11: Constellation Tree gates reactive synergies and grants bonus ability slots.
            builder.Register<TalentTreeManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISynergyUnlockProvider>()
                .As<IAbilitySlotBonusProvider>();
            builder.Register<AudioManager>(Lifetime.Singleton);
            builder.Register<AdManager>(Lifetime.Singleton);

            // Sprint 14.1: adaptive foundation — layout mode (Portrait/Landscape), input mode and build-profile orientation.
            builder.RegisterEntryPoint<LayoutModeService>().AsSelf();
            builder.Register<InputModeService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OrientationPolicyService>().AsSelf();

            // Eagerly construct the tree so it registers its slot bonuses with AbilitySlotService before any UI reads them.
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<TalentTreeManager>();
                // Start tracking the active device from the very first frame, before any UI asks for it.
                container.Resolve<InputModeService>();
            });
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
