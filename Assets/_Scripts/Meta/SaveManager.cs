using System;
using UnityEngine;

namespace ProjectB.Meta
{
    public class SaveManager : IDisposable
    {
        public const string SAVE_KEY = "ProjectB_SaveData";
        
        public SaveData Data { get; private set; }
        public event Action OnDataChanged;

        public SaveManager()
        {
            Load();
            Application.quitting += HandleQuitting;
        }

        private void HandleQuitting()
        {
            Save();
        }

        public void Dispose()
        {
            Application.quitting -= HandleQuitting;
        }

        public void Load()
        {
            if (PlayerPrefs.HasKey(SAVE_KEY))
            {
                try
                {
                    string json = PlayerPrefs.GetString(SAVE_KEY);
                    Data = JsonUtility.FromJson<SaveData>(json);
                    
                    if (Data == null)
                    {
                        Debug.LogWarning("[SaveManager] Loaded JSON was parsed as null. Creating new SaveData.");
                        Data = new SaveData();
                    }
                    else
                    {
                        MigrateIfNeeded();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveManager] Failed to load SaveData. Creating new. Error: {e.Message}");
                    Data = new SaveData();
                }
            }
            else
            {
                Data = new SaveData();
            }
            
            Debug.Log("[SaveManager] Data Loaded.");
            OnDataChanged?.Invoke();
        }

        public void Save()
        {
            if (Data == null) return;
            
            try
            {
                string json = JsonUtility.ToJson(Data, false);
                PlayerPrefs.SetString(SAVE_KEY, json);
                PlayerPrefs.Save();
                Debug.Log("[SaveManager] Data Saved.");
                OnDataChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save Data. Error: {e.Message}");
            }
        }

        public void ResetAll()
        {
            Data = new SaveData();
            Save();
            Debug.Log("[SaveManager] Save Data Reset.");
        }

        private void MigrateIfNeeded()
        {
            // Здесь будет логика миграции при изменении saveVersion
            // Например:
            // if (Data.saveVersion == 1) { /* миграция на v2 */ Data.saveVersion = 2; }
        }

        // --- Удобные методы ---

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Data.coins += amount;
            Save();
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || Data.coins < amount) return false;
            
            Data.coins -= amount;
            Save(); // Сохраняем сразу при трате валюты для безопасности
            return true;
        }

        public void RecordRunResult(RunResult result)
        {
            Data.totalRuns++;
            AddCoins(result.coinsEarned);
            Data.totalKills += result.enemiesKilled;
            Data.totalPlayTime += result.playTime;
            
            if (result.waveReached > Data.bestWave)
            {
                Data.bestWave = result.waveReached;
            }
            
            // Здесь также можно будет триггерить проверку ачивок, когда AchievementManager будет готов
            
            Save();
        }

        public void SetMetaUpgradeLevel(string id, int level)
        {
            Data.metaUpgradeLevels[id] = level;
            Save();
        }
        
        public int GetMetaUpgradeLevel(string id)
        {
            if (Data.metaUpgradeLevels.TryGetValue(id, out int level))
            {
                return level;
            }
            return 0;
        }

        public void UpdateAchievementProgress(string id, int value)
        {
            Data.achievementProgress[id] = value;
            Save();
            // Проверка на завершение будет в AchievementManager
        }

        public void CompleteAchievement(string id)
        {
            if (!Data.completedAchievements.Contains(id))
            {
                Data.completedAchievements.Add(id);
                Save();
            }
        }

        public void UnlockAbility(string id)
        {
            if (!Data.unlockedAbilityIds.Contains(id))
            {
                Data.unlockedAbilityIds.Add(id);
                Save();
            }
        }
    }
}
