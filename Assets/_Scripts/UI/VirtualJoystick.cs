using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectB.UI
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Components")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Header("Limit & Sensitivity Settings")]
        [Tooltip("Автоматически рассчитывать лимит хода ручки из радиуса фона (background.rect.width * 0.5f).")]
        [SerializeField] private bool autoHandleLimit = true;

        [Tooltip("Базовый лимит хода ручки (в единицах Canvas/пикселях). Используется при выключенном autoHandleLimit или отсутствии background.")]
        [SerializeField] private float handleLimit = 100f;

        [Tooltip("Коэффициент максимального отклонения ручки (1.0 = до края подложки).")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float handleRange = 1.0f;

        [Tooltip("Мертвая зона джойстика (0..0.5), в пределах которой Direction равен нулю.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float deadZone = 0f;

        [Tooltip("Учитывать DPI экрана для нормализации физической чувствительности (если Canvas не масштабируется).")]
        [SerializeField] private bool normalizeWithDpi = false;

        [Tooltip("Базовый DPI экрана для нормализации (160 DPI — baseline Android mdpi).")]
        [SerializeField] private float referenceDpi = 160f;

        [Header("Behavior")]
        [SerializeField] private bool isFloating = true;

        private Canvas parentCanvas;

        public Vector2 Direction { get; private set; }

        /// <summary>
        /// Текущий эффективный лимит хода ручки с учетом радиуса фона, коэффициента диапазона и DPI.
        /// </summary>
        public float CurrentHandleLimit
        {
            get
            {
                float limit = handleLimit;
                if (autoHandleLimit && background != null)
                {
                    float bgRadius = Mathf.Min(background.rect.width, background.rect.height) * 0.5f;
                    if (bgRadius <= 0f)
                    {
                        bgRadius = Mathf.Min(Mathf.Abs(background.sizeDelta.x), Mathf.Abs(background.sizeDelta.y)) * 0.5f;
                    }

                    if (bgRadius > 0f)
                    {
                        limit = bgRadius;
                    }
                }

                limit *= handleRange;

                if (normalizeWithDpi && Screen.dpi > 0f)
                {
                    Canvas canvas = GetCanvas();
                    float canvasScale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                    float dpiScale = Screen.dpi / Mathf.Max(referenceDpi, 1f);
                    limit *= (dpiScale / canvasScale);
                }

                return Mathf.Max(limit, 1f);
            }
        }

        private void Awake()
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        private Canvas GetCanvas()
        {
            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
            return parentCanvas;
        }

        private void Start()
        {
            if (isFloating && background != null)
            {
                background.gameObject.SetActive(false);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isFloating && background != null)
            {
                background.gameObject.SetActive(true);
                
                // Position the background exactly at the touch position
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    GetComponent<RectTransform>(), 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out Vector2 localPoint);
                    
                background.localPosition = localPoint;
            }
            
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null) return;

            // Преобразуем координаты экрана в локальные координаты фона джойстика
            RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out Vector2 position);
            
            float limit = CurrentHandleLimit;

            // Вычисляем нормализованное отклонение
            Vector2 rawInput = position / limit;
            float magnitude = rawInput.magnitude;

            if (magnitude > deadZone)
            {
                if (magnitude > 1f)
                {
                    Direction = rawInput.normalized;
                }
                else
                {
                    if (deadZone > 0f)
                    {
                        Direction = rawInput.normalized * ((magnitude - deadZone) / (1f - deadZone));
                    }
                    else
                    {
                        Direction = rawInput;
                    }
                }
            }
            else
            {
                Direction = Vector2.zero;
            }

            // Двигаем ручку джойстика в пределах допустимого радиуса
            Vector2 handlePos = magnitude > 1f ? rawInput.normalized * limit : position;
            handle.anchoredPosition = handlePos;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (isFloating && background != null)
            {
                background.gameObject.SetActive(false);
            }
        }
    }
}
