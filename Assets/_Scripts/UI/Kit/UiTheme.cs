using System;
using UnityEngine;
using TMPro;
using ProjectB.LevelUp;

namespace ProjectB.UI
{
    /// <summary>Semantic colour tokens of <see cref="UiTheme"/>. Components bind to a token instead of a literal colour.</summary>
    public enum UiColor
    {
        Background = 0,
        Surface = 1,
        SurfaceRaised = 2,
        Outline = 3,
        TextPrimary = 4,
        TextSecondary = 5,
        TextDisabled = 6,
        Accent = 7,
        Success = 8,
        Danger = 9,
        Warning = 10,
        Gold = 11,
        Shard = 12,
        Ad = 13,
        Overlay = 14,
        RarityCommon = 15,
        RarityRare = 16,
        RarityEpic = 17,
        RarityLegendary = 18,
        ActiveSlot = 19,
        PassiveSlot = 20,
        HpFill = 21,
        XpFill = 22,
        Ghost = 23,
        SurfaceSunken = 24,
        White = 25,
        TextDark = 26
    }

    /// <summary>Typographic scale (sizes at the 1080×1920 reference resolution).</summary>
    public enum UiTextStyle
    {
        Display = 0,
        H1 = 1,
        H2 = 2,
        Body = 3,
        Caption = 4,
        Number = 5,
        Button = 6,
        Micro = 7
    }

    public enum ButtonStyle
    {
        Primary = 0,
        Secondary = 1,
        Danger = 2,
        Ad = 3,
        Icon = 4,
        Success = 5
    }

    /// <summary>Which 9-slice / icon sprite of the kit a component wants.</summary>
    public enum UiSprite
    {
        None = 0,
        Panel = 1,
        Card = 2,
        Row = 3,
        Button = 4,
        ButtonPressed = 5,
        Pill = 6,
        Circle = 7,
        SlotFrame = 8,
        BarFrame = 9,
        BarFill = 10,
        Solid = 11,
        Glow = 12,
        Outline = 13,
        Knob = 14,
        RingThin = 15,
        Vignette = 16,
        Starfield = 17,
        Squircle = 18,
        SlotDashed = 19,
        CardHeader = 20,
        Shine = 21,
        RibbonTail = 22,
        MenuBg = 23,
        Pip = 24
    }

    [Serializable]
    public struct ButtonStyleColors
    {
        public UiColor face;
        public UiColor label;
    }

    /// <summary>
    /// Single source of truth for the UI look (Sprint 13.1): palette, rarity colours, typography, spacing, durations,
    /// kit sprites and icons. Lives at <c>Resources/UiTheme.asset</c>; every UI script reads colours from here
    /// (no new <c>new Color(...)</c> literals in UI code). Generated and wired by <c>ProjectB/UI/Apply Theme</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "ProjectB/UI/Ui Theme")]
    public class UiTheme : ScriptableObject
    {
        public const string RESOURCE_PATH = "UiTheme";

        [Header("Palette — night surfaces")]
        public Color background = Hex(0x13122E);
        public Color surface = Hex(0x302F6A);
        public Color surfaceRaised = Hex(0x3E3D82);
        public Color surfaceSunken = Hex(0x1A1940);
        public Color outline = Hex(0x52509A);
        public Color overlay = new Color(0.03f, 0.03f, 0.08f, 0.78f);

        [Header("Palette — text")]
        public Color textPrimary = Hex(0xF4F1FF);
        public Color textSecondary = Hex(0xA9A6D6);
        public Color textDisabled = Hex(0x625F8E);
        public Color textDark = Hex(0x331D00);

        [Header("Palette — accents")]
        public Color accent = Hex(0x7C5CFF);
        public Color success = Hex(0x45D483);
        public Color danger = Hex(0xFF4D6A);
        public Color warning = Hex(0xFFA23A);
        public Color gold = Hex(0xFFCB32);
        public Color shard = Hex(0x3FE0D0);
        public Color ad = Hex(0x32A8FF);
        public Color white = Color.white;

        [Header("Rarity (cards, HUD slots, collection, constellation)")]
        public Color rarityCommon = Hex(0xB9C2D0);
        public Color rarityRare = Hex(0x3D9BFF);
        public Color rarityEpic = Hex(0xB65CFF);
        public Color rarityLegendary = Hex(0xFFCB32);

