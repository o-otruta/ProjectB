using System;
using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    public class ComboHudUI : MonoBehaviour
    {
        private static readonly string[] CachedComboLabels = BuildComboLabels(501);
        private readonly string[] cachedBonusLabels =
        {
            string.Empty,
            "+10% XP & COINS",
            "+15% XP & COINS",
            "+20% XP & COINS",
            "+30% XP & COINS"
        };


        [SerializeField] private GameObject rootContainer;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private TextMeshProUGUI bonusText;

        [Header("Expiry blink (replaces the timer bar)")]
        [Tooltip("Fades the combo texts; the combo timer is shown by blinking instead of a bar.")]
        [SerializeField] private CanvasGroup fadeGroup;
        [Tooltip("Remaining combo time (0..1) below which the counter starts blinking.")]
        [SerializeField, Range(0.05f, 1f)] private float blinkStartThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float blinkMinAlpha = 0.3f;
        [Tooltip("Blink rate (Hz) when blinking starts and right before the combo expires.")]
        [SerializeField, Min(0.1f)] private float blinkRateStart = 1.5f;
        [SerializeField, Min(0.1f)] private float blinkRateEnd = 4f;

        private RunComboTracker comboTracker;
        private bool isSubscribed;
        private float blinkPhase;
        private float punchScale = 1f;
        private int lastDisplayedCombo = -1;
        private int lastDisplayedTier = -1;
        private Action cachedLanguageChanged;

        private static string[] BuildComboLabels(int count)
        {
            var arr = new string[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = $"x{i}";
            }
            return arr;
        }

        public static string GetCachedComboLabel(int combo)
        {
            if (combo >= 0 && combo < CachedComboLabels.Length)
            {
                return CachedComboLabels[combo];
            }
            return $"x{combo}";
        }

        [Inject]
        public void Construct(RunComboTracker comboTracker)
        {
            this.comboTracker = comboTracker;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            if (rootContainer != null)
            {
                rootContainer.SetActive(false);
            }
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            RefreshBonusLabels();
            cachedLanguageChanged ??= HandleLanguageChanged;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;

            if (comboTracker != null && !isSubscribed)
            {
                comboTracker.OnComboChanged += HandleComboChanged;
                isSubscribed = true;
            }
            RefreshDisplay();
        }

        private void Unsubscribe()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
            if (comboTracker != null && isSubscribed)
            {
                comboTracker.OnComboChanged -= HandleComboChanged;
                isSubscribed = false;
            }
        }

        private void HandleLanguageChanged()
        {
            RefreshBonusLabels();
            lastDisplayedTier = -1;
            RefreshDisplay();
        }

        private void RefreshBonusLabels()
        {
            cachedBonusLabels[1] = Loc.Format("ui.hud.combo_bonus", 10);
            cachedBonusLabels[2] = Loc.Format("ui.hud.combo_bonus", 15);
            cachedBonusLabels[3] = Loc.Format("ui.hud.combo_bonus", 20);
            cachedBonusLabels[4] = Loc.Format("ui.hud.combo_bonus", 30);
        }

        private void HandleComboChanged(int combo, int tier, float bonusPercent)
        {
            if (combo >= 2 && combo > lastDisplayedCombo)
            {
                punchScale = 1.24f;
            }
            RefreshDisplay();
        }

        private void Update()
        {
            if (comboTracker == null || rootContainer == null || !rootContainer.activeSelf) return;

            UpdateExpiryBlink(comboTracker.NormalizedRemainingTime);

            if (punchScale > 1.001f)
            {
                punchScale = Mathf.Lerp(punchScale, 1f, Mathf.Clamp01(Time.unscaledDeltaTime * 10f));
                rootContainer.transform.localScale = new Vector3(punchScale, punchScale, 1f);
            }
            else if (punchScale != 1f)
            {
                punchScale = 1f;
                rootContainer.transform.localScale = Vector3.one;
            }
        }

        private void UpdateExpiryBlink(float remaining)
        {
            if (fadeGroup == null) return;

            if (remaining >= blinkStartThreshold)
            {
                blinkPhase = 0f;
                if (fadeGroup.alpha != 1f) fadeGroup.alpha = 1f;
                return;
            }

            // Scaled time: the blink freezes with the combo timer on pause / level-up.
            float urgency = 1f - Mathf.Clamp01(remaining / blinkStartThreshold);
            float rate = Mathf.Lerp(blinkRateStart, blinkRateEnd, urgency);
            blinkPhase = Mathf.Repeat(blinkPhase + Time.deltaTime * rate, 1f);
            float wave = 0.5f + 0.5f * Mathf.Cos(blinkPhase * 2f * Mathf.PI);
            fadeGroup.alpha = Mathf.Lerp(blinkMinAlpha, 1f, wave);
        }

        public void RefreshDisplay()
        {
            int combo = comboTracker != null ? comboTracker.CurrentCombo : 0;
            int tier = comboTracker != null ? comboTracker.CurrentMilestoneTier : 0;
            bool visible = combo >= 2;

            if (rootContainer != null && rootContainer.activeSelf != visible)
            {
                rootContainer.SetActive(visible);
                if (!visible)
                {
                    punchScale = 1f;
                    rootContainer.transform.localScale = Vector3.one;
                }
            }

            if (!visible)
            {
                lastDisplayedCombo = combo;
                lastDisplayedTier = tier;
                return;
            }

            Color tierColor = GetTierColor(tier);

            if (comboText != null && combo != lastDisplayedCombo)
            {
                lastDisplayedCombo = combo;
                comboText.text = GetCachedComboLabel(combo);
            }
            if (comboText != null)
            {
                comboText.color = tierColor;
            }

            if (bonusText != null && tier != lastDisplayedTier)
            {
                lastDisplayedTier = tier;
                string bonusLabel = tier >= 0 && tier < cachedBonusLabels.Length
                    ? cachedBonusLabels[tier]
                    : string.Empty;
                bonusText.text = bonusLabel;
                bonusText.gameObject.SetActive(!string.IsNullOrEmpty(bonusLabel));
            }
            if (bonusText != null)
            {
                bonusText.color = UiTheme.Current.textPrimary;
            }

            UpdateExpiryBlink(comboTracker.NormalizedRemainingTime);
        }

        private static Color GetTierColor(int tier)
        {
            UiTheme theme = UiTheme.Current;
            return tier switch
            {
                1 => theme.gold,
                2 => theme.warning,
                3 => theme.danger,
                4 => theme.rarityEpic,
                _ => theme.gold
            };
        }
    }
}
