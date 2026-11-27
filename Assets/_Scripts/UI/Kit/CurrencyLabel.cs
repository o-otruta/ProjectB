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

        private readonly char[] buffer = new char[24];
        private int targetValue;
        private float displayedValue;
        private float startValue;
        private float elapsed;
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
            if (hasValue && value == targetValue && !animating) return;

            bool changed = hasValue && value != targetValue;
            targetValue = value;

            if (!hasValue || !animate || countUpDuration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                hasValue = true;
                animating = false;
                displayedValue = value;
                Write(value);
                return;
            }

            startValue = displayedValue;
            elapsed = 0f;
            animating = true;
            if (changed && punchOnChange) UiTween.Punch(transform, 0.1f, 0.25f);
        }

        private void OnDisable()
        {
            if (animating)
            {
                animating = false;
                displayedValue = targetValue;
                Write(targetValue);
            }
        }

        private void Update()
        {
            if (!animating) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / countUpDuration);
            displayedValue = Mathf.Lerp(startValue, targetValue, UiTween.Evaluate(UiEase.OutCubic, t));
            Write(Mathf.RoundToInt(displayedValue));
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
