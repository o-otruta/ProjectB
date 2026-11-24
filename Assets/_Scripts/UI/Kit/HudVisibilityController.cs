using UnityEngine;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    /// <summary>
    /// Modal rule of the HUD (Sprint 13.3): while the game is <c>Paused</c> / <c>LevelUp</c> / <c>GameOver</c> the
    /// gameplay HUD (bars, slots, combo, compass, ultimate, joystick) fades out through <see cref="CanvasGroup.alpha"/>
    /// in 0.15s and stops catching raycasts — the same <see cref="GameState"/> gating as the floating damage numbers
    /// in <c>VfxFeedbackManager.LateUpdate</c>. Polls one enum per frame, no allocations.
    /// </summary>
    public sealed class HudVisibilityController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] groups;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;

        private GameManager gameManager;
        private float visibility = 1f;
        private bool initialized;

        public bool IsHudVisible => visibility > 0.5f;
        public float Visibility => visibility;

        [Inject]
        public void Construct(GameManager gameManager)
        {
            this.gameManager = gameManager;
        }

        public void SetGameManager(GameManager manager)
        {
            gameManager = manager;
        }

        public static bool ShouldShowHud(GameState state)
        {
            return state == GameState.Playing;
        }

        private void LateUpdate()
        {
            bool show = gameManager == null || ShouldShowHud(gameManager.CurrentState);
            float target = show ? 1f : 0f;
            if (initialized && Mathf.Approximately(visibility, target)) return;

            visibility = initialized
                ? Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime / fadeDuration)
                : target;
            initialized = true;
            Apply();
        }

        /// <summary>Immediately applies the visibility for <paramref name="state"/> (tests / scene setup).</summary>
        public void ForceState(GameState state)
        {
            visibility = ShouldShowHud(state) ? 1f : 0f;
            initialized = true;
            Apply();
        }

        private void Apply()
        {
            if (groups == null) return;
            bool interactive = visibility > 0.99f;
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup g = groups[i];
                if (g == null) continue;
                g.alpha = visibility;
                g.blocksRaycasts = interactive;
                g.interactable = interactive;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(CanvasGroup[] hudGroups)
        {
            groups = hudGroups;
        }
#endif
    }
}
