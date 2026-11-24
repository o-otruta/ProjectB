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
    /// Upgrades tab. Sprint 13.2: a wide Constellation banner (starfield + Boss Shard counter) above the list
    /// replaces the clipped STARS button; rows refresh whenever the coin balance changes.
    /// </summary>
    public class MetaUpgradeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private UpgradeItemUI itemPrefab;

        [Header("Sprint 13: Constellation banner")]
        [SerializeField] private Button constellationBanner;
        [SerializeField] private TMP_Text bannerShardsText;
        [SerializeField] private TMP_Text bannerProgressText;

        private MetaUpgradeManager upgradeManager;
        private SaveManager saveManager;
        private TalentTreeManager talentTreeManager;
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
        public void Construct(MetaUpgradeManager upgradeManager, SaveManager saveManager = null, TalentTreeManager talentTreeManager = null)
        {
            this.upgradeManager = upgradeManager;
            this.saveManager = saveManager;
            this.talentTreeManager = talentTreeManager;

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

        private void RefreshAllItems()
        {
            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.Refresh();
                }
            }
            RefreshBanner();
        }

        private void RefreshBanner()
        {
            if (bannerShardsText != null && saveManager != null && saveManager.Data != null)
            {
                bannerShardsText.text = UiFormat.Grouped(saveManager.Data.bossShards);
            }
            if (bannerProgressText != null && talentTreeManager != null)
            {
                bannerProgressText.text = Loc.Format("ui.constellation.stars_lit_short", talentTreeManager.UnlockedCount, Mathf.Max(0, talentTreeManager.Nodes.Count - 1));
            }
        }
    }
}