        [Header("Gameplay HUD")]
        public Color activeSlot = Hex(0xFF9640);
        public Color passiveSlot = Hex(0x3CC8E0);
        public Color hpFill = Hex(0xFF4F78);
        public Color xpFill = Hex(0x6EE042);
        public Color ghost = Hex(0xFFE3A3);
        [Tooltip("Overdrive tiers: Lv 10 (gold), Lv 20 (glow), Lv 30+ (iridescent base).")]
        public Color overdriveTier1 = Hex(0xFFCB32);
        public Color overdriveTier2 = Hex(0x5CF4FF);
        public Color overdriveTier3 = Hex(0xFF6AE6);

        [Header("Buttons")]
        [Tooltip("Primary = the one main action of a screen (START, CLAIM, RESUME): warm gold face, dark label.")]
        public ButtonStyleColors primaryButton = new ButtonStyleColors { face = UiColor.Gold, label = UiColor.TextDark };
        public ButtonStyleColors secondaryButton = new ButtonStyleColors { face = UiColor.SurfaceRaised, label = UiColor.TextPrimary };
        public ButtonStyleColors dangerButton = new ButtonStyleColors { face = UiColor.Danger, label = UiColor.White };
        public ButtonStyleColors adButton = new ButtonStyleColors { face = UiColor.Ad, label = UiColor.White };
        public ButtonStyleColors iconButton = new ButtonStyleColors { face = UiColor.SurfaceRaised, label = UiColor.TextPrimary };
        [Tooltip("Success = an affordable coin purchase (upgrade buy, coin unlock). Unaffordable buys switch to Secondary.")]
        public ButtonStyleColors successButton = new ButtonStyleColors { face = UiColor.Success, label = UiColor.White };
        [Range(0.8f, 1f)] public float pressScale = 0.94f;
        public Color disabledTint = new Color(0.55f, 0.55f, 0.62f, 0.85f);
        public Color hoverTint = new Color(0.94f, 0.95f, 1f, 1f);
        public Color pressedTint = new Color(0.82f, 0.82f, 0.86f, 1f);

        [Header("Typography (reference 1080×1920)")]
        public TMP_FontAsset displayFont;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset bodyBoldFont;
        public Material titleMaterial;
        public Material numberMaterial;
        public Material bodyMaterial;
        public Material cardTitleMaterial;
        public Material heavyFlatMaterial;
        public float displaySize = 96f;
        public float h1Size = 64f;
        public float h2Size = 44f;
        public float bodySize = 32f;
        public float captionSize = 24f;
        public float numberSize = 40f;
        public float buttonSize = 40f;
        public float microSize = 20f;

        [Header("Spacing & shape")]
        public float gridUnit = 8f;
        public float cornerRadius = 24f;
        [Min(48f)] public float minTouchSize = 120f;

        [Header("Motion (unscaled time)")]
        public float durationFast = 0.12f;
        public float durationNormal = 0.2f;
        public float durationSlow = 0.35f;
        public float staggerDelay = 0.06f;

        [Header("Kit sprites (9-slice, white, tinted at runtime)")]
        public Sprite panelSprite;
        public Sprite cardSprite;
        public Sprite rowSprite;
        public Sprite buttonSprite;
        public Sprite buttonPressedSprite;
        public Sprite pillSprite;
        public Sprite circleSprite;
        public Sprite slotFrameSprite;
        public Sprite barFrameSprite;
        public Sprite barFillSprite;
        public Sprite solidSprite;
        public Sprite glowSprite;
        public Sprite outlineSprite;
        public Sprite knobSprite;
        public Sprite ringThinSprite;
        public Sprite vignetteSprite;
        public Sprite starfieldSprite;
        public Sprite squircleSprite;
        public Sprite slotDashedSprite;
        public Sprite cardHeaderSprite;
        public Sprite shineSprite;
        public Sprite ribbonTailSprite;
        public Sprite menuBgSprite;
        public Sprite pipSprite;
        public Material surfaceMaterial;

