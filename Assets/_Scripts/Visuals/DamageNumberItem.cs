using UnityEngine;
using UnityEngine.Pool;
using TMPro;

namespace ProjectB.Visuals
{
    /// <summary>
    /// Pooled screen-space overlay floating damage number projected from world space.
    /// Uses a static cached string table for 0..2499 to eliminate per-hit GC allocations.
    /// </summary>
    public class DamageNumberItem : MonoBehaviour
    {
        private static readonly string[] CachedNumbers = BuildNumberCache(2500, string.Empty, string.Empty);
        private static readonly string[] CachedCritNumbers = BuildNumberCache(2500, string.Empty, "!");
        private static readonly string[] CachedHealNumbers = BuildNumberCache(2500, "+", " HP");

        [SerializeField] private TextMeshProUGUI textMesh;

        private IObjectPool<DamageNumberItem> pool;
        private System.Action<DamageNumberItem> onReleasedCallback;
        private Camera targetCamera;
        private RectTransform rectTransform;
        private Vector3 currentWorldPos;
        private Vector3 velocity;
        private Color baseColor;
        private float elapsed;
        private float lifetime = 0.65f;
        private float baseScale = 1f;
        private bool isReleased = true;

        private static string[] BuildNumberCache(int count, string prefix, string suffix)
        {
            var arr = new string[count];
            bool hasPrefix = !string.IsNullOrEmpty(prefix);
            bool hasSuffix = !string.IsNullOrEmpty(suffix);
            for (int i = 0; i < count; i++)
            {
                if (!hasPrefix && !hasSuffix)
                {
                    arr[i] = i.ToString();
                }
                else
                {
                    arr[i] = $"{prefix}{i}{suffix}";
                }
            }
            return arr;
        }

        public static string GetCachedNumberText(int value)
        {
            if (value >= 0 && value < CachedNumbers.Length)
            {
                return CachedNumbers[value];
            }
            return value.ToString();
        }

        public static string GetCachedCritNumberText(int value)
        {
            if (value >= 0 && value < CachedCritNumbers.Length)
            {
                return CachedCritNumbers[value];
            }
            return $"{value}!";
        }

        public static string GetCachedHealText(int value)
        {
            if (value >= 0 && value < CachedHealNumbers.Length)
            {
                return CachedHealNumbers[value];
            }
            return $"+{value} HP";
        }

        public void SetTextMesh(TextMeshProUGUI tmp)
        {
            textMesh = tmp;
        }

        public void SetReleaseCallback(System.Action<DamageNumberItem> callback)
        {
            onReleasedCallback = callback;
        }

        public void Show(
            Vector3 worldPos,
            int amount,
            Color color,
            float scale,
            Camera cam,
            IObjectPool<DamageNumberItem> ownerPool,
            bool isCrit = false)
        {
            Vector3 jitter = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(1.0f, 1.45f), Random.Range(-0.25f, 0.25f));
            string label = isCrit ? GetCachedCritNumberText(amount) : GetCachedNumberText(amount);
            ShowInternal(
                worldPos + jitter,
                new Vector3(Random.Range(-0.6f, 0.6f), isCrit ? 2.9f : 2.4f, 0f),
                label,
                color,
                scale,
                isCrit ? 0.78f : 0.65f,
                cam,
                ownerPool);
        }

        public void ShowText(
            Vector3 worldPos,
            string text,
            Color color,
            float scale,
            float duration,
            Camera cam,
            IObjectPool<DamageNumberItem> ownerPool)
        {
            ShowInternal(
                worldPos + Vector3.up * 1.8f,
                new Vector3(0f, 1.3f, 0f),
                text,
                color,
                scale,
                Mathf.Max(0.4f, duration),
                cam,
                ownerPool);
        }

        private void ShowInternal(
            Vector3 startWorldPos,
            Vector3 initialVelocity,
            string displayText,
            Color color,
            float scale,
            float duration,
            Camera cam,
            IObjectPool<DamageNumberItem> ownerPool)
        {
            pool = ownerPool;
            targetCamera = cam;
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
            }

            baseColor = color;
            baseScale = scale;
            elapsed = 0f;
            lifetime = duration;
            isReleased = false;

            currentWorldPos = startWorldPos;
            velocity = initialVelocity;

            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * (scale * 0.7f);
            }

            if (textMesh != null)
            {
                textMesh.text = displayText;
                textMesh.color = baseColor;
            }

            UpdateScreenPosition();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (isReleased || deltaTime <= 0f) return;

            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            currentWorldPos += velocity * deltaTime;
            velocity.y = Mathf.Max(0.4f, velocity.y - 3.2f * deltaTime);

            // Pop-in then gentle settle
            float pop = t < 0.2f ? Mathf.Lerp(0.7f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 0.9f, (t - 0.2f) / 0.8f);
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * (baseScale * pop);
            }

            if (textMesh != null)
            {
                Color c = baseColor;
                c.a = t > 0.55f ? Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f) : 1f;
                textMesh.color = c;
            }

            UpdateScreenPosition();

            if (elapsed >= lifetime)
            {
                ReturnToPool();
            }
        }

        private void UpdateScreenPosition()
        {
            if (targetCamera == null || rectTransform == null) return;

            Vector3 screenPos = targetCamera.WorldToScreenPoint(currentWorldPos);
            bool visible = screenPos.z > 0f;

            if (textMesh != null && textMesh.enabled != visible)
            {
                textMesh.enabled = visible;
            }

            if (visible)
            {
                rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
            }
        }

        public void ReturnToPool()
        {
            if (isReleased) return;
            isReleased = true;
            onReleasedCallback?.Invoke(this);

            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
