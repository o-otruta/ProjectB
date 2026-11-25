using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Data.Enemies;

namespace ProjectB.Meta
{
    /// <summary>
    /// Persistent achievement store: progress, completion and rewards in <see cref="SaveData"/>.
    /// Run-time signals come from <c>AchievementRunTracker</c> (gameplay scope), <c>EnemyDeathHandler</c>,
    /// <c>HeroAbilities</c>, <c>WaveManager</c>; meta signals from <see cref="MetaAchievementSync"/>.
    /// </summary>
    public class AchievementManager
    {
        private const char SET_SEPARATOR = '|';

        private readonly SaveManager _saveManager;
        private readonly List<AchievementData> _allAchievements;
        private readonly Dictionary<string, AchievementData> _achievementsById;
        private readonly Dictionary<AchievementConditionType, List<AchievementData>> _achievementsByCondition;
        private readonly HashSet<string> _completedIds;
        private readonly HashSet<string> _setEntries;
        private int _syncedCompletedCount = -1;
        private int _syncedSetEntryCount = -1;
        private SaveData _syncedData;

        public List<AchievementData> unlockedThisRun { get; private set; }

        public event Action<AchievementData> OnAchievementUnlocked;

        public AchievementManager(SaveManager saveManager)
        {
            _saveManager = saveManager;
            _allAchievements = new List<AchievementData>(Resources.LoadAll<AchievementData>("Achievements"));
            _allAchievements.Sort(CompareForDisplay);
            _achievementsById = new Dictionary<string, AchievementData>(_allAchievements.Count);
            _achievementsByCondition = new Dictionary<AchievementConditionType, List<AchievementData>>();
            _completedIds = new HashSet<string>(StringComparer.Ordinal);
            _setEntries = new HashSet<string>(StringComparer.Ordinal);
            unlockedThisRun = new List<AchievementData>();

            if (_saveManager != null)
            {
                _saveManager.OnDataChanged += InvalidateCaches;
            }
            EnsureCachesSynced();

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

                if (_saveManager?.Data?.achievementProgress != null &&
                    !_saveManager.Data.achievementProgress.ContainsKey(achievement.id))
                {
                    _saveManager.Data.achievementProgress.Add(achievement.id, 0);
                }
            }
        }

        /// <summary>Category → sortOrder → id (stable order for the Achievements screen and tests).</summary>
        public static int CompareForDisplay(AchievementData a, AchievementData b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int cmp = a.category.CompareTo(b.category);
            if (cmp != 0) return cmp;
            cmp = a.sortOrder.CompareTo(b.sortOrder);
            if (cmp != 0) return cmp;
            return string.CompareOrdinal(a.id, b.id);
        }

        public IReadOnlyList<AchievementData> GetAllAchievements() => _allAchievements;

        public bool HasConditionType(AchievementConditionType type) => _achievementsByCondition.ContainsKey(type);

        public void ClearRunUnlocks()
        {
            unlockedThisRun.Clear();
        }

        private void InvalidateCaches()
        {
            _syncedCompletedCount = -1;
            _syncedSetEntryCount = -1;
        }

        private void EnsureCachesSynced()
        {
            SaveData data = _saveManager?.Data;
            if (!ReferenceEquals(data, _syncedData))
            {
                _syncedData = data;
                _syncedCompletedCount = -1;
                _syncedSetEntryCount = -1;
            }

            var completed = data?.completedAchievements;
            int completedCount = completed != null ? completed.Count : 0;
            if (_syncedCompletedCount != completedCount)
            {
                _completedIds.Clear();
                if (completed != null)
                {
                    for (int i = 0; i < completed.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(completed[i])) _completedIds.Add(completed[i]);
                    }
                }
                _syncedCompletedCount = completedCount;
            }

