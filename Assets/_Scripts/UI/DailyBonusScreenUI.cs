using UnityEngine;
using UnityEngine.UI;
using VContainer;
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

        [Inject]
        public void Construct(DailyBonusManager bonusManager)
        {
            this.bonusManager = bonusManager;
            this.bonusManager.OnBonusStateChanged += RefreshUI;
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

            for (int i = 0; i < dayItems.Length; i++)
            {
                if (dayItems[i] == null) continue;

                int dayIndex = i + 1;
                DailyBonusReward reward = data.rewards[i];
                int coinsAmount = bonusManager.GetCoinsRewardAmount(reward);

                bool isClaimed = dayIndex < currentDay || (dayIndex == currentDay && !isRewardAvailable);
                bool isCurrentDay = dayIndex == currentDay;

                dayItems[i].Setup(dayIndex, reward, coinsAmount, isClaimed, isCurrentDay);
            }

            if (claimButton != null) claimButton.gameObject.SetActive(isRewardAvailable);
            if (claimDoubleButton != null) claimDoubleButton.gameObject.SetActive(isRewardAvailable);
            if (allClaimedMessage != null) allClaimedMessage.SetActive(!isRewardAvailable);
        }

        private void OnClaimClicked()
        {
            bonusManager.ClaimReward(watchAdForDouble: false);
        }

        private void OnClaimDoubleClicked()
        {
            // Здесь в будущем будет вызов AdManager для просмотра рекламы
            // Пока просто выдаем удвоенную награду (заглушка)
            Debug.Log("[DailyBonusScreenUI] Watch Ad clicked! (Stub)");
            bonusManager.ClaimReward(watchAdForDouble: true);
        }
    }
}
