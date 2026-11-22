using System;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectB.Arena
{
    /// <summary>
    /// Vacuum Magnet pickup dropped by Mini-Bosses and Bosses.
    /// Pulls all active XP crystals and coins on the entire arena toward the hero.
    /// </summary>
    public class VacuumMagnetPickup : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 2.0f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobSpeed = 3.0f;
        [SerializeField] private float rotateSpeed = 90f;

        private Transform heroTarget;
        private Action<VacuumMagnetPickup> onCollected;
        private IObjectPool<VacuumMagnetPickup> pool;
        private Vector3 baseVisualLocalPos;
        private bool basePosCaptured;
        private bool isCollected;

        public bool IsCollected => isCollected;

        public void Initialize(
            Transform hero,
            Action<VacuumMagnetPickup> collectedCallback,
            IObjectPool<VacuumMagnetPickup> pickupPool = null)
        {
            heroTarget = hero;
            onCollected = collectedCallback;
            pool = pickupPool;
            isCollected = false;

            if (visualRoot != null)
            {
                if (!basePosCaptured)
                {
                    baseVisualLocalPos = visualRoot.localPosition;
                    basePosCaptured = true;
                }
                visualRoot.localPosition = baseVisualLocalPos;
                visualRoot.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (isCollected || Time.deltaTime <= 0f) return;

            if (visualRoot != null)
            {
                visualRoot.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.Self);
                float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                visualRoot.localPosition = baseVisualLocalPos + new Vector3(0f, bob, 0f);
            }

            if (heroTarget != null)
            {
                Vector3 diff = heroTarget.position - transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude <= pickupRadius * pickupRadius)
                {
                    Collect();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected || other == null) return;

            if (heroTarget != null &&
                (other.transform == heroTarget || other.transform.IsChildOf(heroTarget)))
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            onCollected?.Invoke(this);

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
