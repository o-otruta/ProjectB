using UnityEngine;

namespace ProjectB.UI
{
    /// <summary>
    /// Gentle confetti around a celebration banner (LevelUp ribbon): each piece pops in with a small stagger when the
    /// banner appears, then drifts on a slow figure-eight, spins and twinkles. Runs on unscaled time (LevelUp pauses
    /// the game), per-piece parameters are derived from the index, Zero-GC after the first enable.
    /// </summary>
    public sealed class ConfettiDriftUI : MonoBehaviour
    {
        [SerializeField] private RectTransform[] pieces = new RectTransform[0];
        [SerializeField, Min(0f)] private float driftAmplitude = 10f;
        [SerializeField, Min(0f)] private float spinSpeed = 40f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.35f;
        [SerializeField, Min(0f)] private float popStagger = 0.05f;

        private Vector2[] basePositions;
        private float[] baseRotations;
        private float elapsed;

        private void OnEnable()
        {
            CaptureBase();
            elapsed = 0f;
            Apply();
        }

        private void OnDisable()
        {
            // Restore the authored layout so the editor / next show starts from the same place.
            if (basePositions == null) return;
            for (int i = 0; i < pieces.Length; i++)
            {
                RectTransform p = pieces[i];
                if (p == null) continue;
                p.anchoredPosition = basePositions[i];
                p.localRotation = Quaternion.Euler(0f, 0f, baseRotations[i]);
                p.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void CaptureBase()
        {
            if (basePositions != null && basePositions.Length == pieces.Length) return;
            basePositions = new Vector2[pieces.Length];
            baseRotations = new float[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                basePositions[i] = pieces[i].anchoredPosition;
                baseRotations[i] = pieces[i].localEulerAngles.z;
            }
        }

        private void Apply()
        {
            if (basePositions == null) return;
            for (int i = 0; i < pieces.Length; i++)
            {
                RectTransform p = pieces[i];
                if (p == null) continue;
                Evaluate(i, elapsed, driftAmplitude, spinSpeed, popDuration, popStagger, out Vector2 offset, out float spin, out float scale);
                p.anchoredPosition = basePositions[i] + offset;
                p.localRotation = Quaternion.Euler(0f, 0f, baseRotations[i] + spin);
                p.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Offset, extra rotation and scale of piece <paramref name="index"/> at <paramref name="time"/> seconds.</summary>
        public static void Evaluate(int index, float time, float amplitude, float spinSpeed, float popDuration, float popStagger,
            out Vector2 offset, out float spin, out float scale)
        {
            float phase = index * 1.73f;
            float speed = 0.55f + 0.18f * (index % 3);
            float ampX = amplitude * (0.6f + 0.15f * (index % 4));
            float ampY = amplitude * (0.8f + 0.2f * (index % 3));
            float t = time * speed + phase;
            offset = new Vector2(Mathf.Sin(t) * ampX, Mathf.Sin(t * 2f) * 0.5f * ampY);

            float direction = (index & 1) == 0 ? 1f : -1f;
            spin = direction * spinSpeed * (0.6f + 0.2f * (index % 3)) * time;

            float popT = Mathf.Clamp01((time - index * popStagger) / popDuration);
            float pop = popT >= 1f ? 1f : EaseOutBack(popT);
            float twinkle = 1f + 0.12f * Mathf.Sin(time * 3.1f + phase);
            scale = pop * twinkle;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float k = x - 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform[] confettiPieces)
        {
            pieces = confettiPieces ?? new RectTransform[0];
            basePositions = null;
        }
#endif
    }
}
