using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Data;

namespace ProjectB.Meta
{
    public class AchievementManager
    {
        private readonly SaveManager _saveManager;
        private List<AchievementData> _allAchievements;
        public List<AchievementData> unlockedThisRun { get; private set; }
        
        public event Action<AchievementData> OnAchievementUnlocked;

        public AchievementManager(SaveManager saveManager)
        {
            _saveManager = saveManager;
            _allAchievements = new List<AchievementData>(Resources.LoadAll<AchievementData>("Achievements"));
            unlockedThisRun = new List<AchievementData>();
        }

        public IReadOnlyList<AchievementData> GetAllAchievements() => _allAchievements;

        public void ClearRunUnlocks()
        {
            unlockedThisRun.Clear();
        }

        public void AddProgress(string id, int amount)
        {
            var achievement = _allAchievements.Find(a => a.id == id);
            if (achievement == null) return;

            if (_saveManager.Data.completedAchievements.Contains(id)) return;

            int currentProgress = 0;
            _saveManager.Data.achievementProgress.TryGetValue(id, out currentProgress);
            
            int newProgress = currentProgress + amount;
            if (newProgress >= achievement.targetValue)
            {
                newProgress = achievement.targetValue;
                UnlockAchievement(achievement);
            }
            
            _saveManager.UpdateAchievementProgress(id, newProgress);
        }

        public void AddProgressByType(AchievementConditionType conditionType, int amount, string targetId = null)
        {
            foreach (var achievement in _allAchievements)
            {
                if (_saveManager.Data.completedAchievements.Contains(achievement.id)) continue;

                if (achievement.conditionType == conditionType)
                {
                    if (string.IsNullOrEmpty(achievement.targetId) || achievement.targetId == targetId)
                    {
                        AddProgress(achievement.id, amount);
                    }
                }
            }
        }
        
        public void SetProgressByType(AchievementConditionType conditionType, int newValue, string targetId = null)
        {
            foreach (var achievement in _allAchievements)
            {
                if (_saveManager.Data.completedAchievements.Contains(achievement.id)) continue;

                if (achievement.conditionType == conditionType)
                {
                    if (string.IsNullOrEmpty(achievement.targetId) || achievement.targetId == targetId)
                    {
                        int currentProgress = 0;
                        _saveManager.Data.achievementProgress.TryGetValue(achievement.id, out currentProgress);
                        
                        if (newValue > currentProgress)
                        {
                            int diff = newValue - currentProgress;
                            AddProgress(achievement.id, diff);
                        }
                    }
                }
            }
        }

        private void UnlockAchievement(AchievementData achievement)
        {
            if (_saveManager.Data.completedAchievements.Contains(achievement.id)) return;

            _saveManager.CompleteAchievement(achievement.id);
            unlockedThisRun.Add(achievement);

            // Награды
            switch (achievement.rewardType)
            {
                case AchievementRewardType.GiveCoins:
                    _saveManager.AddCoins(achievement.rewardValue);
                    break;
                case AchievementRewardType.UnlockAbility:
                    if (!string.IsNullOrEmpty(achievement.rewardId))
                    {
                        _saveManager.UnlockAbility(achievement.rewardId);
                    }
                    break;
                case AchievementRewardType.UnlockHero:
                    if (!string.IsNullOrEmpty(achievement.rewardId))
                    {
                        if (!_saveManager.Data.unlockedHeroIds.Contains(achievement.rewardId))
                        {
                            _saveManager.Data.unlockedHeroIds.Add(achievement.rewardId);
                            _saveManager.Save();
                        }
                    }
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
    }
}
