using System;
using VContainer;
using VContainer.Unity;
using ProjectB.Data;

namespace ProjectB.Meta
{
    /// <summary>
    /// Feeds meta achievements (runs played, Constellation nodes and Keystones, meta-upgrade levels,
    /// daily bonus claims, unlocked abilities) from <see cref="SaveData"/>. Runs on startup and whenever
    /// a meta system reports a change; all conditions are "raise to value", so repeated syncs are idempotent.
    /// </summary>
    public sealed class MetaAchievementSync : IStartable, IDisposable
    {
        private readonly AchievementManager achievementManager;
        private readonly SaveManager saveManager;
        private readonly TalentTreeManager talentTreeManager;
        private readonly MetaUpgradeManager metaUpgradeManager;
        private readonly DailyBonusManager dailyBonusManager;

        private bool started;
        private bool syncing;

        [Inject]
        public MetaAchievementSync(
            AchievementManager achievementManager,
            SaveManager saveManager,
            TalentTreeManager talentTreeManager = null,
            MetaUpgradeManager metaUpgradeManager = null,
            DailyBonusManager dailyBonusManager = null)
        {
            this.achievementManager = achievementManager;
            this.saveManager = saveManager;
            this.talentTreeManager = talentTreeManager;
            this.metaUpgradeManager = metaUpgradeManager;
            this.dailyBonusManager = dailyBonusManager;
        }

        public void Start()
        {
            if (started) return;
            started = true;

            if (saveManager != null) saveManager.OnDataChanged += Sync;
            if (talentTreeManager != null) talentTreeManager.OnTalentUnlocked += HandleTalentUnlocked;
            if (metaUpgradeManager != null) metaUpgradeManager.OnUpgradePurchased += HandleUpgradePurchased;
            if (dailyBonusManager != null) dailyBonusManager.OnBonusStateChanged += Sync;

            Sync();
        }

        public void Dispose()
        {
            if (!started) return;
            started = false;

            if (saveManager != null) saveManager.OnDataChanged -= Sync;
            if (talentTreeManager != null) talentTreeManager.OnTalentUnlocked -= HandleTalentUnlocked;
            if (metaUpgradeManager != null) metaUpgradeManager.OnUpgradePurchased -= HandleUpgradePurchased;
            if (dailyBonusManager != null) dailyBonusManager.OnBonusStateChanged -= Sync;
        }

        private void HandleTalentUnlocked(TalentNodeData _) => Sync();
        private void HandleUpgradePurchased(string _) => Sync();

        /// <summary>Re-evaluates every meta condition from the current save.</summary>
        public void Sync()
        {
            if (syncing || achievementManager == null || saveManager?.Data == null) return;
            syncing = true;
            try
            {
                SaveData data = saveManager.Data;

                achievementManager.SetProgressByType(AchievementConditionType.TotalRuns, data.totalRuns);
                achievementManager.SetProgressByType(AchievementConditionType.TalentsUnlocked, CountTalents(data, keystonesOnly: false));
                achievementManager.SetProgressByType(AchievementConditionType.KeystonesUnlocked, CountTalents(data, keystonesOnly: true));
                achievementManager.SetProgressByType(AchievementConditionType.MetaUpgradeLevels, SumMetaLevels(data));
                achievementManager.SetProgressByType(AchievementConditionType.DailyBonusClaims, CountDailyClaims(data));
                achievementManager.SetProgressByType(AchievementConditionType.AbilitiesUnlocked, data.unlockedAbilityIds != null ? data.unlockedAbilityIds.Count : 0);
            }
            finally
            {
                syncing = false;
            }
        }

        private int CountTalents(SaveData data, bool keystonesOnly)
        {
            if (data.unlockedTalentIds == null) return 0;
            int count = 0;
            for (int i = 0; i < data.unlockedTalentIds.Count; i++)
            {
                string id = data.unlockedTalentIds[i];
                if (string.IsNullOrEmpty(id)) continue;

                TalentNodeData node = talentTreeManager != null ? talentTreeManager.GetNode(id) : null;
                if (node != null && node.nodeType == TalentNodeType.Origin) continue;
                if (keystonesOnly && (node == null || node.nodeType != TalentNodeType.Keystone)) continue;
                count++;
            }
            return count;
        }

        private static int SumMetaLevels(SaveData data)
        {
            if (data.metaUpgradeLevels == null) return 0;
            int sum = 0;
            foreach (var pair in data.metaUpgradeLevels)
            {
                if (pair.Value > 0) sum += pair.Value;
            }
            return sum;
        }

        private int CountDailyClaims(SaveData data)
        {
            int maxDays = dailyBonusManager != null ? Math.Max(1, dailyBonusManager.GetMaxDays()) : 7;
            int dayInCycle = Math.Max(1, data.dailyBonusDay);
            return Math.Max(0, data.dailyBonusCycle) * maxDays + (dayInCycle - 1);
        }
    }
}
