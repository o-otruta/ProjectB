using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>
    /// Binds a <see cref="Graphic"/> (Image / TMP text) to a <see cref="UiColor"/> token and, for images, to a kit
    /// sprite of <see cref="UiTheme"/>. Applied once on Awake and by the editor <c>Apply Theme</c> tool, so a palette
    /// change recolours every screen without touching scenes by hand. Scripts that tint a graphic dynamically
    /// (rarity frames, states) should not carry this component on that graphic.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class ThemedGraphic : MonoBehaviour
    {
        [SerializeField] private UiColor color = UiColor.Surface;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;
        [SerializeField] private UiSprite sprite = UiSprite.None;

        public UiColor Color => color;
        public UiSprite Sprite => sprite;

        private void Awake()
        {
            ApplyTheme(UiTheme.Current);
        }

        public void Configure(UiColor token, float alphaValue = 1f, UiSprite spriteToken = UiSprite.None)
        {
            color = token;
            alpha = alphaValue;
            sprite = spriteToken;
            ApplyTheme(UiTheme.Current);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            var graphic = GetComponent<Graphic>();
            if (graphic == null) return;

            UnityEngine.Color c = theme.GetColor(color);
            c.a *= alpha;
            if (graphic.color != c) graphic.color = c;

            if (sprite != UiSprite.None && graphic is Image image)
            {
                Sprite s = theme.GetSprite(sprite);
                if (s != null)
                {
                    if (image.sprite != s) image.sprite = s;
                    if (image.type != Image.Type.Filled)
                    {
                        Image.Type targetType = s.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                        if (image.type != targetType) image.type = targetType;
                    }
                }
                if (UiTheme.UsesSurfaceMaterial(sprite) && alpha >= 0.85f && theme.surfaceMaterial != null)
                {
                    if (image.material != theme.surfaceMaterial) image.material = theme.surfaceMaterial;
                }
                else if (image.material == theme.surfaceMaterial)
                {
                    image.material = null;
                }
            }
        }
    }
}
