using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;

namespace ProjectB.UI
{
    public class SettingsPopupUI : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI musicValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;
        [SerializeField] private Button closeButton;
        [Tooltip("Sprint 12.3: language switch row.")]
        [SerializeField] private GameObject languageRow;
        [SerializeField] private TMP_Text languageValueText;
        [SerializeField] private ThemedButton englishButton;
        [SerializeField] private ThemedButton ukrainianButton;
        [SerializeField] private ThemedButton russianButton;
        [SerializeField] private Button backgroundButton;

        private AudioManager audioManager;
        private LocalizationService localization;
        private Action cachedLanguageChanged;

        public bool IsOpen => popupPanel != null && popupPanel.activeSelf;

        private LocalizationService ActiveLocalization => localization ?? LocalizationService.Current;

        [Inject]
        public void Construct(AudioManager audioManager, LocalizationService localization = null)
        {
            UnsubscribeLanguage();
            this.audioManager = audioManager;
            this.localization = localization;
            if (isActiveAndEnabled) SubscribeLanguage();
        }

        private void Awake()
        {
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);
            if (englishButton != null && englishButton.Button != null) englishButton.Button.onClick.AddListener(SelectEnglish);
            if (ukrainianButton != null && ukrainianButton.Button != null) ukrainianButton.Button.onClick.AddListener(SelectUkrainian);
            if (russianButton != null && russianButton.Button != null) russianButton.Button.onClick.AddListener(SelectRussian);

            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void OnEnable()
        {
            SubscribeLanguage();
            RefreshLanguageButtons();
        }

        private void OnDisable()
        {
            UnsubscribeLanguage();
        }

        private void OnDestroy()
        {
            UnsubscribeLanguage();
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.RemoveListener(Hide);
            if (englishButton != null && englishButton.Button != null) englishButton.Button.onClick.RemoveListener(SelectEnglish);
            if (ukrainianButton != null && ukrainianButton.Button != null) ukrainianButton.Button.onClick.RemoveListener(SelectUkrainian);
            if (russianButton != null && russianButton.Button != null) russianButton.Button.onClick.RemoveListener(SelectRussian);
        }

        public void Show()
        {
            if (popupPanel != null) popupPanel.SetActive(true);

            float musicVol = audioManager != null ? audioManager.MusicVolume : 1f;
            float sfxVol = audioManager != null ? audioManager.SfxVolume : 1f;

            if (musicSlider != null) musicSlider.SetValueWithoutNotify(musicVol);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfxVol);
            UpdateLabels(musicVol, sfxVol);
            RefreshLanguageButtons();
        }

        public void Hide()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();
            if (popupPanel != null) popupPanel.SetActive(false);
        }

        public void SelectEnglish() => SelectLanguage(SystemLanguage.English);
        public void SelectUkrainian() => SelectLanguage(SystemLanguage.Ukrainian);
        public void SelectRussian() => SelectLanguage(SystemLanguage.Russian);

        public void SelectLanguage(SystemLanguage language)
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            ActiveLocalization.SetLanguage(language);
            RefreshLanguageButtons();
        }

        public void RefreshLanguageButtons()
        {
            SystemLanguage current = ActiveLocalization.CurrentLanguage;
            if (languageValueText != null)
            {
                languageValueText.text = LocalizationService.GetNativeLanguageName(current);
            }

            if (englishButton != null) englishButton.SetStyle(current == SystemLanguage.English ? ButtonStyle.Primary : ButtonStyle.Secondary);
            if (ukrainianButton != null) ukrainianButton.SetStyle(current == SystemLanguage.Ukrainian ? ButtonStyle.Primary : ButtonStyle.Secondary);
            if (russianButton != null) russianButton.SetStyle(current == SystemLanguage.Russian ? ButtonStyle.Primary : ButtonStyle.Secondary);
        }

        private void SubscribeLanguage()
        {
            cachedLanguageChanged ??= RefreshLanguageButtons;
            ActiveLocalization.LanguageChanged -= cachedLanguageChanged;
            ActiveLocalization.LanguageChanged += cachedLanguageChanged;
        }

        private void UnsubscribeLanguage()
        {
            if (cachedLanguageChanged != null)
            {
                ActiveLocalization.LanguageChanged -= cachedLanguageChanged;
            }
        }

        private void OnMusicSliderChanged(float value)
        {
            audioManager?.SetMusicVolume(value, saveImmediately: false);
            UpdateLabels(value, sfxSlider != null ? sfxSlider.value : 1f);
        }

        private void OnSfxSliderChanged(float value)
        {
            audioManager?.SetSfxVolume(value, saveImmediately: false);
            UpdateLabels(musicSlider != null ? musicSlider.value : 1f, value);
        }

        private void UpdateLabels(float music, float sfx)
        {
            if (musicValueText != null) musicValueText.text = $"{Mathf.RoundToInt(music * 100f)}%";
            if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(sfx * 100f)}%";
        }
    }
}
