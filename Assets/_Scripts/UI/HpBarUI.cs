using UnityEngine;
using UnityEngine.UI;
using ProjectB.Player;
using VContainer;

namespace ProjectB.UI
{
    public class HpBarUI : MonoBehaviour
    {
        private HeroHealth heroHealth;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMPro.TextMeshProUGUI hpText;
        [Tooltip("Sprint 13: themed bar with ghost-trail; when set, the legacy Slider is not needed.")]
        [SerializeField] private ProgressBarUI progressBar;
        [Header("UI redesign: compact bar under the hero")]
        [Tooltip("Pin the bar to the hero on screen (world → screen every LateUpdate) instead of a fixed HUD slot.")]
        [SerializeField] private bool followHero;
        [Tooltip("World offset from the hero pivot (pivot is ~1 m above the feet).")]
        [SerializeField] private Vector3 followWorldOffset = new Vector3(0f, -1f, 0f);
        [Tooltip("Extra offset in canvas units after projection (negative = lower).")]
        [SerializeField] private Vector2 followScreenOffset = new Vector2(0f, -30f);

        private bool isInitialized = false;
        private int lastDisplayedHp = int.MinValue;
        private int lastDisplayedMaxHp = int.MinValue;
        private readonly char[] hpCharBuffer = new char[24];
        private Camera followCamera;
        private RectTransform rectTransform;
        private RectTransform parentRect;
        private Canvas canvas;

        public bool FollowsHero => followHero;

        private void LateUpdate()
        {
            if (!followHero || heroHealth == null) return;
            if (followCamera == null) followCamera = Camera.main;
            if (followCamera == null) return;
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
                parentRect = rectTransform.parent as RectTransform;
                canvas = GetComponentInParent<Canvas>();
            }
            if (parentRect == null || canvas == null) return;

            Vector3 screen = followCamera.WorldToScreenPoint(heroHealth.transform.position + followWorldOffset);
            if (screen.z < 0f) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out Vector2 local))
            {
                rectTransform.localPosition = new Vector3(local.x + followScreenOffset.x, local.y + followScreenOffset.y, 0f);
            }
        }

        [Inject]
        public void Construct(HeroHealth heroHealth)
        {
            this.heroHealth = heroHealth;
            isInitialized = true;
            if (gameObject.activeInHierarchy)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (heroHealth != null)
            {
                heroHealth.OnHealthChanged -= UpdateHpBar;
            }
        }

        private void Start()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            if (heroHealth == null) return;

            heroHealth.OnHealthChanged -= UpdateHpBar;
            heroHealth.OnHealthChanged += UpdateHpBar;
            UpdateHpBar(heroHealth.CurrentHp, heroHealth.MaxHp);
        }

        private void UpdateHpBar(int currentHp, int maxHp)
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHp;
                hpSlider.value = currentHp;
            }
            if (progressBar != null)
            {
                progressBar.SetValue(currentHp, maxHp);
            }
            if (hpText != null && (currentHp != lastDisplayedHp || maxHp != lastDisplayedMaxHp))
            {
                lastDisplayedHp = currentHp;
                lastDisplayedMaxHp = maxHp;
                int len = WriteRatioToBuffer(hpCharBuffer, currentHp, maxHp);
                hpText.SetCharArray(hpCharBuffer, 0, len);
            }
        }

        public static int WriteRatioToBuffer(char[] buffer, int current, int max)
        {
            int pos = WriteNonNegativeInt(buffer, 0, Mathf.Max(0, current));
            buffer[pos++] = ' ';
            buffer[pos++] = '/';
            buffer[pos++] = ' ';
            pos = WriteNonNegativeInt(buffer, pos, Mathf.Max(0, max));
            return pos;
        }

        private static int WriteNonNegativeInt(char[] buffer, int startIndex, int value)
        {
            if (value <= 0)
            {
                buffer[startIndex] = '0';
                return startIndex + 1;
            }

            int temp = value;
            int digits = 0;
            while (temp > 0)
            {
                temp /= 10;
                digits++;
            }

            int endIndex = startIndex + digits;
            int writeIdx = endIndex - 1;
            temp = value;
            while (temp > 0)
            {
                buffer[writeIdx--] = (char)('0' + (temp % 10));
                temp /= 10;
            }

            return endIndex;
        }
    }
}
