using UnityEngine;

namespace ProjectB.UI
{
    /// <summary>
    /// Fade + slide-in played automatically whenever a screen / tab / modal becomes active (Sprint 13.1, 0.2s,
    /// unscaled time). Slides the optional <see cref="slideTarget"/> (defaults to this rect) from an offset back to
    /// the position it had when the transition started, so it cooperates with layout presets.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenTransition : MonoBehaviour
    {
        [SerializeField] private RectTransform slideTarget;
        [SerializeField] private Vector2 slideOffset = new Vector2(0f, -48f);
        [SerializeField] private bool popScale;
        [SerializeField, Min(0f)] private float durationOverride;

        private CanvasGroup group;
        private Vector2 restPosition;
        private bool hasRestPosition;

        public CanvasGroup Group => group != null ? group : (group = GetComponent<CanvasGroup>());

        private void OnEnable()
        {
            PlayIn();
        }

        private void OnDisable()
        {
            UiTween.Kill(Group, complete: true);
            if (slideTarget != null) UiTween.Kill(slideTarget, complete: true);
            UiTween.Kill(transform, complete: true);
            if (hasRestPosition && slideTarget != null) slideTarget.anchoredPosition = restPosition;
            Group.alpha = 1f;
        }

        public void PlayIn()
        {
            float duration = durationOverride > 0f ? durationOverride : UiTheme.Current.durationNormal;
            CanvasGroup g = Group;
            if (!Application.isPlaying)
            {
                g.alpha = 1f;
                return;
            }

            UiTween.Fade(g, 0f, 1f, duration);

            if (slideTarget != null && slideOffset != Vector2.zero)
            {
                if (!UiTween.IsTweening(slideTarget) || !hasRestPosition)
                {
                    restPosition = slideTarget.anchoredPosition;
                    hasRestPosition = true;
                }
                UiTween.Move(slideTarget, restPosition + slideOffset, restPosition, duration, 0f, UiEase.OutCubic);
            }

            if (popScale)
            {
                UiTween.Scale(transform, Vector3.one * 0.92f, Vector3.one, duration * 1.4f, 0f, UiEase.OutBack);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform slide, Vector2 offset, bool pop = false)
        {
            slideTarget = slide;
            slideOffset = offset;
            popScale = pop;
        }
#endif
    }
}
