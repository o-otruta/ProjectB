using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    public class ComboHudUI : MonoBehaviour
    {
        private static readonly string[] CachedComboLabels = BuildComboLabels(501);
        private static readonly string[] CachedBonusLabels =
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
        [SerializeField] private Image timerFillImage;
        [SerializeField] private Image backgroundImage;

        private RunComboTracker comboTracker;
        private bool isSubscribed;
        private float punchScale = 1f;
        private int lastDisplayedCombo = -1;
        private int lastDisplayedTier = -1;

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
            if (comboTracker != null && !isSubscribed)
            {
                comboTracker.OnComboChanged += HandleComboChanged;
                isSubscribed = true;
            }
            RefreshDisplay();
        }

        private void Unsubscribe()
        {
            if (comboTracker != null && isSubscribed)
            {
                comboTracker.OnComboChanged -= HandleComboChanged;
                isSubscribed = false;
            }
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

            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = comboTracker.NormalizedRemainingTime;
            }

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
                string bonusLabel = tier >= 0 && tier < CachedBonusLabels.Length
                    ? CachedBonusLabels[tier]
                    : string.Empty;
                bonusText.text = bonusLabel;
                bonusText.gameObject.SetActive(!string.IsNullOrEmpty(bonusLabel));
            }
            if (bonusText != null)
            {
                bonusText.color = tierColor;
            }

            if (timerFillImage != null)
            {
                timerFillImage.fillAmount = comboTracker.NormalizedRemainingTime;
                timerFillImage.color = tierColor;
            }

            if (backgroundImage != null)
            {
                UiTheme theme = UiTheme.Current;
                backgroundImage.color = tier > 0
                    ? UiTheme.WithAlpha(theme.surfaceRaised, 0.94f)
                    : UiTheme.WithAlpha(theme.surface, 0.9f);
            }
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
                _ => theme.textPrimary
            };
        }
    }
}
