#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using ProjectB.Meta;

namespace ProjectB.Core.BalanceBot
{
    /// <summary>
    /// Isolates player SaveData (PlayerPrefs) during automated bot batches.
    /// Switches SaveManager to InMemorySaveStorage and keeps a persistent backup in PlayerPrefs
    /// so even an editor PlayMode stop or crash restores the exact pre-batch SaveData.
    /// </summary>
    public sealed class BalanceBotSaveIsolationScope : IDisposable
    {
        public const string BACKUP_ACTIVE_KEY = "ProjectB_SaveData_BotBackup_Active";
        public const string BACKUP_HAD_KEY = "ProjectB_SaveData_BotBackup_HasKey";
        public const string BACKUP_JSON_KEY = "ProjectB_SaveData_BotBackup_Json";

        private readonly SaveManager saveManager;
        private readonly ISaveStorage previousStorage;
        private readonly bool hadOriginalSave;
        private readonly string originalSaveJson;
        private bool restored;

        public bool HadOriginalSave => hadOriginalSave;
        public string OriginalSaveJson => originalSaveJson;

        public BalanceBotSaveIsolationScope(SaveManager saveManager)
        {
            this.saveManager = saveManager;
            previousStorage = saveManager != null ? saveManager.Storage : PlayerPrefsSaveStorage.Instance;

            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;

            // Persist backup immediately in case PlayMode is aborted abruptly
            PlayerPrefs.SetInt(BACKUP_ACTIVE_KEY, 1);
            PlayerPrefs.SetInt(BACKUP_HAD_KEY, hadOriginalSave ? 1 : 0);
            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(BACKUP_JSON_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(BACKUP_JSON_KEY);
            }
            PlayerPrefs.Save();

            if (this.saveManager != null)
            {
                this.saveManager.SetStorage(new InMemorySaveStorage(), reload: false);
            }
        }

        public void Restore()
        {
            if (restored) return;
            restored = true;

            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }

            ClearBackupKeys();
            PlayerPrefs.Save();

            if (saveManager != null)
            {
                saveManager.SetStorage(previousStorage ?? PlayerPrefsSaveStorage.Instance, reload: true);
            }
        }

        public void Dispose()
        {
            Restore();
        }

        public static bool HasPendingBackup()
        {
            return PlayerPrefs.GetInt(BACKUP_ACTIVE_KEY, 0) == 1;
        }

        public static bool RestorePendingBackupIfAny()
        {
            if (!HasPendingBackup()) return false;

            bool hadKey = PlayerPrefs.GetInt(BACKUP_HAD_KEY, 0) == 1;
            if (hadKey && PlayerPrefs.HasKey(BACKUP_JSON_KEY))
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, PlayerPrefs.GetString(BACKUP_JSON_KEY));
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }

            ClearBackupKeys();
            PlayerPrefs.Save();
            return true;
        }

        private static void ClearBackupKeys()
        {
            PlayerPrefs.DeleteKey(BACKUP_ACTIVE_KEY);
            PlayerPrefs.DeleteKey(BACKUP_HAD_KEY);
            PlayerPrefs.DeleteKey(BACKUP_JSON_KEY);
        }
    }
}
#endif