            var sets = data?.achievementSetEntries;
            int setCount = sets != null ? sets.Count : 0;
            if (_syncedSetEntryCount != setCount)
            {
                _setEntries.Clear();
                if (sets != null)
                {
                    for (int i = 0; i < sets.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(sets[i])) _setEntries.Add(sets[i]);
                    }
                }
                _syncedSetEntryCount = setCount;
            }
        }

        public bool IsAchievementCompleted(string id)
        {
            EnsureCachesSynced();
            return _completedIds.Contains(id);
        }

        public void AddProgress(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            if (_saveManager?.Data == null) return;
            if (!_achievementsById.TryGetValue(id, out var achievement)) return;

            if (IsAchievementCompleted(id)) return;

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
                if (IsAchievementCompleted(achievement.id)) continue;

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
                if (string.IsNullOrEmpty(achievement.targetId) || achievement.targetId == targetId)
                {
                    RaiseProgressTo(achievement, newValue);
                }
            }
        }

        private void RaiseProgressTo(AchievementData achievement, int newValue)
        {
            if (achievement == null || _saveManager?.Data == null) return;
            if (IsAchievementCompleted(achievement.id)) return;

            _saveManager.Data.achievementProgress.TryGetValue(achievement.id, out int currentProgress);
            if (newValue > currentProgress)
            {
                AddProgress(achievement.id, newValue - currentProgress);
            }
        }

        /// <summary>Adds a distinct member to every set-achievement of this type; progress = number of members.</summary>
        private void AddSetMemberByType(AchievementConditionType conditionType, string memberId)
        {
            if (string.IsNullOrEmpty(memberId)) return;
            if (!_achievementsByCondition.TryGetValue(conditionType, out var list)) return;

            for (int i = 0; i < list.Count; i++)
            {
                AddSetMember(list[i], memberId);
            }
        }

        private void AddSetMember(AchievementData achievement, string memberId)
        {
            if (achievement == null || _saveManager?.Data == null) return;
            if (IsAchievementCompleted(achievement.id)) return;

            EnsureCachesSynced();
            string key = string.Concat(achievement.id, SET_SEPARATOR.ToString(), memberId);
            if (!_setEntries.Add(key)) return;

            _saveManager.Data.achievementSetEntries ??= new List<string>();
            _saveManager.Data.achievementSetEntries.Add(key);
            _syncedSetEntryCount = _saveManager.Data.achievementSetEntries.Count;

            AddProgress(achievement.id, 1);
        }

        /// <summary>True if <paramref name="memberId"/> was already counted for this set achievement (UI / tests).</summary>
        public bool HasSetMember(string achievementId, string memberId)
        {
            EnsureCachesSynced();
            return _setEntries.Contains(string.Concat(achievementId, SET_SEPARATOR.ToString(), memberId));
        }

        private void UnlockAchievement(AchievementData achievement)
        {
            if (IsAchievementCompleted(achievement.id)) return;

            _completedIds.Add(achievement.id);
            _saveManager.CompleteAchievement(achievement.id, saveImmediately: false);
            _syncedCompletedCount = _saveManager?.Data?.completedAchievements?.Count ?? _completedIds.Count;
            unlockedThisRun.Add(achievement);

            // Награды (без немедленного дискового I/O в бою)
            switch (achievement.rewardType)
            {
                case AchievementRewardType.GiveCoins:
                    _saveManager.AddCoins(achievement.rewardValue, saveImmediately: false);
                    break;
                case AchievementRewardType.GiveShards:
                    _saveManager.AddBossShards(achievement.rewardValue, saveImmediately: false);
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

        // ---------------------------------------------------------------- Run signals

        public void OnEnemyKilled()
        {
            AddProgressByType(AchievementConditionType.TotalKills, 1);
        }

        /// <summary>Unconstrained wave progress (legacy entry point; constrained achievements are skipped).</summary>
        public void OnWaveReached(int wave)
        {
            OnWaveReached(wave, default);
        }

        /// <summary>Wave progress with a run snapshot: constrained ReachWave achievements and HeroesReachedWave.</summary>
        public void OnWaveReached(int wave, in AchievementRunContext context)
        {
            if (_achievementsByCondition.TryGetValue(AchievementConditionType.ReachWave, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var achievement = list[i];
                    var constraint = achievement.runConstraint;
                    if (constraint != null && constraint.HasAny && !constraint.IsSatisfiedBy(context)) continue;
                    RaiseProgressTo(achievement, wave);
                }
            }

            if (context.IsValid && !string.IsNullOrEmpty(context.HeroId) &&
                _achievementsByCondition.TryGetValue(AchievementConditionType.HeroesReachedWave, out var heroList))
            {
                for (int i = 0; i < heroList.Count; i++)
                {
                    if (wave >= heroList[i].conditionParam)
                    {
                        AddSetMember(heroList[i], context.HeroId);
                    }
                }
            }
        }

        public void OnBossDefeated(string bossId)
        {
            AddProgressByType(AchievementConditionType.DefeatBoss, 1, bossId);
        }

        public void OnMiniBossDefeated()
        {
            AddProgressByType(AchievementConditionType.DefeatMiniBosses, 1);
        }

        public void OnEliteKilled()
        {
            AddProgressByType(AchievementConditionType.EliteKills, 1);
        }

        public void OnKillsInRun(int killsThisRun)
        {
            SetProgressByType(AchievementConditionType.KillsInRun, killsThisRun);
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

        public void OnShrineCleared()
        {
            AddProgressByType(AchievementConditionType.ClearShrines, 1);
        }

        public void OnComboReached(int combo)
        {
            SetProgressByType(AchievementConditionType.ReachCombo, combo);
        }

        public void OnBarrelExploded(int enemiesHit)
        {
            SetProgressByType(AchievementConditionType.BarrelMultiKill, enemiesHit);
        }

        public void OnSiegeShellBroken()
        {
            AddProgressByType(AchievementConditionType.SiegeShellsBroken, 1);
        }

        public void OnVacuumMagnetCollected(int xpMagnetized)
        {
            SetProgressByType(AchievementConditionType.VacuumMagnetXp, xpMagnetized);
        }

        public void OnSynergyActivated(string synergyId, int distinctSynergiesThisRun)
        {
            AddSetMemberByType(AchievementConditionType.UniqueSynergies, synergyId);
            SetProgressByType(AchievementConditionType.SynergiesInRun, distinctSynergiesThisRun);
        }

        public void OnUltimateUsed()
        {
            AddProgressByType(AchievementConditionType.UltimatesUsed, 1);
        }

        /// <summary>A big boss died. <paramref name="heroHpPercent"/> is 0–100.</summary>
        public void OnBigBossKilled(bool beforeEnrage, float heroHpPercent)
        {
            if (beforeEnrage)
            {
                AddProgressByType(AchievementConditionType.BossBeforeEnrage, 1);
            }

            if (!_achievementsByCondition.TryGetValue(AchievementConditionType.BossAtLowHp, out var list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (heroHpPercent > 0f && heroHpPercent <= list[i].conditionParam)
                {
                    AddProgress(list[i].id, 1);
                }
            }
        }

        public void OnRunDuration(int minutes)
        {
            SetProgressByType(AchievementConditionType.RunDurationMinutes, minutes);
        }

        public void OnWaveEventCompleted(WaveEventType eventType)
        {
            if (eventType == WaveEventType.None) return;
            string name = WaveEventName(eventType);
            AddProgressByType(AchievementConditionType.WaveEventsCompleted, 1, name);
            AddSetMemberByType(AchievementConditionType.UniqueWaveEvents, name);
        }

        public void OnMutatorSurvived(WaveMutatorType mutatorType)
        {
            AddSetMemberByType(AchievementConditionType.UniqueMutators, mutatorType.ToString());
        }

        public void OnSecondWindTriggered()
        {
            AddProgressByType(AchievementConditionType.SecondWindTriggers, 1);
        }

        private static string WaveEventName(WaveEventType type)
        {
            switch (type)
            {
                case WaveEventType.SwarmRush: return "SwarmRush";
                case WaveEventType.GoldGoblin: return "GoldGoblin";
                case WaveEventType.BloodMoon: return "BloodMoon";
                case WaveEventType.EliteAmbush: return "EliteAmbush";
                case WaveEventType.CorridorHorde: return "CorridorHorde";
                default: return type.ToString();
            }
        }
    }
}
