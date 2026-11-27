using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectB.UI
{
    public enum CurrencyType
    {
        Coins = 0,
        Shards = 1
    }

    /// <summary>
    /// Currency pill: icon + grouped amount ("14 702") with an animated count-up and a punch when the value changes.
    /// Zero-GC: digits are written into a cached char buffer and pushed with <c>SetCharArray</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrencyLabel : MonoBehaviour
    {
        [SerializeField] private CurrencyType currency = CurrencyType.Coins;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Image iconImage;
        [SerializeField] private string prefix = string.Empty;
        [SerializeField] private bool useWhiteText;
        [SerializeField, Min(0f)] private float countUpDuration = 0.6f;
        [SerializeField] private bool punchOnChange = true;
        [SerializeField] private bool pulseWhileCounting;
        [SerializeField, Range(0f, 0.45f)] private float pulseAmplitude = 0.24f;
        [SerializeField, Min(0.5f)] private float pulseCycles = 4f;

        private readonly char[] buffer = new char[24];
        private Vector3 amountRestScale = Vector3.one;
        private int targetValue;
        private float displayedValue;
        private float startValue;
        private float elapsed;
        private float startDelay;
        private float activeDuration;
        private float activePulseCycles = 3f;
        private bool animating;
        private bool hasValue;
        private int lastWrittenValue = int.MinValue;
        private int lastWrittenLength = -1;

        public int TargetValue => targetValue;
        public int DisplayedValue => Mathf.RoundToInt(displayedValue);
        public bool IsAnimating => animating;
        public CurrencyType Currency => currency;
        public TMP_Text AmountText => amountText;

        private void Awake()
        {
            ApplyTheme(UiTheme.Current);
            if (amountText != null && amountText.transform.localScale != Vector3.zero)
            {
                amountRestScale = amountText.transform.localScale;
            }
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            if (iconImage != null)
            {
                Sprite s = currency == CurrencyType.Coins ? theme.iconCoin : theme.iconShard;
                if (s != null) iconImage.sprite = s;
                iconImage.color = Color.white;
            }
            if (amountText != null)
            {
                amountText.color = useWhiteText
                    ? theme.textPrimary
                    : (currency == CurrencyType.Coins ? theme.gold : theme.shard);
            }
        }

        /// <summary>Sets the amount. <paramref name="animate"/> counts up/down from the currently displayed value.</summary>
        public void SetValue(int value, bool animate = true)
        {
            SetValue(value, animate, 0f);
        }

        /// <summary>Sets the amount with an optional unscaled start <paramref name="delay"/>.</summary>
        public void SetValue(int value, bool animate, float delay)
        {
            if (hasValue && value == targetValue && !animating) return;

            bool changed = hasValue && value != targetValue;
            targetValue = value;

            if (!hasValue || !animate || countUpDuration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                hasValue = true;
                animating = false;
                startDelay = 0f;
                displayedValue = value;
                Write(value);
                ResetAmountScale();
                return;
            }

            int delta = Mathf.Abs(value - Mathf.RoundToInt(displayedValue));
            activeDuration = delta <= 2 ? Mathf.Min(countUpDuration, 0.40f) : countUpDuration;
            activePulseCycles = delta <= 1 ? 1f : (delta == 2 ? 2f : pulseCycles);
            startValue = displayedValue;
            elapsed = 0f;
            startDelay = Mathf.Max(0f, delay);
            animating = true;

            if (pulseWhileCounting)
            {
                if (amountText != null)
                {
                    UiTween.Kill(amountText.transform, complete: false);
                    amountText.transform.localScale = amountRestScale;
                }
            }
            else if (changed && punchOnChange)
            {
                UiTween.Punch(transform, 0.1f, 0.25f);
            }
        }

        public void CompleteAnimation()
        {
            if (!animating) return;
            animating = false;
            startDelay = 0f;
            displayedValue = targetValue;
            Write(targetValue);
            ResetAmountScale();
        }

        private void ResetAmountScale()
        {
            if (amountText != null)
            {
                UiTween.Kill(amountText.transform, complete: false);
                amountText.transform.localScale = amountRestScale;
            }
        }

        private void OnDisable()
        {
            CompleteAnimation();
        }

        private void Update()
        {
            if (!animating) return;
            float dt = Time.unscaledDeltaTime;
            if (startDelay > 0f)
            {
                startDelay -= dt;
                if (startDelay > 0f) return;
                dt = -startDelay;
                startDelay = 0f;
            }

            elapsed += dt;
            float t = activeDuration > 0f ? Mathf.Clamp01(elapsed / activeDuration) : 1f;
            displayedValue = Mathf.Lerp(startValue, targetValue, UiTween.Evaluate(UiEase.OutCubic, t));
            Write(Mathf.RoundToInt(displayedValue));

            if (pulseWhileCounting && amountText != null && targetValue > startValue)
            {
                if (t < 1f)
                {
                    amountText.transform.localScale = amountRestScale * UiTween.EvaluateCountPulseScale(t, pulseAmplitude, activePulseCycles);
                }
                else
                {
                    amountText.transform.localScale = amountRestScale;
                    if (punchOnChange) UiTween.Punch(amountText.transform, 0.18f, 0.22f);
                }
            }

            if (t >= 1f) animating = false;
        }

        private void Write(int value)
        {
            if (amountText == null || value == lastWrittenValue) return;
            lastWrittenValue = value;
            int pos = UiFormat.WriteString(buffer, 0, prefix);
            pos = UiFormat.WriteGrouped(buffer, pos, value);
            amountText.SetCharArray(buffer, 0, pos);
            if (pos != lastWrittenLength)
            {
                lastWrittenLength = pos;
                if (transform is RectTransform rt) LayoutRebuilder.MarkLayoutForRebuild(rt);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(CurrencyType type, TMP_Text text, Image icon, string textPrefix = "")
        {
            currency = type;
            amountText = text;
            iconImage = icon;
            prefix = textPrefix;
            ApplyTheme(UiTheme.Current);
        }
#endif
    }
}
