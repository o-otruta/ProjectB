using System;
using TMPro;
using UnityEngine;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.UI
{
    /// <summary>
    /// First-Time User Experience (Sprint 12.2): contextual onboarding hints during the player's first run
    /// (<c>SaveManager.IsFirstRun</c>):
    /// 1) drag-to-move hint until first movement;
    /// 2) highlight on the first LevelUp card selection;
    /// 3) highlight on the first ready ultimate after the first LevelUp or wave 2+.
    /// Zero-GC during <c>Update</c>.
    /// </summary>
    public sealed class GameplayFtueUI : MonoBehaviour
    {
        [Header("Movement Hint")]
        [SerializeField] private GameObject moveHintRoot;
        [SerializeField] private CanvasGroup moveHintGroup;
        [SerializeField] private TMP_Text moveHintText;
        [SerializeField, Min(0.05f)] private float moveDismissDistance = 0.35f;

        [Header("First LevelUp Hint")]
        [SerializeField] private GameObject levelUpHintRoot;
        [SerializeField] private CanvasGroup levelUpHintGroup;
        [SerializeField] private TMP_Text levelUpHintText;

        [Header("First Ready Ultimate Hint")]
        [SerializeField] private GameObject ultimateHintRoot;
        [SerializeField] private CanvasGroup ultimateHintGroup;
        [SerializeField] private TMP_Text ultimateHintText;
        [SerializeField] private GameObject ultimateHighlightRing;

        private SaveManager saveManager;
        private LocalizationService localization;
        private GameManager gameManager;
        private WaveManager waveManager;
        private VirtualJoystick virtualJoystick;
        private HeroMovement heroMovement;
        private HeroUltimate heroUltimate;
        private CardSelectionUI cardSelectionUI;
        private GameEventBus eventBus;

        private Action<WaveCompletedEvent> cachedWaveCompletedHandler;
        private Action cachedUltimateChangedHandler;
        private Action cachedCardsShownHandler;
        private Action cachedCardChosenHandler;
        private Action cachedLanguageChangedHandler;

        private Vector3 heroStartPos;
        private bool initialized;
        private bool subscribed;
        private bool isFtueActive;
        private bool isShowingMoveHint;
        private bool isShowingLevelUpHint;
        private bool isShowingUltimateHint;
        private bool hasDismissedMoveHint;
        private bool hasCompletedFirstLevelUp;
        private bool hasDismissedUltimateHint;
        private bool reachedWaveTwo;

        public bool IsFtueActive => isFtueActive;
        public bool IsMoveHintVisible => isShowingMoveHint;
        public bool IsLevelUpHintVisible => isShowingLevelUpHint;
        public bool IsUltimateHintVisible => isShowingUltimateHint;
        public bool HasDismissedMoveHint => hasDismissedMoveHint;
        public bool HasCompletedFirstLevelUp => hasCompletedFirstLevelUp;
        public bool HasDismissedUltimateHint => hasDismissedUltimateHint;

        [Inject]
        public void Construct(
            SaveManager saveManager,
            LocalizationService localization = null,
            GameManager gameManager = null,
            WaveManager waveManager = null,
            VirtualJoystick virtualJoystick = null,
            HeroMovement heroMovement = null,
            HeroUltimate heroUltimate = null,
            CardSelectionUI cardSelectionUI = null,
            GameEventBus eventBus = null)
        {
            this.saveManager = saveManager;
            this.localization = localization;
            this.gameManager = gameManager;
            this.waveManager = waveManager;
            this.virtualJoystick = virtualJoystick;
            this.heroMovement = heroMovement;
            this.heroUltimate = heroUltimate;
            this.cardSelectionUI = cardSelectionUI;
            this.eventBus = eventBus;
        }

        private void Start()
        {
            InitializeFtue();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void InitializeFtue()
        {
            if (initialized) return;
            initialized = true;

            HideAllImmediate();

            isFtueActive = saveManager != null && saveManager.IsFirstRun;
            RefreshLocalizedTexts();

            if (!isFtueActive) return;

            CacheDelegates();
            Subscribe();

            if (heroMovement != null)
            {
                heroStartPos = heroMovement.transform.position;
            }
            if (waveManager != null && waveManager.CurrentWave >= 2)
            {
                reachedWaveTwo = true;
            }

            ShowMoveHint();
        }

        private void Update()
        {
            TickFtue();
        }

        public void TickFtue()
        {
            if (!isFtueActive) return;

            if (!hasDismissedMoveHint)
            {
                bool isPlaying = gameManager == null || gameManager.CurrentState == GameState.Playing;
                if (isPlaying)
                {
                    bool joystickMoved = virtualJoystick != null && virtualJoystick.Direction.sqrMagnitude > 0.01f;
                    bool heroMoved = false;
                    if (!joystickMoved && heroMovement != null)
                    {
                        Vector3 delta = heroMovement.transform.position - heroStartPos;
                        delta.y = 0f;
                        float thresholdSq = moveDismissDistance * moveDismissDistance;
                        heroMoved = delta.sqrMagnitude >= thresholdSq;
                    }

                    if (joystickMoved || heroMoved)
                    {
                        DismissMoveHint();
                    }
                }
            }

            if (!hasDismissedUltimateHint)
            {
                EvaluateUltimateHint();
            }
        }

        public void RefreshLocalizedTexts()
        {
            if (moveHintText != null)
            {
                moveHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_MOVE_HINT, "Drag to move — attack is automatic")
                    : "Drag to move — attack is automatic";
            }

            if (levelUpHintText != null)
            {
                levelUpHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_LEVELUP_HINT, "Choose an upgrade")
                    : "Choose an upgrade";
            }

            if (ultimateHintText != null)
            {
                ultimateHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_ULTIMATE_HINT, "Ultimate ready! Tap to unleash")
                    : "Ultimate ready! Tap to unleash";
            }
        }

        private void CacheDelegates()
        {
            cachedWaveCompletedHandler ??= HandleWaveCompleted;
            cachedUltimateChangedHandler ??= HandleUltimateStateChanged;
            cachedCardsShownHandler ??= HandleCardsShown;
            cachedCardChosenHandler ??= HandleCardChosen;
            cachedLanguageChangedHandler ??= RefreshLocalizedTexts;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            eventBus?.Subscribe(cachedWaveCompletedHandler);
            if (heroUltimate != null) heroUltimate.OnStateChanged += cachedUltimateChangedHandler;
            if (cardSelectionUI != null)
            {
                cardSelectionUI.OnCardsShown += cachedCardsShownHandler;
                cardSelectionUI.OnCardChosen += cachedCardChosenHandler;
            }
            if (localization != null) localization.LanguageChanged += cachedLanguageChangedHandler;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;

            eventBus?.Unsubscribe(cachedWaveCompletedHandler);
            if (heroUltimate != null) heroUltimate.OnStateChanged -= cachedUltimateChangedHandler;
            if (cardSelectionUI != null)
            {
                cardSelectionUI.OnCardsShown -= cachedCardsShownHandler;
                cardSelectionUI.OnCardChosen -= cachedCardChosenHandler;
            }
            if (localization != null) localization.LanguageChanged -= cachedLanguageChangedHandler;
        }

        private void HideAllImmediate()
        {
            isShowingMoveHint = false;
            isShowingLevelUpHint = false;
            isShowingUltimateHint = false;

            if (moveHintRoot != null) moveHintRoot.SetActive(false);
            if (levelUpHintRoot != null) levelUpHintRoot.SetActive(false);
            if (ultimateHintRoot != null) ultimateHintRoot.SetActive(false);
            if (ultimateHighlightRing != null) ultimateHighlightRing.SetActive(false);
        }

        private void ShowMoveHint()
        {
            if (hasDismissedMoveHint) return;
            isShowingMoveHint = true;
            if (moveHintRoot != null) moveHintRoot.SetActive(true);
            if (moveHintGroup != null)
            {
                UiTween.Fade(moveHintGroup, 0f, 1f, UiTheme.Current.durationNormal);
            }
        }

        private void DismissMoveHint()
        {
            if (hasDismissedMoveHint) return;
            hasDismissedMoveHint = true;
            isShowingMoveHint = false;

            if (moveHintGroup != null && moveHintRoot != null && moveHintRoot.activeSelf)
            {
                UiTween.Fade(moveHintGroup, moveHintGroup.alpha, 0f, UiTheme.Current.durationFast, 0f, disableOnComplete: true);
            }
            else if (moveHintRoot != null)
            {
                moveHintRoot.SetActive(false);
            }
        }

        private void HandleCardsShown()
        {
            if (!isFtueActive || hasCompletedFirstLevelUp) return;

            isShowingLevelUpHint = true;
            if (levelUpHintRoot != null) levelUpHintRoot.SetActive(true);
            if (levelUpHintGroup != null)
            {
                UiTween.Fade(levelUpHintGroup, 0f, 1f, UiTheme.Current.durationNormal);
            }
            if (levelUpHintRoot != null && Application.isPlaying)
            {
                UiTween.Punch(levelUpHintRoot.transform, 0.08f, UiTheme.Current.durationNormal);
            }
        }

        private void HandleCardChosen()
        {
            if (!isFtueActive) return;

            if (!hasCompletedFirstLevelUp)
            {
                hasCompletedFirstLevelUp = true;
                isShowingLevelUpHint = false;
                if (levelUpHintRoot != null) levelUpHintRoot.SetActive(false);
                EvaluateUltimateHint();
            }
        }

        private void HandleWaveCompleted(WaveCompletedEvent evt)
        {
            if (!isFtueActive) return;

            if (evt.NextWave >= 2)
            {
                reachedWaveTwo = true;
                EvaluateUltimateHint();
            }
        }

        private void HandleUltimateStateChanged()
        {
            if (!isFtueActive) return;

            if (heroUltimate != null && (heroUltimate.IsActive || heroUltimate.CooldownRemaining > 0f))
            {
                DismissUltimateHint();
            }
            else
            {
                EvaluateUltimateHint();
            }
        }

        private void EvaluateUltimateHint()
        {
            if (!isFtueActive || hasDismissedUltimateHint) return;

            bool unlockMet = hasCompletedFirstLevelUp || reachedWaveTwo || (waveManager != null && waveManager.CurrentWave >= 2);
            bool playing = gameManager == null || gameManager.CurrentState == GameState.Playing;
            bool ultReady = heroUltimate != null && heroUltimate.IsReady;
            bool shouldShow = unlockMet && playing && ultReady;

            if (shouldShow && !isShowingUltimateHint)
            {
                isShowingUltimateHint = true;
                if (ultimateHintRoot != null) ultimateHintRoot.SetActive(true);
                if (ultimateHintGroup != null)
                {
                    UiTween.Fade(ultimateHintGroup, 0f, 1f, UiTheme.Current.durationNormal);
                }
                if (ultimateHighlightRing != null) ultimateHighlightRing.SetActive(true);
            }
            else if (!shouldShow && isShowingUltimateHint)
            {
                isShowingUltimateHint = false;
                if (ultimateHintRoot != null) ultimateHintRoot.SetActive(false);
                if (ultimateHighlightRing != null) ultimateHighlightRing.SetActive(false);
            }
        }

        private void DismissUltimateHint()
        {
            if (hasDismissedUltimateHint) return;
            hasDismissedUltimateHint = true;
            isShowingUltimateHint = false;

            if (ultimateHighlightRing != null) ultimateHighlightRing.SetActive(false);

            if (ultimateHintGroup != null && ultimateHintRoot != null && ultimateHintRoot.activeSelf)
            {
                UiTween.Fade(ultimateHintGroup, ultimateHintGroup.alpha, 0f, UiTheme.Current.durationFast, 0f, disableOnComplete: true);
            }
            else if (ultimateHintRoot != null)
            {
                ultimateHintRoot.SetActive(false);
            }
        }
    }
}
