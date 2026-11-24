using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Ads;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Data.Meta;

namespace ProjectB.UI
{
    /// <summary>
    /// Daily Bonus tab. Sprint 13.2: big title, "Cycle 2 · ×1.2" caption and a countdown to the next reward,
    /// 4 + 3 grid with a wide Day-7 chest cell, collected / today (glow + pulse) / future states,
    /// Claim (Primary) and ▶ x2 (Ad) buttons. The header coin pill counts up on claim.
    /// </summary>
    public class DailyBonusScreenUI : MonoBehaviour
    {
        [SerializeField] private DailyBonusItemUI[] dayItems = new DailyBonusItemUI[7];
        [SerializeField] private Button claimButton;
        [SerializeField] private Button claimDoubleButton;
        [SerializeField] private GameObject allClaimedMessage;

        [Header("Sprint 13")]
        [SerializeField] private TMP_Text cycleText;
        [SerializeField] private TMP_Text timerText;

        private DailyBonusManager bonusManager;
        private AdManager adManager;
        private readonly char[] timerBuffer = new char[40];
        private int lastTimerSeconds = -1;

        [Inject]
        public void Construct(DailyBonusManager bonusManager, AdManager adManager = null)
        {
            this.bonusManager = bonusManager;
            this.adManager = adManager;

            if (this.bonusManager != null)
            {
                this.bonusManager.OnBonusStateChanged += RefreshUI;
            }
            if (this.adManager != null)
            {
                this.adManager.OnAdStateChanged += RefreshUI;
            }
        }

        private void OnEnable()
        {
            lastTimerSeconds = -1;
            if (bonusManager != null)
            {
                RefreshUI();
            }
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
        }

        private void Start()
        {
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            if (claimDoubleButton != null) claimDoubleButton.onClick.AddListener(OnClaimDoubleClicked);
        }

        private void Update()
        {
            if (timerText == null || bonusManager == null) return;
            int seconds = SecondsUntilNextReward(DateTime.UtcNow);
            if (seconds == lastTimerSeconds) return;
            lastTimerSeconds = seconds;

            if (bonusManager.IsRewardAvailable())
            {
                timerText.text = "Reward ready!";
                return;
            }

            int len = UiFormat.WriteString(timerBuffer, 0, "Next reward in ");
            int hours = seconds / 3600;
            len = UiFormat.WriteInt(timerBuffer, len, hours);
            timerBuffer[len++] = 'h';
            timerBuffer[len++] = ' ';
            int minutes = (seconds % 3600) / 60;
            if (minutes < 10) timerBuffer[len++] = '0';
            len = UiFormat.WriteInt(timerBuffer, len, minutes);
            timerBuffer[len++] = 'm';
            timerBuffer[len++] = ' ';
            int secs = seconds % 60;
            if (secs < 10) timerBuffer[len++] = '0';
            len = UiFormat.WriteInt(timerBuffer, len, secs);
            timerBuffer[len++] = 's';
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
            return $"Cycle {cycleIndex + 1} · ×{multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";
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

            if (claimButton != null) claimButton.gameObject.SetActive(isRewardAvailable);
            if (claimDoubleButton != null)
            {
                claimDoubleButton.gameObject.SetActive(isRewardAvailable && isCoinsReward);
                claimDoubleButton.interactable = adManager == null || adManager.CanShowRewarded();
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
