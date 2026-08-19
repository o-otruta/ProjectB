using System;
using UnityEngine;
using VContainer;
using ProjectB.Data.Meta;

namespace ProjectB.Meta
{
    public class DailyBonusManager
    {
        private SaveManager saveManager;
        private DailyBonusRewardData rewardData;

        public event Action OnBonusStateChanged;

        [Inject]
        public DailyBonusManager(SaveManager saveManager)
        {
            this.saveManager = saveManager;
            LoadRewardData();
        }

        private void LoadRewardData()
        {
            rewardData = Resources.Load<DailyBonusRewardData>("DailyBonusRewardData");
            if (rewardData == null)
            {
                Debug.LogError("[DailyBonusManager] Failed to load DailyBonusRewardData from Resources/DailyBonusRewardData");
            }
        }

        public DailyBonusRewardData GetRewardData()
        {
            return rewardData;
        }

        public bool IsRewardAvailable()
        {
            string todayStr = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            return todayStr != saveManager.Data.lastDailyClaimDate;
        }

        public int GetCurrentDay()
        {
            // dailyBonusDay is 1-indexed (1 to 7)
            return Mathf.Clamp(saveManager.Data.dailyBonusDay, 1, 7);
        }

        public int GetCurrentCycle()
        {
            return saveManager.Data.dailyBonusCycle;
        }

        public int GetCoinsRewardAmount(DailyBonusReward baseReward)
        {
            if (baseReward.rewardType != DailyBonusRewardType.Coins) return 0;

            if (rewardData != null)
            {
                int effectiveCycles = Mathf.Min(saveManager.Data.dailyBonusCycle, rewardData.maxMultiplierCycles);
                float multiplier = Mathf.Pow(rewardData.cycleCoinMultiplier, effectiveCycles);
                return Mathf.RoundToInt(baseReward.coinsAmount * multiplier);
            }
            return baseReward.coinsAmount;
        }

        public bool ClaimReward(bool watchAdForDouble = false)
        {
            if (!IsRewardAvailable())
            {
                Debug.LogWarning("[DailyBonusManager] Reward already claimed today.");
                return false;
            }

            if (rewardData == null)
            {
                Debug.LogError("[DailyBonusManager] Reward data is missing.");
                return false;
            }

            int currentDay = GetCurrentDay();
            DailyBonusReward reward = rewardData.rewards[currentDay - 1];

            // Начисляем награду
            if (reward.rewardType == DailyBonusRewardType.Coins)
            {
                int amount = GetCoinsRewardAmount(reward);
                if (watchAdForDouble) amount *= 2;
                saveManager.AddCoins(amount);
            }
            else if (reward.rewardType == DailyBonusRewardType.Ability)
            {
                saveManager.UnlockAbility(reward.abilityId);
                // Если удвоение за рекламу для способностей не применимо, ничего не делаем
                // Или можно выдать запасные монеты
            }

            // Обновляем состояние
            saveManager.Data.lastDailyClaimDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            
            // Продвигаем день
            saveManager.Data.dailyBonusDay++;
            if (saveManager.Data.dailyBonusDay > 7)
            {
                saveManager.Data.dailyBonusDay = 1;
                saveManager.Data.dailyBonusCycle++;
            }

            saveManager.Save();
            OnBonusStateChanged?.Invoke();

            return true;
        }
    }
}
