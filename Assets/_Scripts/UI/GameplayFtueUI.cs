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
    /// 1) move hint until first movement (touch or keyboard wording, follows <see cref="InputModeService"/>);
    /// 2) highlight on the first LevelUp card selection;
    /// 3) highlight on the first ready ultimate after the first LevelUp or wave 2+ (keyboard wording names the key);
    /// 4) a tutorial explosive barrel placed on the path of the first enemy pack with a "kick it" hint
    ///    that follows the barrel on screen (<see cref="BarrelConfig"/>).
    /// Independently of the first run: the very first Boss Shard ever earned shows a short "spend it in the
    /// Constellations" hint (<c>SaveData.ftueShardHintShown</c>).
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

        [Header("First Boss Shard Hint")]
        [Tooltip("Seconds of gameplay the 'spend shards in Constellations' hint stays on screen.")]
        [SerializeField, Min(0.5f)] private float shardHintDuration = 5f;

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
        private Action<BossShardsGainedEvent> cachedShardsGainedHandler;
        private Action<InputMode> cachedInputModeHandler;
        private GameInputService gameInput;

        private FtueHintBubble moveBubble;
        private FtueHintBubble barrelBubble;
        private FtueHintBubble levelUpBubble;
        private GameObject shardHintRoot;
        private CanvasGroup shardHintGroup;
        private TMP_Text shardHintText;
        private bool isShardHintPending;
        private bool isShowingShardHint;
        private float shardHintTimer;

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
        public bool IsShardHintPending => isShardHintPending;
        public bool IsShardHintVisible => isShowingShardHint;
        public TMP_Text MoveHintText => moveHintText;
        public TMP_Text UltimateHintText => ultimateHintText;
        public TMP_Text ShardHintText => shardHintText;
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
            BarrelConfig barrelConfig = null,
            GameInputService gameInput = null)
        {
            this.gameInput = gameInput;
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
            SaveData data = saveManager?.Data;
            isShardHintPending = data != null && !data.ftueShardHintShown && data.totalBossShardsEarned == 0;
            RefreshLocalizedTexts();

            if (!isFtueActive && !isShardHintPending) return;

            ApplyBubbleStyle();
            CacheDelegates();
            Subscribe();
            if (!isFtueActive) return;

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
            if (isShowingShardHint)
            {
                TickShardHint(Time.deltaTime);
            }

            if (!isFtueActive) return;

            if (isShowingLevelUpHint)
            {
                PositionLevelUpHint();
            }

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
            }

            barrelHintRoot = clone;
            barrelHintGroup = clone.GetComponent<CanvasGroup>();
            barrelHintText = clone.GetComponentInChildren<TMP_Text>(true);
            barrelBubble = FtueHintBubble.Ensure(clone, barrelHintText, maxWidth: 760f);
            barrelBubble?.SetPointer(FtuePointerSide.Down);
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
                const float screenMargin = 24f;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                Vector2 half = parentRect.rect.size * 0.5f - rect.rect.size * 0.5f - new Vector2(screenMargin, screenMargin);
                half = Vector2.Max(half, Vector2.zero);
                float desiredX = local.x;
                local.x = Mathf.Clamp(local.x, -half.x, half.x);
                local.y = Mathf.Clamp(local.y, -half.y, half.y);
                rect.anchoredPosition = local;
                // A bubble pushed in from the screen edge keeps its tail over the barrel.
                if (barrelBubble != null) barrelBubble.SetPointerSlide(desiredX - local.x);
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

        /// <summary>
        /// Large onboarding text + pointers (<see cref="FtueHintBubble"/>). Must run before the barrel / shard hints are
        /// cloned from the move hint so the clones inherit the style and the pointer child.
        /// </summary>
        private void ApplyBubbleStyle()
        {
            moveBubble = FtueHintBubble.Ensure(moveHintRoot, moveHintText);
            // Same look as every other hint; it may cover the build strip, the pointer aims at the first card.
            levelUpBubble = FtueHintBubble.Ensure(levelUpHintRoot, levelUpHintText);
            // Positioned above the first card at runtime, so its layout preset must not snap it back.
            if (levelUpHintRoot != null && levelUpHintRoot.TryGetComponent(out ResponsiveRect levelUpPreset))
            {
                levelUpPreset.enabled = false;
            }
            FtueHintBubble ultBubble = FtueHintBubble.Ensure(ultimateHintRoot, ultimateHintText, maxWidth: 720f);
            if (ultBubble != null)
            {
                var ultButton = ultimateHighlightRing != null ? ultimateHighlightRing.transform.parent as RectTransform : null;
                ultBubble.SetPointer(FtuePointerSide.Down, ultButton);
            }
            RefreshMovePointer();
        }

        /// <summary>Touch: the move hint points down at the joystick area; keys need no pointer.</summary>
        private void RefreshMovePointer()
        {
            moveBubble?.SetPointer(IsKeyboardMode ? FtuePointerSide.None : FtuePointerSide.Down);
        }

        private void HandleShardsGained(BossShardsGainedEvent evt)
        {
            if (!isShardHintPending || evt.Amount <= 0) return;
            ShowShardHint();
        }

        /// <summary>Shows the one-time "Boss Shard → Constellations" hint and marks it as shown in the save.</summary>
        public void ShowShardHint()
        {
            if (!isShardHintPending) return;
            isShardHintPending = false;
            saveManager?.MarkFtueShardHintShown(saveImmediately: false);

            EnsureShardHintBuilt();
            isShowingShardHint = true;
            shardHintTimer = shardHintDuration;
            if (shardHintRoot != null) shardHintRoot.SetActive(true);
            if (shardHintGroup != null)
            {
                UiTween.Fade(shardHintGroup, 0f, 1f, UiTheme.Current.durationNormal);
            }
            if (shardHintRoot != null && Application.isPlaying)
            {
                UiTween.Punch(shardHintRoot.transform, 0.08f, UiTheme.Current.durationNormal);
            }
        }

        private void TickShardHint(float dt)
        {
            bool playing = gameManager == null || gameManager.CurrentState == GameState.Playing;
            if (!playing || dt <= 0f) return;

            shardHintTimer -= dt;
            if (shardHintTimer > 0f) return;

            isShowingShardHint = false;
            if (shardHintGroup != null && shardHintRoot != null && shardHintRoot.activeSelf)
            {
                UiTween.Fade(shardHintGroup, shardHintGroup.alpha, 0f, UiTheme.Current.durationFast, 0f, disableOnComplete: true);
            }
            else if (shardHintRoot != null)
            {
                shardHintRoot.SetActive(false);
            }
        }

        /// <summary>The shard hint reuses the movement hint pill (same themed style and screen spot) as a runtime clone.</summary>
        private void EnsureShardHintBuilt()
        {
            if (shardHintRoot != null || moveHintRoot == null) return;

            GameObject clone = Instantiate(moveHintRoot, moveHintRoot.transform.parent);
            clone.name = "FtueShardHint";
            clone.transform.SetSiblingIndex(moveHintRoot.transform.GetSiblingIndex() + 1);
            shardHintRoot = clone;
            shardHintGroup = clone.GetComponent<CanvasGroup>();
            shardHintText = clone.GetComponentInChildren<TMP_Text>(true);
            FtueHintBubble.Ensure(clone, shardHintText)?.SetPointer(FtuePointerSide.None);
            clone.SetActive(false);
            RefreshLocalizedTexts();
        }

        private bool IsKeyboardMode => inputModeService != null && inputModeService.Mode == InputMode.KeyboardMouse;

        /// <summary>Human-readable keyboard key of the Ultimate action ("Space"), or null if it has none.</summary>
        private string GetUltimateKeyLabel()
        {
            UnityEngine.InputSystem.InputAction action = gameInput != null ? gameInput.UltimateAction : null;
            if (action == null) return null;

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                string path = bindings[i].effectivePath;
                if (!string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>", StringComparison.Ordinal))
                {
                    return UnityEngine.InputSystem.InputControlPath.ToHumanReadableString(
                        path, UnityEngine.InputSystem.InputControlPath.HumanReadableStringOptions.OmitDevice);
                }
            }
            return null;
        }

        private void HandleInputModeChanged(InputMode _)
        {
            RefreshLocalizedTexts();
            RefreshMovePointer();
        }

        public void RefreshLocalizedTexts()
        {
            if (moveHintText != null)
            {
                moveHintText.text = IsKeyboardMode
                    ? (localization != null
                        ? localization.Get(LocalizationService.KEY_FTUE_MOVE_HINT_KEYBOARD, "WASD or arrows to move — attack is automatic")
                        : "WASD or arrows to move — attack is automatic")
                    : (localization != null
                        ? localization.Get(LocalizationService.KEY_FTUE_MOVE_HINT, "Drag to move — attack is automatic")
                        : "Drag to move — attack is automatic");
            }

            if (shardHintText != null)
            {
                shardHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_SHARD_HINT, "Boss Shard! Spend it in the Constellations menu")
                    : "Boss Shard! Spend it in the Constellations menu";
            }

            if (levelUpHintText != null)
            {
                levelUpHintText.text = localization != null
                    ? localization.Get(LocalizationService.KEY_FTUE_LEVELUP_HINT, "Choose an upgrade")
                    : "Choose an upgrade";
            }

            if (ultimateHintText != null)
            {
                string key = IsKeyboardMode ? GetUltimateKeyLabel() : null;
                if (!string.IsNullOrEmpty(key))
                {
                    ultimateHintText.text = localization != null
                        ? localization.Format(LocalizationService.KEY_FTUE_ULTIMATE_HINT_KEYBOARD, key)
                        : string.Format("Ultimate ready! Press {0}", key);
                }
                else
                {
                    ultimateHintText.text = localization != null
                        ? localization.Get(LocalizationService.KEY_FTUE_ULTIMATE_HINT, "Ultimate ready! Tap to unleash")
                        : "Ultimate ready! Tap to unleash";
                }
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
            cachedShardsGainedHandler ??= HandleShardsGained;
            cachedInputModeHandler ??= HandleInputModeChanged;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;

            eventBus?.Subscribe(cachedWaveCompletedHandler);
            eventBus?.Subscribe(cachedBarrelIgnitedHandler);
            eventBus?.Subscribe(cachedBarrelExplodedHandler);
            eventBus?.Subscribe(cachedShardsGainedHandler);
            if (inputModeService != null) inputModeService.InputModeChanged += cachedInputModeHandler;
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
            eventBus?.Unsubscribe(cachedShardsGainedHandler);
            if (inputModeService != null) inputModeService.InputModeChanged -= cachedInputModeHandler;
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
            if (levelUpHintRoot != null)
            {
                // Drawn over the build strip it may cover, pointing down at the first card.
                levelUpHintRoot.transform.SetAsLastSibling();
                levelUpHintRoot.SetActive(true);
            }
            CardUI firstCard = cardSelectionUI != null ? cardSelectionUI.GetCardSlot(0) : null;
            levelUpBubble?.SetPointer(FtuePointerSide.Down, firstCard != null ? firstCard.transform as RectTransform : null);
            PositionLevelUpHint();
            if (levelUpHintGroup != null)
            {
                UiTween.Fade(levelUpHintGroup, 0f, 1f, UiTheme.Current.durationNormal);
            }
            if (levelUpHintRoot != null && Application.isPlaying)
            {
                UiTween.Punch(levelUpHintRoot.transform, 0.08f, UiTheme.Current.durationNormal);
            }
        }

        /// <summary>
        /// Sits the LevelUp hint right above the first card with its tail touching the card's top edge. Uses the card's
        /// unscaled rect, so the cards' bounce-in animation does not make the bubble jump. Zero-GC.
        /// </summary>
        public void PositionLevelUpHint()
        {
            if (levelUpHintRoot == null || cardSelectionUI == null) return;
            CardUI card = cardSelectionUI.GetCardSlot(0);
            var cardRect = card != null ? card.transform as RectTransform : null;
            var rect = levelUpHintRoot.transform as RectTransform;
            var parent = rect != null ? rect.parent as RectTransform : null;
            var cardParent = cardRect != null ? cardRect.parent : null;
            if (cardRect == null || parent == null || cardParent == null) return;

            Rect cr = cardRect.rect;
            Vector3 cardTopWorld = cardParent.TransformPoint(cardRect.localPosition + new Vector3(cr.center.x, cr.yMax, 0f));
            Vector3 cardTop = parent.InverseTransformPoint(cardTopWorld);

            float tail = levelUpBubble != null ? levelUpBubble.PointerLength : 0f;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            Vector2 parentCenter = parent.rect.center;
            rect.anchoredPosition = new Vector2(cardTop.x - parentCenter.x, cardTop.y + tail - parentCenter.y);
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
