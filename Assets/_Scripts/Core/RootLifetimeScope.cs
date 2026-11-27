using UnityEngine;
using VContainer;
using VContainer.Unity;
using ProjectB.Ads;
using ProjectB.Data.Platform;
using ProjectB.Meta;
using ProjectB.Visuals;

namespace ProjectB.Core
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            GameplayShaderWarmup.WarmUpIfNeeded();
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
            // Meta achievements (runs, Constellation, meta upgrades, daily bonus, unlocked abilities).
            builder.RegisterEntryPoint<MetaAchievementSync>().AsSelf();
            builder.Register<AudioManager>(Lifetime.Singleton);
            builder.Register<AdManager>(Lifetime.Singleton);
            builder.Register<LocalizationService>(Lifetime.Singleton);

            // Sprint 14.1: adaptive foundation — layout mode (Portrait/Landscape), input mode and build-profile orientation.
            builder.RegisterEntryPoint<LayoutModeService>().AsSelf();
            builder.Register<InputModeService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<OrientationPolicyService>().AsSelf();
            builder.RegisterEntryPoint<WebBrowserLifecycle>().AsSelf();

            // Sprint 14.3: multi-input config and project-wide InputActionAsset service.
            InputConfig inputConfig = Resources.Load<InputConfig>(InputConfig.RESOURCE_PATH);
            if (inputConfig == null) inputConfig = InputConfig.CreateRuntimeDefault();
            builder.RegisterInstance(inputConfig);
            builder.Register<GameInputService>(Lifetime.Singleton);

            // Sprint 14.2: short-side camera framing, frustum spawn ring and HUD compass margins.
            CameraConfig cameraConfig = Resources.Load<CameraConfig>(CameraConfig.RESOURCE_PATH);
            if (cameraConfig == null) cameraConfig = CameraConfig.CreateRuntimeDefault();
            builder.RegisterInstance(cameraConfig);

            // Eagerly construct LocalizationService first so Current is bound to SaveManager before any UI reads or subscribes.
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<LocalizationService>();
                container.Resolve<TalentTreeManager>();
                // Start tracking the active device from the very first frame, before any UI asks for it.
                container.Resolve<InputModeService>();
                container.Resolve<GameInputService>();
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
