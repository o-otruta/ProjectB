using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.LevelUp;

namespace ProjectB.UI
{
    /// <summary>
    /// Ability / synergy card of the collection. Sprint 13.2: rarity frame, rarity-tinted meta tag, synergy chips
    /// ("icon + icon → name") instead of a yellow text line, locked entries as a dark silhouette + lock + condition.
    /// All colours come from <see cref="UiTheme"/>.
    /// </summary>
    public class AbilityCollectionItemUI : MonoBehaviour
    {
        public struct SynergyChip
        {
            public Sprite leftIcon;
            public Sprite rightIcon;
            public string name;
            public bool unlocked;
        }

        [SerializeField] private Image cardBackground;
        [SerializeField] private Image rarityStripe;
        [SerializeField] private Image iconBackground;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI initialsText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI metaTagText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Sprint 13")]
        [SerializeField] private Image frameImage;
        [SerializeField] private GameObject lockBadge;
        [SerializeField] private Transform synergyChipsContainer;
        [SerializeField] private GameObject synergyChipTemplate;

        private readonly List<GameObject> chipInstances = new List<GameObject>(4);

        public bool SupportsSynergyChips => synergyChipsContainer != null && synergyChipTemplate != null;

        public void Setup(AbilityData data, bool isUnlocked, string unlockRequirementText, string synergyHint = null)
        {
            if (data == null) return;

            UiTheme theme = UiTheme.Current;
            Color rarityColor = theme.GetRarityColor(data.rarity);
            Color lockedRarity = UiTheme.Shade(rarityColor, 0.45f);

            if (cardBackground != null) cardBackground.color = isUnlocked ? theme.surface : theme.surfaceSunken;
            if (frameImage != null) frameImage.color = isUnlocked ? rarityColor : UiTheme.WithAlpha(lockedRarity, 0.7f);
            if (rarityStripe != null) rarityStripe.color = isUnlocked ? rarityColor : lockedRarity;
            if (iconBackground != null) iconBackground.color = isUnlocked ? theme.surfaceRaised : theme.surfaceSunken;
            if (lockBadge != null) lockBadge.SetActive(!isUnlocked);

            bool hasIcon = data.icon != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    iconImage.sprite = data.icon;
                    // Locked: dark silhouette of the icon.
                    iconImage.color = isUnlocked ? Color.white : UiTheme.WithAlpha(theme.background, 0.95f);
                }
            }

