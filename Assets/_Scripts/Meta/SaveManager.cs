using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Core;

namespace ProjectB.Meta
{
    public interface ISaveStorage
    {
        bool HasKey(string key);
        string GetString(string key);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        public static readonly PlayerPrefsSaveStorage Instance = new PlayerPrefsSaveStorage();

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool HasKey(string key) => !string.IsNullOrEmpty(key) && entries.ContainsKey(key);
        public string GetString(string key) => !string.IsNullOrEmpty(key) && entries.TryGetValue(key, out string val) ? val : string.Empty;
        public void SetString(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) entries[key] = value ?? string.Empty;
        }
        public void DeleteKey(string key)
        {
            if (!string.IsNullOrEmpty(key)) entries.Remove(key);
        }
        public void Save() { }
    }

    public class SaveManager : IDisposable
    {
        public const string SAVE_KEY = "ProjectB_SaveData";
        
        private ISaveStorage storage;

        public SaveData Data { get; private set; }
        public ISaveStorage Storage => storage;
        public bool IsDirty { get; private set; }
        public bool WasInterruptedRunRecovered { get; private set; }
        public bool IsFirstRun => Data == null || Data.totalRuns == 0;
        public event Action OnDataChanged;

        [Inject]
        public SaveManager() : this(null)
        {
        }

        public SaveManager(ISaveStorage customStorage)
        {
            storage = customStorage ?? PlayerPrefsSaveStorage.Instance;
            Load();
            Application.quitting += HandleQuitting;
            Application.focusChanged += HandleFocusChanged;
        }

        public void SetStorage(ISaveStorage customStorage, bool reload = true)
        {
            storage = customStorage ?? PlayerPrefsSaveStorage.Instance;
            IsDirty = false;
            if (reload)
            {
                Load();
            }
        }

        public void ReplaceDataInMemory(SaveData newData, bool saveToActiveStorage = true)
        {
            WasInterruptedRunRecovered = false;
            Data = newData ?? new SaveData();
            IsDirty = false;
            if (saveToActiveStorage)
            {
                Save();
            }
            else
            {
                OnDataChanged?.Invoke();
            }
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
            WasInterruptedRunRecovered = false;

            if (storage.HasKey(SAVE_KEY))
            {
                try
                {
                    string json = storage.GetString(SAVE_KEY);
                    Data = JsonUtility.FromJson<SaveData>(json);
                    
                    if (Data == null)
                    {
                        Debug.LogWarning("[SaveManager] Loaded JSON was parsed as null. Creating new SaveData.");
                        Data = new SaveData();
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

            if (Data.runInProgress)
            {
                Data.totalRuns++;
                Data.runInProgress = false;
                WasInterruptedRunRecovered = true;
                DevLog.Log("[SaveManager] Recovered interrupted run checkpoint.");
                Save();
                return;
            }
            
            DevLog.Log("[SaveManager] Data Loaded.");
            OnDataChanged?.Invoke();
        }

        public void Save()
        {
            if (Data == null) return;
            
            try
            {
                string json = JsonUtility.ToJson(Data, false);
                storage.SetString(SAVE_KEY, json);
                storage.Save();
                if (storage is PlayerPrefsSaveStorage)
                {
                    WebBrowserBridge.FlushStorage();
                }
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
            WasInterruptedRunRecovered = false;
            Data = new SaveData();
            Save();
            DevLog.Log("[SaveManager] Save Data Reset.");
        }

        // --- Онбординг / FTUE ---

        /// <summary>
        /// After the first run, tops the balance up to <paramref name="cheapestUpgradeCost"/> once so the first meta
        /// purchase is always possible. Returns the granted amount (0 if already granted or already affordable).
        /// </summary>
        public int GrantFtueStarterCoins(int cheapestUpgradeCost, bool saveImmediately = true)
        {
            if (Data == null || Data.ftueStarterCoinsGranted) return 0;
            Data.ftueStarterCoinsGranted = true;
            int granted = Mathf.Max(0, cheapestUpgradeCost - Data.coins);
            Data.coins += granted;
            CommitChange(saveImmediately);
            return granted;
        }

        public void MarkFtueMenuFlowCompleted(bool saveImmediately = true)
        {
            if (Data == null || Data.ftueMenuFlowCompleted) return;
            Data.ftueMenuFlowCompleted = true;
            CommitChange(saveImmediately);
        }

        public void MarkFtueShardHintShown(bool saveImmediately = false)
        {
            if (Data == null || Data.ftueShardHintShown) return;
            Data.ftueShardHintShown = true;
            CommitChange(saveImmediately);
        }

        public void MarkFtueConstellationsHintDismissed(bool saveImmediately = true)
        {
            if (Data == null || Data.ftueConstellationsHintDismissed) return;
            Data.ftueConstellationsHintDismissed = true;
            CommitChange(saveImmediately);
        }

        public bool HasAnyMetaUpgrade()
        {
            if (Data?.metaUpgradeLevels == null) return false;
            foreach (var kvp in Data.metaUpgradeLevels)
            {
                if (kvp.Value > 0) return true;
            }
            return false;
        }

        // --- Boss Shards & Constellation ---

        public int BossShards => Data != null ? Mathf.Max(0, Data.bossShards) : 0;

        public void AddBossShards(int amount, bool saveImmediately = true)
        {
            if (amount <= 0 || Data == null) return;
            Data.bossShards += amount;
            Data.totalBossShardsEarned += amount;
            CommitChange(saveImmediately);
        }

        public bool SpendBossShards(int amount, bool saveImmediately = true)
        {
            if (Data == null || amount < 0 || Data.bossShards < amount) return false;
            if (amount == 0) return true;
            Data.bossShards -= amount;
            CommitChange(saveImmediately);
            return true;
        }

        public bool IsTalentUnlocked(string talentId)
        {
            if (string.IsNullOrEmpty(talentId) || Data?.unlockedTalentIds == null) return false;
            return Data.unlockedTalentIds.Contains(talentId);
        }

        public void UnlockTalent(string talentId, bool saveImmediately = true)
        {
            if (string.IsNullOrEmpty(talentId) || Data == null) return;
            if (Data.unlockedTalentIds == null) Data.unlockedTalentIds = new List<string>();
            if (!Data.unlockedTalentIds.Contains(talentId))
            {
                Data.unlockedTalentIds.Add(talentId);
                CommitChange(saveImmediately);
            }
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
            if (result.bossShardsEarned > 0)
            {
                Data.bossShards += result.bossShardsEarned;
                Data.totalBossShardsEarned += result.bossShardsEarned;
            }
            Data.totalKills += result.enemiesKilled;
            Data.totalPlayTime += result.playTime;
            
            if (result.waveReached > Data.bestWave)
            {
                Data.bestWave = result.waveReached;
            }

            Data.runInProgress = false;
            Save();
        }

        public bool ApplyRunProgressDelta(
            int wave,
            int deltaCoins,
            int deltaShards,
            int deltaKills,
            float deltaPlayTime,
            bool? setRunInProgress = null,
            bool saveImmediately = true)
        {
            if (Data == null) return false;

            bool changed = false;

            if (deltaCoins > 0)
            {
                Data.coins += deltaCoins;
                changed = true;
            }

            if (deltaShards > 0)
            {
                Data.bossShards += deltaShards;
                Data.totalBossShardsEarned += deltaShards;
                changed = true;
            }

            if (deltaKills > 0)
            {
                Data.totalKills += deltaKills;
                changed = true;
            }

            if (deltaPlayTime > 0f)
            {
                Data.totalPlayTime += deltaPlayTime;
                changed = true;
            }

            if (wave > Data.bestWave)
            {
                Data.bestWave = wave;
                changed = true;
            }

            if (setRunInProgress.HasValue && Data.runInProgress != setRunInProgress.Value)
            {
                Data.runInProgress = setRunInProgress.Value;
                changed = true;
            }

            if (changed)
            {
                CommitChange(saveImmediately);
            }
            else if (saveImmediately)
            {
                SaveIfDirty();
            }

            return changed;
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
            if (string.IsNullOrEmpty(id) || Data == null) return;
            if (!Data.unlockedAbilityIds.Contains(id))
            {
                Data.unlockedAbilityIds.Add(id);
                CommitChange(saveImmediately);
            }
        }

        public bool IsAbilityUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id) || Data?.unlockedAbilityIds == null) return false;
            return Data.unlockedAbilityIds.Contains(id);
        }

        public void UnlockHero(string id, bool saveImmediately = true)
        {
            if (string.IsNullOrEmpty(id) || Data == null) return;
            if (!Data.unlockedHeroIds.Contains(id))
            {
                Data.unlockedHeroIds.Add(id);
                CommitChange(saveImmediately);
            }
        }

        public bool IsHeroUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id) || Data?.unlockedHeroIds == null) return false;
            return Data.unlockedHeroIds.Contains(id);
        }

        public string GetSelectedHeroId()
        {
            if (Data == null || string.IsNullOrEmpty(Data.selectedHeroId))
            {
                return "knight";
            }
            return Data.selectedHeroId;
        }

        public void SetSelectedHeroId(string heroId, bool saveImmediately = true)
        {
            if (Data == null || string.IsNullOrEmpty(heroId)) return;
            if (Data.selectedHeroId == heroId) return;

            Data.selectedHeroId = heroId;
            CommitChange(saveImmediately);
        }

        public string GetLanguageCode()
        {
            return Data != null ? (Data.languageCode ?? string.Empty) : string.Empty;
        }

        public void SetLanguageCode(string code, bool saveImmediately = true)
        {
            if (Data == null) return;
            string normalized = code ?? string.Empty;
            if (Data.languageCode == normalized) return;
            Data.languageCode = normalized;
            CommitChange(saveImmediately);
        }

        public int GetBonusActiveSlots() => Data != null ? Mathf.Max(0, Data.bonusActiveSlots) : 0;
        public int GetBonusPassiveSlots() => Data != null ? Mathf.Max(0, Data.bonusPassiveSlots) : 0;

        public void AddBonusActiveSlots(int amount, bool saveImmediately = true)
        {
            if (amount <= 0 || Data == null) return;
            Data.bonusActiveSlots += amount;
            CommitChange(saveImmediately);
        }

        public void AddBonusPassiveSlots(int amount, bool saveImmediately = true)
        {
            if (amount <= 0 || Data == null) return;
            Data.bonusPassiveSlots += amount;
            CommitChange(saveImmediately);
        }
    }
}
