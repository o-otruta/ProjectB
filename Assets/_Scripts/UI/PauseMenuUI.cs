using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Enemies;
using ProjectB.LevelUp;

namespace ProjectB.UI
{
    /// <summary>
    /// Pause menu. Sprint 13.4: run summary chips (wave, time, kills, coins), the current build (ability icons with
    /// levels + active synergies), <c>Resume</c> / <c>Settings</c> (opens the shared <see cref="SettingsPopupUI"/>
    /// instead of duplicated volume sliders) / <c>Quit to Menu</c> with a themed confirmation.
    /// The legacy slider fields stay optional for older scenes and tests.
    /// </summary>
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

        [Header("Sprint 13: summary & build")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPopupUI settingsPopup;
        [SerializeField] private TMP_Text waveValueText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text killsValueText;
        [SerializeField] private TMP_Text coinsValueText;
        [SerializeField] private Transform buildContainer;
        [SerializeField] private AbilityHudSlotUI buildSlotPrefab;
        [SerializeField] private Transform synergyContainer;
        [SerializeField] private GameObject synergySection;
        [SerializeField] private GameObject synergyChipTemplate;
        [SerializeField] private KeyHintBadgeUI pauseKeyHint;

        private GameManager gameManager;
        private AudioManager audioManager;
        private RunStatistics runStatistics;
        private WaveManager waveManager;
        private HeroAbilities heroAbilities;
        private GameInputService gameInput;
        private InputModeService inputModeService;
        private Action<InputMode> cachedInputModeHandler;
        private Action cachedSettingsClosedHandler;
        private bool isSubscribedToGameOver;
        private bool isSubscribedToPause;
        private readonly List<AbilityHudSlotUI> buildSlots = new List<AbilityHudSlotUI>(12);
        private readonly List<GameObject> synergyChips = new List<GameObject>(8);
        private readonly char[] buffer = new char[16];

        public bool IsPanelOpen => pausePanel != null && pausePanel.activeSelf;
        public bool IsQuitConfirmOpen => quitConfirmPanel != null && quitConfirmPanel.activeSelf;

        [Inject]
        public void Construct(
            GameManager gameManager,
            AudioManager audioManager = null,
            RunStatistics runStatistics = null,
            WaveManager waveManager = null,
            HeroAbilities heroAbilities = null,
            GameInputService gameInput = null,
            InputModeService inputModeService = null)
        {
            if (this.gameManager != null)
            {
                if (isSubscribedToGameOver)
                {
                    this.gameManager.OnGameOver -= HandleGameOver;
                    isSubscribedToGameOver = false;
                }
                if (isSubscribedToPause)
                {
                    this.gameManager.OnPauseStateChanged -= HandlePauseStateChanged;
                    isSubscribedToPause = false;
                }
            }

            UnsubscribeInputMode();

            this.gameManager = gameManager;
            this.audioManager = audioManager;
            this.runStatistics = runStatistics;
            this.waveManager = waveManager;
            this.heroAbilities = heroAbilities;
            this.gameInput = gameInput;
            this.inputModeService = inputModeService;

            if (this.gameManager != null)
            {
                if (!isSubscribedToGameOver)
                {
                    this.gameManager.OnGameOver += HandleGameOver;
                    isSubscribedToGameOver = true;
                }
                if (!isSubscribedToPause)
                {
                    this.gameManager.OnPauseStateChanged += HandlePauseStateChanged;
                    isSubscribedToPause = true;
                }
            }

            SubscribeInputMode();
        }

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.AddListener(OnQuitToMenuClicked);
            if (confirmQuitButton != null) confirmQuitButton.onClick.AddListener(OnConfirmQuitClicked);
            if (cancelQuitButton != null) cancelQuitButton.onClick.AddListener(OnCancelQuitClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);

            if (settingsPopup != null)
            {
                cachedSettingsClosedHandler ??= RestorePauseFocus;
                settingsPopup.OnClosed += cachedSettingsClosedHandler;
            }

            if (synergyChipTemplate != null) synergyChipTemplate.SetActive(false);
            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            UnsubscribeInputMode();
            if (settingsPopup != null && cachedSettingsClosedHandler != null)
            {
                settingsPopup.OnClosed -= cachedSettingsClosedHandler;
            }

            if (pauseButton != null) pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (quitToMenuButton != null) quitToMenuButton.onClick.RemoveListener(OnQuitToMenuClicked);
            if (confirmQuitButton != null) confirmQuitButton.onClick.RemoveListener(OnConfirmQuitClicked);
            if (cancelQuitButton != null) cancelQuitButton.onClick.RemoveListener(OnCancelQuitClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);

            if (gameManager != null)
            {
                if (isSubscribedToGameOver)
                {
                    gameManager.OnGameOver -= HandleGameOver;
                    isSubscribedToGameOver = false;
                }
                if (isSubscribedToPause)
                {
                    gameManager.OnPauseStateChanged -= HandlePauseStateChanged;
                    isSubscribedToPause = false;
                }
            }
        }

        private void Update()
        {
            if (gameInput != null && (gameInput.WasPausePressedThisFrame() || gameInput.WasCancelPressedThisFrame()))
            {
                HandlePauseOrBackInput();
            }
        }

