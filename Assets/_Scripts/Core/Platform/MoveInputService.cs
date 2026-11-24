using UnityEngine;
using VContainer;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.Core
{
    /// <summary>
    /// Aggregates movement input from WASD / arrow keys / gamepad left stick (<see cref="GameInputService"/>)
    /// and the on-screen <see cref="VirtualJoystick"/> with radial deadzone and unit-circle clamping (Zero-GC, Sprint 14.3).
    /// </summary>
    public sealed class MoveInputService
    {
        private readonly GameInputService gameInput;
        private readonly VirtualJoystick virtualJoystick;
        private readonly float deadzone;

        [Inject]
        public MoveInputService(
            GameInputService gameInput = null,
            VirtualJoystick virtualJoystick = null,
            InputConfig config = null)
        {
            this.gameInput = gameInput;
            this.virtualJoystick = virtualJoystick;
            deadzone = config != null ? Mathf.Clamp(config.moveDeadzone, 0f, 0.5f) : 0.15f;
        }

        public Vector2 Direction
        {
            get
            {
                Vector2 actionRaw = gameInput != null ? gameInput.ReadMove() : Vector2.zero;
                Vector2 joyRaw = virtualJoystick != null ? virtualJoystick.Direction : Vector2.zero;
                return Aggregate(actionRaw, joyRaw, deadzone);
            }
        }

        public static Vector2 Aggregate(Vector2 actionInput, Vector2 joystickInput, float deadzone)
        {
            Vector2 actionClean = ApplyDeadzoneAndClamp(actionInput, deadzone);
            Vector2 joyClean = ClampUnit(joystickInput);

            bool hasAction = actionClean.sqrMagnitude > 1e-6f;
            bool hasJoy = joyClean.sqrMagnitude > 1e-6f;

            if (!hasAction) return joyClean;
            if (!hasJoy) return actionClean;

            Vector2 combined = actionClean + joyClean;
            return ClampUnit(combined);
        }

        private static Vector2 ApplyDeadzoneAndClamp(Vector2 raw, float deadzone)
        {
            float sqrMag = raw.sqrMagnitude;
            if (sqrMag <= deadzone * deadzone) return Vector2.zero;

            float mag = Mathf.Sqrt(sqrMag);
            if (mag <= 1e-5f) return Vector2.zero;

            float denom = Mathf.Max(0.0001f, 1f - deadzone);
            float scaled = Mathf.Clamp01((mag - deadzone) / denom);
            float factor = scaled / mag;
            return new Vector2(raw.x * factor, raw.y * factor);
        }

        private static Vector2 ClampUnit(Vector2 v)
        {
            float sqrMag = v.sqrMagnitude;
            if (sqrMag <= 1f) return v;
            float invMag = 1f / Mathf.Sqrt(sqrMag);
            return new Vector2(v.x * invMag, v.y * invMag);
        }
    }
}
