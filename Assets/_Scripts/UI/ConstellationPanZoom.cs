using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectB.UI
{
    /// <summary>
    /// One-thumb friendly 360° pan &amp; zoom for the Constellation star map.
    /// Drag anywhere to pan in every direction (with inertia and a soft elastic bounce at the edges),
    /// pinch with two fingers (or mouse wheel) to zoom, and animate focus onto any point.
    /// Attach to the viewport (with a raycastable Graphic); <see cref="content"/> is the 2200×2200 star map.
    /// </summary>
    public class ConstellationPanZoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;

        [Header("Zoom")]
        [SerializeField] private float minZoom = 0.45f;
        [SerializeField] private float maxZoom = 1.6f;
        [SerializeField] private float defaultZoom = 0.8f;
        [SerializeField] private float buttonZoomStep = 1.25f;
        [SerializeField] private float wheelZoomSpeed = 0.0015f;

        [Header("Pan")]
        [Tooltip("Extra space (viewport units) the map may be dragged past its edge before clamping")]
        [SerializeField] private float edgeMargin = 220f;
        [Tooltip("Velocity retained per second after release (like ScrollRect.decelerationRate)")]
        [SerializeField] [Range(0f, 1f)] private float decelerationRate = 0.12f;
        [Tooltip("Seconds the elastic bounce takes to return inside the bounds")]
        [SerializeField] private float elasticity = 0.12f;

        private Vector2 velocity;
        private bool isDragging;
        private bool isPinching;
        private float lastPinchDistance;
        private Vector2 elasticVelocity;

        private bool isAnimating;
        private Vector2 animFromPos, animToPos;
        private float animFromZoom, animToZoom;
        private float animTime, animDuration;

        public RectTransform Content => content;
        public RectTransform Viewport => viewport;
        public float Zoom => content != null ? content.localScale.x : 1f;
        public Vector2 Position => content != null ? content.anchoredPosition : Vector2.zero;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;
        public bool IsAnimating => isAnimating;
        public bool IsInteracting => isDragging || isPinching;

        public event Action OnViewChanged;

        public void Configure(RectTransform viewportRect, RectTransform contentRect)
        {
            viewport = viewportRect;
            content = contentRect;
        }

        private void OnEnable()
        {
            velocity = Vector2.zero;
            isDragging = false;
            isPinching = false;
        }

        // ---------------- Pure helpers (unit-tested) ----------------

        /// <summary>
        /// Maximum allowed |offset| of the content centre from the viewport centre on each axis so that the
        /// map can be panned to every edge (plus <paramref name="margin"/>) but never fully off-screen.
        /// </summary>
        public static Vector2 ComputeMaxOffset(Vector2 contentSize, float zoom, Vector2 viewportSize, float margin)
        {
            float halfW = Mathf.Max(0f, contentSize.x * zoom * 0.5f - viewportSize.x * 0.5f) + margin;
            float halfH = Mathf.Max(0f, contentSize.y * zoom * 0.5f - viewportSize.y * 0.5f) + margin;
            return new Vector2(halfW, halfH);
        }

        public static Vector2 ClampOffset(Vector2 position, Vector2 maxOffset)
        {
            return new Vector2(
                Mathf.Clamp(position.x, -maxOffset.x, maxOffset.x),
                Mathf.Clamp(position.y, -maxOffset.y, maxOffset.y));
        }

        /// <summary>Rubber-band resistance (same curve as ScrollRect): overshoot shrinks the further you pull.</summary>
        public static float RubberDelta(float overStretching, float viewSize)
        {
            if (viewSize <= 0f) return 0f;
            return (1f - 1f / (Mathf.Abs(overStretching) * 0.55f / viewSize + 1f)) * viewSize * Mathf.Sign(overStretching);
        }

        /// <summary>Content position that places <paramref name="contentLocalPoint"/> at <paramref name="viewportOffset"/>.</summary>
        public static Vector2 PositionToShowPoint(Vector2 contentLocalPoint, float zoom, Vector2 viewportOffset)
        {
            return viewportOffset - contentLocalPoint * zoom;
        }

        /// <summary>New content position that keeps the viewport point <paramref name="pivotInViewport"/> fixed while zooming.</summary>
        public static Vector2 ZoomAroundPivot(Vector2 position, float oldZoom, float newZoom, Vector2 pivotInViewport)
        {
            if (oldZoom <= 0f) return position;
            Vector2 contentPoint = (pivotInViewport - position) / oldZoom;
            return pivotInViewport - contentPoint * newZoom;
        }

        // ---------------- Runtime ----------------

        private Vector2 ViewportSize => viewport != null ? viewport.rect.size : new Vector2(1000f, 1600f);
        private Vector2 ContentSize => content != null ? content.rect.size : Vector2.one * 2200f;
        private Vector2 CurrentMaxOffset => ComputeMaxOffset(ContentSize, Zoom, ViewportSize, edgeMargin);

        public void SetView(Vector2 position, float zoom)
        {
            if (content == null) return;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            content.localScale = new Vector3(zoom, zoom, 1f);
            content.anchoredPosition = ClampOffset(position, ComputeMaxOffset(ContentSize, zoom, ViewportSize, edgeMargin));
            velocity = Vector2.zero;
            OnViewChanged?.Invoke();
        }

        public void ResetView()
        {
            isAnimating = false;
            SetView(Vector2.zero, defaultZoom);
        }

        public void CenterOnOrigin(float duration = 0.35f)
        {
            AnimateTo(Vector2.zero, defaultZoom, duration);
        }

        public void ZoomIn() => ZoomByFactor(buttonZoomStep, Vector2.zero, 0.2f);
        public void ZoomOut() => ZoomByFactor(1f / buttonZoomStep, Vector2.zero, 0.2f);

        public void ZoomByFactor(float factor, Vector2 pivotInViewport, float duration = 0f)
        {
            if (content == null || factor <= 0f) return;
            float oldZoom = isAnimating ? animToZoom : Zoom;
            Vector2 oldPos = isAnimating ? animToPos : Position;
            float newZoom = Mathf.Clamp(oldZoom * factor, minZoom, maxZoom);
            Vector2 newPos = ZoomAroundPivot(oldPos, oldZoom, newZoom, pivotInViewport);

            if (duration <= 0f)
            {
                isAnimating = false;
                SetView(newPos, newZoom);
            }
            else
            {
                AnimateTo(newPos, newZoom, duration);
            }
        }

        /// <summary>Smoothly brings a content-local point to the given viewport offset (e.g. upper half of the screen).</summary>
        public void FocusOn(Vector2 contentLocalPoint, Vector2 viewportOffset, float zoom = -1f, float duration = 0.35f)
        {
            float targetZoom = zoom > 0f ? Mathf.Clamp(zoom, minZoom, maxZoom) : Zoom;
            AnimateTo(PositionToShowPoint(contentLocalPoint, targetZoom, viewportOffset), targetZoom, duration);
        }

        public void AnimateTo(Vector2 position, float zoom, float duration)
        {
            if (content == null) return;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            animFromPos = Position;
            animFromZoom = Zoom;
            animToZoom = zoom;
            animToPos = ClampOffset(position, ComputeMaxOffset(ContentSize, zoom, ViewportSize, edgeMargin));
            animTime = 0f;
            animDuration = Mathf.Max(0.01f, duration);
            isAnimating = true;
            velocity = Vector2.zero;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isPinching) return;
            isDragging = true;
            isAnimating = false;
            velocity = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || isPinching || content == null) return;

            Vector2 delta = ScreenDeltaToViewport(eventData.delta);
            Vector2 pos = content.anchoredPosition;
            Vector2 max = CurrentMaxOffset;
            Vector2 view = ViewportSize;

            pos += delta;
            // Elastic resistance past the bounds
            if (pos.x > max.x) pos.x = max.x + RubberDelta(pos.x - max.x, view.x);
            else if (pos.x < -max.x) pos.x = -max.x + RubberDelta(pos.x + max.x, view.x);
            if (pos.y > max.y) pos.y = max.y + RubberDelta(pos.y - max.y, view.y);
            else if (pos.y < -max.y) pos.y = -max.y + RubberDelta(pos.y + max.y, view.y);

            content.anchoredPosition = pos;

            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            velocity = Vector2.Lerp(velocity, delta / dt, 0.5f);
            OnViewChanged?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (viewport == null) return;
            float factor = 1f + eventData.scrollDelta.y * wheelZoomSpeed * 60f;
            Vector2 pivot = ScreenToViewportLocal(eventData.position);
            ZoomByFactor(Mathf.Clamp(factor, 0.5f, 1.5f), pivot, 0f);
        }

        private Vector2 ScreenDeltaToViewport(Vector2 screenDelta)
        {
            Canvas canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
            float scale = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            return screenDelta / scale;
        }

        private Vector2 ScreenToViewportLocal(Vector2 screenPos)
        {
            if (viewport == null) return Vector2.zero;
            Canvas canvas = viewport.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, cam, out Vector2 local);
            // viewport local is relative to its pivot; content is anchored at the viewport centre
            Vector2 pivotOffset = (viewport.pivot - new Vector2(0.5f, 0.5f)) * viewport.rect.size;
            return local + pivotOffset;
        }

        private void Update()
        {
            if (content == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            HandlePinch();

            if (isAnimating)
            {
                animTime += dt;
                float t = Mathf.Clamp01(animTime / animDuration);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                float z = Mathf.Lerp(animFromZoom, animToZoom, eased);
                content.localScale = new Vector3(z, z, 1f);
                content.anchoredPosition = Vector2.Lerp(animFromPos, animToPos, eased);
                if (t >= 1f) isAnimating = false;
                OnViewChanged?.Invoke();
                return;
            }

            if (isDragging || isPinching) return;

            StepInertia(dt);
        }

        /// <summary>Inertia + elastic spring back. Exposed for tests.</summary>
        public void StepInertia(float dt)
        {
            if (content == null || dt <= 0f) return;

            Vector2 pos = content.anchoredPosition;
            Vector2 max = CurrentMaxOffset;
            bool changed = false;

            for (int axis = 0; axis < 2; axis++)
            {
                float limit = max[axis];
                float overshoot = pos[axis] > limit ? pos[axis] - limit : (pos[axis] < -limit ? pos[axis] + limit : 0f);

                if (Mathf.Abs(overshoot) > 0.01f)
                {
                    float target = pos[axis] - overshoot;
                    float v = elasticVelocity[axis];
                    pos[axis] = Mathf.SmoothDamp(pos[axis], target, ref v, elasticity, Mathf.Infinity, dt);
                    elasticVelocity[axis] = v;
                    velocity[axis] = 0f;
                    changed = true;
                }
                else if (Mathf.Abs(velocity[axis]) > 1f)
                {
                    velocity[axis] *= Mathf.Pow(decelerationRate, dt);
                    pos[axis] += velocity[axis] * dt;
                    changed = true;
                }
                else
                {
                    velocity[axis] = 0f;
                }
            }

            if (changed)
            {
                content.anchoredPosition = pos;
                OnViewChanged?.Invoke();
            }
        }

        public void SetVelocity(Vector2 v) => velocity = v;
        public Vector2 Velocity => velocity;

        private void HandlePinch()
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null)
            {
                isPinching = false;
                return;
            }

            int pressed = 0;
            Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
            var touches = ts.touches;
            for (int i = 0; i < touches.Count && pressed < 2; i++)
            {
                if (!touches[i].press.isPressed) continue;
                if (pressed == 0) p0 = touches[i].position.ReadValue();
                else p1 = touches[i].position.ReadValue();
                pressed++;
            }

            if (pressed < 2)
            {
                isPinching = false;
                return;
            }

            float dist = Vector2.Distance(p0, p1);
            if (!isPinching)
            {
                isPinching = true;
                isDragging = false;
                isAnimating = false;
                velocity = Vector2.zero;
                lastPinchDistance = dist;
                return;
            }

            if (lastPinchDistance > 1f && dist > 1f)
            {
                Vector2 mid = ScreenToViewportLocal((p0 + p1) * 0.5f);
                ZoomByFactor(dist / lastPinchDistance, mid, 0f);
            }
            lastPinchDistance = dist;
        }
    }
}
