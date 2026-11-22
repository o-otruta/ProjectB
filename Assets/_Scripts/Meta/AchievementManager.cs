using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Data;

namespace ProjectB.Meta
{
    public class AchievementManager
    {
        private readonly SaveManager _saveManager;
        private readonly List<AchievementData> _allAchievements;
        private readonly Dictionary<string, AchievementData> _achievementsById;
        private readonly Dictionary<AchievementConditionType, List<AchievementData>> _achievementsByCondition;

        public List<AchievementData> unlockedThisRun { get; private set; }
        
        public event Action<AchievementData> OnAchievementUnlocked;

        public AchievementManager(SaveManager saveManager)
        {
            _saveManager = saveManager;
            _allAchievements = new List<AchievementData>(Resources.LoadAll<AchievementData>("Achievements"));
            _allAchievements.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                int typeCmp = a.conditionType.CompareTo(b.conditionType);
                if (typeCmp != 0) return typeCmp;
                int targetCmp = a.targetValue.CompareTo(b.targetValue);
                if (targetCmp != 0) return targetCmp;
                return string.CompareOrdinal(a.id, b.id);
            });
            _achievementsById = new Dictionary<string, AchievementData>(_allAchievements.Count);
            _achievementsByCondition = new Dictionary<AchievementConditionType, List<AchievementData>>();
            unlockedThisRun = new List<AchievementData>();

            for (int i = 0; i < _allAchievements.Count; i++)
            {
                var achievement = _allAchievements[i];
                if (achievement == null || string.IsNullOrEmpty(achievement.id)) continue;

                _achievementsById[achievement.id] = achievement;

                if (!_achievementsByCondition.TryGetValue(achievement.conditionType, out var list))
                {
                    list = new List<AchievementData>();
                    _achievementsByCondition[achievement.conditionType] = list;
                }
                list.Add(achievement);
            }
        }

        public IReadOnlyList<AchievementData> GetAllAchievements() => _allAchievements;

        public void ClearRunUnlocks()
        {
            unlockedThisRun.Clear();
        }

        public void AddProgress(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            if (!_achievementsById.TryGetValue(id, out var achievement)) return;

            if (_saveManager.Data.completedAchievements.Contains(id)) return;

            _saveManager.Data.achievementProgress.TryGetValue(id, out int currentProgress);
            
            int newProgress = currentProgress + amount;
            bool shouldUnlock = newProgress >= achievement.targetValue;
            if (shouldUnlock)
            {
                newProgress = achievement.targetValue;
            }

            // Накапливаем в памяти без синхронного PlayerPrefs.Save() посреди боя;
            // сброс на диск произойдёт при окончании забега, смене сцены или OnApplicationPause.
            _saveManager.UpdateAchievementProgress(id, newProgress, saveImmediately: false);

            if (shouldUnlock)
            {
                UnlockAchievement(achievement);
            }
        }

        public void AddProgressByType(AchievementConditionType conditionType, int amount, string targetId = null)
        {
            if (!_achievementsByCondition.TryGetValue(conditionType, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var achievement = list[i];
                if (_saveManager.Data.completedAchievements.Contains(achievement.id)) continue;

                if (string.IsNullOrEmpty(achievement.targetId) || achievement.targetId == targetId)
                {
                    AddProgress(achievement.id, amount);
                }
            }
        }
        
        public void SetProgressByType(AchievementConditionType conditionType, int newValue, string targetId = null)
        {
            if (!_achievementsByCondition.TryGetValue(conditionType, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                var achievement = list[i];
                if (_saveManager.Data.completedAchievements.Contains(achievement.id)) continue;

                if (string.IsNullOrEmpty(achievement.targetId) || achievement.targetId == targetId)
                {
                    _saveManager.Data.achievementProgress.TryGetValue(achievement.id, out int currentProgress);
                    
                    if (newValue > currentProgress)
                    {
                        AddProgress(achievement.id, newValue - currentProgress);
                    }
                }
            }
        }

        private void UnlockAchievement(AchievementData achievement)
        {
            if (_saveManager.Data.completedAchievements.Contains(achievement.id)) return;

            _saveManager.CompleteAchievement(achievement.id, saveImmediately: false);
            unlockedThisRun.Add(achievement);

            // Награды (без немедленного дискового I/O в бою)
            switch (achievement.rewardType)
            {
                case AchievementRewardType.GiveCoins:
                    _saveManager.AddCoins(achievement.rewardValue, saveImmediately: false);
                    break;
                case AchievementRewardType.UnlockAbility:
                    if (!string.IsNullOrEmpty(achievement.rewardId))
                    {
                        _saveManager.UnlockAbility(achievement.rewardId, saveImmediately: false);
                    }
                    break;
                case AchievementRewardType.UnlockHero:
                    if (!string.IsNullOrEmpty(achievement.rewardId))
                    {
                        _saveManager.UnlockHero(achievement.rewardId, saveImmediately: false);
                    }
                    break;
                case AchievementRewardType.UnlockActiveSlot:
                    _saveManager.AddBonusActiveSlots(Mathf.Max(1, achievement.rewardValue), saveImmediately: false);
                    break;
                case AchievementRewardType.UnlockPassiveSlot:
                    _saveManager.AddBonusPassiveSlots(Mathf.Max(1, achievement.rewardValue), saveImmediately: false);
                    break;
                case AchievementRewardType.None:
                default:
                    break;
            }

            OnAchievementUnlocked?.Invoke(achievement);
        }
        
        // Обработчики событий
        public void OnEnemyKilled()
        {
            AddProgressByType(AchievementConditionType.TotalKills, 1);
        }
        
        public void OnWaveReached(int wave)
        {
            SetProgressByType(AchievementConditionType.ReachWave, wave);
        }
        
        public void OnBossDefeated(string bossId)
        {
            AddProgressByType(AchievementConditionType.DefeatBoss, 1, bossId);
        }
        
        public void OnAbilityUpgraded(string abilityId, int level)
        {
            SetProgressByType(AchievementConditionType.MaxAbilityLevel, level, abilityId);
        }

        public void OnAbilitiesCollectedInRun(int uniqueAbilitiesCount)
        {
            SetProgressByType(AchievementConditionType.CollectAbilitiesInRun, uniqueAbilitiesCount);
        }

        public void OnNoDamageWaves(int consecutiveWaves)
        {
            SetProgressByType(AchievementConditionType.NoDamageWaves, consecutiveWaves);
        }

        public void OnSecretItemFound(string secretItemId)
        {
            AddProgressByType(AchievementConditionType.FindSecretItem, 1, secretItemId);
        }
    }
}
