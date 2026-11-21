using UnityEngine;
using UnityEngine.Pool;
using TMPro;

namespace ProjectB.Visuals
{
    /// <summary>
    /// Pooled screen-space overlay floating damage number projected from world space.
    /// Uses a static cached string table for 0..999 to eliminate per-hit GC allocations.
    /// </summary>
    public class DamageNumberItem : MonoBehaviour
    {
        private static readonly string[] CachedNumbers = BuildNumberCache(1000);

        [SerializeField] private TextMeshProUGUI textMesh;

        private IObjectPool<DamageNumberItem> pool;
        private Camera targetCamera;
        private RectTransform rectTransform;
        private Vector3 currentWorldPos;
        private Vector3 velocity;
        private Color baseColor;
        private float elapsed;
        private float lifetime = 0.65f;
        private float baseScale = 1f;

        private static string[] BuildNumberCache(int count)
        {
            var arr = new string[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = i.ToString();
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

        public void SetTextMesh(TextMeshProUGUI tmp)
        {
            textMesh = tmp;
        }

        public void Show(
            Vector3 worldPos,
            int amount,
            Color color,
            float scale,
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
            lifetime = 0.65f;

            Vector3 jitter = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(1.0f, 1.45f), Random.Range(-0.25f, 0.25f));
            currentWorldPos = worldPos + jitter;
            velocity = new Vector3(Random.Range(-0.6f, 0.6f), 2.4f, 0f);

            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * (scale * 0.7f);
            }

            if (textMesh != null)
            {
                textMesh.text = GetCachedNumberText(amount);
                textMesh.color = baseColor;
            }

            UpdateScreenPosition();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            currentWorldPos += velocity * Time.deltaTime;
            velocity.y = Mathf.Max(0.4f, velocity.y - 3.2f * Time.deltaTime);

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

        private void ReturnToPool()
        {
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
