using System;
using UnityEngine;

namespace ProjectB.Meta
{
    public class SaveManager : IDisposable
    {
        public const string SAVE_KEY = "ProjectB_SaveData";
        
        public SaveData Data { get; private set; }
        public bool IsDirty { get; private set; }
        public event Action OnDataChanged;

        public SaveManager()
        {
            Load();
            Application.quitting += HandleQuitting;
            Application.focusChanged += HandleFocusChanged;
        }

        private void HandleQuitting()
        {
            Save();
        }

        private void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                SaveIfDirty();
            }
        }

        public void Dispose()
        {
            Application.quitting -= HandleQuitting;
            Application.focusChanged -= HandleFocusChanged;
            SaveIfDirty();
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
                IsDirty = false;
                OnDataChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save Data. Error: {e.Message}");
            }
        }

        public void SaveIfDirty()
        {
            if (IsDirty)
            {
                Save();
            }
        }

        private void CommitChange(bool saveImmediately)
        {
            IsDirty = true;
            if (saveImmediately)
            {
                Save();
            }
            else
            {
                OnDataChanged?.Invoke();
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

        public void AddCoins(int amount, bool saveImmediately = true)
        {
            if (amount <= 0) return;
            Data.coins += amount;
            CommitChange(saveImmediately);
        }

        public bool SpendCoins(int amount, bool saveImmediately = true)
        {
            if (amount <= 0 || Data.coins < amount) return false;
            
            Data.coins -= amount;
            CommitChange(saveImmediately);
            return true;
        }

        public void RecordRunResult(RunResult result)
        {
            Data.totalRuns++;
            if (result.coinsEarned > 0)
            {
                Data.coins += result.coinsEarned;
            }
            Data.totalKills += result.enemiesKilled;
            Data.totalPlayTime += result.playTime;
            
            if (result.waveReached > Data.bestWave)
            {
                Data.bestWave = result.waveReached;
            }
            
            Save();
        }

        public void SetMetaUpgradeLevel(string id, int level, bool saveImmediately = true)
        {
            Data.metaUpgradeLevels[id] = level;
            CommitChange(saveImmediately);
        }
        
        public int GetMetaUpgradeLevel(string id)
        {
            if (Data.metaUpgradeLevels.TryGetValue(id, out int level))
            {
                return level;
            }
            return 0;
        }

        public void UpdateAchievementProgress(string id, int value, bool saveImmediately = false)
        {
            Data.achievementProgress[id] = value;
            IsDirty = true;
            if (saveImmediately)
            {
                Save();
            }
        }

        public void CompleteAchievement(string id, bool saveImmediately = false)
        {
            if (!Data.completedAchievements.Contains(id))
            {
                Data.completedAchievements.Add(id);
                CommitChange(saveImmediately);
            }
        }

        public void UnlockAbility(string id, bool saveImmediately = true)
        {
            if (!Data.unlockedAbilityIds.Contains(id))
            {
                Data.unlockedAbilityIds.Add(id);
                CommitChange(saveImmediately);
            }
        }

        public void UnlockHero(string id, bool saveImmediately = true)
        {
            if (!Data.unlockedHeroIds.Contains(id))
            {
                Data.unlockedHeroIds.Add(id);
                CommitChange(saveImmediately);
            }
        }
    }
}
