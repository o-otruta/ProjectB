using System;
using UnityEngine;

namespace ProjectB.Arena
{
    public enum SecretItemType
    {
        TreasureChest = 0,
        AncientScroll = 1,
        MysteryCrystal = 2
    }

    /// <summary>
    /// Interactive secret pickup spawned on the arena (Chest, Ancient Scroll, Mystery Crystal).
    /// Features bobbing/rotation visuals and lightweight distance pickup check with trigger collider.
    /// </summary>
    public class InteractableItem : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private SecretItemType itemType = SecretItemType.TreasureChest;
        [SerializeField] private string itemId = "secret_chest";
        [SerializeField] private string displayName = "Treasure Chest";

        [Header("Pickup & Visuals")]
        [SerializeField] private float pickupRadius = 1.8f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float bobSpeed = 2.5f;
        [SerializeField] private float rotateSpeed = 55f;

        private Transform heroTarget;
        private Action<InteractableItem> onCollected;
        private Vector3 baseVisualLocalPos;
        private bool isCollected;

        public SecretItemType ItemType => itemType;
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public bool IsCollected => isCollected;

        public void Initialize(Transform hero, Action<InteractableItem> collectedCallback)
        {
            heroTarget = hero;
            onCollected = collectedCallback;
            isCollected = false;

            if (visualRoot != null)
            {
                baseVisualLocalPos = visualRoot.localPosition;
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
            if (isCollected) return;
            if (heroTarget != null && (other.transform == heroTarget || other.transform.IsChildOf(heroTarget)))
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;
            onCollected?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
