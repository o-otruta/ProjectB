using System;
using ProjectB.Core;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>Implemented by every adaptive UI component; lets the editor preview menu apply a mode to the whole scene.</summary>
    public interface ILayoutModeConsumer
    {
        void ApplyLayoutMode(LayoutMode mode);
    }

    /// <summary>
    /// Subscription helper composed by adaptive UI components (they can't share a base class: some derive from LayoutGroup).
    /// Resolves <see cref="LayoutModeService"/> lazily from the root scope in Play Mode; in Edit Mode it stays unbound
    /// and the component keeps its serialized state. Handlers are passed once and cached (Zero-GC).
    /// </summary>
    public sealed class LayoutModeBinding
    {
        private readonly Action<LayoutMode> onModeChanged;
        private readonly Action onScreenChanged;
        private LayoutModeService service;
        private bool subscribed;

        public LayoutModeService Service => service;
        public bool IsBound => service != null;

        public LayoutModeBinding(Action<LayoutMode> onModeChanged, Action onScreenChanged = null)
        {
            this.onModeChanged = onModeChanged;
            this.onScreenChanged = onScreenChanged;
        }

        /// <summary>Explicit binding (DI or tests). Keeps the subscription state.</summary>
        public void Bind(LayoutModeService newService)
        {
            if (ReferenceEquals(newService, service)) return;
            bool wasSubscribed = subscribed;
            Unsubscribe();
            service = newService;
            if (wasSubscribed) Subscribe();
        }

        /// <summary>Subscribes (resolving the service if needed). Returns true when a service is available.</summary>
        public bool Enable()
        {
            if (service == null)
            {
                RootServices.TryResolve(out service);
            }
            Subscribe();
            return service != null;
        }

        public void Disable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || service == null) return;
            if (onModeChanged != null) service.LayoutModeChanged += onModeChanged;
            if (onScreenChanged != null) service.ScreenChanged += onScreenChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || service == null) return;
            if (onModeChanged != null) service.LayoutModeChanged -= onModeChanged;
            if (onScreenChanged != null) service.ScreenChanged -= onScreenChanged;
            subscribed = false;
        }
    }
}
