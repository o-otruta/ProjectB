using System;
using UnityEngine;
using VContainer;
using ProjectB.Data.Meta;

namespace ProjectB.Meta
{
    public class DailyBonusManager
    {
        public const int DEFAULT_MAX_DAYS = 7;

        private readonly SaveManager saveManager;
        private DailyBonusRewardData rewardData;

        public event Action OnBonusStateChanged;

        [Inject]
        public DailyBonusManager(SaveManager saveManager)
        {
            this.saveManager = saveManager;
            LoadRewardData();
            ValidateStreak();
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

        public int GetMaxDays()
        {
            if (rewardData != null && rewardData.rewards != null && rewardData.rewards.Length > 0)
            {
                return rewardData.rewards.Length;
            }
            return DEFAULT_MAX_DAYS;
        }

        public bool IsRewardAvailable()
        {
            if (saveManager?.Data == null) return false;

            string lastClaimStr = saveManager.Data.lastDailyClaimDate;
            if (string.IsNullOrEmpty(lastClaimStr)) return true;

            if (DateTime.TryParse(lastClaimStr, out DateTime lastClaimDate))
            {
                DateTime today = DateTime.UtcNow.Date;
                lastClaimDate = lastClaimDate.Date;

                // Защита от перевода часов назад (чит): дата сегодня раньше последнего claim
                if (today < lastClaimDate)
                {
                    Debug.LogWarning($"[DailyBonusManager] Time manipulation detected! Current date ({today:yyyy-MM-dd}) is earlier than last claim ({lastClaimDate:yyyy-MM-dd}).");
                    return false;
                }

                // Уже получено сегодня
                if (today == lastClaimDate)
                {
                    return false;
                }

                return true;
            }

            // Поврежденная строка даты — разрешаем клейм
            return true;
        }

        public void ValidateStreak()
        {
            if (saveManager?.Data == null) return;

            int maxDays = GetMaxDays();
            saveManager.Data.dailyBonusDay = Mathf.Clamp(saveManager.Data.dailyBonusDay, 1, maxDays);
            saveManager.Data.dailyBonusCycle = Mathf.Max(0, saveManager.Data.dailyBonusCycle);

            string lastClaimStr = saveManager.Data.lastDailyClaimDate;
            if (string.IsNullOrEmpty(lastClaimStr)) return;

            if (DateTime.TryParse(lastClaimStr, out DateTime lastClaimDate))
            {
                DateTime today = DateTime.UtcNow.Date;
                lastClaimDate = lastClaimDate.Date;

                // Пропуск дня: прошло 2 или более дней с момента последнего получения награды
                if ((today - lastClaimDate).TotalDays >= 2)
                {
                    if (saveManager.Data.dailyBonusDay != 1)
                    {
                        Debug.Log($"[DailyBonusManager] Daily streak broken! Last claim: {lastClaimDate:yyyy-MM-dd}, Today: {today:yyyy-MM-dd}. Resetting to Day 1.");
                        saveManager.Data.dailyBonusDay = 1;
                        saveManager.Save();
                        OnBonusStateChanged?.Invoke();
                    }
                }
            }
        }

        public int GetCurrentDay()
        {
            ValidateStreak();
            return Mathf.Clamp(saveManager.Data.dailyBonusDay, 1, GetMaxDays());
        }

        public int GetCurrentCycle()
        {
            return Mathf.Max(0, saveManager.Data.dailyBonusCycle);
        }

        public int GetCoinsRewardAmount(DailyBonusReward baseReward)
        {
            if (baseReward.rewardType != DailyBonusRewardType.Coins) return 0;

            if (rewardData != null && saveManager?.Data != null)
            {
                int effectiveCycles = Mathf.Min(GetCurrentCycle(), rewardData.maxMultiplierCycles);
                float multiplier = Mathf.Pow(rewardData.cycleCoinMultiplier, effectiveCycles);
                return Mathf.RoundToInt(baseReward.coinsAmount * multiplier);
            }
            return baseReward.coinsAmount;
        }

        public bool ClaimReward(bool watchAdForDouble = false)
        {
            if (!IsRewardAvailable())
            {
                Debug.LogWarning("[DailyBonusManager] Reward not available today.");
                return false;
            }

            if (rewardData == null || rewardData.rewards == null || rewardData.rewards.Length == 0)
            {
                Debug.LogError("[DailyBonusManager] Reward data is missing or empty.");
                return false;
            }

            ValidateStreak();

            int currentDay = GetCurrentDay();
            int rewardIndex = currentDay - 1;

            if (rewardIndex < 0 || rewardIndex >= rewardData.rewards.Length)
            {
                Debug.LogError($"[DailyBonusManager] Invalid reward index: {rewardIndex} (Total rewards: {rewardData.rewards.Length}). Clamping.");
                rewardIndex = Mathf.Clamp(rewardIndex, 0, rewardData.rewards.Length - 1);
            }

            DailyBonusReward reward = rewardData.rewards[rewardIndex];

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
            }

            // Обновляем состояние
            saveManager.Data.lastDailyClaimDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

            // Продвигаем день
            int maxDays = GetMaxDays();
            saveManager.Data.dailyBonusDay++;
            if (saveManager.Data.dailyBonusDay > maxDays)
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
