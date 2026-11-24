using System;
using UnityEngine;
using VContainer;
using ProjectB.Data.Platform;
using ProjectB.Player;

namespace ProjectB.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("The character the camera should follow")]
        public Transform target;

        [Tooltip("Offset relative to the target (e.g. 0, 20, -12 for a steeper top-down view)")]
        public Vector3 offset = new Vector3(0f, 20f, -12f);

        [Tooltip("How smoothly the camera catches up to the target")]
        public float smoothTime = 0.0f;

        [Header("Short-Side Framing & Spawn Bounds (Sprint 14.2)")]
        [SerializeField] private CameraConfig cameraConfig;

        private Camera cam;
        private LayoutModeService layoutModeService;
        private Action<LayoutMode> cachedLayoutModeHandler;
        private Action cachedScreenChangedHandler;
        private bool subscribedToLayout;

        private Vector3 velocity = Vector3.zero;
        private float defaultFOV = 60f;
        private float defaultOrthoSize = 5f;
        private float lastAspect = -1f;
        private bool hasCachedFrustum;

        private readonly Vector2[] frustumCornersXZ = new Vector2[4];
        private readonly Vector2[] frustumEdgeNormals = new Vector2[4];
        private readonly float[] frustumEdgeDists = new float[4];

        private float shakeTimer;
        private float shakeDuration;
        private float shakeIntensity;

        public float DefaultOrthoSize => defaultOrthoSize;
        public float DefaultFOV => defaultFOV;
        public float CachedAspect { get; private set; }
        public float CachedFrustumMaxRadius { get; private set; }
        public float CachedSpawnRadiusMin { get; private set; }
        public float CachedSpawnRadiusMax { get; private set; }
        public int FrustumCacheVersion { get; private set; }
        public CameraConfig Config => ResolveConfig();

        [Inject]
        public void Construct(
            HeroMovement hero,
            LayoutModeService layoutModeService = null,
            CameraConfig cameraConfig = null)
        {
            if (target == null && hero != null)
            {
                target = hero.transform;
            }
            BindServices(layoutModeService, cameraConfig);
        }

        public void BindServices(LayoutModeService layoutMode, CameraConfig config = null)
        {
            UnsubscribeLayoutEvents();
            layoutModeService = layoutMode;
            if (config != null) cameraConfig = config;
            if (isActiveAndEnabled) SubscribeLayoutEvents();
            RefreshFromScreen(force: true);
        }

        public void Shake(float intensity = 0.28f, float duration = 0.16f)
        {
            if (intensity <= 0f || duration <= 0f) return;
            if (shakeTimer <= 0f || intensity >= shakeIntensity)
            {
                shakeIntensity = intensity;
                shakeDuration = duration;
                shakeTimer = duration;
            }
        }

        private void Awake()
        {
            EnsureCameraCached();
        }

        private void OnEnable()
        {
            EnsureCameraCached();
            if (layoutModeService == null && Application.isPlaying)
            {
                RootServices.TryResolve(out layoutModeService);
            }
            if (cameraConfig == null && Application.isPlaying)
            {
                RootServices.TryResolve(out cameraConfig);
            }
            SubscribeLayoutEvents();
        }

        private void OnDisable()
        {
            UnsubscribeLayoutEvents();
        }

        private void Start()
        {
            EnsureCameraCached();
            if (offset != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(-offset);
            }
            RefreshFromScreen(force: true);
        }

        private void LateUpdate()
        {
            if (layoutModeService == null && Screen.height > 0)
            {
                float currentAspect = (float)Screen.width / Screen.height;
                if (Mathf.Abs(currentAspect - lastAspect) > 0.005f)
                {
                    RefreshForAspect(currentAspect);
                }
            }

            if (target == null) return;

            Vector3 targetPosition = target.position + offset;
            if (smoothTime > 0f)
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            }
            else
            {
                transform.position = targetPosition;
            }

            if (shakeTimer > 0f && Time.deltaTime > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float damper = shakeDuration > 0f ? Mathf.Clamp01(shakeTimer / shakeDuration) : 0f;
                Vector2 rnd = UnityEngine.Random.insideUnitCircle * (shakeIntensity * damper);
                transform.position += transform.right * rnd.x + transform.up * rnd.y;
            }
        }

        public void SetDefaultOrthoSize(float newOrthoSize)
        {
            defaultOrthoSize = Mathf.Max(1f, newOrthoSize);
            RefreshFromScreen(force: true);
        }

        public void SetDefaultFOV(float newFOV)
        {
            defaultFOV = Mathf.Clamp(newFOV, 10f, 120f);
            RefreshFromScreen(force: true);
        }

        public void RefreshFromScreen(bool force = false)
        {
            float aspect = ResolveCurrentAspect();
            RefreshForAspect(aspect, force);
        }

        public void RefreshForAspect(float aspect, bool force = false)
        {
            EnsureCameraCached();
            if (aspect <= 0.01f) aspect = 9f / 16f;
            if (!force && hasCachedFrustum && Mathf.Abs(aspect - lastAspect) <= 0.001f)
            {
                return;
            }

            lastAspect = aspect;
            CachedAspect = aspect;
            CameraConfig cfg = ResolveConfig();

            if (cam != null)
            {
                if (cam.orthographic)
                {
                    cam.orthographicSize = CalculateOrthoSize(aspect, cfg);
                }
                else
                {
                    cam.fieldOfView = CalculateVerticalFov(aspect, cfg);
                }
            }

            Quaternion viewRot = offset != Vector3.zero ? Quaternion.LookRotation(-offset) : transform.rotation;
            float vFov = cam != null ? cam.fieldOfView : CalculateVerticalFov(aspect, cfg);
            bool ortho = cam != null && cam.orthographic;
            float orthoSize = cam != null ? cam.orthographicSize : CalculateOrthoSize(aspect, cfg);

            ComputeGroundFrustumQuad(
                offset,
                viewRot,
                vFov,
                aspect,
                ortho,
                orthoSize,
                frustumCornersXZ,
                frustumEdgeNormals,
                frustumEdgeDists,
                out float maxCornerRad);

            CachedFrustumMaxRadius = maxCornerRad;
            CachedSpawnRadiusMin = maxCornerRad + cfg.spawnMargin;
            CachedSpawnRadiusMax = CachedSpawnRadiusMin + cfg.spawnBandWidth;
            hasCachedFrustum = true;
            FrustumCacheVersion++;
        }

        /// <summary>
        /// Exact Zero-GC ray exit distance from target along <paramref name="dirXZ"/> to lie <c>spawnMargin + extraMargin</c>
        /// meters outside the projected ground frustum.
        /// </summary>
        public float GetOffscreenSpawnDistance(Vector3 dirXZ, float extraMargin = 0f)
        {
            if (!hasCachedFrustum)
            {
                RefreshForAspect(ResolveCurrentAspect(), force: true);
            }

            Vector2 d = new Vector2(dirXZ.x, dirXZ.z);
            float sqrMag = d.sqrMagnitude;
            if (sqrMag <= 1e-6f)
            {
                d = Vector2.up;
            }
            else
            {
                float inv = 1f / Mathf.Sqrt(sqrMag);
                d.x *= inv;
                d.y *= inv;
            }

            float margin = ResolveConfig().spawnMargin + Mathf.Max(0f, extraMargin);
            float minExit = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float denom = frustumEdgeNormals[i].x * d.x + frustumEdgeNormals[i].y * d.y;
                if (denom > 1e-4f)
                {
                    float t = (frustumEdgeDists[i] + margin) / denom;
                    if (t < minExit) minExit = t;
                }
            }

            if (minExit == float.MaxValue || minExit <= 0f)
            {
                return CachedSpawnRadiusMin + Mathf.Max(0f, extraMargin);
            }

            return minExit;
        }

        public bool IsWorldOffsetInsideFrustum(Vector3 offsetFromTargetXZ, float margin = 0f)
        {
            if (!hasCachedFrustum)
            {
                RefreshForAspect(ResolveCurrentAspect(), force: true);
            }

            Vector2 p = new Vector2(offsetFromTargetXZ.x, offsetFromTargetXZ.z);
            for (int i = 0; i < 4; i++)
            {
                float signedDist = frustumEdgeNormals[i].x * p.x + frustumEdgeNormals[i].y * p.y - frustumEdgeDists[i];
                if (signedDist >= margin) return false;
            }
            return true;
        }

        public static float CalculateVerticalFov(float aspect, CameraConfig config)
        {
            if (aspect <= 0.01f) aspect = 9f / 16f;
            float shortSideFov = config != null ? config.shortSideFov : 35.98f;
            float landscapeFov = config != null ? config.landscapeShortSideFov : 50f;
            float minAspect = config != null ? config.minAspectRatio : (9f / 21f);
            float maxAspect = config != null ? config.maxAspectRatio : (21f / 9f);
            float minFov = config != null ? config.minVerticalFov : 22f;
            float maxFov = config != null ? config.maxVerticalFov : 80f;

            float vFov;
            if (aspect < 1f)
            {
                float effectiveAspect = Mathf.Max(aspect, minAspect);
                vFov = Camera.HorizontalToVerticalFieldOfView(shortSideFov, effectiveAspect);
            }
            else if (aspect <= maxAspect)
            {
                vFov = landscapeFov;
            }
            else
            {
                float clampedHorizontalFov = Camera.VerticalToHorizontalFieldOfView(landscapeFov, maxAspect);
                vFov = Camera.HorizontalToVerticalFieldOfView(clampedHorizontalFov, aspect);
            }

            return Mathf.Clamp(vFov, minFov, maxFov);
        }

        public static float CalculateOrthoSize(float aspect, CameraConfig config)
        {
            if (aspect <= 0.01f) aspect = 9f / 16f;
            float shortOrtho = config != null ? config.shortSideOrthoSize : 5f;
            float minAspect = config != null ? config.minAspectRatio : (9f / 21f);
            float maxAspect = config != null ? config.maxAspectRatio : (21f / 9f);

            if (aspect < 1f)
            {
                return shortOrtho / Mathf.Max(aspect, minAspect);
            }
            if (aspect <= maxAspect)
            {
                return shortOrtho;
            }
            return shortOrtho * (maxAspect / aspect);
        }

        public static void ComputeGroundFrustumQuad(
            Vector3 cameraOffset,
            Quaternion cameraRotation,
            float verticalFovDeg,
            float aspect,
            bool orthographic,
            float orthoSize,
            Vector2[] outCornersXZ,
            Vector2[] outEdgeNormals,
            float[] outEdgeDists,
            out float maxCornerRadius)
        {
            if (aspect <= 0.01f) aspect = 9f / 16f;

            Vector3 fwd = cameraRotation * Vector3.forward;
            Vector3 right = cameraRotation * Vector3.right;
            Vector3 up = cameraRotation * Vector3.up;

            if (orthographic)
            {
                float halfH = Mathf.Max(0.5f, orthoSize);
                float halfW = halfH * aspect;
                outCornersXZ[0] = IntersectGround(cameraOffset - right * halfW - up * halfH, fwd);
                outCornersXZ[1] = IntersectGround(cameraOffset + right * halfW - up * halfH, fwd);
                outCornersXZ[2] = IntersectGround(cameraOffset + right * halfW + up * halfH, fwd);
                outCornersXZ[3] = IntersectGround(cameraOffset - right * halfW + up * halfH, fwd);
            }
            else
            {
                float halfH = Mathf.Tan(Mathf.Clamp(verticalFovDeg, 5f, 140f) * 0.5f * Mathf.Deg2Rad);
                float halfW = halfH * aspect;
                outCornersXZ[0] = IntersectGround(cameraOffset, (fwd - right * halfW - up * halfH).normalized);
                outCornersXZ[1] = IntersectGround(cameraOffset, (fwd + right * halfW - up * halfH).normalized);
                outCornersXZ[2] = IntersectGround(cameraOffset, (fwd + right * halfW + up * halfH).normalized);
                outCornersXZ[3] = IntersectGround(cameraOffset, (fwd - right * halfW + up * halfH).normalized);
            }

            Vector2 centroid = Vector2.zero;
            float maxRadSq = 0f;
            for (int i = 0; i < 4; i++)
            {
                centroid += outCornersXZ[i];
                float sq = outCornersXZ[i].sqrMagnitude;
                if (sq > maxRadSq) maxRadSq = sq;
            }
            centroid *= 0.25f;
            maxCornerRadius = Mathf.Sqrt(maxRadSq);

            for (int i = 0; i < 4; i++)
            {
                Vector2 a = outCornersXZ[i];
                Vector2 b = outCornersXZ[(i + 1) & 3];
                Vector2 edge = b - a;
                Vector2 n = new Vector2(edge.y, -edge.x);
                float lenSq = n.sqrMagnitude;
                if (lenSq > 1e-8f)
                {
                    n /= Mathf.Sqrt(lenSq);
                }
                else
                {
                    n = Vector2.up;
                }

                if (Vector2.Dot(n, a - centroid) < 0f)
                {
                    n = -n;
                }

                outEdgeNormals[i] = n;
                outEdgeDists[i] = Vector2.Dot(n, a);
            }
        }

        private static Vector2 IntersectGround(Vector3 rayOrigin, Vector3 rayDir)
        {
            float dy = rayDir.y;
            if (dy > -0.05f) dy = -0.05f;
            float originY = Mathf.Max(0.5f, rayOrigin.y);
            float t = -originY / dy;
            return new Vector2(rayOrigin.x + rayDir.x * t, rayOrigin.z + rayDir.z * t);
        }

        private void EnsureCameraCached()
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
                if (cam != null)
                {
                    defaultFOV = cam.fieldOfView;
                    defaultOrthoSize = cam.orthographicSize;
                }
            }
        }

        private float ResolveCurrentAspect()
        {
            if (layoutModeService != null && layoutModeService.ScreenHeight > 0)
            {
                return layoutModeService.Aspect;
            }
            if (Screen.height > 0)
            {
                return (float)Screen.width / Screen.height;
            }
            if (cam != null && cam.aspect > 0.01f)
            {
                return cam.aspect;
            }
            return 9f / 16f;
        }

        private CameraConfig ResolveConfig()
        {
            if (cameraConfig == null)
            {
                cameraConfig = Resources.Load<CameraConfig>(CameraConfig.RESOURCE_PATH);
                if (cameraConfig == null)
                {
                    cameraConfig = CameraConfig.CreateRuntimeDefault();
                }
            }
            return cameraConfig;
        }

        private void SubscribeLayoutEvents()
        {
            if (subscribedToLayout || layoutModeService == null) return;
            cachedLayoutModeHandler ??= HandleLayoutModeChanged;
            cachedScreenChangedHandler ??= HandleScreenChanged;
            layoutModeService.LayoutModeChanged += cachedLayoutModeHandler;
            layoutModeService.ScreenChanged += cachedScreenChangedHandler;
            subscribedToLayout = true;
        }

        private void UnsubscribeLayoutEvents()
        {
            if (!subscribedToLayout || layoutModeService == null) return;
            if (cachedLayoutModeHandler != null) layoutModeService.LayoutModeChanged -= cachedLayoutModeHandler;
            if (cachedScreenChangedHandler != null) layoutModeService.ScreenChanged -= cachedScreenChangedHandler;
            subscribedToLayout = false;
        }

        private void HandleLayoutModeChanged(LayoutMode _)
        {
            RefreshFromScreen(force: true);
        }

        private void HandleScreenChanged()
        {
            RefreshFromScreen(force: false);
        }
    }
}
