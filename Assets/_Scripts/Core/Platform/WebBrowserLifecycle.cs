using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using ProjectB.Meta;

namespace ProjectB.Core
{
    /// <summary>
    /// Manages browser tab visibility/blur auto-pause, dirty save flush, and WebAudio unlock on first user gesture.
    /// Registered as a singleton entry point in <see cref="RootLifetimeScope"/>.
    /// </summary>
    public sealed class WebBrowserLifecycle : IInitializable, IDisposable
    {
        public const string HOST_OBJECT_NAME = "[WebBrowserLifecycle]";

        private readonly AudioManager audioManager;
        private readonly SaveManager saveManager;
        private GameObject hostObject;
        private WebBrowserLifecycleHost hostComponent;

        [Inject]
        public WebBrowserLifecycle(AudioManager audioManager = null, SaveManager saveManager = null)
        {
            this.audioManager = audioManager;
            this.saveManager = saveManager;
        }

        public void Initialize()
        {
            if (Application.isPlaying && hostObject == null)
            {
                hostObject = new GameObject(HOST_OBJECT_NAME);
                UnityEngine.Object.DontDestroyOnLoad(hostObject);
                hostComponent = hostObject.AddComponent<WebBrowserLifecycleHost>();
                hostComponent.Bind(this);
            }

            WebBrowserBridge.RegisterBrowserHooks(HOST_OBJECT_NAME);
        }

        public void HandleUserGesture()
        {
            audioManager?.NotifyUserGestureUnlocked();
        }

        public void HandleVisibilityChanged(bool isHidden, GameManager explicitGameManager = null)
        {
            if (!isHidden) return;

            saveManager?.SaveIfDirty();

            GameManager gm = explicitGameManager != null
                ? explicitGameManager
                : (Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<GameManager>() : null);
            gm?.HandleApplicationBackgrounded();
        }

        internal void PollUnlockInput()
        {
            if (audioManager == null || audioManager.IsAudioUnlocked) return;

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

            if (pressed)
            {
                HandleUserGesture();
            }
        }

        public void Dispose()
        {
            if (hostObject != null)
            {
                UnityEngine.Object.Destroy(hostObject);
                hostObject = null;
                hostComponent = null;
            }
        }
    }

    public sealed class WebBrowserLifecycleHost : MonoBehaviour
    {
        private WebBrowserLifecycle owner;

        public void Bind(WebBrowserLifecycle lifecycle)
        {
            owner = lifecycle;
        }

        private void Update()
        {
            owner?.PollUnlockInput();
        }

        public void OnBrowserVisibilityChanged(string hiddenFlag)
        {
            owner?.HandleVisibilityChanged(hiddenFlag == "1");
        }

        public void OnBrowserUserGesture(string _)
        {
            owner?.HandleUserGesture();
        }
    }
}
