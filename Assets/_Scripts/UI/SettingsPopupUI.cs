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
        [Tooltip("Sprint 13: reserved row for the language switch (Sprint 12.3 fills it in).")]
        [SerializeField] private GameObject languageRow;
        [SerializeField] private Button backgroundButton;

        private AudioManager audioManager;

        public bool IsOpen => popupPanel != null && popupPanel.activeSelf;

        [Inject]
        public void Construct(AudioManager audioManager)
        {
            this.audioManager = audioManager;
        }

        private void Awake()
        {
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);

            if (popupPanel != null) popupPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.RemoveListener(Hide);
        }

        public void Show()
        {
            if (popupPanel != null) popupPanel.SetActive(true);

            float musicVol = audioManager != null ? audioManager.MusicVolume : 1f;
            float sfxVol = audioManager != null ? audioManager.SfxVolume : 1f;

            if (musicSlider != null) musicSlider.SetValueWithoutNotify(musicVol);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfxVol);
            UpdateLabels(musicVol, sfxVol);
        }

        public void Hide()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();
            if (popupPanel != null) popupPanel.SetActive(false);
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
