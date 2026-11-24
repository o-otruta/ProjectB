using UnityEngine;

namespace ProjectB.Data.Platform
{
    /// <summary>
    /// Input parameters for multi-input aggregation, joystick behavior, and button hover polish (Sprint 14.3).
    /// </summary>
    [CreateAssetMenu(fileName = "InputConfig", menuName = "ProjectB/Platform/Input Config")]
    public class InputConfig : ScriptableObject
    {
        public const string RESOURCE_PATH = "InputConfig";

        [Header("Movement Aggregation")]
        [Range(0f, 0.5f)] public float moveDeadzone = 0.15f;

        [Header("Virtual Joystick")]
        public bool floatingJoystickInLandscape = true;

        [Header("UI Hover / Focus")]
        [Range(1f, 1.15f)] public float buttonHoverScale = 1.04f;

        [Header("LevelUp Input Guard")]
        [Tooltip("Unscaled delay in seconds after showing or rerolling LevelUp cards before selection/reroll input is accepted.")]
        [Range(0f, 2f)] public float cardSelectInputDelay = 0.4f;

        /// <summary>Hidden in-memory instance with default values, used when the Resources asset is missing.</summary>
        public static InputConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<InputConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }

        private void OnValidate()
        {
            moveDeadzone = Mathf.Clamp(moveDeadzone, 0f, 0.5f);
            buttonHoverScale = Mathf.Clamp(buttonHoverScale, 1f, 1.15f);
            cardSelectInputDelay = Mathf.Clamp(cardSelectInputDelay, 0f, 2f);
        }
    }
}
