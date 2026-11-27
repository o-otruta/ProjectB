using System;
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
            public string synergyId;
        }

        private sealed class ChipView
        {
            public GameObject root;
            public Image background;
            public Button button;
            public Image leftIcon;
            public Image rightIcon;
            public TMP_Text label;
            public bool hasData;
            public Sprite lastLeftIcon;
            public Sprite lastRightIcon;
            public string lastName;
            public bool lastUnlocked;
            public string lastSynergyId;
        }

        private enum BoundKind : byte
        {
            None = 0,
            Ability = 1,
            Synergy = 2
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

        private readonly List<ChipView> chipViews = new List<ChipView>(4);

        private BoundKind boundKind;
        private AbilityData boundAbility;
        private SynergyData boundSynergy;
        private bool boundUnlocked;
        private string boundUnlockReq;
        private string boundSynergyHint;
        private string boundComboPairLabel;
        private SystemLanguage boundLanguage;
        private bool boundSupportsChips;
        private Action<string> onSynergyClicked;
        private Button cardButton;

        public bool SupportsSynergyChips => synergyChipsContainer != null && synergyChipTemplate != null;

        public void SetSynergyClickHandler(Action<string> handler)
        {
            onSynergyClicked = handler;
            if (cardButton != null)
            {
                cardButton.interactable = boundKind == BoundKind.Synergy && boundSynergy != null && onSynergyClicked != null;
            }
        }

        public void Setup(AbilityData data, bool isUnlocked, string unlockRequirementText, string synergyHint = null)
        {
            if (data == null) return;

            SystemLanguage currentLanguage = LocalizationService.Current.CurrentLanguage;
            bool supportsChips = SupportsSynergyChips;
            if (boundKind == BoundKind.Ability
                && ReferenceEquals(boundAbility, data)
                && boundUnlocked == isUnlocked
                && boundLanguage == currentLanguage
                && boundSupportsChips == supportsChips
                && string.Equals(boundUnlockReq, unlockRequirementText, StringComparison.Ordinal)
                && string.Equals(boundSynergyHint, synergyHint, StringComparison.Ordinal))
            {
                return;
            }

            boundKind = BoundKind.Ability;
            boundAbility = data;
            boundSynergy = null;
            boundUnlocked = isUnlocked;
            boundUnlockReq = unlockRequirementText;
            boundSynergyHint = synergyHint;
            boundComboPairLabel = null;
            boundLanguage = currentLanguage;
            boundSupportsChips = supportsChips;
            if (cardButton != null) cardButton.interactable = false;

            UiTheme theme = UiTheme.Current;
            Color rarityColor = theme.GetRarityColor(data.rarity);
            Color lockedRarity = UiTheme.Shade(rarityColor, 0.45f);

            if (cardBackground != null) cardBackground.color = isUnlocked ? theme.surface : theme.surfaceSunken;
            if (frameImage != null) frameImage.color = isUnlocked ? rarityColor : UiTheme.WithAlpha(lockedRarity, 0.7f);
            if (rarityStripe != null) rarityStripe.color = isUnlocked ? rarityColor : lockedRarity;
            if (iconBackground != null) iconBackground.color = isUnlocked ? theme.surfaceRaised : theme.surfaceSunken;
            if (lockBadge != null && lockBadge.activeSelf != !isUnlocked) lockBadge.SetActive(!isUnlocked);

            bool hasIcon = data.icon != null;
            if (iconImage != null)
            {
                if (iconImage.gameObject.activeSelf != hasIcon) iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    if (iconImage.sprite != data.icon) iconImage.sprite = data.icon;
                    // Locked: dark silhouette of the icon.
                    iconImage.color = isUnlocked ? Color.white : UiTheme.WithAlpha(theme.background, 0.95f);
                }
            }

            string localizedName = Loc.Name(data);
            string localizedDesc = Loc.Desc(data);

            if (initialsText != null)
            {
                bool showInitials = !hasIcon;
                if (initialsText.gameObject.activeSelf != showInitials) initialsText.gameObject.SetActive(showInitials);
                if (showInitials)
                {
                    SetTextIfChanged(initialsText, AbilityHudSlotUI.GetInitials(localizedName));
                    initialsText.color = isUnlocked ? rarityColor : theme.textDisabled;
                }
            }

            if (nameText != null)
            {
                SetTextIfChanged(nameText, localizedName);
                nameText.color = isUnlocked ? theme.textPrimary : theme.textSecondary;
            }

            if (metaTagText != null)
            {
                string typeLabel = data.type == AbilityType.Passive ? Loc.Get("ui.abilities.passive", "Passive") : Loc.Get("ui.abilities.active", "Active");
                string rarityLabel = Loc.Get($"ui.rarity.{data.rarity.ToString().ToLowerInvariant()}", data.rarity.ToString());
                SetTextIfChanged(metaTagText, $"{typeLabel} • {rarityLabel}");
                metaTagText.color = isUnlocked ? rarityColor : theme.textDisabled;
            }

            if (descriptionText != null)
            {
                string desc = string.IsNullOrEmpty(synergyHint)
                    ? localizedDesc
                    : $"{localizedDesc}\n<color=#{ColorUtility.ToHtmlStringRGB(theme.gold)}>{synergyHint}</color>";
                SetTextIfChanged(descriptionText, desc);
                descriptionText.color = isUnlocked ? theme.textSecondary : theme.textDisabled;
            }

            if (statusText != null)
            {
                if (isUnlocked)
                {
                    SetTextIfChanged(statusText, Loc.Get("ui.common.unlocked", "Unlocked"));
                    statusText.color = theme.success;
                    bool showStatus = !supportsChips;
                    if (statusText.gameObject.activeSelf != showStatus) statusText.gameObject.SetActive(showStatus);
                }
                else
                {
                    if (!statusText.gameObject.activeSelf) statusText.gameObject.SetActive(true);
                    string req = string.IsNullOrEmpty(unlockRequirementText)
                        ? Loc.Get("ui.common.locked", "Locked")
                        : $"<sprite name=\"lock\"> {unlockRequirementText}";
                    SetTextIfChanged(statusText, req);
                    statusText.color = theme.warning;
                }
            }
        }

        public void SetupSynergy(SynergyData synergy, string comboPairLabel, bool isUnlocked)
        {
            if (synergy == null) return;

            SystemLanguage currentLanguage = LocalizationService.Current.CurrentLanguage;
            bool supportsChips = SupportsSynergyChips;
            if (boundKind == BoundKind.Synergy
                && ReferenceEquals(boundSynergy, synergy)
                && boundUnlocked == isUnlocked
                && boundLanguage == currentLanguage
                && boundSupportsChips == supportsChips
                && string.Equals(boundComboPairLabel, comboPairLabel, StringComparison.Ordinal))
            {
                return;
            }

            boundKind = BoundKind.Synergy;
            boundAbility = null;
            boundSynergy = synergy;
            boundUnlocked = isUnlocked;
            boundUnlockReq = null;
            boundSynergyHint = null;
            boundComboPairLabel = comboPairLabel;
            boundLanguage = currentLanguage;
            boundSupportsChips = supportsChips;

            if (cardButton == null)
            {
                cardButton = GetComponent<Button>();
                if (cardButton == null)
                {
                    cardButton = gameObject.AddComponent<Button>();
                    cardButton.transition = Selectable.Transition.None;
                    if (cardBackground != null) cardButton.targetGraphic = cardBackground;
                }
                cardButton.onClick.AddListener(HandleCardClicked);
            }
            cardButton.interactable = onSynergyClicked != null;

            UiTheme theme = UiTheme.Current;
            Color accentColor = theme.shard;

            if (cardBackground != null) cardBackground.color = isUnlocked ? theme.surface : theme.surfaceSunken;
            if (frameImage != null) frameImage.color = isUnlocked ? accentColor : UiTheme.WithAlpha(UiTheme.Shade(accentColor, 0.45f), 0.7f);
            if (rarityStripe != null) rarityStripe.color = isUnlocked ? accentColor : UiTheme.Shade(accentColor, 0.45f);
            if (iconBackground != null) iconBackground.color = theme.surfaceRaised;
            if (lockBadge != null && lockBadge.activeSelf != !isUnlocked) lockBadge.SetActive(!isUnlocked);

            Sprite icon = synergy.icon;
            bool hasIcon = icon != null;
            if (iconImage != null)
            {
                if (iconImage.gameObject.activeSelf != hasIcon) iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    if (iconImage.sprite != icon) iconImage.sprite = icon;
                    iconImage.color = isUnlocked ? Color.white : UiTheme.WithAlpha(theme.background, 0.95f);
                }
            }

            string localizedSynergyName = Loc.Name(synergy);
            string localizedSynergyDesc = Loc.Desc(synergy);

            if (initialsText != null)
            {
                bool showInitials = !hasIcon;
                if (initialsText.gameObject.activeSelf != showInitials) initialsText.gameObject.SetActive(showInitials);
                if (showInitials)
                {
                    SetTextIfChanged(initialsText, AbilityHudSlotUI.GetInitials(localizedSynergyName));
                    initialsText.color = accentColor;
                }
            }

            if (nameText != null)
            {
                SetTextIfChanged(nameText, localizedSynergyName);
                nameText.color = isUnlocked ? theme.textPrimary : theme.textSecondary;
            }

            if (metaTagText != null)
            {
                string tag = supportsChips || string.IsNullOrEmpty(comboPairLabel)
                    ? Loc.Get("ui.abilities.reactive_synergy", "Reactive Synergy")
                    : $"{Loc.Get("ui.abilities.synergy_tag", "Synergy")} • {comboPairLabel}";
                SetTextIfChanged(metaTagText, tag);
                metaTagText.color = accentColor;
            }

            if (descriptionText != null)
            {
                SetTextIfChanged(descriptionText, localizedSynergyDesc);
                descriptionText.color = isUnlocked ? theme.textSecondary : theme.textDisabled;
            }

            if (statusText != null)
            {
                if (!statusText.gameObject.activeSelf) statusText.gameObject.SetActive(true);
                string status = isUnlocked
                    ? Loc.Get("ui.abilities.synergy_auto", "Auto-activates in run")
                    : Loc.Get("ui.abilities.synergy_unlock_hint", "<sprite name=\"lock\"> Unlock in the Constellation Tree");
                SetTextIfChanged(statusText, status);
                statusText.color = isUnlocked ? theme.success : theme.warning;
            }
        }

        /// <summary>Synergy chips "icon + icon → name" (unlocked chips bright, locked chips dimmed).</summary>
        public void SetSynergyChips(IReadOnlyList<SynergyChip> chips)
        {
            if (!SupportsSynergyChips) return;
            if (synergyChipTemplate.activeSelf) synergyChipTemplate.SetActive(false);

            UiTheme theme = UiTheme.Current;
            int count = chips != null ? chips.Count : 0;

            while (chipViews.Count < count)
            {
                GameObject instance = Instantiate(synergyChipTemplate, synergyChipsContainer);
                Transform t = instance.transform;
                Image bg = instance.GetComponent<Image>();
                if (bg != null) bg.raycastTarget = true;
                Button btn = instance.GetComponent<Button>();
                if (btn == null)
                {
                    btn = instance.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    if (bg != null) btn.targetGraphic = bg;
                }
                var view = new ChipView
                {
                    root = instance,
                    background = bg,
                    button = btn,
                    leftIcon = ResolveIconImage(t.Find("Left")),
                    rightIcon = ResolveIconImage(t.Find("Right")),
                    label = t.Find("Name") != null ? t.Find("Name").GetComponent<TMP_Text>() : null
                };
                ChipView captured = view;
                btn.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrEmpty(captured.lastSynergyId))
                    {
                        onSynergyClicked?.Invoke(captured.lastSynergyId);
                    }
                });
                chipViews.Add(view);
            }

            for (int i = 0; i < chipViews.Count; i++)
            {
                ChipView view = chipViews[i];
                bool visible = i < count;
                if (view.root.activeSelf != visible) view.root.SetActive(visible);
                if (!visible) continue;

                SynergyChip data = chips[i];
                if (view.button != null)
                {
                    view.button.interactable = onSynergyClicked != null && !string.IsNullOrEmpty(data.synergyId);
                }
                if (view.hasData
                    && view.lastLeftIcon == data.leftIcon
                    && view.lastRightIcon == data.rightIcon
                    && view.lastUnlocked == data.unlocked
                    && string.Equals(view.lastSynergyId, data.synergyId, StringComparison.Ordinal)
                    && string.Equals(view.lastName, data.name, StringComparison.Ordinal))
                {
                    continue;
                }

                view.hasData = true;
                view.lastLeftIcon = data.leftIcon;
                view.lastRightIcon = data.rightIcon;
                view.lastName = data.name;
                view.lastUnlocked = data.unlocked;
                view.lastSynergyId = data.synergyId;

                ApplyIcon(view.leftIcon, data.leftIcon, data.unlocked);
                ApplyIcon(view.rightIcon, data.rightIcon, data.unlocked);
                if (view.label != null)
                {
                    SetTextIfChanged(view.label, data.name);
                    view.label.color = data.unlocked ? theme.shard : theme.textDisabled;
                }
                if (view.background != null)
                {
                    view.background.color = UiTheme.WithAlpha(data.unlocked ? theme.shard : theme.outline, 0.18f);
                }
            }

            bool showContainer = count > 0;
            if (synergyChipsContainer.gameObject.activeSelf != showContainer)
            {
                synergyChipsContainer.gameObject.SetActive(showContainer);
            }
        }

        private static Image ResolveIconImage(Transform t)
        {
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static void ApplyIcon(Image img, Sprite sprite, bool unlocked)
        {
            if (img == null) return;
            if (img.sprite != sprite) img.sprite = sprite;
            bool shouldEnable = sprite != null;
            if (img.enabled != shouldEnable) img.enabled = shouldEnable;
            img.color = unlocked ? Color.white : UiTheme.WithAlpha(Color.white, 0.4f);
        }

        private void HandleCardClicked()
        {
            if (boundKind == BoundKind.Synergy && boundSynergy != null && !string.IsNullOrEmpty(boundSynergy.id))
            {
                onSynergyClicked?.Invoke(boundSynergy.id);
            }
        }

        private static void SetTextIfChanged(TMP_Text textComponent, string value)
        {
            if (!string.Equals(textComponent.text, value, StringComparison.Ordinal))
            {
                textComponent.text = value;
            }
        }
    }
}
