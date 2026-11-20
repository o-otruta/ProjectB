using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;

namespace ProjectB.UI
{
    public class AbilityCollectionItemUI : MonoBehaviour
    {
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image rarityStripe;
        [SerializeField] private Image iconBackground;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI initialsText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI metaTagText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Colors")]
        [SerializeField] private Color unlockedBgColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        [SerializeField] private Color lockedBgColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        [SerializeField] private Color unlockedStatusColor = new Color(0.35f, 0.92f, 0.45f, 1f);
        [SerializeField] private Color lockedStatusColor = new Color(1f, 0.72f, 0.28f, 1f);

        public void Setup(AbilityData data, bool isUnlocked, string unlockRequirementText)
        {
            if (data == null) return;

            Color rarityColor = AbilityHudSlotUI.GetRarityColor(data.rarity);

            if (cardBackground != null)
            {
                cardBackground.color = isUnlocked ? unlockedBgColor : lockedBgColor;
            }

            if (rarityStripe != null)
            {
                rarityStripe.color = isUnlocked ? rarityColor : new Color(rarityColor.r * 0.45f, rarityColor.g * 0.45f, rarityColor.b * 0.45f, 1f);
            }

            if (iconBackground != null)
            {
                iconBackground.color = isUnlocked
                    ? new Color(0.1f, 0.12f, 0.18f, 1f)
                    : new Color(0.08f, 0.08f, 0.09f, 1f);
            }

            bool hasIcon = data.icon != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    iconImage.sprite = data.icon;
                    iconImage.color = isUnlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
                }
            }

            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(!hasIcon);
                if (!hasIcon)
                {
                    initialsText.text = AbilityHudSlotUI.GetInitials(data.abilityName);
                    initialsText.color = isUnlocked ? rarityColor : Color.gray;
                }
            }

            if (nameText != null)
            {
                nameText.text = data.abilityName;
                nameText.color = isUnlocked ? Color.white : new Color(0.65f, 0.65f, 0.68f, 1f);
            }

            if (metaTagText != null)
            {
                metaTagText.text = $"{data.type} • {data.rarity}";
                metaTagText.color = isUnlocked ? rarityColor : Color.gray;
            }

            if (descriptionText != null)
            {
                descriptionText.text = data.description;
                descriptionText.color = isUnlocked
                    ? new Color(0.84f, 0.86f, 0.9f, 1f)
                    : new Color(0.55f, 0.55f, 0.58f, 1f);
            }

            if (statusText != null)
            {
                if (isUnlocked)
                {
                    statusText.text = "Unlocked";
                    statusText.color = unlockedStatusColor;
                }
                else
                {
                    statusText.text = string.IsNullOrEmpty(unlockRequirementText)
                        ? "Locked"
                        : $"Locked — {unlockRequirementText}";
                    statusText.color = lockedStatusColor;
                }
            }
        }
    }
}
