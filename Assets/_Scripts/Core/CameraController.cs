using UnityEngine;

namespace ProjectB.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("The character the camera should follow")]
        public Transform target;
        
        [Tooltip("Offset relative to the target (e.g. 0, 15, -15 for 45 degrees)")]
        public Vector3 offset = new Vector3(0, 15f, -15f);
        
        [Tooltip("How smoothly the camera catches up to the target")]
        public float smoothTime = 0.0f;
        
        [Header("Aspect Ratio Handling")]
        [Tooltip("If true, the camera will expand its vertical view on longer screens to prevent cropping sides")]
        public bool maintainHorizontalView = true;
        
        [Tooltip("Base aspect ratio (default is 9:16 = 0.5625)")]
        public float targetAspect = 9f / 16f;

        private Camera cam;
        private Vector3 velocity = Vector3.zero;
        private float defaultFOV;

        void Start()
        {
            cam = GetComponent<Camera>();
            defaultFOV = cam.fieldOfView;
            
            // Устанавливаем поворот 45 градусов вниз (top-down 45°)
            transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            
            AdjustAspect();
        }

        void LateUpdate()
        {
            if (target == null) return;

            // Плавное следование за целью через SmoothDamp
            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }

        private void AdjustAspect()
        {
            if (!maintainHorizontalView) return;

            float currentAspect = (float)Screen.width / Screen.height;

            // Если экран "уже" (например 9:19.5), чем наш базовый 9:16
            if (currentAspect < targetAspect)
            {
                if (cam.orthographic)
                {
                    // Для ортографической камеры увеличиваем размер
                    float defaultOrthoSize = cam.orthographicSize;
                    cam.orthographicSize = defaultOrthoSize * (targetAspect / currentAspect);
                }
                else
                {
                    // Для перспективной камеры используем встроенные методы Unity для пересчета FOV
                    float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(defaultFOV, targetAspect);
                    cam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(horizontalFOV, currentAspect);
                }
            }
        }
    }
}
