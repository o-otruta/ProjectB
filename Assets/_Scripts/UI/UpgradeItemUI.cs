using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// Meta upgrade row (Sprint 13.2): icon in a frame, name, <c>Lv 3/20</c> with a segmented progress strip,
    /// delta with units (<c>+24% → +32%</c>) and a price button in three states — affordable, not enough coins
    /// (dimmed, red price, error shake on tap) and <c>MAX</c>.
    /// </summary>
    public class UpgradeItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Image iconImage;

        [Header("Sprint 13")]
        [SerializeField] private TMP_Text deltaText;
        [SerializeField] private Transform segmentsContainer;
        [SerializeField] private Image segmentTemplate;
        [SerializeField] private Image costIcon;
        [SerializeField] private ThemedButton themedBuyButton;

        private MetaUpgradeData upgradeData;
        private MetaUpgradeManager upgradeManager;
        private readonly List<Image> segments = new List<Image>(20);
        private int lastLevel = -1;

        public Button BuyButton => buyButton;
        public string UpgradeId => upgradeData != null ? upgradeData.id : null;

        public void Setup(MetaUpgradeData data, MetaUpgradeManager manager)
        {
            this.upgradeData = data;
            this.upgradeManager = manager;

            if (nameText != null) nameText.text = Loc.Name(data);
            if (descriptionText != null) descriptionText.text = Loc.Desc(data);
            if (iconImage != null && data.icon != null) iconImage.sprite = data.icon;
            if (segmentTemplate != null) segmentTemplate.gameObject.SetActive(false);

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

            UiTheme theme = UiTheme.Current;
            int currentLevel = upgradeManager.GetCurrentLevel(upgradeData.id);
            int cost = upgradeManager.GetUpgradeCost(upgradeData.id);
            bool isMaxLevel = currentLevel >= upgradeData.maxLevel;
            bool canAfford = !isMaxLevel && upgradeManager.CanAfford(upgradeData.id);

            float currentEffect = currentLevel * upgradeData.effectPerLevel;
            float nextEffect = (currentLevel + 1) * upgradeData.effectPerLevel;

            string localizedName = Loc.Name(upgradeData);
            if (descriptionText != null) descriptionText.text = Loc.Desc(upgradeData);

            if (deltaText != null)
            {
                deltaText.text = FormatDelta(upgradeData.effectType, currentEffect, nextEffect, isMaxLevel);
                if (nameText != null) nameText.text = localizedName;
            }
            else if (nameText != null)
            {
                string effectStr = isMaxLevel ? $"(+{currentEffect})" : $"(+{currentEffect} > +{nextEffect})";
                nameText.text = $"{localizedName} <color=#AAAAAA><size=70%>{effectStr}</size></color>";
            }

            if (levelText != null)
            {
                string lvLabel = Loc.Format("ui.upgrade.level", currentLevel, upgradeData.maxLevel);
                levelText.text = isMaxLevel
                    ? $"{lvLabel}  <color=#{ColorUtility.ToHtmlStringRGB(theme.gold)}>{Loc.Get("ui.common.max", "MAX")}</color>"
                    : lvLabel;
            }

            RefreshSegments(currentLevel, upgradeData.maxLevel);

            if (costText != null)
            {
                costText.text = isMaxLevel ? Loc.Get("ui.common.max", "MAX") : UiFormat.Grouped(cost);
                costText.color = isMaxLevel || canAfford ? theme.textPrimary : theme.danger;
            }
            if (costIcon != null) costIcon.gameObject.SetActive(!isMaxLevel);

            if (buyButton != null)
            {
                buyButton.interactable = canAfford;
            }
            if (themedBuyButton != null)
            {
                // Green only when the purchase is actually possible; MAX / not enough coins → neutral grey-blue face.
                themedBuyButton.SetStyle(canAfford ? ButtonStyle.Success : ButtonStyle.Secondary);
                if (costText != null) costText.color = isMaxLevel ? theme.gold : (canAfford ? theme.white : theme.danger);
            }

            if (lastLevel >= 0 && currentLevel > lastLevel && Application.isPlaying)
            {
                UiTween.Punch(transform, 0.05f, 0.25f);
            }
            lastLevel = currentLevel;
        }

        private void RefreshSegments(int level, int maxLevel)
        {
            if (segmentsContainer == null || segmentTemplate == null) return;
            UiTheme theme = UiTheme.Current;
            maxLevel = Mathf.Clamp(maxLevel, 1, 30);

            while (segments.Count < maxLevel)
            {
                Image seg = Instantiate(segmentTemplate, segmentsContainer);
                seg.gameObject.SetActive(true);
                segments.Add(seg);
            }

            for (int i = 0; i < segments.Count; i++)
            {
                bool visible = i < maxLevel;
                segments[i].gameObject.SetActive(visible);
                if (visible) segments[i].color = i < level ? (level >= maxLevel ? theme.gold : theme.success) : theme.surfaceSunken;
            }
        }

        /// <summary>"+24% → +32%" with per-effect units (%, ×, Lv, slot).</summary>
        public static string FormatDelta(MetaUpgradeEffectType type, float current, float next, bool isMax)
        {
            string cur = FormatEffect(type, current);
            return isMax ? cur : $"{cur} → {FormatEffect(type, next)}";
        }

        public static string FormatEffect(MetaUpgradeEffectType type, float value)
        {
            string number = Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            switch (type)
            {
                case MetaUpgradeEffectType.CritMultiplier:
                    return $"+{number}×";
                case MetaUpgradeEffectType.StartLevel:
                    return $"+{number} {Loc.Get("ui.unit.level_short", "Lv")}";
                case MetaUpgradeEffectType.BonusActiveSlots:
                case MetaUpgradeEffectType.BonusPassiveSlots:
                    return $"+{number} {Loc.Get("ui.unit.slot", "slot")}";
                default:
                    return $"+{number}%";
            }
        }

        private void OnBuyClicked()
        {
            if (upgradeManager.TryBuyUpgrade(upgradeData.id))
            {
                UiAudio.Confirm();
            }
            else
            {
                DevLog.Log($"[UpgradeItemUI] Purchase failed for {upgradeData.id}. Not enough coins or max level.");
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
