using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    public class UpgradeItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Image iconImage;

        private MetaUpgradeData upgradeData;
        private MetaUpgradeManager upgradeManager;

        public void Setup(MetaUpgradeData data, MetaUpgradeManager manager)
        {
            this.upgradeData = data;
            this.upgradeManager = manager;

            if (nameText != null) nameText.text = data.displayName;
            if (descriptionText != null) descriptionText.text = data.description;
            if (iconImage != null && data.icon != null) iconImage.sprite = data.icon;

            // Subscribe to button click
            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyClicked);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (upgradeData == null || upgradeManager == null) return;

            int currentLevel = upgradeManager.GetCurrentLevel(upgradeData.id);
            int cost = upgradeManager.GetUpgradeCost(upgradeData.id);
            bool isMaxLevel = currentLevel >= upgradeData.maxLevel;

            if (nameText != null)
            {
                float currentEffect = currentLevel * upgradeData.effectPerLevel;
                float nextEffect = (currentLevel + 1) * upgradeData.effectPerLevel;
                
                string effectStr = isMaxLevel ? $"(+{currentEffect})" : $"(+{currentEffect} > +{nextEffect})";
                nameText.text = $"{upgradeData.displayName} <color=#AAAAAA><size=70%>{effectStr}</size></color>";
            }

            if (levelText != null)
            {
                levelText.text = isMaxLevel ? "MAX" : $"Lv.{currentLevel}/{upgradeData.maxLevel}";
            }

            if (costText != null)
            {
                costText.text = isMaxLevel ? "MAX" : cost.ToString();
            }

            if (buyButton != null)
            {
                buyButton.interactable = !isMaxLevel && upgradeManager.CanAfford(upgradeData.id);
            }
        }

        private void OnBuyClicked()
        {
            if (upgradeManager.TryBuyUpgrade(upgradeData.id))
            {
                // Refresh will be handled globally by MetaUpgradeUI listening to the manager
            }
            else
            {
                Debug.Log($"[UpgradeItemUI] Purchase failed for {upgradeData.id}. Not enough coins or max level.");
            }
        }
        private void OnDestroy()
        {
            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(OnBuyClicked);
            }
        }
    }
}
