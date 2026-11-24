using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace ProjectB.UI
{
    /// <summary>
    /// One bottom-navigation tab: the active tab widens, raises and enlarges its icon, fades in its label and the
    /// accent plate (Sprint 13.2 keeps the original animation, now themed and on unscaled time).
    /// </summary>
    public class BottomTabUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform iconRect;
        public TextMeshProUGUI labelText;
        public Image bgImage;
        public LayoutElement layoutElement;
        public Button button;
        public Image iconImage;

        [Header("Settings")]
        public float activeWidth = 250f;
        public float inactiveWidth = 150f;
        public float activeIconY = 25f;
        public float inactiveIconY = 0f;
        public float activeIconScale = 1.15f;
        public float inactiveIconScale = 1.0f;
        public float animationDuration = 0.2f;

        private bool isActive = false;
        private bool hasState;
        private Coroutine animCoroutine;
        private Color plateColor = Color.white;
        private Color labelColor = Color.white;
        private Color iconActiveColor = Color.white;
        private Color iconInactiveColor = Color.white;

        public bool IsActive => isActive;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
            ApplyTheme(UiTheme.Current);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            plateColor = theme.accent;
            labelColor = theme.textPrimary;
            iconActiveColor = theme.white;
            iconInactiveColor = UiTheme.WithAlpha(theme.white, 0.7f);
            ApplyState(isActive ? 1f : 0f);
        }

        public void SetActiveState(bool state, bool instant = false)
        {
            if (hasState && isActive == state) return;
            hasState = true;
            isActive = state;

            if (animCoroutine != null)
            {
                StopCoroutine(animCoroutine);
                animCoroutine = null;
            }

            if (instant || !isActiveAndEnabled || !Application.isPlaying)
            {
                ApplyState(isActive ? 1f : 0f);
            }
            else
            {
                animCoroutine = StartCoroutine(AnimateTransition(isActive ? 1f : 0f));
            }
        }

        private IEnumerator AnimateTransition(float targetWeight)
        {
            float currentWidth = layoutElement != null ? layoutElement.preferredWidth : inactiveWidth;
            float startWeight = Mathf.Clamp01((currentWidth - inactiveWidth) / (activeWidth - inactiveWidth));
            if (float.IsNaN(startWeight)) startWeight = 0f;

            float time = 0f;
            while (time < animationDuration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / animationDuration);
                ApplyState(Mathf.Lerp(startWeight, targetWeight, t));
                yield return null;
            }
            ApplyState(targetWeight);
            animCoroutine = null;
        }

        private void ApplyState(float weight) // weight 0 = inactive, 1 = active
        {
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = Mathf.Lerp(inactiveWidth, activeWidth, weight);
                layoutElement.flexibleWidth = 1;
            }

            if (iconRect != null)
            {
                iconRect.anchoredPosition = new Vector2(iconRect.anchoredPosition.x, Mathf.Lerp(inactiveIconY, activeIconY, weight));
                float scale = Mathf.Lerp(inactiveIconScale, activeIconScale, weight);
                iconRect.localScale = new Vector3(scale, scale, 1f);
            }

            if (iconImage != null)
            {
                iconImage.color = Color.Lerp(iconInactiveColor, iconActiveColor, weight);
            }

            if (labelText != null)
            {
                Color c = labelColor;
                c.a = weight;
                labelText.color = c;
            }

            if (bgImage != null)
            {
                Color c = plateColor;
                c.a = weight;
                bgImage.color = c;
            }
        }
    }
}
