using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Core
{
    /// <summary>
    /// Minimal localization service (Sprint 12.2): holds default EN fallbacks for FTUE keys
    /// and caches string lookups without per-frame allocations. Extended with EN/UK/RU JSON tables in Sprint 12.3.
    /// </summary>
    public sealed class LocalizationService
    {
        public const string KEY_FTUE_MOVE_HINT = "ftue.move_hint";
        public const string KEY_FTUE_LEVELUP_HINT = "ftue.levelup_hint";
        public const string KEY_FTUE_ULTIMATE_HINT = "ftue.ultimate_hint";
        public const string KEY_FTUE_UPGRADES_HINT = "ftue.upgrades_hint";

        private readonly Dictionary<string, string> entries = new Dictionary<string, string>(64, StringComparer.Ordinal);

        public SystemLanguage CurrentLanguage { get; private set; } = SystemLanguage.English;

        public event Action LanguageChanged;

        public LocalizationService()
        {
            entries[KEY_FTUE_MOVE_HINT] = "Drag to move — attack is automatic";
            entries[KEY_FTUE_LEVELUP_HINT] = "Choose an upgrade";
            entries[KEY_FTUE_ULTIMATE_HINT] = "Ultimate ready! Tap to unleash";
            entries[KEY_FTUE_UPGRADES_HINT] = "Upgrade your hero!";
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return entries.TryGetValue(key, out string value) ? value : key;
        }

        public string Get(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key) && entries.TryGetValue(key, out string value))
            {
                return value;
            }
            return fallback ?? key ?? string.Empty;
        }

        public bool HasKey(string key)
        {
            return !string.IsNullOrEmpty(key) && entries.ContainsKey(key);
        }

        public void RegisterFallback(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            entries[key] = value ?? string.Empty;
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

        public void SetLanguage(SystemLanguage language)
        {
            if (CurrentLanguage == language) return;
            CurrentLanguage = language;
            LanguageChanged?.Invoke();
        }
    }
}
