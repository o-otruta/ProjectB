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
        [SerializeField] private Sprite lockedIconSprite;

        public AbilityData CurrentData { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsEmpty => CurrentData == null && !IsLocked;

        public void SetAbility(AbilityData data, int level)
        {
            if (data == null)
            {
                SetEmpty(AbilityType.Active);
                return;
            }

            CurrentData = data;
            IsLocked = false;
            gameObject.SetActive(true);

            Color rarityColor = GetRarityColor(data.rarity);
            if (borderImage != null) borderImage.color = rarityColor;
            if (backgroundImage != null)
            {
                backgroundImage.color = data.type == AbilityType.Passive
                    ? new Color(0.09f, 0.16f, 0.21f, 0.94f)
                    : new Color(0.14f, 0.13f, 0.19f, 0.94f);
            }

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

            SetLevelBadgeVisible(true);
            if (levelText != null)
            {
                levelText.text = $"Lv{level}";
            }
        }

        public void SetEmpty(AbilityType slotType)
        {
            CurrentData = null;
            IsLocked = false;
            gameObject.SetActive(true);

            if (borderImage != null)
            {
                borderImage.color = slotType == AbilityType.Passive
                    ? new Color(0.24f, 0.78f, 0.88f, 0.34f)
                    : new Color(0.95f, 0.58f, 0.2f, 0.34f);
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.08f, 0.1f, 0.15f, 0.42f);
            }
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }
            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(false);
            }
            SetLevelBadgeVisible(false);
        }

        public void SetLocked(AbilityType slotType)
        {
            CurrentData = null;
            IsLocked = true;
            gameObject.SetActive(true);

            if (borderImage != null)
            {
                borderImage.color = new Color(0.32f, 0.36f, 0.44f, 0.32f);
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.05f, 0.06f, 0.09f, 0.58f);
            }

            bool hasLockSprite = lockedIconSprite != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasLockSprite);
                if (hasLockSprite)
                {
                    iconImage.sprite = lockedIconSprite;
                    iconImage.color = new Color(0.62f, 0.68f, 0.78f, 0.75f);
                }
            }
            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(!hasLockSprite);
                if (!hasLockSprite)
                {
                    initialsText.text = "LK";
                    initialsText.color = new Color(0.55f, 0.6f, 0.68f, 0.75f);
                }
            }
            SetLevelBadgeVisible(false);
        }

        private void SetLevelBadgeVisible(bool visible)
        {
            if (levelText == null) return;
            if (levelText.transform.parent != null && levelText.transform.parent != transform)
            {
                levelText.transform.parent.gameObject.SetActive(visible);
            }
            levelText.gameObject.SetActive(visible);
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
