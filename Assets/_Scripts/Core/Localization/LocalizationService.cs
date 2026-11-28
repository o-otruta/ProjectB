using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Meta;

namespace ProjectB.Core
{
    /// <summary>
    /// Localization service (Sprint 12.2 + 12.3): loads <c>Resources/Localization/{en,uk,ru}.json</c>,
    /// resolves initial language from saved preference or <c>Application.systemLanguage</c>,
    /// caches strings and integer-formatted labels for Zero-GC lookups, and notifies listeners on change.
    /// </summary>
    public sealed class LocalizationService
    {
        public const string RESOURCE_FOLDER = "Localization";

        public const string KEY_FTUE_MOVE_HINT = "ftue.move_hint";
        public const string KEY_FTUE_LEVELUP_HINT = "ftue.levelup_hint";
        public const string KEY_FTUE_ULTIMATE_HINT = "ftue.ultimate_hint";
        public const string KEY_FTUE_UPGRADES_HINT = "ftue.upgrades_hint";
        public const string KEY_FTUE_BARREL_HINT = "ftue.barrel_hint";
        public const string KEY_FTUE_MOVE_HINT_KEYBOARD = "ftue.move_hint_keyboard";
        public const string KEY_FTUE_ULTIMATE_HINT_KEYBOARD = "ftue.ultimate_hint_keyboard";
        public const string KEY_FTUE_SHARD_HINT = "ftue.shard_hint";
        public const string KEY_FTUE_UPGRADE_BUY_HINT = "ftue.upgrade_buy_hint";
        public const string KEY_FTUE_CONSTELLATIONS_HINT = "ftue.constellations_hint";
        public const string KEY_FTUE_PLAY_TAB_HINT = "ftue.play_tab_hint";

        public static readonly SystemLanguage[] SupportedLanguages =
        {
            SystemLanguage.English,
            SystemLanguage.Ukrainian,
            SystemLanguage.Russian
        };

        private static LocalizationService current;

        public static LocalizationService Current
        {
            get
            {
                if (current == null)
                {
                    if (Application.isPlaying)
                    {
                        Debug.LogWarning("[LocalizationService] Current was accessed before DI constructed LocalizationService; creating fallback instance without SaveManager.");
                    }
                    current = new LocalizationService();
                }
                return current;
            }
        }

        public static void SetCurrent(LocalizationService service)
        {
            current = service;
        }

        private readonly SaveManager saveManager;
        private readonly Dictionary<string, string> englishEntries = new Dictionary<string, string>(512, StringComparer.Ordinal);
        private readonly Dictionary<string, string> entries = new Dictionary<string, string>(512, StringComparer.Ordinal);
        private readonly Dictionary<string, string> manualFallbacks = new Dictionary<string, string>(16, StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> intCache = new Dictionary<string, string[]>(32, StringComparer.Ordinal);

        public SystemLanguage CurrentLanguage { get; private set; } = SystemLanguage.English;
        public IReadOnlyDictionary<string, string> ActiveEntries => entries;

        public event Action LanguageChanged;

        [Inject]
        public LocalizationService(SaveManager saveManager = null)
        {
            this.saveManager = saveManager;
            current = this;

            SeedDefaultFallbacks(englishEntries);
            LoadTableInto("en", englishEntries);

            string savedCode = saveManager != null ? saveManager.GetLanguageCode() : string.Empty;
            SystemLanguage systemLang = Application.isPlaying ? Application.systemLanguage : SystemLanguage.English;
            CurrentLanguage = ResolveInitialLanguage(savedCode, systemLang);

            LoadActiveTable();
        }

        public static SystemLanguage ResolveInitialLanguage(string savedCode, SystemLanguage systemLanguage)
        {
            if (TryParseLanguageCode(savedCode, out SystemLanguage savedLang))
            {
                return savedLang;
            }

            return systemLanguage switch
            {
                SystemLanguage.Ukrainian => SystemLanguage.Ukrainian,
                SystemLanguage.Russian => SystemLanguage.Russian,
                _ => SystemLanguage.English
            };
        }

        public static string GetLanguageCode(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Ukrainian => "uk",
                SystemLanguage.Russian => "ru",
                _ => "en"
            };
        }

        public static string GetNativeLanguageName(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Ukrainian => "Українська",
                SystemLanguage.Russian => "Русский",
                _ => "English"
            };
        }

