using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.LevelUp;

namespace ProjectB.UI
{
    public class AbilityHudSlotUI : MonoBehaviour
    {
        [SerializeField] private Image borderImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI initialsText;
        [SerializeField] private TextMeshProUGUI levelText;

        public void SetAbility(AbilityData data, int level)
        {
            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            Color rarityColor = GetRarityColor(data.rarity);
            if (borderImage != null) borderImage.color = rarityColor;
            if (backgroundImage != null) backgroundImage.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);

            bool hasIcon = data.icon != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    iconImage.sprite = data.icon;
                    iconImage.color = Color.white;
                }
            }

            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(!hasIcon);
                if (!hasIcon)
                {
                    initialsText.text = GetInitials(data.abilityName);
                    initialsText.color = rarityColor;
                }
            }

            if (levelText != null)
            {
                levelText.text = $"Lv{level}";
            }
        }

        public static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => new Color(0.75f, 0.78f, 0.82f, 1f),
                CardRarity.Rare => new Color(0.25f, 0.6f, 1f, 1f),
                CardRarity.Epic => new Color(0.72f, 0.32f, 0.95f, 1f),
                CardRarity.Legendary => new Color(1f, 0.78f, 0.2f, 1f),
                _ => Color.white
            };
        }

        public static string GetInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "?";

            string trimmed = displayName.Trim();
            int spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx > 0 && spaceIdx + 1 < trimmed.Length)
            {
                return $"{char.ToUpperInvariant(trimmed[0])}{char.ToUpperInvariant(trimmed[spaceIdx + 1])}";
            }

            return trimmed.Length >= 2
                ? trimmed.Substring(0, 2).ToUpperInvariant()
                : trimmed.ToUpperInvariant();
        }
    }
}
