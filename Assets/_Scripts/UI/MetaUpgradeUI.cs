using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Meta;
using ProjectB.Data;

namespace ProjectB.UI
{
    public class MetaUpgradeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private UpgradeItemUI itemPrefab;

        private MetaUpgradeManager upgradeManager;
        private List<UpgradeItemUI> spawnedItems = new List<UpgradeItemUI>();

        [Inject]
        public void Construct(MetaUpgradeManager upgradeManager)
        {
            this.upgradeManager = upgradeManager;
            
            if (this.upgradeManager != null)
            {
                this.upgradeManager.OnUpgradePurchased += HandleUpgradePurchased;
                InitializeList();
            }
        }

        private void OnDestroy()
        {
            if (upgradeManager != null)
            {
                upgradeManager.OnUpgradePurchased -= HandleUpgradePurchased;
            }
        }

        private void InitializeList()
        {
            // Очищаем старые элементы, если есть
            foreach (var item in spawnedItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            spawnedItems.Clear();

            if (itemPrefab == null || itemsContainer == null || upgradeManager == null)
            {
                Debug.LogWarning("[MetaUpgradeUI] Missing references or UpgradeManager is null.");
                return;
            }

            // Спавним UI элементы для каждого апгрейда
            var upgrades = upgradeManager.GetAllUpgrades();
            foreach (var upgradeData in upgrades)
            {
                var itemUI = Instantiate(itemPrefab, itemsContainer);
                itemUI.Setup(upgradeData, upgradeManager);
                spawnedItems.Add(itemUI);
            }
        }

        private void HandleUpgradePurchased(string upgradeId)
        {
            // Обновляем все элементы, так как покупка одного может повлиять на возможность купить другие 
            // (из-за того, что изменилось количество денег).
            // В идеале SaveManager кидает ивент изменения денег, и мы слушаем его.
            RefreshAllItems();
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            RefreshAllItems();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
        
        public void Toggle()
        {
            if (panel != null)
            {
                if (panel.activeSelf) Hide();
                else Show();
            }
        }

        private void RefreshAllItems()
        {
            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.Refresh();
                }
            }
        }
    }
}
