using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectB.Abilities;
using ProjectB.LevelUp;

namespace ProjectB.UI
{
    /// <summary>
    /// One HUD ability socket. Sprint 13: frame tinted by rarity, level badge in the corner and Overdrive tiers —
    /// Lv 10 gold frame, Lv 20 pulsing glow, Lv 30+ iridescent frame (animated on unscaled time only for tier ≥ 2).
    /// </summary>
    public class AbilityHudSlotUI : MonoBehaviour
    {
        [SerializeField] private Image borderImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI initialsText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Sprite lockedIconSprite;
        [Tooltip("Sprint 13: Overdrive glow behind the frame (tier 2+).")]
        [SerializeField] private Image glowImage;

        private readonly char[] levelBuffer = new char[8];
        private int overdriveTier;
        private float animTime;

        public AbilityData CurrentData { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsEmpty => CurrentData == null && !IsLocked;
        public int OverdriveTier => overdriveTier;

        public void SetAbility(AbilityData data, int level)
        {
            if (data == null)
            {
                SetEmpty(AbilityType.Active);
                return;
            }

            UiTheme theme = UiTheme.Current;
            CurrentData = data;
            IsLocked = false;
            gameObject.SetActive(true);

            Color rarityColor = theme.GetRarityColor(data.rarity);
            overdriveTier = data.type == AbilityType.Active ? ActiveAbility.CalculateOverdriveTier(level) : 0;
            Color frameColor = theme.GetOverdriveColor(overdriveTier, rarityColor);

            if (borderImage != null) borderImage.color = frameColor;
            if (backgroundImage != null)
            {
                backgroundImage.color = UiTheme.WithAlpha(
                    data.type == AbilityType.Passive ? UiTheme.Shade(theme.passiveSlot, 0.25f) : theme.surface, 0.94f);
            }
            if (glowImage != null)
            {
                bool glow = overdriveTier >= 2;
                glowImage.gameObject.SetActive(glow);
                if (glow) glowImage.color = UiTheme.WithAlpha(frameColor, 0.6f);
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
                levelBuffer[0] = 'L';
                levelBuffer[1] = 'v';
                int len = UiFormat.WriteInt(levelBuffer, 2, Mathf.Max(0, level));
                levelText.SetCharArray(levelBuffer, 0, len);
                levelText.color = overdriveTier > 0 ? theme.GetOverdriveColor(overdriveTier, theme.textPrimary) : theme.textPrimary;
            }
        }

        public void SetEmpty(AbilityType slotType)
        {
            UiTheme theme = UiTheme.Current;
            CurrentData = null;
            IsLocked = false;
            overdriveTier = 0;
            gameObject.SetActive(true);

            if (borderImage != null)
            {
                borderImage.color = UiTheme.WithAlpha(slotType == AbilityType.Passive ? theme.passiveSlot : theme.activeSlot, 0.35f);
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = UiTheme.WithAlpha(theme.surfaceSunken, 0.55f);
            }
            if (glowImage != null) glowImage.gameObject.SetActive(false);
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
            UiTheme theme = UiTheme.Current;
            CurrentData = null;
            IsLocked = true;
            overdriveTier = 0;
            gameObject.SetActive(true);

            if (borderImage != null)
            {
                borderImage.color = UiTheme.WithAlpha(theme.outline, 0.35f);
            }
            if (backgroundImage != null)
            {
                backgroundImage.color = UiTheme.WithAlpha(theme.surfaceSunken, 0.6f);
            }
            if (glowImage != null) glowImage.gameObject.SetActive(false);

            Sprite lockSprite = lockedIconSprite != null ? lockedIconSprite : theme.iconLock;
            bool hasLockSprite = lockSprite != null;
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasLockSprite);
                if (hasLockSprite)
                {
                    iconImage.sprite = lockSprite;
                    iconImage.color = UiTheme.WithAlpha(theme.textSecondary, 0.75f);
                }
            }
            if (initialsText != null)
            {
                initialsText.gameObject.SetActive(!hasLockSprite);
                if (!hasLockSprite)
                {
                    initialsText.text = "LK";
                    initialsText.color = UiTheme.WithAlpha(theme.textDisabled, 0.75f);
                }
            }
            SetLevelBadgeVisible(false);
        }

        private void Update()
        {
            if (overdriveTier < 2 || CurrentData == null) return;
            animTime += Time.unscaledDeltaTime;

            if (glowImage != null && glowImage.gameObject.activeSelf)
            {
                Color g = glowImage.color;
                g.a = 0.35f + 0.35f * (Mathf.Sin(animTime * 4f) + 1f) * 0.5f;
                glowImage.color = g;
            }

            if (overdriveTier >= 3 && borderImage != null)
            {
                // Iridescent frame: hue cycles around the tier-3 colour.
                Color.RGBToHSV(UiTheme.Current.overdriveTier3, out float h, out float s, out float v);
                borderImage.color = Color.HSVToRGB(Mathf.Repeat(h + animTime * 0.25f, 1f), s, v);
            }
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

        /// <summary>Rarity colour from the active <see cref="UiTheme"/> (kept for older call sites).</summary>
        public static Color GetRarityColor(CardRarity rarity)
        {
            return UiTheme.Current.GetRarityColor(rarity);
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
