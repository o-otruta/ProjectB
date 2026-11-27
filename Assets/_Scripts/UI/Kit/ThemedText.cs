using UnityEngine;
using TMPro;

namespace ProjectB.UI
{
    /// <summary>
    /// Binds a TMP text to a <see cref="UiTextStyle"/> of <see cref="UiTheme"/>: font, material preset
    /// (Title / Number / Body) and size. Auto-sized texts keep their auto-size and get the style size as the maximum.
    /// Applied on Awake and by the editor <c>Apply Theme</c> tool.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ThemedText : MonoBehaviour
    {
        [SerializeField] private UiTextStyle style = UiTextStyle.Body;
        [SerializeField] private bool applySize = true;
        [SerializeField, Min(0.1f)] private float sizeScale = 1f;
        [SerializeField] private bool useHeavyFlat;

        public UiTextStyle Style => style;

        private void Awake()
        {
            ApplyTheme(UiTheme.Current);
        }

        public void Configure(UiTextStyle textStyle, float scale = 1f, bool setSize = true, bool heavyFlat = false)
        {
            style = textStyle;
            sizeScale = scale;
            applySize = setSize;
            useHeavyFlat = heavyFlat;
            ApplyTheme(UiTheme.Current);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            var text = GetComponent<TMP_Text>();
            if (text == null) return;

            TMP_FontAsset font = theme.GetFont(style);
            bool skipOutline = (style == UiTextStyle.Button || style == UiTextStyle.Display)
                && GetComponentInParent<ThemedButton>(true) is ThemedButton parentBtn
                && parentBtn.Style == ButtonStyle.Primary
                && parentBtn.Label == text;
            bool flatHeavy = useHeavyFlat || skipOutline;
            if (flatHeavy && theme.bodyBoldFont != null)
            {
                font = theme.bodyBoldFont;
            }
            if (font != null && text.font != font) text.font = font;

            Material preset = theme.GetMaterial(style);
            if (flatHeavy)
            {
                text.fontSharedMaterial = (theme.heavyFlatMaterial != null && theme.bodyBoldFont != null)
                    ? theme.heavyFlatMaterial
                    : (font != null ? font.material : text.fontSharedMaterial);
            }
            else if (preset != null && font != null && preset.mainTexture == font.atlasTexture)
            {
                text.fontSharedMaterial = preset;
            }
            else if (font != null)
            {
                text.fontSharedMaterial = font.material;
            }

            if (!applySize) return;
            float size = theme.GetFontSize(style) * sizeScale;
            if (text.enableAutoSizing)
            {
                text.fontSizeMax = size;
                text.fontSizeMin = Mathf.Min(text.fontSizeMin, size * 0.6f);
            }
            else
            {
                text.fontSize = size;
            }
        }
    }
}