            string localizedName = Loc.Name(data);
            string localizedDesc = Loc.Desc(data);

            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(!hasIcon);
                if (!hasIcon)
                {
                    initialsText.text = AbilityHudSlotUI.GetInitials(localizedName);
                    initialsText.color = isUnlocked ? rarityColor : theme.textDisabled;
                }
            }

            if (nameText != null)
            {
                nameText.text = localizedName;
                nameText.color = isUnlocked ? theme.textPrimary : theme.textSecondary;
            }

            if (metaTagText != null)
            {
                string typeLabel = data.type == AbilityType.Passive ? Loc.Get("ui.abilities.passive", "Passive") : Loc.Get("ui.abilities.active", "Active");
                string rarityLabel = Loc.Get($"ui.rarity.{data.rarity.ToString().ToLowerInvariant()}", data.rarity.ToString());
                metaTagText.text = $"{typeLabel} • {rarityLabel}";
                metaTagText.color = isUnlocked ? rarityColor : theme.textDisabled;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(synergyHint)
                    ? localizedDesc
                    : $"{localizedDesc}\n<color=#{ColorUtility.ToHtmlStringRGB(theme.gold)}>{synergyHint}</color>";
                descriptionText.color = isUnlocked ? theme.textSecondary : theme.textDisabled;
            }

            if (statusText != null)
            {
                if (isUnlocked)
                {
                    statusText.text = Loc.Get("ui.common.unlocked", "Unlocked");
                    statusText.color = theme.success;
                    statusText.gameObject.SetActive(!SupportsSynergyChips);
                }
                else
                {
                    statusText.gameObject.SetActive(true);
                    statusText.text = string.IsNullOrEmpty(unlockRequirementText)
                        ? Loc.Get("ui.common.locked", "Locked")
                        : $"<sprite name=\"lock\"> {unlockRequirementText}";
                    statusText.color = theme.warning;
                }
            }
        }

        public void SetupSynergy(SynergyData synergy, string comboPairLabel, bool isUnlocked)
        {
            if (synergy == null) return;

            UiTheme theme = UiTheme.Current;
            Color accentColor = theme.shard;

            if (cardBackground != null) cardBackground.color = isUnlocked ? theme.surface : theme.surfaceSunken;
            if (frameImage != null) frameImage.color = isUnlocked ? accentColor : UiTheme.WithAlpha(UiTheme.Shade(accentColor, 0.45f), 0.7f);
            if (rarityStripe != null) rarityStripe.color = isUnlocked ? accentColor : UiTheme.Shade(accentColor, 0.45f);
            if (iconBackground != null) iconBackground.color = theme.surfaceRaised;
            if (lockBadge != null) lockBadge.SetActive(!isUnlocked);

            Sprite icon = synergy.icon;
            bool hasIcon = icon != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    iconImage.sprite = icon;
                    iconImage.color = isUnlocked ? Color.white : UiTheme.WithAlpha(theme.background, 0.95f);
                }
            }

            string localizedSynergyName = Loc.Name(synergy);
            string localizedSynergyDesc = Loc.Desc(synergy);

            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(!hasIcon);
                if (!hasIcon)
                {
                    initialsText.text = AbilityHudSlotUI.GetInitials(localizedSynergyName);
                    initialsText.color = accentColor;
                }
            }

            if (nameText != null)
            {
                nameText.text = localizedSynergyName;
                nameText.color = isUnlocked ? theme.textPrimary : theme.textSecondary;
            }

            if (metaTagText != null)
            {
                metaTagText.text = SupportsSynergyChips || string.IsNullOrEmpty(comboPairLabel)
                    ? Loc.Get("ui.abilities.reactive_synergy", "Reactive Synergy")
                    : $"{Loc.Get("ui.abilities.synergy_tag", "Synergy")} • {comboPairLabel}";
                metaTagText.color = accentColor;
            }

            if (descriptionText != null)
            {
                descriptionText.text = localizedSynergyDesc;
                descriptionText.color = isUnlocked ? theme.textSecondary : theme.textDisabled;
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = isUnlocked
                    ? Loc.Get("ui.abilities.synergy_auto", "Auto-activates in run")
                    : Loc.Get("ui.abilities.synergy_unlock_hint", "<sprite name=\"lock\"> Unlock in the Constellation Tree");
                statusText.color = isUnlocked ? theme.success : theme.warning;
            }
        }

        /// <summary>Synergy chips "icon + icon → name" (unlocked chips bright, locked chips dimmed).</summary>
        public void SetSynergyChips(IReadOnlyList<SynergyChip> chips)
        {
            if (!SupportsSynergyChips) return;
            synergyChipTemplate.SetActive(false);
            UiTheme theme = UiTheme.Current;
            int count = chips != null ? chips.Count : 0;

            while (chipInstances.Count < count)
            {
                chipInstances.Add(Instantiate(synergyChipTemplate, synergyChipsContainer));
            }

            for (int i = 0; i < chipInstances.Count; i++)
            {
                GameObject chip = chipInstances[i];
                bool visible = i < count;
                chip.SetActive(visible);
                if (!visible) continue;

                SynergyChip data = chips[i];
                Transform t = chip.transform;
                SetIcon(t.Find("Left"), data.leftIcon, data.unlocked);
                SetIcon(t.Find("Right"), data.rightIcon, data.unlocked);
                var label = t.Find("Name") != null ? t.Find("Name").GetComponent<TMP_Text>() : null;
                if (label != null)
                {
                    label.text = data.name;
                    label.color = data.unlocked ? theme.shard : theme.textDisabled;
                }
                var bg = chip.GetComponent<Image>();
                if (bg != null) bg.color = UiTheme.WithAlpha(data.unlocked ? theme.shard : theme.outline, 0.18f);
            }

            synergyChipsContainer.gameObject.SetActive(count > 0);
        }

        private static void SetIcon(Transform t, Sprite sprite, bool unlocked)
        {
            if (t == null) return;
            var img = t.GetComponent<Image>();
            if (img == null) return;
            img.sprite = sprite;
            img.enabled = sprite != null;
            img.color = unlocked ? Color.white : UiTheme.WithAlpha(Color.white, 0.4f);
        }
    }
}
