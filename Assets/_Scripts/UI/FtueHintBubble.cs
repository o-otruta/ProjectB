using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>Edge of an onboarding bubble the pointer sticks out of.</summary>
    public enum FtuePointerSide
    {
        None = 0,
        Down = 1,
        Up = 2,
        Left = 3,
        Right = 4
    }

    /// <summary>
    /// Shared look of all onboarding hints (gameplay and menus): large fixed-size text, the pill grown to fit it
    /// (one line where the screen allows), and a small speech-bubble tail (<c>UiTheme.pointerSprite</c>, drawn with
    /// the pill's toy-surface material and colour). The tail tucks its open base under the pill's face so the two read
    /// as one shape, and slides along the edge towards an optional target. Added at runtime with <see cref="Ensure"/>;
    /// refits whenever the text or a layout preset changes the rect. Zero-GC per frame (preferred sizes are measured
    /// only when the text changes).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FtueHintBubble : MonoBehaviour
    {
        private static readonly Vector2 POINTER_SIZE = new Vector2(64f, 52f);
        // How far the tail base reaches inside each pill edge to meet the face (kit_pill: shadow + outline + ledge
        // at the bottom, pad + outline at the top, pad + outline at the sides; 1 sprite px = 1 UI unit).
        private const float BOTTOM_TUCK = 20f;
        private const float TOP_TUCK = 7f;
        private const float SIDE_TUCK = 9f;
        private const float EDGE_MARGIN = 32f;

        private TMP_Text text;
        private Image background;
        private RectTransform rect;
        private RectTransform pointer;
        private Image pointerImage;
        private RectTransform pointerTarget;
        private FtuePointerSide pointerSide;
        private float manualSlide;

        private float fontSize = 46f;
        private Vector2 padding = new Vector2(72f, 36f);
        private float maxWidth = 960f;

        private string fittedText;
        private Vector2 fittedSize;
        private float fittedParentWidth = -1f;
        private readonly Vector3[] corners = new Vector3[4];

        public RectTransform Rect => rect != null ? rect : rect = transform as RectTransform;
        public RectTransform Pointer => pointer;
        public FtuePointerSide PointerSide => pointerSide;
        /// <summary>How far the tail sticks out below the pill's visible outline (for spacing the bubble from its target).</summary>
        public float PointerLength => POINTER_SIZE.y - BOTTOM_TUCK + 6f;

        /// <summary>Adds (or returns) the bubble on <paramref name="root"/> and applies the onboarding text style.</summary>
        /// <param name="verticalPadding">Total top + bottom padding; tight layouts (LevelUp header) pass a smaller value.</param>
        public static FtueHintBubble Ensure(GameObject root, TMP_Text label = null, float fontSize = 0f, float maxWidth = 0f,
            float verticalPadding = 0f)
        {
            if (root == null) return null;
            if (!root.TryGetComponent(out FtueHintBubble bubble))
            {
                bubble = root.AddComponent<FtueHintBubble>();
            }
            bubble.Setup(label != null ? label : root.GetComponentInChildren<TMP_Text>(true), fontSize, maxWidth, verticalPadding);
            return bubble;
        }

        private void Setup(TMP_Text label, float size, float width, float verticalPadding)
        {
            rect = transform as RectTransform;
            text = label;
            background = GetComponent<Image>();
            UiTheme theme = UiTheme.Current;
            fontSize = size > 0f ? size : (theme != null ? Mathf.Max(theme.h2Size, 44f) + 2f : 46f);
            if (width > 0f) maxWidth = width;
            if (verticalPadding > 0f) padding.y = verticalPadding;

            if (text != null)
            {
                text.enableAutoSizing = false;
                text.fontSize = fontSize;
                // ThemedText re-applies its size on Awake (first activation of the hidden hint): keep it in sync.
                if (text.TryGetComponent(out ThemedText themed)) themed.SetFontSize(fontSize);
                text.textWrappingMode = TextWrappingModes.Normal;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                RectTransform textRect = text.rectTransform;
                if (textRect.parent == transform)
                {
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = padding * 0.5f;
                    textRect.offsetMax = -padding * 0.5f;
                }
            }

            // The pointer shares the pill colour, so the pill is made opaque to hide the overlapping half.
            if (background != null)
            {
                Color c = background.color;
                c.a = 1f;
                background.color = c;
                background.raycastTarget = false;
            }

            EnsurePointer();
            fittedText = null;
            Refit();
        }

        /// <summary>Shows the pointer on <paramref name="side"/>; with a target it slides along that edge towards it.</summary>
        public void SetPointer(FtuePointerSide side, RectTransform target = null)
        {
            pointerSide = side;
            pointerTarget = target;
            EnsurePointer();
            UpdatePointer();
        }

        /// <summary>
        /// Without a UI target: slides the pointer along its edge by <paramref name="offset"/> local units, e.g. when a
        /// bubble that follows a world object is clamped to the screen and its centre no longer sits over the object.
        /// </summary>
        public void SetPointerSlide(float offset)
        {
            manualSlide = offset;
        }

        /// <summary>Re-measures now (e.g. right before positioning the bubble next to its target).</summary>
        public void Refit()
        {
            if (text == null || Rect == null) return;

            fittedText = text.text;
            var parent = rect.parent as RectTransform;
            fittedParentWidth = parent != null ? parent.rect.width : -1f;

            if (string.IsNullOrEmpty(fittedText))
            {
                fittedSize = rect.sizeDelta;
                return;
            }

            float limit = fittedParentWidth > 0f ? Mathf.Min(maxWidth, fittedParentWidth - EDGE_MARGIN * 2f) : maxWidth;
            limit = Mathf.Max(limit, padding.x + fontSize * 4f);
            Vector2 oneLine = text.GetPreferredValues(fittedText, float.PositiveInfinity, float.PositiveInfinity);
            float width = Mathf.Min(oneLine.x + padding.x, limit);
            float textHeight = text.GetPreferredValues(fittedText, width - padding.x, float.PositiveInfinity).y;
            fittedSize = new Vector2(Mathf.Ceil(width), Mathf.Ceil(textHeight + padding.y));
            rect.sizeDelta = fittedSize;
        }

        private void LateUpdate()
        {
            if (text != null)
            {
                if (!Mathf.Approximately(text.fontSize, fontSize))
                {
                    text.enableAutoSizing = false;
                    text.fontSize = fontSize;
                    fittedText = null;
                }
                var parent = rect.parent as RectTransform;
                float parentWidth = parent != null ? parent.rect.width : -1f;
                if (!ReferenceEquals(text.text, fittedText) || rect.sizeDelta != fittedSize ||
                    !Mathf.Approximately(parentWidth, fittedParentWidth))
                {
                    Refit();
                }
            }
            UpdatePointer();
        }

        private void EnsurePointer()
        {
            if (pointer != null) return;

            // Runtime clones of a bubble (barrel / shard hints) already carry the copied pointer child.
            Transform existing = transform.Find("FtuePointer");
            if (existing != null)
            {
                pointer = (RectTransform)existing;
                pointerImage = existing.GetComponent<Image>();
                return;
            }

            var go = new GameObject("FtuePointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            pointer = (RectTransform)go.transform;
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.pivot = new Vector2(0.5f, 1f); // base of the tail; the tip points along -Y before rotation
            pointer.sizeDelta = POINTER_SIZE;
            pointerImage = go.GetComponent<Image>();
            pointerImage.raycastTarget = false;
            pointerImage.type = Image.Type.Simple;
            UiTheme theme = UiTheme.Current;
            if (theme != null && theme.pointerSprite != null) pointerImage.sprite = theme.pointerSprite;
            if (background != null && background.material != null) pointerImage.material = background.material;
            go.SetActive(false);
        }

        private void UpdatePointer()
        {
            if (pointer == null) return;

            bool visible = pointerSide != FtuePointerSide.None;
            if (pointer.gameObject.activeSelf != visible) pointer.gameObject.SetActive(visible);
            if (!visible) return;

            if (background != null && pointerImage.color != background.color) pointerImage.color = background.color;

            Rect r = Rect.rect;
            bool horizontalEdge = pointerSide == FtuePointerSide.Down || pointerSide == FtuePointerSide.Up;
            float slide = manualSlide;
            if (pointerTarget != null && pointerTarget.gameObject.activeInHierarchy)
            {
                pointerTarget.GetWorldCorners(corners);
                Vector3 targetCenter = rect.InverseTransformPoint((corners[0] + corners[2]) * 0.5f);
                slide = horizontalEdge ? targetCenter.x - r.center.x : targetCenter.y - r.center.y;
            }
            // Keep the tail off the pill's rounded corners.
            float halfEdge = (horizontalEdge ? r.width : r.height) * 0.5f;
            float slideLimit = Mathf.Max(0f, halfEdge - POINTER_SIZE.x * 0.5f - 30f);
            slide = Mathf.Clamp(slide, -slideLimit, slideLimit);

            Vector2 center = r.center;
            Vector2 basePoint;
            float angle;
            switch (pointerSide)
            {
                case FtuePointerSide.Down: basePoint = new Vector2(center.x + slide, r.yMin + BOTTOM_TUCK); angle = 0f; break;
                case FtuePointerSide.Up: basePoint = new Vector2(center.x + slide, r.yMax - TOP_TUCK); angle = 180f; break;
                case FtuePointerSide.Left: basePoint = new Vector2(r.xMin + SIDE_TUCK, center.y + slide); angle = -90f; break;
                default: basePoint = new Vector2(r.xMax - SIDE_TUCK, center.y + slide); angle = 90f; break;
            }

            // Anchored at the bubble's centre, so the offset is relative to rect.center in local space.
            pointer.anchoredPosition = basePoint - center;
            if (!Mathf.Approximately(pointer.localEulerAngles.z, angle < 0f ? angle + 360f : angle))
            {
                pointer.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
