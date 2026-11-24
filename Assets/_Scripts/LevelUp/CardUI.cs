using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using ProjectB.UI;

namespace ProjectB.LevelUp
{
    /// <summary>
    /// One LevelUp card. Sprint 13: rarity lives in the frame, the icon frame and a soft glow (not in the whole card),
    /// a category tag (<c>NEW ACTIVE</c> / <c>UPGRADE</c> / <c>WEAPON</c>…), a level / delta badge, a moving shine on
    /// Legendary cards and a lock + <c>▶ AD</c> badge on the rewarded third card.
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject adBadgeObject;
        [SerializeField] private TMP_Text adBadgeText;
        [SerializeField] private GameObject levelBadgeObject;
        [SerializeField] private TMP_Text levelBadgeText;
        [SerializeField] private Image levelBadgeBg;

        [Header("Sprint 13")]
        [SerializeField] private Image frameImage;
        [SerializeField] private Image glowImage;
        [SerializeField] private Image iconFrameImage;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private Image tagBg;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private RectTransform shine;
        [SerializeField] private CanvasGroup canvasGroup;

        private CardData currentData;
        private Action<CardData> onCardClicked;
        private bool isInteractable = true;
        private string currentAdBadgeLabel = string.Empty;
        private string currentLevelBadgeLabel = string.Empty;
        private Button cachedButton;
        private bool buttonChecked;
        private bool shineActive;
        private float shineTime;

        public bool IsInteractable => isInteractable;
        public CardData CurrentData => currentData;
        public string AdBadgeLabel => currentAdBadgeLabel;
        public string LevelBadgeLabel => levelBadgeText != null ? levelBadgeText.text : currentLevelBadgeLabel;
        public bool IsLevelBadgeVisible => levelBadgeObject != null
            ? levelBadgeObject.activeSelf
            : !string.IsNullOrEmpty(currentLevelBadgeLabel);

        public void Setup(
            CardData data,
            Action<CardData> clickCallback,
            bool requiresAd = false,
            bool canWatchAd = true,
            string levelBadge = null,
            Color? levelBadgeColor = null)
        {
            UiTheme theme = UiTheme.Current;
            currentData = data;
            onCardClicked = clickCallback;
            isInteractable = !requiresAd || canWatchAd;
            currentAdBadgeLabel = requiresAd ? (canWatchAd ? "WATCH AD" : "AD UNAVAILABLE") : string.Empty;
            currentLevelBadgeLabel = levelBadge ?? string.Empty;

            if (!buttonChecked)
            {
                cachedButton = GetComponent<Button>();
                buttonChecked = true;
            }
            if (cachedButton != null)
            {
                cachedButton.interactable = isInteractable;
            }

            SetDimmed(false);

            if (nameText != null) nameText.text = data.cardName;
            if (descriptionText != null) descriptionText.text = data.description;

            if (iconImage != null)
            {
                if (data.icon != null)
                {
                    iconImage.sprite = data.icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (adBadgeObject != null)
            {
                adBadgeObject.SetActive(requiresAd);
            }
            if (adBadgeText != null && requiresAd)
            {
                adBadgeText.text = canWatchAd ? "<sprite name=\"ad\"> AD" : "NO AD";
            }
            if (lockOverlay != null) lockOverlay.SetActive(requiresAd);

            bool hasLevelBadge = !string.IsNullOrEmpty(currentLevelBadgeLabel);
            // Sprint 13: with a category tag on the card, the badge only shows level progress ("Lv 2 → Lv 3").
            bool showLevelBadge = hasLevelBadge && (tagText == null || IsLevelProgressLabel(currentLevelBadgeLabel));
            if (levelBadgeObject != null)
            {
                levelBadgeObject.SetActive(showLevelBadge);
            }
            if (levelBadgeText != null && hasLevelBadge)
            {
                levelBadgeText.text = tagText != null
                    ? currentLevelBadgeLabel.Replace("->", "→")
                    : currentLevelBadgeLabel;
            }
            if (levelBadgeBg != null && hasLevelBadge && levelBadgeColor.HasValue)
            {
                levelBadgeBg.color = levelBadgeColor.Value;
            }

            if (tagText != null)
            {
                tagText.text = FormatCardTag(data);
            }

            Color rarityColor = theme.GetRarityColor(data.rarity);
            if (!isInteractable) rarityColor = UiTheme.Shade(rarityColor, 0.5f);

            if (frameImage != null)
            {
                // Sprint 13 layout: rarity in the frame and glow, neutral card body.
                frameImage.color = rarityColor;
                if (bgImage != null) bgImage.color = isInteractable ? theme.surfaceRaised : theme.surfaceSunken;
            }
            else if (bgImage != null)
            {
                // Legacy layout (no frame): tint the whole card.
                bgImage.color = isInteractable ? rarityColor : UiTheme.WithAlpha(UiTheme.Shade(rarityColor, 0.9f), 0.7f);
            }

            if (glowImage != null)
            {
                glowImage.color = UiTheme.WithAlpha(rarityColor, data.rarity >= CardRarity.Epic ? 0.55f : 0.28f);
            }
            if (iconFrameImage != null) iconFrameImage.color = rarityColor;
            if (tagBg != null) tagBg.color = UiTheme.WithAlpha(rarityColor, 0.25f);
            if (tagText != null) tagText.color = rarityColor;

            shineActive = shine != null && data.rarity == CardRarity.Legendary && isInteractable;
            if (shine != null) shine.gameObject.SetActive(shineActive);
            shineTime = 0f;
        }

        public static bool IsLevelProgressLabel(string label)
        {
            return !string.IsNullOrEmpty(label) && label.StartsWith("Lv", StringComparison.Ordinal);
        }

        /// <summary>Category chip shown on the card.</summary>
        public static string FormatCardTag(CardData card)
        {
            switch (card)
            {
                case AbilityCardData abilityCard:
                    return abilityCard.abilityData != null && abilityCard.abilityData.type == ProjectB.Abilities.AbilityType.Passive
                        ? "NEW PASSIVE"
                        : "NEW ACTIVE";
                case AbilityModifierCardData:
                    return "UPGRADE";
                case WeaponUpgradeCardData:
                    return "WEAPON";
                case StatUpgradeCardData:
                    return "STAT";
                case SynergyCardData:
                    return "SYNERGY";
                default:
                    return string.Empty;
            }
        }

        public void SetDimmed(bool dimmed)
        {
            if (canvasGroup != null) canvasGroup.alpha = dimmed ? 0.3f : 1f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable)
            {
                UiAudio.Error();
                UiTween.Shake(transform as RectTransform);
                return;
            }
            onCardClicked?.Invoke(currentData);
        }

        private void Update()
        {
            if (!shineActive || shine == null) return;
            shineTime += Time.unscaledDeltaTime;
            var parentRect = shine.parent as RectTransform;
            float width = parentRect != null ? parentRect.rect.width : 600f;
            float t = Mathf.Repeat(shineTime / 2.2f, 1f);
            float x = Mathf.Lerp(-width * 0.7f, width * 0.7f, Mathf.Clamp01(t * 1.6f));
            shine.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
