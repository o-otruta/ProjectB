using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Enemies;

namespace ProjectB.UI
{
    /// <summary>
    /// Full-screen radial fog vignette shown during the Fog Wave Mutator.
    /// Zero GC allocations in Update; disables the Image component when alpha is zero.
    /// </summary>
    public class WaveMutatorFogOverlayUI : MonoBehaviour
    {
        [SerializeField] private Image fogImage;

        private WaveEventManager waveEventManager;
        private float currentAlpha = -1f;

        public float CurrentAlpha => Mathf.Max(0f, currentAlpha);
        public Image FogImage => fogImage;

        [Inject]
        public void Construct(WaveEventManager waveEventManager = null)
        {
            this.waveEventManager = waveEventManager;
            EnsureImageInitialized();
            TickOverlay();
        }

        private void Awake()
        {
            EnsureImageInitialized();
            ApplyAlpha(0f);
        }

        private void EnsureImageInitialized()
        {
            if (fogImage == null)
            {
                fogImage = GetComponent<Image>();
            }
            if (fogImage != null)
            {
                fogImage.raycastTarget = false;
                if (fogImage.sprite == null && UiTheme.Current != null)
                {
                    fogImage.sprite = UiTheme.Current.vignetteSprite;
                }
            }
        }

        private void Update()
        {
            TickOverlay();
        }

        public void TickOverlay()
        {
            float target = waveEventManager != null ? waveEventManager.FogOverlayTargetAlpha : 0f;
            ApplyAlpha(target);
        }

        private void ApplyAlpha(float alpha)
        {
            float clamped = Mathf.Clamp01(alpha);
            if (Mathf.Approximately(currentAlpha, clamped)) return;
            currentAlpha = clamped;

            if (fogImage != null)
            {
                UiTheme theme = UiTheme.Current;
                Color baseColor = theme != null ? UiTheme.Shade(theme.background, 0.85f) : Color.black;
                baseColor.a = clamped;
                fogImage.color = baseColor;
                bool shouldEnable = clamped > 0.002f;
                if (fogImage.enabled != shouldEnable)
                {
                    fogImage.enabled = shouldEnable;
                }
            }
        }
    }
}
