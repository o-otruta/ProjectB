using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Text;
using ProjectB.Core;
using ProjectB.UI;

namespace ProjectB.LevelUp
{
    /// <summary>
    /// One LevelUp card. Sprint 13: rarity lives in the frame, the icon frame and a soft glow (not in the whole card),
    /// a category tag (<c>NEW ACTIVE</c> / <c>UPGRADE</c> / <c>WEAPON</c>…), a level / delta badge, a moving shine on
    /// Legendary cards and a lock + <c>▶ AD</c> badge on the rewarded third card.
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerClickHandler, ISubmitHandler
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
        [SerializeField] private KeyHintBadgeUI keyHintBadge;

        [Header("UI redesign (variant A): rarity header band")]
        [Tooltip("The Tag image becomes a full-width band in the rarity colour: 'RARITY · CATEGORY' left, 'Lv a → b' right.")]
        [SerializeField] private bool rarityBand;
        [SerializeField] private Image[] levelPips;
        [SerializeField] private GameObject newTag;
        [SerializeField] private TMP_Text newTagText;
        [SerializeField] private Image iconInnerImage;
        [SerializeField] private Image activePipGlow;

        private static readonly string[] RarityKeys = { "ui.rarity.common", "ui.rarity.rare", "ui.rarity.epic", "ui.rarity.legendary" };
        private static readonly string[] RarityFallbacks = { "Common", "Rare", "Epic", "Legendary" };
        private static readonly System.Text.StringBuilder HighlightBuilder = new System.Text.StringBuilder(256);

        private CardData currentData;
        private Action<CardData> onCardClicked;
        private Func<bool> isInputLockedCheck;
        private bool isInteractable = true;
        private string currentAdBadgeLabel = string.Empty;
        private string currentLevelBadgeLabel = string.Empty;
        private Button cachedButton;
        private bool buttonChecked;
        private bool shineActive;
        private float shineTime;

