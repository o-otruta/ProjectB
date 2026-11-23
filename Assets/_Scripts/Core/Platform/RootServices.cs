using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ProjectB.Core
{
    /// <summary>
    /// Resolves RootLifetimeScope singletons for scene components that are not registered in a scene scope —
    /// e.g. dozens of adaptive UI components or pooled/instantiated list items (Sprint 14.1).
    /// Play Mode only; call from OnEnable/Awake, never per frame.
    /// </summary>
    public static class RootServices
    {
        public static bool TryResolve<T>(out T service)
        {
            service = default;
            if (!Application.isPlaying) return false;

            VContainerSettings settings = VContainerSettings.Instance;
            if (settings == null || settings.RootLifetimeScope == null) return false;

            LifetimeScope root = settings.GetOrCreateRootLifetimeScopeInstance();
            IObjectResolver container = root != null ? root.Container : null;
            return container != null && container.TryResolve(out service);
        }
    }
}
