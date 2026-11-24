using System.Collections.Generic;

namespace ProjectB.Meta
{
    [System.Serializable]
    public class SaveData
    {
        // --- Валюта ---
        public int coins;

        // --- Мета-апгрейды (индекс → уровень прокачки) ---
        // Ключ: string ID апгрейда (из MetaUpgradeData.id), Значение: текущий уровень
        public SerializableDictionary<string, int> metaUpgradeLevels = new SerializableDictionary<string, int>();

        // --- Ачивки ---
        // Ключ: string ID ачивки, Значение: текущий прогресс (int)
        public SerializableDictionary<string, int> achievementProgress = new SerializableDictionary<string, int>();
        
        // Список ID выполненных ачивок
        public List<string> completedAchievements = new List<string>();

        // --- Разблокировки ---
        // ID разблокированных способностей (через ачивки)
        public List<string> unlockedAbilityIds = new List<string>();
        // ID разблокированных героев
        public List<string> unlockedHeroIds = new List<string>();
        // ID выбранного героя для старта забега
        public string selectedHeroId = "knight";
        // Дополнительные слоты способностей, открытые через ачивки
        public int bonusActiveSlots;
        public int bonusPassiveSlots;

        // --- Кумулятивная статистика (для ачивок) ---
        public int totalKills;
        public int totalRuns;
        public int bestWave;
        public float totalPlayTime; // секунды
        // Sprint 15.1: чекпоинт активного забега, чтобы при холодном старте после выгрузки ОС не задвоить totalRuns
        public bool runInProgress;

        // --- Ежедневный бонус ---
        public int dailyBonusDay = 1;      // текущий день цикла (1-7)
        public int dailyBonusCycle;        // номер цикла (для увеличения наград)
        public string lastDailyClaimDate;  // ISO 8601, "2026-08-15"

        // --- Настройки ---
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public string languageCode = "";

        // --- Реклама и IAP ---
        public int rewardedAdsWatchedToday;
        public string lastAdWatchDate; // ISO 8601 yyyy-MM-dd (UTC)
        public int runsSinceLastInterstitial;
        public bool adsRemoved;

        // --- Дерево Созвездий (Sprint 11, saveVersion 2) ---
        // Валюта Boss Shards: мини-боссы (+1), боссы (+5), Алтари (+2)
        public int bossShards;
        // Суммарно заработано осколков за всё время (статистика)
        public int totalBossShardsEarned;
        // ID открытых узлов Дерева Созвездий
        public List<string> unlockedTalentIds = new List<string>();

        // --- Онбординг / FTUE (Sprint 12.2) ---
        public bool ftueUpgradesHintDismissed;

        // --- Версионирование ---
        public const int CURRENT_SAVE_VERSION = 2;
        public int saveVersion = CURRENT_SAVE_VERSION;
    }
}
