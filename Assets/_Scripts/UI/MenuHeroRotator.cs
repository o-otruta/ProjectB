using UnityEngine;

namespace ProjectB.UI
{
    public class MenuHeroRotator : MonoBehaviour
    {
        public float speed = 30f;

        private void Awake()
        {
            ConfigureAsPreviewDummy();
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
            transform.Rotate(0, speed * Time.deltaTime, 0);
        }
    }
}
