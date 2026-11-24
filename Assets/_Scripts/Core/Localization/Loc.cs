using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.Data.Enemies;
using ProjectB.LevelUp;

namespace ProjectB.Core
{
    /// <summary>
    /// Concise localization lookup helpers for runtime UI and ScriptableObject display fields.
    /// </summary>
    public static class Loc
    {
        public static string Get(string key) => LocalizationService.Current.Get(key);
        public static string Get(string key, string fallback) => LocalizationService.Current.Get(key, fallback);
        public static string Resolve(string key, string fallback)
            => string.IsNullOrEmpty(key) ? (fallback ?? string.Empty) : LocalizationService.Current.Get(key, fallback);

        public static string Format(string key, object arg0) => LocalizationService.Current.Format(key, arg0);
        public static string Format(string key, object arg0, object arg1) => LocalizationService.Current.Format(key, arg0, arg1);
        public static string Format(string key, params object[] args) => LocalizationService.Current.Format(key, args);
        public static string FormatInt(string key, int value) => LocalizationService.Current.FormatInt(key, value);

        public static string Name(AbilityData data) => data != null ? Resolve(data.nameKey, data.abilityName) : string.Empty;
        public static string Desc(AbilityData data) => data != null ? Resolve(data.descriptionKey, data.description) : string.Empty;
        public static string Name(CardData data) => data != null ? Resolve(data.nameKey, data.cardName) : string.Empty;
        public static string Desc(CardData data) => data != null ? Resolve(data.descriptionKey, data.description) : string.Empty;
        public static string Name(TalentNodeData data) => data != null ? Resolve(data.nameKey, data.displayName) : string.Empty;
        public static string Desc(TalentNodeData data) => data != null ? Resolve(data.descriptionKey, data.description) : string.Empty;
        public static string Title(AchievementData data) => data != null ? Resolve(data.nameKey, data.title) : string.Empty;
        public static string Desc(AchievementData data) => data != null ? Resolve(data.descriptionKey, data.description) : string.Empty;
        public static string Name(HeroData data) => data != null ? Resolve(data.nameKey, data.heroName) : string.Empty;
        public static string Role(HeroData data) => data != null ? Resolve(data.roleKey, data.role) : string.Empty;
        public static string PassiveName(HeroData data) => data != null ? Resolve(data.passiveNameKey, data.passiveName) : string.Empty;
        public static string PassiveDesc(HeroData data) => data != null ? Resolve(data.passiveDescriptionKey, data.passiveDescription) : string.Empty;
        public static string UltimateName(HeroData data) => data != null ? Resolve(data.ultimateNameKey, data.ultimateName) : string.Empty;
        public static string UltimateDesc(HeroData data) => data != null ? Resolve(data.ultimateDescriptionKey, data.ultimateDescription) : string.Empty;
        public static string Name(SynergyData data) => data != null ? Resolve(data.nameKey, data.synergyName) : string.Empty;
        public static string Desc(SynergyData data) => data != null ? Resolve(data.descriptionKey, data.description) : string.Empty;
        public static string Name(MetaUpgradeData data) => data != null ? Resolve(data.nameKey, data.displayName) : string.Empty;
        public static string Desc(MetaUpgradeData data) => data != null ? Resolve(data.descriptionKey, data.description) : string.Empty;
        public static string Name(BossData data) => data != null ? Resolve(data.nameKey, data.bossDisplayName) : string.Empty;
    }
}