        public static bool TryParseLanguageCode(string code, out SystemLanguage language)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                language = SystemLanguage.English;
                return false;
            }

            switch (code.Trim().ToLowerInvariant())
            {
                case "en":
                case "english":
                    language = SystemLanguage.English;
                    return true;
                case "uk":
                case "ua":
                case "ukrainian":
                    language = SystemLanguage.Ukrainian;
                    return true;
                case "ru":
                case "russian":
                    language = SystemLanguage.Russian;
                    return true;
                default:
                    language = SystemLanguage.English;
                    return false;
            }
        }

        private static void SeedDefaultFallbacks(Dictionary<string, string> target)
        {
            target[KEY_FTUE_MOVE_HINT] = "Drag to move — attack is automatic";
            target[KEY_FTUE_LEVELUP_HINT] = "Choose an upgrade";
            target[KEY_FTUE_ULTIMATE_HINT] = "Ultimate ready! Tap to unleash";
            target[KEY_FTUE_UPGRADES_HINT] = "Upgrade your hero!";
            target[KEY_FTUE_BARREL_HINT] = "Kick the barrel into enemies!";
            target[KEY_FTUE_MOVE_HINT_KEYBOARD] = "WASD or arrows to move — attack is automatic";
            target[KEY_FTUE_ULTIMATE_HINT_KEYBOARD] = "Ultimate ready! Press {0}";
            target[KEY_FTUE_SHARD_HINT] = "Boss Shard! Spend it in the Constellations menu";
            target[KEY_FTUE_UPGRADE_BUY_HINT] = "Buy your first upgrade";
            target[KEY_FTUE_CONSTELLATIONS_HINT] = "Spend Boss Shards on Constellation stars";
            target[KEY_FTUE_PLAY_TAB_HINT] = "Ready for the next run?";
        }

        private static void LoadTableInto(string code, Dictionary<string, string> target)
        {
            TextAsset asset = Resources.Load<TextAsset>($"{RESOURCE_FOLDER}/{code}");
            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                LocalizationJsonParser.ParseInto(asset.text, target);
            }
        }

        private void LoadActiveTable()
        {
            entries.Clear();
            intCache.Clear();

            string code = GetLanguageCode(CurrentLanguage);
            if (CurrentLanguage == SystemLanguage.English)
            {
                foreach (var kvp in englishEntries)
                {
                    entries[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                LoadTableInto(code, entries);
            }

            foreach (var kvp in manualFallbacks)
            {
                if (!entries.ContainsKey(kvp.Key))
                {
                    entries[kvp.Key] = kvp.Value;
                }
            }
        }

        public void Reload()
        {
            englishEntries.Clear();
            SeedDefaultFallbacks(englishEntries);
            LoadTableInto("en", englishEntries);
            LoadActiveTable();
            LanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (entries.TryGetValue(key, out string value)) return value;
            if (englishEntries.TryGetValue(key, out string enValue)) return enValue;
            return key;
        }

        public string Get(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (entries.TryGetValue(key, out string value)) return value;
                if (englishEntries.TryGetValue(key, out string enValue)) return enValue;
            }
            return fallback ?? key ?? string.Empty;
        }

        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return entries.ContainsKey(key) || englishEntries.ContainsKey(key);
        }

        public void RegisterFallback(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            string safeVal = value ?? string.Empty;
            manualFallbacks[key] = safeVal;
            entries[key] = safeVal;
            intCache.Remove(key);
        }

        public string FormatInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return value.ToString();
            if (value >= 0 && value <= 512)
            {
                if (!intCache.TryGetValue(key, out string[] arr))
                {
                    arr = new string[513];
                    intCache[key] = arr;
                }
                if (arr[value] == null)
                {
                    arr[value] = Format(key, value);
                }
                return arr[value];
            }
            return Format(key, value);
        }

        public string Format(string key, object arg0)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, arg0);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public string Format(string key, object arg0, object arg1)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, arg0, arg1);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public string Format(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public void SetLanguage(SystemLanguage language, bool persist = true)
        {
            SystemLanguage normalized = language == SystemLanguage.Ukrainian || language == SystemLanguage.Russian
                ? language
                : SystemLanguage.English;

            if (CurrentLanguage == normalized) return;
            CurrentLanguage = normalized;
            LoadActiveTable();

            if (persist && saveManager != null)
            {
                saveManager.SetLanguageCode(GetLanguageCode(normalized), saveImmediately: true);
            }

            LanguageChanged?.Invoke();
        }
    }
}
