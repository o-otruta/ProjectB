using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectB.UI
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleLimit = 100f;
        [SerializeField] private bool isFloating = true;

        public Vector2 Direction { get; private set; }

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

            Vector2 position;
            // Преобразуем координаты экрана в локальные координаты фона джойстика
            RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position);
            
            // Нормализуем направление, если оно выходит за пределы круга
            Direction = position / handleLimit;
            if (Direction.magnitude > 1f)
            {
                Direction = Direction.normalized;
            }

            // Двигаем ручку джойстика
            handle.anchoredPosition = Direction * handleLimit;
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
