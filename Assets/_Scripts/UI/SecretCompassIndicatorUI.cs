using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Player;

namespace ProjectB.UI
{
    /// <summary>
    /// Off-screen edge compass indicator pointing toward the active secret arena item
    /// when the hero is within revealDistance and the item is outside the viewport.
    /// Zero GC allocations in LateUpdate.
    /// </summary>
    public class SecretCompassIndicatorUI : MonoBehaviour
    {
        private static readonly string[] CachedDistanceLabels = BuildDistanceLabels(101);

        [Header("UI References")]
        [SerializeField] private RectTransform indicatorRect;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private Image arrowImage;
        [SerializeField] private TextMeshProUGUI distanceText;

        [Header("Compass Settings")]
        [SerializeField] private float revealDistance = 45f;
        [SerializeField] private float sidePadding = 85f;
        [SerializeField] private float topPadding = 240f;
        [SerializeField] private float bottomPadding = 260f;
        [SerializeField] private float pulseSpeed = 4.5f;
        [SerializeField] private float pulseAmplitude = 0.08f;

        private SecretItemSpawner secretItemSpawner;
        private HeroHealth heroHealth;
        private CameraController cameraController;
        private Camera mainCamera;
        private RectTransform parentRect;
        private int lastDisplayedMeters = -1;
        private bool isVisible;

        private static string[] BuildDistanceLabels(int count)
        {
            var labels = new string[count];
            for (int i = 0; i < count; i++)
            {
                labels[i] = $"{i}m";
            }
            return labels;
        }

        public static string GetCachedDistanceLabel(int meters)
        {
            if (meters >= 0 && meters < CachedDistanceLabels.Length)
            {
                return CachedDistanceLabels[meters];
            }
            return CachedDistanceLabels[CachedDistanceLabels.Length - 1];
        }

        [Inject]
        public void Construct(
            SecretItemSpawner secretItemSpawner,
            HeroHealth heroHealth,
            CameraController cameraController = null)
        {
            this.secretItemSpawner = secretItemSpawner;
            this.heroHealth = heroHealth;
            this.cameraController = cameraController;
        }

        private void Awake()
        {
            if (indicatorRect == null)
            {
                indicatorRect = (RectTransform)transform;
            }

            parentRect = indicatorRect.parent as RectTransform;

            var graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = false;
                }
            }

            SetIndicatorVisible(false, force: true);
        }

        private void Start()
        {
            mainCamera = cameraController != null
                ? cameraController.GetComponent<Camera>()
                : Camera.main;
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (secretItemSpawner == null || heroHealth == null || heroHealth.IsDead)
            {
                SetIndicatorVisible(false);
                return;
            }

            InteractableItem activeItem = secretItemSpawner.ActiveItem;
            if (activeItem == null || activeItem.IsCollected || !activeItem.gameObject.activeInHierarchy)
            {
                SetIndicatorVisible(false);
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = cameraController != null
                    ? cameraController.GetComponent<Camera>()
                    : Camera.main;
                if (mainCamera == null)
                {
                    SetIndicatorVisible(false);
                    return;
                }
            }

            if (parentRect == null && indicatorRect != null)
            {
                parentRect = indicatorRect.parent as RectTransform;
            }

            Vector2 canvasSize = parentRect != null
                ? parentRect.rect.size
                : new Vector2(1080f, 1920f);

            if (canvasSize.x <= 1f || canvasSize.y <= 1f)
            {
                canvasSize = new Vector2(1080f, 1920f);
            }

            bool shouldShow = EvaluateIndicatorState(
                heroHealth.transform.position,
                activeItem.transform.position,
                mainCamera,
                canvasSize,
                out Vector2 anchoredPos,
                out float arrowAngleDeg,
                out int distanceMeters);

            if (!shouldShow)
            {
                SetIndicatorVisible(false);
                return;
            }

            SetIndicatorVisible(true);

            if (indicatorRect != null)
            {
                indicatorRect.anchoredPosition = anchoredPos;
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
                indicatorRect.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (arrowRect != null)
            {
                arrowRect.localEulerAngles = new Vector3(0f, 0f, arrowAngleDeg);
            }

            if (distanceText != null && distanceMeters != lastDisplayedMeters)
            {
                lastDisplayedMeters = distanceMeters;
                distanceText.text = GetCachedDistanceLabel(distanceMeters);
            }
        }

        public bool EvaluateIndicatorState(
            Vector3 heroPos,
            Vector3 itemPos,
            Camera cam,
            Vector2 canvasSize,
            out Vector2 anchoredPos,
            out float arrowAngleDeg,
            out int distanceMeters)
        {
            anchoredPos = Vector2.zero;
            arrowAngleDeg = 0f;

            float dx = itemPos.x - heroPos.x;
            float dz = itemPos.z - heroPos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            distanceMeters = Mathf.RoundToInt(dist);

            if (dist > revealDistance || cam == null)
            {
                return false;
            }

            Vector3 vp = cam.WorldToViewportPoint(itemPos);
            bool onScreen = vp.z > 0f
                && vp.x >= 0.05f && vp.x <= 0.95f
                && vp.y >= 0.05f && vp.y <= 0.95f;

            if (onScreen)
            {
                return false;
            }

            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f)
            {
                dir = -dir;
            }

            dir.x *= canvasSize.x;
            dir.y *= canvasSize.y;

            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.up;
            }
            dir.Normalize();

            float halfW = canvasSize.x * 0.5f;
            float halfH = canvasSize.y * 0.5f;
            float minX = Mathf.Min(0f, -halfW + sidePadding);
            float maxX = Mathf.Max(0f, halfW - sidePadding);
            float minY = Mathf.Min(0f, -halfH + bottomPadding);
            float maxY = Mathf.Max(0f, halfH - topPadding);

            float t = float.MaxValue;
            if (dir.x > 0.0001f) t = Mathf.Min(t, maxX / dir.x);
            else if (dir.x < -0.0001f) t = Mathf.Min(t, minX / dir.x);

            if (dir.y > 0.0001f) t = Mathf.Min(t, maxY / dir.y);
            else if (dir.y < -0.0001f) t = Mathf.Min(t, minY / dir.y);

            if (t == float.MaxValue || t < 0f)
            {
                t = 0f;
            }

            anchoredPos = dir * t;
            arrowAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            return true;
        }

        private void SetIndicatorVisible(bool visible, bool force = false)
        {
            if (!force && isVisible == visible)
            {
                return;
            }
            isVisible = visible;

            if (indicatorRect != null && indicatorRect != (RectTransform)transform)
            {
                if (indicatorRect.gameObject.activeSelf != visible)
                {
                    indicatorRect.gameObject.SetActive(visible);
                }
            }
            else
            {
                if (arrowImage != null && arrowImage.enabled != visible)
                {
                    arrowImage.enabled = visible;
                }
                if (distanceText != null && distanceText.enabled != visible)
                {
                    distanceText.enabled = visible;
                }
            }
        }
    }
}
