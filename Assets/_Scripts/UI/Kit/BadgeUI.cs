using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectB.UI
{
    /// <summary>
    /// Round notification badge with a number ("3") or a plain dot; gently pulses while visible (unscaled time).
    /// <c>SetCount(0)</c> hides it, <c>SetDot(true)</c> shows a dot without a number.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BadgeUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private UiColor color = UiColor.Danger;
        [SerializeField] private bool pulse = true;
        [SerializeField, Min(0f)] private float pulseAmplitude = 0.08f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 4f;

        private readonly char[] buffer = new char[4];
        private int count;
        private Vector3 restScale = Vector3.one;

        public int Count => count;
        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            restScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            ApplyTheme(UiTheme.Current);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            if (background != null)
            {
                background.color = theme.GetColor(color);
                if (theme.circleSprite != null) background.sprite = theme.circleSprite;
            }
            if (countText != null) countText.color = theme.white;
        }

        public void SetCount(int value)
        {
            count = Mathf.Max(0, value);
            bool visible = count > 0;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible || countText == null) return;

            countText.gameObject.SetActive(true);
            int len;
            if (count > 9)
            {
                buffer[0] = '9';
                buffer[1] = '+';
                len = 2;
            }
            else
            {
                len = UiFormat.WriteInt(buffer, 0, count);
            }
            countText.SetCharArray(buffer, 0, len);
        }

        /// <summary>Dot badge with an exclamation mark ("!"), e.g. "reward available".</summary>
        public void SetDot(bool visible)
        {
            count = visible ? 1 : 0;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (countText != null)
            {
                countText.gameObject.SetActive(visible);
                if (visible) countText.text = "!";
            }
        }

        private void Update()
        {
            if (!pulse) return;
            float s = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
            transform.localScale = restScale * s;
        }

#if UNITY_EDITOR
        public void EditorSetup(Image bg, TMP_Text text, UiColor badgeColor = UiColor.Danger)
        {
            background = bg;
            countText = text;
            color = badgeColor;
            ApplyTheme(UiTheme.Current);
        }
#endif
    }
}
