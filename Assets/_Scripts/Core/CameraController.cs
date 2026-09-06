using UnityEngine;
using VContainer;
using ProjectB.Player;

namespace ProjectB.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("The character the camera should follow")]
        public Transform target;
        
        [Inject]
        public void Construct(HeroMovement hero)
        {
            if (target == null && hero != null)
            {
                target = hero.transform;
            }
        }
        
        [Tooltip("Offset relative to the target (e.g. 0, 20, -10 for a steeper top-down view)")]
        public Vector3 offset = new Vector3(0, 20f, -10f);
        
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
        private float defaultOrthoSize;
        private float lastAspect;

        public float DefaultOrthoSize => defaultOrthoSize;
        public float DefaultFOV => defaultFOV;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                defaultFOV = cam.fieldOfView;
                defaultOrthoSize = cam.orthographicSize;
            }
        }

        void Start()
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
                defaultFOV = cam.fieldOfView;
                defaultOrthoSize = cam.orthographicSize;
            }
            
            // Автоматически поворачиваем камеру так, чтобы она смотрела на цель (исходя из offset)
            if (offset != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(-offset);
            }
            
            if (Screen.height > 0)
            {
                lastAspect = (float)Screen.width / Screen.height;
                AdjustAspect();
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            
            if (Screen.height > 0)
            {
                float currentAspect = (float)Screen.width / Screen.height;
                if (Mathf.Abs(currentAspect - lastAspect) > 0.01f)
                {
                    lastAspect = currentAspect;
                    AdjustAspect();
                }
            }

            // Следование за целью (SmoothDamp при smoothTime > 0, либо мгновенный снап)
            Vector3 targetPosition = target.position + offset;
            if (smoothTime > 0f)
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        public void SetDefaultOrthoSize(float newOrthoSize)
        {
            defaultOrthoSize = newOrthoSize;
            AdjustAspect();
        }

        public void SetDefaultFOV(float newFOV)
        {
            defaultFOV = newFOV;
            AdjustAspect();
        }

        private void AdjustAspect()
        {
            if (!maintainHorizontalView || cam == null || Screen.height <= 0) return;

            float currentAspect = (float)Screen.width / Screen.height;

            // Если экран "уже" (например 9:19.5), чем наш базовый 9:16
            if (currentAspect < targetAspect)
            {
                if (cam.orthographic)
                {
                    // Для ортографической камеры увеличиваем размер относительно закешированного базового размера
                    cam.orthographicSize = defaultOrthoSize * (targetAspect / currentAspect);
                }
                else
                {
                    // Для перспективной камеры используем встроенные методы Unity для пересчета FOV
                    float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(defaultFOV, targetAspect);
                    cam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(horizontalFOV, currentAspect);
                }
            }
            else
            {
                // Если экран шире или равен целевому соотношению, восстанавливаем базовые параметры
                if (cam.orthographic)
                {
                    cam.orthographicSize = defaultOrthoSize;
                }
                else
                {
                    cam.fieldOfView = defaultFOV;
                }
            }
        }
    }
}
