using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;

namespace ProjectB.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI musicValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitToMenuButton;

        private GameManager gameManager;
        private AudioManager audioManager;
        private bool isSubscribedToGameOver;

        public bool IsPanelOpen => pausePanel != null && pausePanel.activeSelf;

        [Inject]
        public void Construct(GameManager gameManager, AudioManager audioManager = null)
        {
            if (this.gameManager != null && isSubscribedToGameOver)
            {
                this.gameManager.OnGameOver -= HandleGameOver;
                isSubscribedToGameOver = false;
            }

            this.gameManager = gameManager;
            this.audioManager = audioManager;

            if (this.gameManager != null && !isSubscribedToGameOver)
            {
                this.gameManager.OnGameOver += HandleGameOver;
                isSubscribedToGameOver = true;
            }
        }

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.AddListener(OnQuitToMenuClicked);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);

            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (pauseButton != null) pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.RemoveListener(OnQuitToMenuClicked);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);

            if (gameManager != null && isSubscribedToGameOver)
            {
                gameManager.OnGameOver -= HandleGameOver;
                isSubscribedToGameOver = false;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsPanelOpen)
                {
                    OnResumeClicked();
                }
                else if (gameManager != null && gameManager.CanPause)
                {
                    OnPauseClicked();
                }
            }
        }

        public void OnPauseClicked()
        {
            if (gameManager != null && !gameManager.PauseGame()) return;

            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            SyncAudioControls();

            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
        }

        public void OnResumeClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            gameManager?.ResumeGame();
        }

        public void OnQuitToMenuClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            gameManager?.GoToMenu();
        }

        private void SyncAudioControls()
        {
            float musicVol = audioManager != null ? audioManager.MusicVolume : 1f;
            float sfxVol = audioManager != null ? audioManager.SfxVolume : 1f;

            if (musicSlider != null) musicSlider.SetValueWithoutNotify(musicVol);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfxVol);
            UpdateLabels(musicVol, sfxVol);
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

        private void HandleGameOver(int _, int __)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }
    }
}