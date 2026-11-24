using System;
using TMPro;
using UnityEngine;
using ProjectB.Core;

namespace ProjectB.UI
{
    /// <summary>
    /// Binds a static <see cref="TMP_Text"/> to a localization key in <see cref="LocalizationService"/>
    /// and refreshes automatically whenever <see cref="LocalizationService.LanguageChanged"/> fires.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private bool uppercase;

        private TMP_Text cachedText;
        private Action cachedLanguageChanged;
        private LocalizationService subscribedService;

        public string Key => key;
        public bool Uppercase => uppercase;

        private void Awake()
        {
            cachedText = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetKey(string localizationKey, bool applyImmediately = true)
        {
            key = localizationKey;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            if (applyImmediately) Refresh();
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (cachedText == null) cachedText = GetComponent<TMP_Text>();
            if (cachedText == null) return;

            string localized = LocalizationService.Current.Get(key);
            if (uppercase && !string.IsNullOrEmpty(localized))
            {
                localized = localized.ToUpperInvariant();
            }

            if (!string.Equals(cachedText.text, localized, StringComparison.Ordinal))
            {
                cachedText.text = localized;
            }
        }

        private void Subscribe()
        {
            LocalizationService target = LocalizationService.Current;
            if (subscribedService == target) return;
            Unsubscribe();
            cachedLanguageChanged ??= Refresh;
            target.LanguageChanged += cachedLanguageChanged;
            subscribedService = target;
        }

        private void Unsubscribe()
        {
            if (subscribedService == null) return;
            subscribedService.LanguageChanged -= cachedLanguageChanged;
            subscribedService = null;
        }

#if UNITY_EDITOR
        public void EditorSetup(string localizationKey, bool toUpper = false)
        {
            key = localizationKey;
            uppercase = toUpper;
        }
#endif
    }
}