        public bool IsInteractable => isInteractable;
        public CardData CurrentData => currentData;
        public Button CardButton => buttonChecked ? cachedButton : (cachedButton = GetComponent<Button>());
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
            Color? levelBadgeColor = null,
            Func<bool> isInputLockedCheck = null)
        {
            UiTheme theme = UiTheme.Current;
            currentData = data;
            onCardClicked = clickCallback;
            this.isInputLockedCheck = isInputLockedCheck;
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
            if (keyHintBadge != null)
            {
                keyHintBadge.SetAllowed(isInteractable);
            }

            SetDimmed(false);

            if (nameText != null) nameText.text = Loc.Name(data);
            if (descriptionText != null)
            {
                string desc = Loc.Desc(data);
                descriptionText.text = rarityBand ? HighlightNumbers(desc, ColorUtility.ToHtmlStringRGB(theme.success)) : desc;
            }

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
                adBadgeText.text = canWatchAd
                    ? Loc.Get("ui.card.ad_badge", "<sprite name=\"ad\"> AD")
                    : Loc.Get("ui.card.no_ad", "NO AD");
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
                tagText.text = rarityBand ? FormatBandLabel(data) : FormatCardTag(data);
            }

            Color rarityColor = theme.GetRarityColor(data.rarity);
            if (!isInteractable) rarityColor = UiTheme.Shade(rarityColor, 0.5f);

            if (frameImage != null)
            {
                // Sprint 13 layout: rarity in the frame and glow, neutral card body.
                // Redesign v2: the band carries the rarity; Epic/Legendary get full rarity ring, Rare gets a subtle tint.
                if (rarityBand)
                {
                    frameImage.color = data.rarity >= CardRarity.Epic ? rarityColor : Color.clear;
                }
                else
                {
                    frameImage.color = rarityColor;
                }
                if (bgImage != null) bgImage.color = isInteractable ? (rarityBand ? theme.surface : theme.surfaceRaised) : theme.surfaceSunken;
            }
            else if (bgImage != null)
            {
                // Legacy layout (no frame): tint the whole card.
                bgImage.color = isInteractable ? rarityColor : UiTheme.WithAlpha(UiTheme.Shade(rarityColor, 0.9f), 0.7f);
            }

            if (glowImage != null)
            {
                if (rarityBand)
                {
                    glowImage.color = data.rarity == CardRarity.Legendary
                        ? UiTheme.WithAlpha(rarityColor, 0.68f)
                        : (data.rarity == CardRarity.Epic ? UiTheme.WithAlpha(rarityColor, 0.45f) : Color.clear);
                }
                else
                {
                    glowImage.color = UiTheme.WithAlpha(rarityColor, data.rarity >= CardRarity.Epic ? 0.55f : 0.28f);
                }
            }
            if (iconFrameImage != null) iconFrameImage.color = rarityColor;
            if (iconInnerImage != null)
            {
                iconInnerImage.color = rarityBand && data.rarity == CardRarity.Legendary
                    ? UiTheme.Hex(0x3A2C0E)
                    : theme.surfaceSunken;
            }
            if (rarityBand)
            {
                // Solid rarity band with dark text; the level badge sits inside it without its own pill.
                if (tagBg != null) tagBg.color = rarityColor;
                if (tagText != null) tagText.color = theme.background;
                if (levelBadgeBg != null) levelBadgeBg.color = Color.clear;
                if (levelBadgeText != null) levelBadgeText.color = theme.background;
                bool isNew = data is AbilityCardData;
                if (newTag != null) newTag.SetActive(isNew);
                if (newTagText != null && isNew) newTagText.text = Loc.Get("ui.card.new", "NEW!");
                SetLevelProgress(0, 0, 0);
            }
            else
            {
                if (tagBg != null) tagBg.color = UiTheme.WithAlpha(rarityColor, 0.25f);
                if (tagText != null) tagText.color = rarityColor;
            }

            shineActive = shine != null && data.rarity == CardRarity.Legendary && isInteractable;
            if (shine != null) shine.gameObject.SetActive(shineActive);
            shineTime = 0f;
        }

        public static bool IsLevelProgressLabel(string label)
        {
            return !string.IsNullOrEmpty(label)
                && (label.StartsWith("Lv", StringComparison.Ordinal)
                    || label.IndexOf("->", StringComparison.Ordinal) >= 0
                    || label.IndexOf('→') >= 0);
        }

        /// <summary>Category chip shown on the card.</summary>
        public static string FormatCardTag(CardData card)
        {
            switch (card)
            {
                case AbilityCardData abilityCard:
                    return abilityCard.abilityData != null && abilityCard.abilityData.type == ProjectB.Abilities.AbilityType.Passive
                        ? Loc.Get("ui.card.new_passive", "NEW PASSIVE")
                        : Loc.Get("ui.card.new_active", "NEW ACTIVE");
                case AbilityModifierCardData:
                    return Loc.Get("ui.card.upgrade", "UPGRADE");
                case WeaponUpgradeCardData:
                    return Loc.Get("ui.card.weapon", "WEAPON");
                case StatUpgradeCardData:
                    return Loc.Get("ui.card.stat", "STAT");
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Level pips in the rarity band: <paramref name="current"/> filled, the pip the card grants highlighted, the
        /// rest empty. Hidden when <paramref name="cap"/> is 0 (stat / weapon cards) or exceeds the pip count
        /// (Path of Mastery caps 20 / 50 — the "Lv a → b" label carries the progress then).
        /// </summary>
        public void SetLevelProgress(int current, int next, int cap)
        {
            if (levelPips == null) return;
            UiTheme theme = UiTheme.Current;
            bool show = rarityBand && cap > 0 && cap <= levelPips.Length;
            float pipW = cap > 5 ? 32f : 52f;
            float pipH = cap > 5 ? 18f : 24f;
            for (int i = 0; i < levelPips.Length; i++)
            {
                Image pip = levelPips[i];
                if (pip == null) continue;
                bool visible = show && i < cap;
                pip.gameObject.SetActive(visible);
                if (!visible) continue;
                pip.rectTransform.sizeDelta = new Vector2(pipW, pipH);
                // Owned levels gold, the level this card grants bright white (slightly taller), future levels dark recessed.
                if (i < current)
                {
                    pip.color = theme.gold;
                    pip.rectTransform.localScale = Vector3.one;
                }
                else if (i < next)
                {
                    pip.color = theme.white;
                    pip.rectTransform.localScale = new Vector3(1.06f, 1.12f, 1f);
                }
                else
                {
                    pip.color = UiTheme.WithAlpha(theme.surfaceSunken, 0.9f);
                    pip.rectTransform.localScale = Vector3.one;
                }
            }

            if (activePipGlow != null)
            {
                int grantedIdx = next - 1;
                bool showGlow = show && next > current && grantedIdx >= 0 && grantedIdx < cap;
                activePipGlow.gameObject.SetActive(showGlow);
                if (showGlow)
                {
                    const float spacing = 10f;
                    float cx = grantedIdx * (pipW + spacing) + pipW * 0.5f;
                    activePipGlow.rectTransform.anchoredPosition = new Vector2(cx, -pipH * 0.5f);
                }
            }
        }

        public int VisibleLevelPips
        {
            get
            {
                int count = 0;
                if (levelPips == null) return 0;
                for (int i = 0; i < levelPips.Length; i++)
                {
                    if (levelPips[i] != null && levelPips[i].gameObject.activeSelf) count++;
                }
                return count;
            }
        }

        /// <summary>Band label: "RARE · UPGRADE".</summary>
        public static string FormatBandLabel(CardData card)
        {
            int r = Mathf.Clamp((int)card.rarity, 0, RarityKeys.Length - 1);
            string rarity = Loc.Get(RarityKeys[r], RarityFallbacks[r]).ToUpperInvariant();
            string category = FormatCardTag(card);
            return string.IsNullOrEmpty(category) ? rarity : rarity + " · " + category;
        }

        /// <summary>
        /// Wraps numeric tokens (<c>+12%</c>, <c>0.5</c>, <c>×2</c>, <c>-10%</c>) outside rich-text tags in
        /// <c>&lt;color=#hex&gt;</c> so the effect of a card reads at a glance. Runs once per shown card, not per frame.
        /// </summary>
        public static string HighlightNumbers(string text, string hexColor)
        {
            if (string.IsNullOrEmpty(text)) return text;
            StringBuilder sb = HighlightBuilder;
            sb.Clear();
            int i = 0;
            int n = text.Length;
            bool inTag = false;
            while (i < n)
            {
                char c = text[i];
                if (inTag)
                {
                    sb.Append(c);
                    if (c == '>') inTag = false;
                    i++;
                    continue;
                }
                if (c == '<')
                {
                    inTag = true;
                    sb.Append(c);
                    i++;
                    continue;
                }

                // A number (optionally signed or "×"-prefixed) only counts when not glued to a word: "x2" yes, "box2" / "Lv2" no.
                bool gluedToWord = i > 0 && char.IsLetterOrDigit(text[i - 1]);
                bool signed = (c == '+' || c == '-' || c == '−' || c == '×' || c == 'x') && i + 1 < n && char.IsDigit(text[i + 1]);
                if (!gluedToWord && (signed || char.IsDigit(c)))
                {
                    int start = i;
                    int j = signed ? i + 1 : i;
                    while (j < n && char.IsDigit(text[j])) j++;
                    if (j + 1 < n && (text[j] == '.' || text[j] == ',') && char.IsDigit(text[j + 1]))
                    {
                        j++;
                        while (j < n && char.IsDigit(text[j])) j++;
                    }
                    if (j < n && (text[j] == '%' || text[j] == '×' || text[j] == 'x')) j++;
                    sb.Append("<color=#").Append(hexColor).Append('>').Append(text, start, j - start).Append("</color>");
                    i = j;
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        public void SetDimmed(bool dimmed)
        {
            if (canvasGroup != null) canvasGroup.alpha = dimmed ? 0.3f : 1f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Activate();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Activate();
        }

        public void Activate()
        {
            if (isInputLockedCheck != null && isInputLockedCheck()) return;
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