        public void HandlePauseOrBackInput()
        {
            if (settingsPopup != null && settingsPopup.IsOpen)
            {
                settingsPopup.Hide();
            }
            else if (IsQuitConfirmOpen)
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

        private void SubscribeInputMode()
        {
            if (inputModeService == null) return;
            cachedInputModeHandler ??= HandleInputModeChanged;
            inputModeService.InputModeChanged -= cachedInputModeHandler;
            inputModeService.InputModeChanged += cachedInputModeHandler;
        }

        private void UnsubscribeInputMode()
        {
            if (inputModeService != null && cachedInputModeHandler != null)
            {
                inputModeService.InputModeChanged -= cachedInputModeHandler;
            }
        }

        private void HandleInputModeChanged(InputMode mode)
        {
            if (mode == InputMode.Touch)
            {
                UiFocusHelper.ClearSelection();
            }
            else if (IsPanelOpen && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                RestorePauseFocus();
            }
        }

        private void RestorePauseFocus()
        {
            if (!IsPanelOpen) return;
            if (IsQuitConfirmOpen)
            {
                UiFocusHelper.SetFirstSelected(cancelQuitButton, inputModeService);
            }
            else if (settingsPopup == null || !settingsPopup.IsOpen)
            {
                UiFocusHelper.SetFirstSelected(resumeButton, inputModeService);
            }
        }

        public void OnPauseClicked()
        {
            if (gameManager != null)
            {
                if (!gameManager.PauseGame()) return;
                audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
                return;
            }

            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            ShowPausePanel();
        }

        public void OnResumeClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
            audioManager?.SaveSettings();

            if (gameManager != null)
            {
                gameManager.ResumeGame();
                return;
            }

            HidePausePanel();
        }

        private void HandlePauseStateChanged(bool paused)
        {
            if (paused)
            {
                ShowPausePanel();
            }
            else
            {
                HidePausePanel();
            }
        }

        private void ShowPausePanel()
        {
            SyncAudioControls();
            RefreshSummary();
            RefreshBuild();

            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }
            UiFocusHelper.SetFirstSelected(resumeButton, inputModeService);
        }

        private void HidePausePanel()
        {
            if (settingsPopup != null && settingsPopup.IsOpen) settingsPopup.Hide();
            if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
            UiFocusHelper.ClearSelection();
        }

        public void OnSettingsClicked()
        {
            if (settingsPopup != null) settingsPopup.Show();
        }

        public void OnQuitToMenuClicked()
        {
            audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);

            if (quitConfirmPanel != null)
            {
                quitConfirmPanel.SetActive(true);
                UiFocusHelper.SetFirstSelected(cancelQuitButton, inputModeService);
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
            UiFocusHelper.SetFirstSelected(quitToMenuButton != null ? quitToMenuButton : resumeButton, inputModeService);
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

        /// <summary>Wave / time / kills / coins chips.</summary>
        public void RefreshSummary()
        {
            int wave = waveManager != null ? waveManager.CurrentWave : 1;
            SetNumber(waveValueText, wave);
            if (timeValueText != null)
            {
                int seconds = runStatistics != null ? Mathf.FloorToInt(runStatistics.PlayTime) : 0;
                int len = UiFormat.WriteClock(buffer, 0, seconds);
                timeValueText.SetCharArray(buffer, 0, len);
            }
            SetNumber(killsValueText, runStatistics != null ? runStatistics.EnemiesKilled : 0);
            SetNumber(coinsValueText, runStatistics != null ? runStatistics.CoinsEarned : 0);
        }

        private void SetNumber(TMP_Text text, int value)
        {
            if (text == null) return;
            int len = UiFormat.WriteGrouped(buffer, 0, value);
            text.SetCharArray(buffer, 0, len);
        }

        /// <summary>Current build: every equipped ability as a slot with level + active synergy chips.</summary>
        public void RefreshBuild()
        {
            if (heroAbilities == null) return;

            int index = 0;
            if (buildContainer != null && buildSlotPrefab != null)
            {
                var actives = heroAbilities.ActiveAbilities;
                for (int i = 0; i < actives.Count; i++)
                {
                    if (actives[i] == null) continue;
                    GetBuildSlot(index++).SetAbility(actives[i].Data, actives[i].Level);
                }

                var passives = heroAbilities.PassiveAbilities;
                for (int i = 0; i < passives.Count; i++)
                {
                    if (passives[i] == null) continue;
                    GetBuildSlot(index++).SetAbility(passives[i].Data, passives[i].Level);
                }

                for (int i = index; i < buildSlots.Count; i++)
                {
                    buildSlots[i].gameObject.SetActive(false);
                }
            }

            var synergies = heroAbilities.ActiveSynergies;
            int synCount = synergies != null ? synergies.Count : 0;
            if (synergySection != null) synergySection.SetActive(synCount > 0);
            if (synergyContainer == null || synergyChipTemplate == null) return;

            for (int i = 0; i < synCount; i++)
            {
                SynergyData syn = synergies[i];
                GameObject chip = GetSynergyChip(i);
                chip.SetActive(true);
                var label = chip.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = syn != null ? Loc.Name(syn) : string.Empty;
                var icons = chip.GetComponentsInChildren<Image>(true);
                for (int k = 0; k < icons.Length; k++)
                {
                    if (icons[k].name == "Icon")
                    {
                        icons[k].sprite = syn != null ? syn.icon : null;
                        icons[k].enabled = syn != null && syn.icon != null;
                    }
                }
            }
            for (int i = synCount; i < synergyChips.Count; i++)
            {
                synergyChips[i].SetActive(false);
            }
        }

        private AbilityHudSlotUI GetBuildSlot(int index)
        {
            while (buildSlots.Count <= index)
            {
                buildSlots.Add(Instantiate(buildSlotPrefab, buildContainer));
            }
            return buildSlots[index];
        }

        private GameObject GetSynergyChip(int index)
        {
            while (synergyChips.Count <= index)
            {
                synergyChips.Add(Instantiate(synergyChipTemplate, synergyContainer));
            }
            return synergyChips[index];
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
            if (settingsPopup != null && settingsPopup.IsOpen) settingsPopup.Hide();
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
