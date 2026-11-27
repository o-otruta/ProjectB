using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Ads;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Data.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// Daily Bonus modal popup. 4 + 3 grid (Portrait) / 7-day row (Landscape) with a wide Day-7 chest cell,
    /// collected / today (glow + pulse) / future states, Claim (Primary) and ▶ x2 (Ad) buttons.
    /// </summary>
    public class DailyBonusScreenUI : MonoBehaviour
    {
        [Header("Popup Shell")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundButton;

        [SerializeField] private DailyBonusItemUI[] dayItems = new DailyBonusItemUI[7];
        [SerializeField] private Button claimButton;
        [SerializeField] private Button claimDoubleButton;
        [SerializeField] private GameObject allClaimedMessage;

        [Header("Sprint 13")]
        [SerializeField] private TMP_Text cycleText;
        [SerializeField] private TMP_Text timerText;

        private DailyBonusManager bonusManager;
        private AdManager adManager;
        private InputModeService inputModeService;
        private readonly char[] timerBuffer = new char[64];
        private int lastTimerSeconds = -1;
        private string cachedNextRewardPrefix = "Next reward in ";
        private string cachedHourSuffix = "h";
        private string cachedMinuteSuffix = "m";
        private string cachedSecondSuffix = "s";
        private Action cachedLanguageChanged;
        private bool buttonsWired;

        public bool IsOpen => popupPanel != null ? popupPanel.activeSelf : gameObject.activeSelf;
        public event Action OnClosed;

        public Selectable GetFirstSelectable()
        {
            if (claimButton != null && claimButton.gameObject.activeInHierarchy && claimButton.interactable)
            {
                return claimButton;
            }
            if (claimDoubleButton != null && claimDoubleButton.gameObject.activeInHierarchy && claimDoubleButton.interactable)
            {
                return claimDoubleButton;
            }
            if (closeButton != null && closeButton.gameObject.activeInHierarchy && closeButton.interactable)
            {
                return closeButton;
            }
            Transform root = popupPanel != null ? popupPanel.transform : transform;
            return UiFocusHelper.FindFirstInteractable(root);
        }

        [Inject]
        public void Construct(DailyBonusManager bonusManager, AdManager adManager = null, InputModeService inputModeService = null)
        {
            if (this.bonusManager != null)
            {
                this.bonusManager.OnBonusStateChanged -= RefreshUI;
            }
            if (this.adManager != null)
            {
                this.adManager.OnAdStateChanged -= RefreshUI;
            }

            this.bonusManager = bonusManager;
            this.adManager = adManager;
            this.inputModeService = inputModeService;

            if (this.bonusManager != null)
            {
                this.bonusManager.OnBonusStateChanged += RefreshUI;
            }
            if (this.adManager != null)
            {
                this.adManager.OnAdStateChanged += RefreshUI;
            }
        }

        private void Awake()
        {
            WireButtons();
            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void WireButtons()
        {
            if (buttonsWired) return;
            buttonsWired = true;
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            if (claimDoubleButton != null) claimDoubleButton.onClick.AddListener(OnClaimDoubleClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            cachedLanguageChanged ??= HandleLanguageChanged;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
            RefreshLocalizedTimerUnits();
            lastTimerSeconds = -1;
            if (bonusManager != null)
            {
                RefreshUI();
            }
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
        }

        private void HandleLanguageChanged()
        {
            RefreshLocalizedTimerUnits();
            lastTimerSeconds = -1;
            if (bonusManager != null) RefreshUI();
        }

        private void RefreshLocalizedTimerUnits()
        {
            cachedNextRewardPrefix = Loc.Get("ui.daily.next_reward_prefix", "Next reward in ");
            cachedHourSuffix = Loc.Get("ui.unit.hours", "h");
            cachedMinuteSuffix = Loc.Get("ui.unit.minutes", "m");
            cachedSecondSuffix = Loc.Get("ui.unit.seconds", "s");
        }

        private void OnDestroy()
        {
            if (bonusManager != null)
            {
                bonusManager.OnBonusStateChanged -= RefreshUI;
            }
            if (adManager != null)
            {
                adManager.OnAdStateChanged -= RefreshUI;
            }
            if (buttonsWired)
            {
                if (claimButton != null) claimButton.onClick.RemoveListener(OnClaimClicked);
                if (claimDoubleButton != null) claimDoubleButton.onClick.RemoveListener(OnClaimDoubleClicked);
                if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
                if (backgroundButton != null) backgroundButton.onClick.RemoveListener(Hide);
            }
        }

        public void Show()
        {
            WireButtons();
            if (popupPanel != null) popupPanel.SetActive(true);
            else if (!gameObject.activeSelf) gameObject.SetActive(true);

            RefreshLocalizedTimerUnits();
            lastTimerSeconds = -1;
            if (bonusManager != null) RefreshUI();
            UiFocusHelper.SetFirstSelected(GetFirstSelectable(), inputModeService);
        }

        public void Hide()
        {
            UiAudio.Click();
            if (popupPanel != null) popupPanel.SetActive(false);
            else if (gameObject.activeSelf) gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen || timerText == null || bonusManager == null) return;
            int seconds = SecondsUntilNextReward(DateTime.UtcNow);
            if (seconds == lastTimerSeconds) return;
            lastTimerSeconds = seconds;

            if (bonusManager.IsRewardAvailable())
            {
                timerText.text = Loc.Get("ui.daily.reward_ready", "Reward ready!");
                return;
            }

            int len = UiFormat.WriteString(timerBuffer, 0, cachedNextRewardPrefix);
            int hours = seconds / 3600;
            len = UiFormat.WriteInt(timerBuffer, len, hours);
            len = UiFormat.WriteString(timerBuffer, len, cachedHourSuffix);
            timerBuffer[len++] = ' ';
            int minutes = (seconds % 3600) / 60;
            if (minutes < 10) timerBuffer[len++] = '0';
            len = UiFormat.WriteInt(timerBuffer, len, minutes);
            len = UiFormat.WriteString(timerBuffer, len, cachedMinuteSuffix);
            timerBuffer[len++] = ' ';
            int secs = seconds % 60;
            if (secs < 10) timerBuffer[len++] = '0';
            len = UiFormat.WriteInt(timerBuffer, len, secs);
            len = UiFormat.WriteString(timerBuffer, len, cachedSecondSuffix);
            timerText.SetCharArray(timerBuffer, 0, len);
        }

        /// <summary>Seconds until the next UTC day (claims reset at UTC midnight, see DailyBonusManager).</summary>
        public static int SecondsUntilNextReward(DateTime utcNow)
        {
            DateTime next = utcNow.Date.AddDays(1);
            return Mathf.Max(0, (int)Math.Ceiling((next - utcNow).TotalSeconds));
        }

        public static string FormatCycle(int cycleIndex, float multiplierPerCycle, int maxCycles)
        {
            int effective = Mathf.Min(Mathf.Max(0, cycleIndex), Mathf.Max(0, maxCycles));
            float multiplier = Mathf.Pow(multiplierPerCycle, effective);
            return Loc.Format("ui.daily.cycle", cycleIndex + 1, multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void RefreshUI()
        {
            if (bonusManager == null) return;

            DailyBonusRewardData data = bonusManager.GetRewardData();
            if (data == null) return;

            int currentDay = bonusManager.GetCurrentDay();
            bool isRewardAvailable = bonusManager.IsRewardAvailable();
            bool justCompletedCycle = !isRewardAvailable && bonusManager.HasClaimedToday() && currentDay == 1;

            if (cycleText != null)
            {
                cycleText.text = FormatCycle(bonusManager.GetCurrentCycle(), data.cycleCoinMultiplier, data.maxMultiplierCycles);
            }
            lastTimerSeconds = -1;

            for (int i = 0; i < dayItems.Length; i++)
            {
                if (dayItems[i] == null) continue;

                int dayIndex = i + 1;
                if (data.rewards == null || i >= data.rewards.Length)
                {
                    dayItems[i].gameObject.SetActive(false);
                    continue;
                }

                DailyBonusReward reward = data.rewards[i];
                int coinsAmount = bonusManager.GetCoinsRewardAmount(reward);

                bool isClaimed = justCompletedCycle || dayIndex < currentDay;
                bool isCurrentDay = isRewardAvailable && dayIndex == currentDay;

                dayItems[i].Setup(dayIndex, reward, coinsAmount, isClaimed, isCurrentDay);
            }

            bool isCoinsReward = data.rewards != null
                && data.rewards.Length > 0
                && data.rewards[Mathf.Clamp(currentDay - 1, 0, data.rewards.Length - 1)].rewardType == DailyBonusRewardType.Coins;

            bool adsSupported = adManager == null || adManager.IsAdsAvailable;
            bool showDouble = isRewardAvailable && isCoinsReward && adsSupported;

            if (claimButton != null)
            {
                claimButton.gameObject.SetActive(isRewardAvailable);
                if (isRewardAvailable && !showDouble && claimButton.transform is RectTransform claimRt)
                {
                    Vector2 pos = claimRt.anchoredPosition;
                    if (pos.x < -1f)
                    {
                        pos.x = 0f;
                        claimRt.anchoredPosition = pos;
                    }
                }
            }
            if (claimDoubleButton != null)
            {
                claimDoubleButton.gameObject.SetActive(showDouble);
                if (showDouble)
                {
                    claimDoubleButton.interactable = adManager == null || adManager.CanShowRewarded();
                }
            }
            if (allClaimedMessage != null) allClaimedMessage.SetActive(!isRewardAvailable);
        }

        private void OnClaimClicked()
        {
            if (bonusManager != null && bonusManager.ClaimReward(watchAdForDouble: false))
            {
                UiAudio.Play(SfxType.CoinPickup);
            }
        }

        private void OnClaimDoubleClicked()
        {
            if (adManager != null)
            {
                adManager.ShowRewarded(AdPlacement.DailyBonusDouble, () =>
                {
                    if (bonusManager != null && bonusManager.ClaimReward(watchAdForDouble: true))
                    {
                        UiAudio.Play(SfxType.CoinPickup);
                    }
                });
            }
            else
            {
                bonusManager?.ClaimReward(watchAdForDouble: true);
            }
        }
    }
}
