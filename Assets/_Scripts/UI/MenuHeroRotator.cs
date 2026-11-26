using UnityEngine;

namespace ProjectB.UI
{
    public enum PreviewMotion
    {
        /// <summary>Continuous turntable spin.</summary>
        Spin = 0,
        /// <summary>Gentle idle sway around the authored facing (the hero keeps facing the camera).</summary>
        Sway = 1
    }

    public class MenuHeroRotator : MonoBehaviour
    {
        public PreviewMotion motion = PreviewMotion.Sway;
        [Tooltip("Spin: degrees per second.")]
        public float speed = 30f;
        [Tooltip("Sway: max yaw away from the authored facing, degrees.")]
        public float swayAmplitude = 22f;
        [Tooltip("Sway: seconds per full left-right cycle.")]
        [Min(0.1f)] public float swayPeriod = 5f;

        private float baseYaw;
        private float swayTime;

        private void Awake()
        {
            baseYaw = transform.localEulerAngles.y;
            ConfigureAsPreviewDummy();
        }

        /// <summary>Yaw of the sway motion at <paramref name="time"/> seconds.</summary>
        public static float EvaluateSwayYaw(float baseYaw, float amplitude, float period, float time)
        {
            return baseYaw + Mathf.Sin(time * (Mathf.PI * 2f) / Mathf.Max(0.1f, period)) * amplitude;
        }

        /// <summary>
        /// Disables physics simulation/interpolation and gameplay components when Hero prefab is used as a menu preview.
        /// </summary>
        public void ConfigureAsPreviewDummy()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            if (TryGetComponent<Collider>(out var col))
            {
                col.enabled = false;
            }

            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb != null && mb != this)
                {
                    mb.enabled = false;
                }
            }
        }

        private void Update()
        {
            if (motion == PreviewMotion.Spin)
            {
                transform.Rotate(0, speed * Time.deltaTime, 0);
                return;
            }

            swayTime += Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0f, EvaluateSwayYaw(baseYaw, swayAmplitude, swayPeriod, swayTime), 0f);
        }
    }
}
