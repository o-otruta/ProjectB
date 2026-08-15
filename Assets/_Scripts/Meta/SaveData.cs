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

        // --- Кумулятивная статистика (для ачивок) ---
        public int totalKills;
        public int totalRuns;
        public int bestWave;
        public float totalPlayTime; // секунды

        // --- Ежедневный бонус ---
        public int dailyBonusDay = 1;      // текущий день цикла (1-7)
        public int dailyBonusCycle;        // номер цикла (для увеличения наград)
        public string lastDailyClaimDate;  // ISO 8601, "2026-08-15"

        // --- Настройки ---
        public float musicVolume = 1f;
        public float sfxVolume = 1f;

        // --- Версионирование ---
        public int saveVersion = 1;
    }
}
