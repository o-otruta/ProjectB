using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectB.UI
{
    public enum ProgressLabelFormat
    {
        None = 0,
        Ratio = 1,     // "176 / 200"
        Percent = 2,   // "88%"
        Current = 3    // "176" (compact bars, e.g. HP under the hero)
    }

    /// <summary>
    /// Themed progress bar without a <c>Slider</c> (Sprint 13.1): a filled <see cref="Image"/> plus an optional
    /// lagging "ghost" fill that shows recently lost value (HP damage trail). Optional zero-GC label.
    /// Ghost animation runs on unscaled time and only while the ghost lags behind.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Image ghostImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private ProgressLabelFormat labelFormat = ProgressLabelFormat.Ratio;
        [SerializeField] private UiColor fillColor = UiColor.HpFill;
        [SerializeField, Min(0f)] private float ghostDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float ghostSpeed = 0.9f;
        [SerializeField, Min(0f)] private float fillSmoothing = 0f;

        [Header("Split-colour label (optional)")]
        [Tooltip("Mask that shows the normal label only over the empty part of the bar.")]
        [SerializeField] private RectMask2D labelMask;
        [Tooltip("Dark copy of the label, shown only over the filled part (inverted look).")]
        [SerializeField] private TMP_Text invertedLabel;
        [SerializeField] private RectMask2D invertedMask;
        [SerializeField] private UiColor invertedLabelColor = UiColor.Background;
        [Tooltip("Inset of the fill inside the bar frame (px), so the colour split matches the fill edge.")]
        [SerializeField, Min(0f)] private float fillInset = 4f;
        [SerializeField] private RectTransform labelRoot;

        private readonly char[] buffer = new char[32];
        private float targetNormalized;
        private float fillNormalized;
        private float ghostNormalized;
        private float ghostWait;
        private int lastCurrent = int.MinValue;
        private int lastMax = int.MinValue;

        public float Normalized => targetNormalized;
        public float GhostNormalized => ghostNormalized;
        public Image FillImage => fillImage;
        public TMP_Text Label => label;
        public TMP_Text InvertedLabel => invertedLabel;
        public bool HasSplitLabel => invertedLabel != null && invertedMask != null;

        private void Awake()
        {
            ApplyTheme(UiTheme.Current);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            if (fillImage != null) fillImage.color = theme.GetColor(fillColor);
            if (ghostImage != null) ghostImage.color = UiTheme.WithAlpha(theme.ghost, 0.85f);
            if (invertedLabel != null) invertedLabel.color = theme.GetColor(invertedLabelColor);
        }

        /// <summary>Writes the label (and its inverted copy) without allocations.</summary>
        public void SetLabel(char[] chars, int length)
        {
            if (label != null) label.SetCharArray(chars, 0, length);
            if (invertedLabel != null) invertedLabel.SetCharArray(chars, 0, length);
        }

        /// <summary>Punches the whole label (both colour halves together).</summary>
        public void PunchLabel(float strength = 0.3f, float duration = 0.3f)
        {
            Transform target = labelRoot != null ? labelRoot : (label != null ? label.transform : null);
            if (target != null) UiTween.Punch(target, strength, duration);
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateSplit();
        }

        public void SetFillColor(UiColor token)
        {
            fillColor = token;
            if (fillImage != null) fillImage.color = UiTheme.Current.GetColor(token);
        }

        public void SetValue(int current, int max, bool instant = false)
        {
            float n = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            SetNormalized(n, instant);

            if (label == null || labelFormat == ProgressLabelFormat.None) return;
            if (current == lastCurrent && max == lastMax) return;
            lastCurrent = current;
            lastMax = max;

            int len;
            if (labelFormat == ProgressLabelFormat.Percent)
            {
                len = UiFormat.WriteInt(buffer, 0, Mathf.RoundToInt(n * 100f));
                buffer[len++] = '%';
            }
            else if (labelFormat == ProgressLabelFormat.Current)
            {
                len = UiFormat.WriteInt(buffer, 0, Mathf.Max(0, current));
            }
            else
            {
                len = UiFormat.WriteInt(buffer, 0, Mathf.Max(0, current));
                buffer[len++] = ' ';
                buffer[len++] = '/';
                buffer[len++] = ' ';
                len = UiFormat.WriteInt(buffer, len, Mathf.Max(0, max));
            }
            SetLabel(buffer, len);
        }

        public void SetNormalized(float normalized, bool instant = false)
        {
            normalized = Mathf.Clamp01(normalized);
            float previous = targetNormalized;
            targetNormalized = normalized;

            if (instant || !Application.isPlaying || !isActiveAndEnabled)
            {
                fillNormalized = normalized;
                ghostNormalized = normalized;
                ghostWait = 0f;
                Apply();
                return;
            }

            if (normalized < previous)
            {
                // Loss: fill drops now, ghost trails behind after a short delay.
                if (ghostNormalized < previous) ghostNormalized = previous;
                ghostWait = ghostDelay;
            }
            else if (ghostNormalized < normalized)
            {
                ghostNormalized = normalized;
            }

            if (fillSmoothing <= 0f) fillNormalized = normalized;
            Apply();
        }

        private void Update()
        {
            bool fillDirty = !Mathf.Approximately(fillNormalized, targetNormalized);
            bool ghostDirty = ghostImage != null && ghostNormalized > targetNormalized + 0.0005f;
            if (!fillDirty && !ghostDirty) return;

            float dt = Time.unscaledDeltaTime;
            if (fillDirty)
            {
                fillNormalized = fillSmoothing > 0f
                    ? Mathf.MoveTowards(fillNormalized, targetNormalized, dt / fillSmoothing)
                    : targetNormalized;
            }

            if (ghostDirty)
            {
                if (ghostWait > 0f) ghostWait -= dt;
                else ghostNormalized = Mathf.MoveTowards(ghostNormalized, targetNormalized, ghostSpeed * dt);
            }
            else
            {
                ghostNormalized = targetNormalized;
            }

            Apply();
        }

        private void Apply()
        {
            if (fillImage != null) fillImage.fillAmount = fillNormalized;
            if (ghostImage != null) ghostImage.fillAmount = Mathf.Max(ghostNormalized, fillNormalized);
            UpdateSplit();
        }

        /// <summary>Clips the dark label to the filled part and the normal label to the empty part.</summary>
        private void UpdateSplit()
        {
            if (invertedMask == null) return;
            float width = ((RectTransform)transform).rect.width;
            if (width <= 0f) return;
            float edge = fillInset + fillNormalized * Mathf.Max(0f, width - fillInset * 2f);
            invertedMask.padding = new Vector4(0f, 0f, Mathf.Max(0f, width - edge), 0f);
            if (labelMask != null) labelMask.padding = new Vector4(edge, 0f, 0f, 0f);
        }

#if UNITY_EDITOR
        public void EditorSetupSplitLabel(RectTransform root, RectMask2D normalMask, TMP_Text inverted, RectMask2D inverseMask, float inset, UiColor invertedColor = UiColor.Background)
        {
            labelRoot = root;
            labelMask = normalMask;
            invertedLabel = inverted;
            invertedMask = inverseMask;
            fillInset = inset;
            invertedLabelColor = invertedColor;
            ApplyTheme(UiTheme.Current);
            UpdateSplit();
        }

        public void EditorSetup(Image fill, Image ghost, TMP_Text text, ProgressLabelFormat format, UiColor color)
        {
            fillImage = fill;
            ghostImage = ghost;
            label = text;
            labelFormat = format;
            fillColor = color;
            ApplyTheme(UiTheme.Current);
        }
#endif
    }
}
