using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Meta;
using ProjectB.Data;

namespace ProjectB.UI
{
    /// <summary>
    /// Upgrades tab. Permanent coin-based meta-upgrades list; rows refresh whenever the coin balance changes.
    /// </summary>
    public class MetaUpgradeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private UpgradeItemUI itemPrefab;

        private MetaUpgradeManager upgradeManager;
        private SaveManager saveManager;
        private readonly List<UpgradeItemUI> spawnedItems = new List<UpgradeItemUI>();
        private Action cachedLanguageChanged;

        private void OnEnable()
        {
            cachedLanguageChanged ??= RefreshAllItems;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
        }

        [Inject]
        public void Construct(MetaUpgradeManager upgradeManager, SaveManager saveManager = null)
        {
            this.upgradeManager = upgradeManager;
            this.saveManager = saveManager;

            if (this.upgradeManager != null)
            {
                this.upgradeManager.OnUpgradePurchased += HandleUpgradePurchased;
                InitializeList();
            }

            if (this.saveManager != null)
            {
                this.saveManager.OnDataChanged += HandleDataChanged;
            }
        }

        private void OnDestroy()
        {
            if (upgradeManager != null)
            {
                upgradeManager.OnUpgradePurchased -= HandleUpgradePurchased;
            }
            if (saveManager != null)
            {
                saveManager.OnDataChanged -= HandleDataChanged;
            }
        }

        private void InitializeList()
        {
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
            RefreshAllItems();
        }

        private void HandleDataChanged()
        {
            if (panel != null && panel.activeInHierarchy) RefreshAllItems();
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

        public Selectable GetFirstSelectable()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null && spawnedItems[i].BuyButton != null &&
                    spawnedItems[i].BuyButton.gameObject.activeInHierarchy && spawnedItems[i].BuyButton.interactable)
                {
                    return spawnedItems[i].BuyButton;
                }
            }
            return UiFocusHelper.FindFirstInteractable(panel != null ? panel.transform : transform);
        }

        /// <summary>FTUE: buy button of the cheapest upgrade the player can afford right now (null if none).</summary>
        public Button GetCheapestAffordableBuyButton()
        {
            if (upgradeManager == null) return null;
            Button best = null;
            int bestCost = int.MaxValue;
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                UpgradeItemUI item = spawnedItems[i];
                if (item == null || item.BuyButton == null || string.IsNullOrEmpty(item.UpgradeId)) continue;
                if (!upgradeManager.CanAfford(item.UpgradeId)) continue;
                int cost = upgradeManager.GetUpgradeCost(item.UpgradeId);
                if (cost >= 0 && cost < bestCost)
                {
                    bestCost = cost;
                    best = item.BuyButton;
                }
            }
            return best;
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
