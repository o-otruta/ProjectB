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

        [Header("Quit Confirmation")]
        [SerializeField] private GameObject quitConfirmPanel;
        [SerializeField] private Button confirmQuitButton;
        [SerializeField] private Button cancelQuitButton;
        [SerializeField] private TextMeshProUGUI quitConfirmText;

        private GameManager gameManager;
        private AudioManager audioManager;
        private bool isSubscribedToGameOver;

        public bool IsPanelOpen => pausePanel != null && pausePanel.activeSelf;
        public bool IsQuitConfirmOpen => quitConfirmPanel != null && quitConfirmPanel.activeSelf;

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
            if (confirmQuitButton != null) confirmQuitButton.onClick.AddListener(OnConfirmQuitClicked);
            if (cancelQuitButton != null) cancelQuitButton.onClick.AddListener(OnCancelQuitClicked);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);

            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (pauseButton != null) pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.RemoveListener(OnQuitToMenuClicked);
            if (confirmQuitButton != null) confirmQuitButton.onClick.RemoveListener(OnConfirmQuitClicked);
            if (cancelQuitButton != null) cancelQuitButton.onClick.RemoveListener(OnCancelQuitClicked);
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
                if (IsQuitConfirmOpen)
                {
                    OnCancelQuitClicked();
                }
                else if (IsPanelOpen)
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

            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
        }

        public void OnResumeClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();

            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            gameManager?.ResumeGame();
        }

        public void OnQuitToMenuClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);

            if (quitConfirmPanel != null)
            {
                quitConfirmPanel.SetActive(true);
                return;
            }

            OnConfirmQuitClicked();
        }

        public void OnCancelQuitClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            if (quitConfirmPanel != null)
            {
                quitConfirmPanel.SetActive(false);
            }
        }

        public void OnConfirmQuitClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();

            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            gameManager?.AbandonRunAndGoToMenu();
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
            if (quitConfirmPanel != null)
            {
                quitConfirmPanel.SetActive(false);
            }
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }
    }
}