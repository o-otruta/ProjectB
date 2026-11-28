using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>
    /// Onboarding pointer for menus: highlights one target and places one hint bubble next to it (to the right of a
    /// left-side vertical nav, above targets in the lower half, below otherwise).
    /// Tabs get the authored pulsing glow inside them; buttons pulse themselves via <see cref="PulseUI"/> (the soft
    /// kit_glow sprite would stay hidden behind a button). Plain helper owned by a screen; <see cref="LateTick"/>
    /// keeps the bubble attached while layouts or scroll positions change. Zero-GC per frame.
    /// </summary>
    public sealed class FtueSpotlight
    {
        private const float BUBBLE_GAP = 8f;
        private const float SCREEN_MARGIN = 24f;

        private readonly GameObject glow;
        private readonly RectTransform glowRect;
        private readonly Vector2 glowDefaultSize;
        private readonly RectTransform bubble;
        private readonly RectTransform bubbleParent;
        private readonly TMP_Text bubbleText;

        private readonly FtueHintBubble bubbleHint;

        private RectTransform target;
        private PulseUI targetPulse;
        private bool targetPulseWasEnabled;
        private bool showBubble;
        private readonly Vector3[] corners = new Vector3[4];

        public RectTransform Target => target;
        public bool IsVisible => target != null;
        public string Text => bubbleText != null ? bubbleText.text : null;

        public FtueSpotlight(GameObject glow, GameObject bubble, TMP_Text bubbleText)
        {
            this.glow = glow;
            glowRect = glow != null ? glow.transform as RectTransform : null;
            glowDefaultSize = glowRect != null ? glowRect.sizeDelta : new Vector2(170f, 170f);
            this.bubble = bubble != null ? bubble.transform as RectTransform : null;
            bubbleParent = this.bubble != null ? this.bubble.parent as RectTransform : null;
            this.bubbleText = bubbleText;
            bubbleHint = FtueHintBubble.Ensure(bubble, bubbleText);

            // The bubble is positioned next to arbitrary targets, so its per-layout preset must not snap it back.
            var responsive = bubble != null ? bubble.GetComponent<ResponsiveRect>() : null;
            if (responsive != null) responsive.enabled = false;
        }

        /// <param name="text">Bubble text; null or empty shows only the highlight (for self-explanatory buttons).</param>
        /// <param name="isTab">Bottom-bar tab: show the authored glow inside it. Otherwise the target itself pulses.</param>
        public void Show(RectTransform newTarget, string text, bool isTab = false)
        {
            if (newTarget == null)
            {
                Hide();
                return;
            }

            showBubble = !string.IsNullOrEmpty(text);
            if (showBubble && bubbleText != null && bubbleText.text != text)
            {
                bubbleText.text = text;
                bubbleHint?.Refit();
            }

            if (target != newTarget)
            {
                ReleaseTargetPulse();
                target = newTarget;

                if (isTab)
                {
                    if (glowRect != null)
                    {
                        glowRect.SetParent(target, false);
                        glowRect.SetSiblingIndex(target.childCount > 1 ? 1 : 0);
                        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
                        glowRect.pivot = new Vector2(0.5f, 0.5f);
                        glowRect.localScale = Vector3.one;
                        glowRect.sizeDelta = glowDefaultSize;
                        glowRect.anchoredPosition = target.rect.center;
                    }
                    if (glow != null && !glow.activeSelf) glow.SetActive(true);
                }
                else
                {
                    if (glow != null && glow.activeSelf) glow.SetActive(false);
                    AttachTargetPulse();
                }
            }

            LateTick();
        }

        public void Hide()
        {
            ReleaseTargetPulse();
            target = null;
            if (glow != null && glow.activeSelf) glow.SetActive(false);
            if (bubble != null && bubble.gameObject.activeSelf) bubble.gameObject.SetActive(false);
        }

        public void LateTick()
        {
            if (target == null || bubble == null) return;
            if (!showBubble || !target.gameObject.activeInHierarchy)
            {
                if (bubble.gameObject.activeSelf) bubble.gameObject.SetActive(false);
                return;
            }
            if (!bubble.gameObject.activeSelf) bubble.gameObject.SetActive(true);
            PositionBubble();
        }

        private void AttachTargetPulse()
        {
            if (target == null || !Application.isPlaying) return;
            if (!target.TryGetComponent(out targetPulse))
            {
                targetPulse = target.gameObject.AddComponent<PulseUI>();
                targetPulseWasEnabled = false;
            }
            else
            {
                targetPulseWasEnabled = targetPulse.enabled;
            }
            targetPulse.enabled = true;
        }

        private void ReleaseTargetPulse()
        {
            if (targetPulse != null && !targetPulseWasEnabled) targetPulse.enabled = false;
            targetPulse = null;
        }

        private void PositionBubble()
        {
            if (bubbleParent == null) return;

            target.GetWorldCorners(corners);
            Vector3 min = bubbleParent.InverseTransformPoint(corners[0]);
            Vector3 max = bubbleParent.InverseTransformPoint(corners[2]);
            Rect parentRect = bubbleParent.rect;
            Vector2 center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            Vector2 half = bubble.rect.size * 0.5f;
            float gap = BUBBLE_GAP + (bubbleHint != null ? bubbleHint.PointerLength : 0f);

            float nx = parentRect.width > 0f ? (center.x - parentRect.xMin) / parentRect.width : 0.5f;
            float ny = parentRect.height > 0f ? (center.y - parentRect.yMin) / parentRect.height : 0.5f;

            Vector2 pos;
            FtuePointerSide side;
            if (nx < 0.18f)
            {
                pos = new Vector2(max.x + gap + half.x, center.y);
                side = FtuePointerSide.Left;
            }
            else if (ny < 0.5f)
            {
                pos = new Vector2(center.x, max.y + gap + half.y);
                side = FtuePointerSide.Down;
            }
            else
            {
                pos = new Vector2(center.x, min.y - gap - half.y);
                side = FtuePointerSide.Up;
            }
            bubbleHint?.SetPointer(side, target);

            float minX = parentRect.xMin + half.x + SCREEN_MARGIN;
            float maxX = Mathf.Max(minX, parentRect.xMax - half.x - SCREEN_MARGIN);
            float minY = parentRect.yMin + half.y + SCREEN_MARGIN;
            float maxY = Mathf.Max(minY, parentRect.yMax - half.y - SCREEN_MARGIN);
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = new Vector2(0.5f, 0.5f);
            bubble.anchoredPosition = pos - parentRect.center;
        }
    }
}
