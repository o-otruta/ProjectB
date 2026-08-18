using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace ProjectB.UI
{
    public class BottomTabUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform iconRect;
        public TextMeshProUGUI labelText;
        public Image bgImage;
        public LayoutElement layoutElement;
        public Button button;

        [Header("Settings")]
        public float activeWidth = 250f;
        public float inactiveWidth = 150f;
        public float activeIconY = 25f;
        public float inactiveIconY = 0f;
        public float activeIconScale = 1.15f;
        public float inactiveIconScale = 1.0f;
        public float animationDuration = 0.2f;

        private bool isActive = false;
        private Coroutine animCoroutine;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();
        }

        public void SetActiveState(bool state, bool instant = false)
        {
            if (isActive == state) return;
            isActive = state;

            if (animCoroutine != null)
            {
                StopCoroutine(animCoroutine);
            }

            if (instant)
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
            // Calculate current visual weight based on preferredWidth
            float currentWidth = layoutElement != null ? layoutElement.preferredWidth : inactiveWidth;
            float startWeight = Mathf.Clamp01((currentWidth - inactiveWidth) / (activeWidth - inactiveWidth));
            if (float.IsNaN(startWeight)) startWeight = 0f;
                               
            float time = 0f;
            while (time < animationDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / animationDuration);
                float weight = Mathf.Lerp(startWeight, targetWeight, t);
                ApplyState(weight);
                yield return null;
            }
            ApplyState(targetWeight);
        }

        private void ApplyState(float weight) // weight 0 = inactive, 1 = active
        {
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = Mathf.Lerp(inactiveWidth, activeWidth, weight);
                layoutElement.flexibleWidth = 1; // Allow them to fill the remaining space
            }

            if (iconRect != null)
            {
                iconRect.anchoredPosition = new Vector2(iconRect.anchoredPosition.x, Mathf.Lerp(inactiveIconY, activeIconY, weight));
                float scale = Mathf.Lerp(inactiveIconScale, activeIconScale, weight);
                iconRect.localScale = new Vector3(scale, scale, 1f);
            }

            if (labelText != null)
            {
                Color c = labelText.color;
                c.a = weight;
                labelText.color = c;
            }

            if (bgImage != null)
            {
                Color c = bgImage.color;
                c.a = weight;
                bgImage.color = c;
            }
        }
    }
}
