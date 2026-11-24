using UnityEngine;
using UnityEngine.UI;

namespace ProjectB.UI
{
    /// <summary>
    /// Idle "breathing" pulse for call-to-action elements (START, today's daily reward, ready ultimate):
    /// scale and/or glow alpha oscillate on unscaled time. Zero-GC, no allocations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseUI : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float scaleAmplitude = 0.04f;
        [SerializeField, Min(0.1f)] private float speed = 3.2f;
        [SerializeField] private Graphic glow;
        [SerializeField] private Vector2 glowAlphaRange = new Vector2(0.25f, 0.9f);

        private Vector3 restScale = Vector3.one;
        private float phase;

        public Graphic Glow => glow;

        private void Awake()
        {
            restScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        }

        private void OnEnable()
        {
            phase = 0f;
        }

        private void OnDisable()
        {
            transform.localScale = restScale;
        }

        private void Update()
        {
            phase += Time.unscaledDeltaTime * speed;
            float k = (Mathf.Sin(phase) + 1f) * 0.5f;
            if (scaleAmplitude > 0f) transform.localScale = restScale * (1f + scaleAmplitude * k);
            if (glow != null)
            {
                Color c = glow.color;
                c.a = Mathf.Lerp(glowAlphaRange.x, glowAlphaRange.y, k);
                glow.color = c;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(float amplitude, float pulseSpeed, Graphic glowGraphic = null)
        {
            scaleAmplitude = amplitude;
            speed = pulseSpeed;
            glow = glowGraphic;
        }
#endif
    }
}