        [Header("Icons")]
        public Sprite iconCoin;
        public Sprite iconShard;
        public Sprite iconAd;
        public Sprite iconGear;
        public Sprite iconPause;
        public Sprite iconLock;
        public Sprite iconCheck;
        public Sprite iconChevronLeft;
        public Sprite iconChevronRight;
        public Sprite iconHeart;
        public Sprite iconSpeed;
        public Sprite iconSword;
        public Sprite iconClock;
        public Sprite iconSkull;
        public Sprite iconStar;
        public Sprite iconPlus;
        public Sprite iconMinus;
        public Sprite iconTarget;
        public Sprite iconClose;
        public Sprite iconPlay;
        public Sprite iconReroll;
        public Sprite iconFlame;
        public Sprite iconMusic;
        public Sprite iconSound;
        public Sprite iconGlobe;

        [Header("Main menu tab icons")]
        public Sprite tabUpgrades;
        public Sprite tabConstellations;
        public Sprite tabAbilities;
        public Sprite tabPlay;
        public Sprite tabAchievements;
        public Sprite tabBonus;

        [Header("TMP inline sprites (<sprite name=coin>)")]
        public TMP_SpriteAsset iconSpriteAsset;

        private static UiTheme cached;

        /// <summary>The active theme: <c>Resources/UiTheme</c>, or an in-memory default (never null).</summary>
        public static UiTheme Current
        {
            get
            {
                if (cached == null)
                {
                    cached = Resources.Load<UiTheme>(RESOURCE_PATH);
                    if (cached == null)
                    {
                        cached = CreateInstance<UiTheme>();
                        cached.hideFlags = HideFlags.DontSave;
                        cached.name = "UiTheme (Default)";
                    }
                }
                return cached;
            }
        }

        /// <summary>Drops the cached instance (editor tooling / tests).</summary>
        public static void ResetCache()
        {
            cached = null;
        }

        public Color GetColor(UiColor token)
        {
            switch (token)
            {
                case UiColor.Background: return background;
                case UiColor.Surface: return surface;
                case UiColor.SurfaceRaised: return surfaceRaised;
                case UiColor.SurfaceSunken: return surfaceSunken;
                case UiColor.Outline: return outline;
                case UiColor.TextPrimary: return textPrimary;
                case UiColor.TextSecondary: return textSecondary;
                case UiColor.TextDisabled: return textDisabled;
                case UiColor.Accent: return accent;
                case UiColor.Success: return success;
                case UiColor.Danger: return danger;
                case UiColor.Warning: return warning;
                case UiColor.Gold: return gold;
                case UiColor.Shard: return shard;
                case UiColor.Ad: return ad;
                case UiColor.Overlay: return overlay;
                case UiColor.RarityCommon: return rarityCommon;
                case UiColor.RarityRare: return rarityRare;
                case UiColor.RarityEpic: return rarityEpic;
                case UiColor.RarityLegendary: return rarityLegendary;
                case UiColor.ActiveSlot: return activeSlot;
                case UiColor.PassiveSlot: return passiveSlot;
                case UiColor.HpFill: return hpFill;
                case UiColor.XpFill: return xpFill;
                case UiColor.Ghost: return ghost;
                case UiColor.White: return white;
                case UiColor.TextDark: return textDark;
                default: return Color.magenta;
            }
        }

