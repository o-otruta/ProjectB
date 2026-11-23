using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using VContainer;

namespace ProjectB.Core
{
    /// <summary>Which kind of device the player is currently using. Drives joystick visibility and key hints (Sprint 14.3).</summary>
    public enum InputMode
    {
        Touch = 0,
        KeyboardMouse = 1,
        Gamepad = 2
    }

    /// <summary>
    /// Tracks the last actively used input device family (Singleton in <see cref="RootLifetimeScope"/>, Sprint 14.1).
    /// Listens to <c>InputSystem.onEvent</c> with a cached delegate and struct enumerators only (Zero-GC).
    /// Filters noise: keyboard needs a key press, gamepad needs ≥ 50% actuation (stick drift is ignored), and mouse
    /// events shortly after a touch are ignored (mobile browsers synthesize mouse events from touches).
    /// </summary>
    public sealed class InputModeService : IDisposable
    {
        public const float GAMEPAD_ACTUATION_THRESHOLD = 0.5f;
        public const double MOUSE_AFTER_TOUCH_SUPPRESS_SECONDS = 0.5;

        private readonly Action<InputEventPtr, InputDevice> onEventHandler;
        private bool subscribed;
        private double lastTouchTime = double.NegativeInfinity;

        public InputMode Mode { get; private set; }

        /// <summary>Raised when <see cref="Mode"/> changes. Handlers must be cached delegates (Zero-GC).</summary>
        public event Action<InputMode> InputModeChanged;

        [Inject]
        public InputModeService()
            : this(DetectInitialMode(), Application.isPlaying)
        {
        }

        public InputModeService(InputMode initialMode, bool listenToInputSystem)
        {
            Mode = initialMode;
            onEventHandler = HandleInputEvent;
            if (listenToInputSystem)
            {
                InputSystem.onEvent += onEventHandler;
                subscribed = true;
            }
        }

        public static InputMode DetectInitialMode()
        {
            if (Application.isMobilePlatform) return InputMode.Touch;
            if (Touchscreen.current != null && Mouse.current == null && Keyboard.current == null) return InputMode.Touch;
            return InputMode.KeyboardMouse;
        }

        /// <summary>
        /// Registers activity of a device family at <paramref name="time"/> (realtime seconds).
        /// Returns true if the mode changed. Public for tests and for the virtual joystick / on-screen controls.
        /// </summary>
        public bool RegisterActivity(InputMode candidate, double time)
        {
            if (candidate == InputMode.Touch)
            {
                lastTouchTime = time;
            }
            else if (candidate == InputMode.KeyboardMouse && Mode == InputMode.Touch &&
                     time - lastTouchTime < MOUSE_AFTER_TOUCH_SUPPRESS_SECONDS)
            {
                return false;
            }

            if (candidate == Mode) return false;

            Mode = candidate;
            InputModeChanged?.Invoke(candidate);
            return true;
        }

        private void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == null) return;
            FourCC type = eventPtr.type;
            if (type != StateEvent.Type && type != DeltaStateEvent.Type) return;

            double now = Time.realtimeSinceStartupAsDouble;

            if (device is Touchscreen || device is Pen)
            {
                RegisterActivity(InputMode.Touch, now);
                return;
            }

            if (device is Mouse)
            {
                if (Mode != InputMode.KeyboardMouse) RegisterActivity(InputMode.KeyboardMouse, now);
                return;
            }

            if (device is Keyboard)
            {
                if (Mode != InputMode.KeyboardMouse && eventPtr.HasButtonPress())
                {
                    RegisterActivity(InputMode.KeyboardMouse, now);
                }
                return;
            }

            if (device is Gamepad || device is Joystick)
            {
                if (Mode != InputMode.Gamepad &&
                    eventPtr.GetFirstButtonPressOrNull(GAMEPAD_ACTUATION_THRESHOLD, buttonControlsOnly: false) != null)
                {
                    RegisterActivity(InputMode.Gamepad, now);
                }
            }
        }

        public void Dispose()
        {
            if (!subscribed) return;
            InputSystem.onEvent -= onEventHandler;
            subscribed = false;
        }
    }
}
