using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ProjectB.Ads;
using ProjectB.Data;
using ProjectB.Meta;
using ProjectB.Data.Meta;

namespace ProjectB.UI
{
    public class DailyBonusScreenUI : MonoBehaviour
    {
        [SerializeField] private DailyBonusItemUI[] dayItems = new DailyBonusItemUI[7];
        [SerializeField] private Button claimButton;
        [SerializeField] private Button claimDoubleButton;
        [SerializeField] private GameObject allClaimedMessage;

        private DailyBonusManager bonusManager;
        private AdManager adManager;

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

        private void RefreshUI()
        {
            if (bonusManager == null) return;

            DailyBonusRewardData data = bonusManager.GetRewardData();
            if (data == null) return;

            int currentDay = bonusManager.GetCurrentDay();
            bool isRewardAvailable = bonusManager.IsRewardAvailable();
            bool justCompletedCycle = !isRewardAvailable && bonusManager.HasClaimedToday() && currentDay == 1;

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
            bonusManager?.ClaimReward(watchAdForDouble: false);
        }

        private void OnClaimDoubleClicked()
        {
            if (adManager != null)
            {
                adManager.ShowRewarded(AdPlacement.DailyBonusDouble, () =>
                {
                    bonusManager?.ClaimReward(watchAdForDouble: true);
                });
            }
            else
            {
                bonusManager?.ClaimReward(watchAdForDouble: true);
            }
        }
    }
}
