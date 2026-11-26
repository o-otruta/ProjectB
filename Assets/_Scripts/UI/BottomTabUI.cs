using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using ProjectB.Data.Platform;

namespace ProjectB.UI
{
    /// <summary>
    /// One bottom-navigation tab: the active tab widens (or grows taller in Landscape left-rail mode),
    /// raises and enlarges its icon, fades in its label and the accent plate.
    /// </summary>
    public class BottomTabUI : MonoBehaviour, ILayoutModeConsumer
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

        [Header("Landscape Left Rail")]
        public float landscapeActiveHeight = 184f;
        public float landscapeInactiveHeight = 148f;
        public float landscapeActiveIconY = 18f;
        public float landscapeInactiveIconY = 4f;

        private bool isActive = false;
        private bool hasState;
        private float currentWeight;
        private LayoutMode currentMode = LayoutMode.Portrait;
        private LayoutModeBinding layoutBinding;
        private Coroutine animCoroutine;
        private Color plateColor = Color.white;
        private Color labelColor = Color.white;
        private Color iconActiveColor = Color.white;
        private Color iconInactiveColor = Color.white;

        public bool IsActive => isActive;
        public LayoutMode AppliedMode => currentMode;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
            ApplyTheme(UiTheme.Current);
        }

        private void OnEnable()
        {
            if (layoutBinding == null) layoutBinding = new LayoutModeBinding(ApplyLayoutMode);
            if (layoutBinding.Enable())
            {
                ApplyLayoutMode(layoutBinding.Service.Mode);
            }
        }

        private void OnDisable()
        {
            layoutBinding?.Disable();
        }

        public void ApplyLayoutMode(LayoutMode mode)
        {
            currentMode = mode;
            ApplyState(currentWeight);
        }

        public void ApplyTheme(UiTheme theme)
        {
            if (theme == null) return;
            plateColor = theme.gold;
            labelColor = theme.textDark;
            iconActiveColor = theme.white;
            iconInactiveColor = UiTheme.WithAlpha(theme.white, 0.78f);
            if (labelText != null && labelText.font != null)
            {
                labelText.fontSharedMaterial = labelText.font.material;
            }
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
            float startWeight = currentWeight;

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
            currentWeight = weight;
            if (layoutElement != null)
            {
                if (currentMode == LayoutMode.Landscape)
                {
                    layoutElement.preferredWidth = -1f;
                    layoutElement.flexibleWidth = 1f;
                    layoutElement.preferredHeight = Mathf.Lerp(landscapeInactiveHeight, landscapeActiveHeight, weight);
                    layoutElement.flexibleHeight = 1f;
                }
                else
                {
                    layoutElement.preferredWidth = Mathf.Lerp(inactiveWidth, activeWidth, weight);
                    layoutElement.flexibleWidth = 1f;
                    layoutElement.preferredHeight = -1f;
                    layoutElement.flexibleHeight = 0f;
                }
            }

            if (iconRect != null)
            {
                float targetY = currentMode == LayoutMode.Landscape
                    ? Mathf.Lerp(landscapeInactiveIconY, landscapeActiveIconY, weight)
                    : Mathf.Lerp(inactiveIconY, activeIconY, weight);
                iconRect.anchoredPosition = new Vector2(iconRect.anchoredPosition.x, targetY);
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
                float popTop = currentMode == LayoutMode.Portrait ? Mathf.Lerp(0f, 26f, weight) : 0f;
                Vector2 max = bgImage.rectTransform.offsetMax;
                if (!Mathf.Approximately(max.y, popTop))
                {
                    bgImage.rectTransform.offsetMax = new Vector2(max.x, popTop);
                }
            }
        }
    }
}