        public Color GetRarityColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return rarityCommon;
                case CardRarity.Rare: return rarityRare;
                case CardRarity.Epic: return rarityEpic;
                case CardRarity.Legendary: return rarityLegendary;
                default: return rarityCommon;
            }
        }

        public static UiColor RarityToken(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Rare: return UiColor.RarityRare;
                case CardRarity.Epic: return UiColor.RarityEpic;
                case CardRarity.Legendary: return UiColor.RarityLegendary;
                default: return UiColor.RarityCommon;
            }
        }

        /// <summary>Overdrive tier colour (tier 0 → <paramref name="fallback"/>).</summary>
        public Color GetOverdriveColor(int tier, Color fallback)
        {
            if (tier >= 3) return overdriveTier3;
            if (tier == 2) return overdriveTier2;
            if (tier == 1) return overdriveTier1;
            return fallback;
        }

        public ButtonStyleColors GetButtonStyle(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Secondary: return secondaryButton;
                case ButtonStyle.Danger: return dangerButton;
                case ButtonStyle.Ad: return adButton;
                case ButtonStyle.Icon: return iconButton;
                case ButtonStyle.Success: return successButton;
                default: return primaryButton;
            }
        }

        public float GetFontSize(UiTextStyle style)
        {
            switch (style)
            {
                case UiTextStyle.Display: return displaySize;
                case UiTextStyle.H1: return h1Size;
                case UiTextStyle.H2: return h2Size;
                case UiTextStyle.Caption: return captionSize;
                case UiTextStyle.Number: return numberSize;
                case UiTextStyle.Button: return buttonSize;
                case UiTextStyle.Micro: return microSize;
                default: return bodySize;
            }
        }

        public TMP_FontAsset GetFont(UiTextStyle style)
        {
            switch (style)
            {
                case UiTextStyle.Display:
                case UiTextStyle.H1:
                case UiTextStyle.Number:
                case UiTextStyle.Button:
                    return displayFont != null ? displayFont : bodyFont;
                case UiTextStyle.H2:
                    return bodyBoldFont != null ? bodyBoldFont : displayFont;
                case UiTextStyle.Caption:
                case UiTextStyle.Micro:
                    return bodyBoldFont != null ? bodyBoldFont : bodyFont;
                default:
                    return bodyFont;
            }
        }

        /// <summary>Material preset: Title (outline + underlay), Number (thick outline), CardTitle (heavy bold + outline), or Body (plain).</summary>
        public Material GetMaterial(UiTextStyle style)
        {
            switch (style)
            {
                case UiTextStyle.Display:
                case UiTextStyle.H1:
                case UiTextStyle.Button:
                    return titleMaterial;
                case UiTextStyle.Number:
                    return numberMaterial;
                case UiTextStyle.H2:
                    return cardTitleMaterial != null ? cardTitleMaterial : titleMaterial;
                default:
                    return bodyMaterial;
            }
        }

        public Sprite GetSprite(UiSprite sprite)
        {
            switch (sprite)
            {
                case UiSprite.Panel: return panelSprite;
                case UiSprite.Card: return cardSprite;
                case UiSprite.Row: return rowSprite;
                case UiSprite.Button: return buttonSprite;
                case UiSprite.ButtonPressed: return buttonPressedSprite;
                case UiSprite.Pill: return pillSprite;
                case UiSprite.Circle: return circleSprite;
                case UiSprite.SlotFrame: return slotFrameSprite;
                case UiSprite.BarFrame: return barFrameSprite;
                case UiSprite.BarFill: return barFillSprite;
                case UiSprite.Solid: return solidSprite;
                case UiSprite.Glow: return glowSprite;
                case UiSprite.Outline: return outlineSprite;
                case UiSprite.Knob: return knobSprite;
                case UiSprite.RingThin: return ringThinSprite;
                case UiSprite.Vignette: return vignetteSprite;
                case UiSprite.Starfield: return starfieldSprite;
                case UiSprite.Squircle: return squircleSprite;
                case UiSprite.SlotDashed: return slotDashedSprite;
                case UiSprite.CardHeader: return cardHeaderSprite;
                case UiSprite.Shine: return shineSprite;
                case UiSprite.RibbonTail: return ribbonTailSprite;
                case UiSprite.MenuBg: return menuBgSprite;
                case UiSprite.Pip: return pipSprite;
                default: return null;
            }
        }

        public static bool UsesSurfaceMaterial(UiSprite sprite)
        {
            switch (sprite)
            {
                case UiSprite.Panel:
                case UiSprite.Card:
                case UiSprite.Row:
                case UiSprite.Button:
                case UiSprite.ButtonPressed:
                case UiSprite.Pill:
                case UiSprite.Circle:
                case UiSprite.Knob:
                case UiSprite.Squircle:
                case UiSprite.CardHeader:
                case UiSprite.BarFrame:
                case UiSprite.BarFill:
                case UiSprite.RibbonTail:
                case UiSprite.Pip:
                    return true;
                default:
                    return false;
            }
        }

        public bool UsesSurfaceMaterial(Sprite sprite)
        {
            if (sprite == null) return false;
            return sprite == panelSprite
                || sprite == cardSprite
                || sprite == rowSprite
                || sprite == buttonSprite
                || sprite == buttonPressedSprite
                || sprite == pillSprite
                || sprite == circleSprite
                || sprite == knobSprite
                || sprite == squircleSprite
                || sprite == cardHeaderSprite
                || sprite == barFrameSprite
                || sprite == barFillSprite
                || sprite == ribbonTailSprite
                || sprite == pipSprite;
        }

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        /// <summary>Darkens a colour towards black (used for locked / pressed variants).</summary>
        public static Color Shade(Color c, float factor)
        {
            return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
        }
    }
}
