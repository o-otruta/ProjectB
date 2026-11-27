using System;
using TMPro;
using UnityEngine;
using VContainer;
using ProjectB.Arena;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
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
    /// 3) highlight on the first ready ultimate after the first LevelUp or wave 2+;
    /// 4) a tutorial explosive barrel placed on the path of the first enemy pack with a "kick it" hint
    ///    that follows the barrel on screen (<see cref="BarrelConfig"/>).
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

        [Header("Tutorial Barrel Hint")]
        [SerializeField] private GameObject barrelHintRoot;
        [SerializeField] private CanvasGroup barrelHintGroup;
        [SerializeField] private TMP_Text barrelHintText;
        [Tooltip("Hint follows the tutorial barrel on screen; offset in world units above the barrel.")]
        [SerializeField, Min(0f)] private float barrelHintWorldHeight = 2.4f;
        [SerializeField, Min(0.05f)] private float barrelProbeInterval = 0.25f;

        private const int BARREL_PROBE_BUFFER = 16;
        private static readonly Collider[] BarrelProbeBuffer = new Collider[BARREL_PROBE_BUFFER];

        private SaveManager saveManager;
        private LocalizationService localization;
        private GameManager gameManager;
        private WaveManager waveManager;
        private VirtualJoystick virtualJoystick;
        private HeroMovement heroMovement;
        private HeroUltimate heroUltimate;
        private CardSelectionUI cardSelectionUI;
        private GameEventBus eventBus;
        private MoveInputService moveInputService;
        private InputModeService inputModeService;
        private ArenaGenerator arenaGenerator;
        private BarrelConfig barrelConfig;
        private Camera worldCamera;
        private Canvas hintCanvas;
        private ExplosiveBarrel tutorialBarrel;
        private float barrelProbeTimer;
        private float barrelHintTimer;
        private int enemyProbeMask;
        private Action<BarrelIgnitedEvent> cachedBarrelIgnitedHandler;
        private Action<BarrelExplodedEvent> cachedBarrelExplodedHandler;

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
        private bool isShowingBarrelHint;
        private bool hasCompletedBarrelHint;

        public bool IsFtueActive => isFtueActive;
        public bool IsMoveHintVisible => isShowingMoveHint;
        public bool IsLevelUpHintVisible => isShowingLevelUpHint;
        public bool IsUltimateHintVisible => isShowingUltimateHint;
        public bool HasDismissedMoveHint => hasDismissedMoveHint;
        public bool HasCompletedFirstLevelUp => hasCompletedFirstLevelUp;
        public bool HasDismissedUltimateHint => hasDismissedUltimateHint;
        public bool IsBarrelHintVisible => isShowingBarrelHint;
        public bool HasCompletedBarrelHint => hasCompletedBarrelHint;
        public ExplosiveBarrel TutorialBarrel => tutorialBarrel;

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
            GameEventBus eventBus = null,
            MoveInputService moveInputService = null,
            InputModeService inputModeService = null,
            ArenaGenerator arenaGenerator = null,
            BarrelConfig barrelConfig = null)
        {
            this.arenaGenerator = arenaGenerator;
            this.barrelConfig = barrelConfig;
            this.saveManager = saveManager;
            this.localization = localization;
            this.gameManager = gameManager;
            this.waveManager = waveManager;
            this.virtualJoystick = virtualJoystick;
            this.heroMovement = heroMovement;
            this.heroUltimate = heroUltimate;
            this.cardSelectionUI = cardSelectionUI;
            this.eventBus = eventBus;
            this.moveInputService = moveInputService;
            this.inputModeService = inputModeService;
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
            EnsureBarrelHintBuilt();

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
                    bool joystickMoved = (moveInputService != null && moveInputService.Direction.sqrMagnitude > 0.01f)
                        || (virtualJoystick != null && virtualJoystick.Direction.sqrMagnitude > 0.01f);
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

            if (!hasCompletedBarrelHint)
            {
                TickBarrelHint(Time.deltaTime);
            }
        }

        private void TickBarrelHint(float dt)
        {
            if (barrelConfig != null && !barrelConfig.ftueSpawnBarrel)
            {
                hasCompletedBarrelHint = true;
                return;
            }

            bool playing = gameManager == null || gameManager.CurrentState == GameState.Playing;
            if (!playing || dt <= 0f) return;

            if (isShowingBarrelHint)
            {
                barrelHintTimer -= dt;
                if (tutorialBarrel == null || tutorialBarrel.IsDead || !tutorialBarrel.gameObject.activeInHierarchy || barrelHintTimer <= 0f)
                {
                    CompleteBarrelHint();
                    return;
                }
                PositionBarrelHint();
                return;
            }

            // Wait until the player has learned to move, then place the barrel when the first pack approaches.
            if (!hasDismissedMoveHint || arenaGenerator == null || heroMovement == null) return;

            barrelProbeTimer -= dt;
            if (barrelProbeTimer > 0f) return;
            barrelProbeTimer = barrelProbeInterval;

            float triggerDistance = barrelConfig != null ? barrelConfig.ftueTriggerEnemyDistance : 11f;
            if (TryFindNearestEnemyDirection(triggerDistance, out Vector3 enemyDir))
            {
                SpawnTutorialBarrel(enemyDir);
            }
        }

        /// <summary>Places the tutorial barrel towards <paramref name="direction"/> from the hero and shows the hint.</summary>
        public ExplosiveBarrel SpawnTutorialBarrel(Vector3 direction)
        {
            if (hasCompletedBarrelHint || arenaGenerator == null || heroMovement == null) return null;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            float distance = barrelConfig != null ? barrelConfig.ftueBarrelDistance : 3.5f;
            Vector3 heroPos = heroMovement.transform.position;
            heroPos.y = 0f;
            Vector3 position = heroPos + direction * distance;

            int obstacleMask = LayerMask.GetMask("Obstacles");
            if (obstacleMask != 0)
            {
                // Rotate the candidate around the hero (0, +-35, +-70 deg) until it is clear of obstacles.
                for (int i = 0; i < 5; i++)
                {
                    float angle = i == 0 ? 0f : ((i + 1) / 2) * 35f * ((i & 1) == 1 ? 1f : -1f);
                    Vector3 candidate = heroPos + Quaternion.Euler(0f, angle, 0f) * direction * distance;
                    if (!Physics.CheckSphere(candidate + Vector3.up * 0.7f, 0.9f, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        position = candidate;
                        break;
                    }
                }
            }

            tutorialBarrel = arenaGenerator.SpawnBarrelAtPosition(position);
            if (tutorialBarrel == null)
            {
                hasCompletedBarrelHint = true;
                return null;
            }

            ShowBarrelHint();
            return tutorialBarrel;
        }

        private bool TryFindNearestEnemyDirection(float radius, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (enemyProbeMask == 0)
            {
                int mask = LayerMask.GetMask("Enemy", "Enemies");
                enemyProbeMask = mask != 0 ? mask : (1 << 8);
            }

            Vector3 heroPos = heroMovement.transform.position;
            int hits = Physics.OverlapSphereNonAlloc(heroPos, radius, BarrelProbeBuffer, enemyProbeMask, QueryTriggerInteraction.Collide);
            float bestSq = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                Collider col = BarrelProbeBuffer[i];
                BarrelProbeBuffer[i] = null;
                if (col == null) continue;
                EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 diff = enemy.transform.position - heroPos;
                diff.y = 0f;
                float sq = diff.sqrMagnitude;
                if (sq < bestSq && sq > 0.01f)
                {
                    bestSq = sq;
                    direction = diff;
                }
            }
            return bestSq < float.MaxValue;
        }

        /// <summary>
        /// The barrel hint bubble is cloned from the movement hint at runtime (first run only), so it shares the
        /// themed pill style without an extra baked scene object. A rotated square under the pill acts as a pointer.
        /// </summary>
        private void EnsureBarrelHintBuilt()
        {
            if (barrelHintRoot != null || moveHintRoot == null) return;

            GameObject clone = Instantiate(moveHintRoot, moveHintRoot.transform.parent);
            clone.name = "FtueBarrelHint";
            clone.transform.SetSiblingIndex(moveHintRoot.transform.GetSiblingIndex() + 1);

            var responsive = clone.GetComponent<ResponsiveRect>();
            if (responsive != null) Destroy(responsive);

            if (clone.transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(600f, 84f);
            }

            var pill = clone.GetComponent<UnityEngine.UI.Image>();
            var pointerGo = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            pointerGo.transform.SetParent(clone.transform, false);
            pointerGo.transform.SetAsFirstSibling();
            var pointerRect = (RectTransform)pointerGo.transform;
            pointerRect.anchorMin = pointerRect.anchorMax = new Vector2(0.5f, 0f);
            pointerRect.pivot = new Vector2(0.5f, 0.5f);
            pointerRect.anchoredPosition = new Vector2(0f, 2f);
            pointerRect.sizeDelta = new Vector2(34f, 34f);
            pointerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var pointerImage = pointerGo.GetComponent<UnityEngine.UI.Image>();
            pointerImage.raycastTarget = false;
            if (pill != null)
            {
                Color c = pill.color;
                c.a = 1f;
                pointerImage.color = c;
            }

            barrelHintRoot = clone;
            barrelHintGroup = clone.GetComponent<CanvasGroup>();
            barrelHintText = clone.GetComponentInChildren<TMP_Text>(true);
            clone.SetActive(false);
            RefreshLocalizedTexts();
        }

        private void ShowBarrelHint()
        {
            isShowingBarrelHint = true;
            barrelHintTimer = barrelConfig != null ? barrelConfig.ftueHintTimeout : 14f;
            if (barrelHintRoot != null) barrelHintRoot.SetActive(true);
            if (barrelHintGroup != null)
            {
                UiTween.Fade(barrelHintGroup, 0f, 1f, UiTheme.Current.durationNormal);
            }
            PositionBarrelHint();
        }

        private void PositionBarrelHint()
        {
            if (barrelHintRoot == null || tutorialBarrel == null) return;
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            var rect = barrelHintRoot.transform as RectTransform;
            var parentRect = rect != null ? rect.parent as RectTransform : null;
            if (rect == null || parentRect == null) return;

            if (hintCanvas == null) hintCanvas = barrelHintRoot.GetComponentInParent<Canvas>();
            Camera uiCamera = hintCanvas != null && hintCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? hintCanvas.worldCamera : null;

            Vector3 world = tutorialBarrel.transform.position + Vector3.up * barrelHintWorldHeight;
            Vector3 screen = worldCamera.WorldToScreenPoint(world);
            if (screen.z < 0f) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out Vector2 local))
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                Vector2 half = parentRect.rect.size * 0.5f - rect.rect.size * 0.5f;
                local.x = Mathf.Clamp(local.x, -half.x, half.x);
                local.y = Mathf.Clamp(local.y, -half.y, half.y);
                rect.anchoredPosition = local;
            }
        }

        private void CompleteBarrelHint()
        {
            if (hasCompletedBarrelHint) return;
            hasCompletedBarrelHint = true;
            isShowingBarrelHint = false;

            if (barrelHintGroup != null && barrelHintRoot != null && barrelHintRoot.activeSelf)
            {
                UiTween.Fade(barrelHintGroup, barrelHintGroup.alpha, 0f, UiTheme.Current.durationFast, 0f, disableOnComplete: true);
            }
            else if (barrelHintRoot != null)
            {
                barrelHintRoot.SetActive(false);
            }
        }

        private void HandleBarrelIgnited(BarrelIgnitedEvent evt)
        {
            if (!isFtueActive) return;
            CompleteBarrelHint();
        }

        private void HandleBarrelExploded(BarrelExplodedEvent evt)
        {
            if (!isFtueActive) return;
            CompleteBarrelHint();
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

            if (barrelHintText != null)
            {
                barrelHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_BARREL_HINT, "Kick the barrel into enemies!")
                    : "Kick the barrel into enemies!";
            }
        }

        private void CacheDelegates()
        {
            cachedWaveCompletedHandler ??= HandleWaveCompleted;
            cachedUltimateChangedHandler ??= HandleUltimateStateChanged;
            cachedCardsShownHandler ??= HandleCardsShown;
            cachedCardChosenHandler ??= HandleCardChosen;
            cachedLanguageChangedHandler ??= RefreshLocalizedTexts;
            cachedBarrelIgnitedHandler ??= HandleBarrelIgnited;
            cachedBarrelExplodedHandler ??= HandleBarrelExploded;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            eventBus?.Subscribe(cachedWaveCompletedHandler);
            eventBus?.Subscribe(cachedBarrelIgnitedHandler);
            eventBus?.Subscribe(cachedBarrelExplodedHandler);
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
            eventBus?.Unsubscribe(cachedBarrelIgnitedHandler);
            eventBus?.Unsubscribe(cachedBarrelExplodedHandler);
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
            isShowingBarrelHint = false;

            if (barrelHintRoot != null) barrelHintRoot.SetActive(false);
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
