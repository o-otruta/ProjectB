using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Ads;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.Data.Platform;
using ProjectB.UI;

namespace ProjectB.LevelUp
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private CardUI[] cardSlots;
        [SerializeField] private int freeCardsCount = 3;

        [Header("Reroll")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollButtonText;
        [SerializeField] private KeyHintBadgeUI rerollKeyHint;
        [SerializeField] private GameObject rerollFreeBadge;
        [SerializeField] private TMP_Text rerollFreeBadgeText;
        [SerializeField] private Image rerollIcon;

        [Header("Sprint 13: header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private CanvasGroup panelGroup;

        [Header("UI redesign: current build strip")]
        [SerializeField] private LevelUpBuildBarUI buildBar;

        [Header("Animation")]
        [SerializeField] private float cardBounceDuration = 0.28f;
        [SerializeField] private float cardStaggerDelay = 0.06f;
        [SerializeField] private float cardOvershootScale = 1.08f;
        [SerializeField] private float selectFeedbackDuration = 0.18f;

        private System.Action<CardData> onCardSelectedCallback;
        private System.Func<List<CardData>, bool, List<CardData>> onRerollCallback;
        private List<CardData> currentCards;
        private AdManager adManager;
        private HeroAbilities heroAbilities;
        private AudioManager audioManager;
        private HeroExperience heroExperience;
        private GameInputService gameInput;
        private InputModeService inputModeService;
        private InputConfig inputConfig;
        private GameEventBus eventBus;
        private System.Func<float> unscaledTimeProvider;
        private System.Func<bool> cachedIsInputLockedCheck;
        private System.Action<InputMode> cachedInputModeHandler;
        private readonly char[] levelBuffer = new char[16];

        private int freeRerollsAvailable;
        private bool hasRerolledThisLevelUp;
        private bool isAnimatingCards;
        private float animationElapsed;
        private float inputUnlockTime;
        private bool pendingInitialFocus;

        public event System.Action OnCardsShown;
        public event System.Action OnCardChosen;

        public bool IsActive => selectionPanel != null && selectionPanel.activeSelf;
        public bool IsInputLocked => IsActive && GetUnscaledTime() < inputUnlockTime;
        public IReadOnlyList<CardData> CurrentOfferedCards => currentCards;
        public int FreeSlotsCount => freeCardsCount;

        /// <summary>Card slot view by index (null if out of range); used by onboarding to point at the first card.</summary>
        public CardUI GetCardSlot(int index)
        {
            return cardSlots != null && index >= 0 && index < cardSlots.Length ? cardSlots[index] : null;
        }
        public int FreeRerollsAvailable => freeRerollsAvailable;
        public bool HasRerolledCurrentOffer => hasRerolledThisLevelUp;
        public bool CanFreeReroll =>
            IsActive
            && onRerollCallback != null
            && !hasRerolledThisLevelUp
            && freeRerollsAvailable > 0;

        public bool CanRerollCurrentOffer =>
            onRerollCallback != null
            && !hasRerolledThisLevelUp
            && (freeRerollsAvailable > 0 || (adManager != null && adManager.CanShowRewarded()));

        public string RerollButtonLabel
        {
            get
            {
                if (onRerollCallback == null) return string.Empty;
                if (hasRerolledThisLevelUp) return Loc.Get("ui.levelup.rerolled", "Rerolled");
                if (freeRerollsAvailable > 0) return Loc.Format("ui.levelup.reroll_free", freeRerollsAvailable);
                if (adManager != null && adManager.CanShowRewarded()) return Loc.Get("ui.levelup.reroll_ad", "<sprite name=\"ad\"> Reroll");
                return Loc.Get("ui.levelup.reroll_unavailable", "Reroll unavailable");
            }
        }

        [Inject]
        public void Construct(
            AdManager adManager = null,
            HeroAbilities heroAbilities = null,
            AudioManager audioManager = null,
            HeroExperience heroExperience = null,
            GameInputService gameInput = null,
            InputModeService inputModeService = null,
            InputConfig inputConfig = null,
            GameEventBus eventBus = null)
        {
            UnsubscribeInputMode();
            this.adManager = adManager;
            this.heroAbilities = heroAbilities;
            this.audioManager = audioManager;
            this.heroExperience = heroExperience;
            this.gameInput = gameInput;
            this.inputModeService = inputModeService;
            if (inputConfig != null) this.inputConfig = inputConfig;
            if (eventBus != null) this.eventBus = eventBus;
            SubscribeInputMode();
        }

        public void SetInputConfig(InputConfig config) => inputConfig = config;
        public void SetUnscaledTimeProviderForTest(System.Func<float> provider) => unscaledTimeProvider = provider;

        private void Awake()
        {
            cachedIsInputLockedCheck ??= IsInputLockedCheck;
            if (inputConfig == null && Application.isPlaying)
            {
                RootServices.TryResolve(out inputConfig);
            }
            if (rerollButton != null)
            {
                rerollButton.onClick.AddListener(OnRerollClicked);
            }
            Hide();
        }

        private void OnDestroy()
        {
            UnsubscribeInputMode();
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveListener(OnRerollClicked);
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
            else if (IsActive && !IsInputLocked && UnityEngine.EventSystems.EventSystem.current != null &&
                     UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null)
            {
                FocusFirstCard();
            }
        }

        private void Update()
        {
            TickInputLock();

            if (IsActive && !IsInputLocked && onCardSelectedCallback != null && gameInput != null)
            {
                if (gameInput.WasCardPressedThisFrame(0)) TrySelectSlotByIndex(0);
                else if (gameInput.WasCardPressedThisFrame(1)) TrySelectSlotByIndex(1);
                else if (gameInput.WasCardPressedThisFrame(2)) TrySelectSlotByIndex(2);
                else if (gameInput.WasRerollPressedThisFrame() && CanRerollCurrentOffer) OnRerollClicked();
            }

            if (!isAnimatingCards || !IsActive || cardSlots == null) return;

            animationElapsed += Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.05f, cardBounceDuration);
            bool allDone = true;

            for (int i = 0; i < cardSlots.Length; i++)
            {
                var slot = cardSlots[i];
                if (slot == null || !slot.gameObject.activeSelf) continue;

                float startTime = i * Mathf.Max(0f, cardStaggerDelay);
                float localTime = animationElapsed - startTime;
                float t = Mathf.Clamp01(localTime / duration);

                if (t < 1f)
                {
                    allDone = false;
                }

                float scale = EvaluateBounceScale(t, cardOvershootScale);
                slot.transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (allDone)
            {
                isAnimatingCards = false;
                ResetSlotScales();
            }
        }

        public void ShowCards(
            List<CardData> selectedCards,
            System.Action<CardData> onSelected,
            System.Func<List<CardData>, bool, List<CardData>> onReroll = null,
            int freeRerolls = 0)
        {
            onCardSelectedCallback = onSelected;
            onRerollCallback = onReroll;
            currentCards = selectedCards;
            freeRerollsAvailable = Mathf.Max(0, freeRerolls);
            hasRerolledThisLevelUp = false;

            RefreshSlots();
            RefreshRerollButton();
            RefreshHeader();
            if (buildBar != null) buildBar.Refresh(heroAbilities);

            if (selectionPanel != null)
                selectionPanel.SetActive(true);

            if (panelGroup != null)
            {
                panelGroup.interactable = true;
                UiTween.Fade(panelGroup, 0f, 1f, UiTheme.Current.durationNormal);
            }
            if (titleText != null && Application.isPlaying)
            {
                UiTween.Scale(titleText.transform, Vector3.one * 0.6f, Vector3.one, UiTheme.Current.durationSlow, 0f, UiEase.OutBack);
            }

            StartBounceAnimation();
            ArmInputLock();
            PublishOfferShown(isReroll: false);
            OnCardsShown?.Invoke();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Automation-only card pick: selects a free slot immediately, bypassing input lock and feedback delay.
        /// Never picks ad-gated slots (>= freeCardsCount).
        /// </summary>
        public bool SelectCardForAutomation(int slotIndex)
        {
            if (!IsActive || onCardSelectedCallback == null || currentCards == null) return false;
            if (slotIndex < 0 || slotIndex >= freeCardsCount || slotIndex >= currentCards.Count) return false;

            CardData card = currentCards[slotIndex];
            if (card == null) return false;

            var callback = onCardSelectedCallback;
            PublishCardSelected(card);
            onCardSelectedCallback = null;
            onRerollCallback = null;
            currentCards = null;
            freeRerollsAvailable = 0;
            hasRerolledThisLevelUp = false;

            OnCardChosen?.Invoke();
            Hide();
            callback.Invoke(card);
            return true;
        }

        /// <summary>
        /// Automation-only free reroll: consumes a free reroll if available, bypassing input lock.
        /// Never uses rewarded ad rerolls.
        /// </summary>
        public bool TryFreeRerollForAutomation()
        {
            if (!CanFreeReroll) return false;
            ExecuteReroll(usedFreeReroll: true);
            return true;
        }
#endif

        private void PublishOfferShown(bool isReroll)
        {
            if (eventBus == null || currentCards == null) return;
            int count = currentCards.Count;
            CardData c0 = count > 0 ? currentCards[0] : null;
            CardData c1 = count > 1 ? currentCards[1] : null;
            CardData c2 = count > 2 ? currentCards[2] : null;
            int level = heroExperience != null ? heroExperience.CurrentLevel : 1;
            eventBus.Publish(new CardOfferShownEvent(level, c0, c1, c2, count, freeCardsCount, isReroll));
        }

        private void PublishCardSelected(CardData selectedCard)
        {
            if (eventBus == null) return;
            int count = currentCards != null ? currentCards.Count : 0;
            CardData c0 = count > 0 ? currentCards[0] : null;
            CardData c1 = count > 1 ? currentCards[1] : null;
            CardData c2 = count > 2 ? currentCards[2] : null;
            int level = heroExperience != null ? heroExperience.CurrentLevel : 1;
            eventBus.Publish(new CardSelectedEvent(level, selectedCard, c0, c1, c2, count, freeCardsCount, hasRerolledThisLevelUp));
        }

        public bool TrySelectSlotByIndex(int slotIndex)
        {
            if (IsInputLocked || !IsActive || onCardSelectedCallback == null || cardSlots == null || currentCards == null) return false;
            if (slotIndex < 0 || slotIndex >= cardSlots.Length || slotIndex >= currentCards.Count) return false;

            CardUI slot = cardSlots[slotIndex];
            if (slot == null || !slot.gameObject.activeSelf) return false;

            slot.Activate();
            return true;
        }

        public void TickInputLock()
        {
            if (!IsActive) return;
            if (pendingInitialFocus && !IsInputLocked)
            {
                pendingInitialFocus = false;
                FocusFirstCard();
            }
        }

        private bool IsInputLockedCheck() => IsInputLocked;

        private float GetUnscaledTime() => unscaledTimeProvider != null ? unscaledTimeProvider() : Time.unscaledTime;

        private float ResolveInputDelay()
        {
            if (inputConfig != null) return Mathf.Max(0f, inputConfig.cardSelectInputDelay);
            return Application.isPlaying ? 0.4f : 0f;
        }

        private void ArmInputLock()
        {
            float delay = ResolveInputDelay();
            UiFocusHelper.ClearSelection();
            if (delay > 0f)
            {
                inputUnlockTime = GetUnscaledTime() + delay;
                pendingInitialFocus = true;
            }
            else
            {
                inputUnlockTime = 0f;
                pendingInitialFocus = false;
                FocusFirstCard();
            }
        }

        private void FocusFirstCard()
        {
            if (cardSlots == null) return;
            for (int i = 0; i < cardSlots.Length; i++)
            {
                CardUI slot = cardSlots[i];
                if (slot == null || !slot.gameObject.activeSelf || !slot.IsInteractable) continue;
                Button btn = slot.CardButton;
                if (btn != null && btn.interactable)
                {
                    UiFocusHelper.SetFirstSelected(btn, inputModeService);
                    return;
                }
            }
        }

        private void RefreshHeader()
        {
            if (titleText != null) titleText.text = Loc.Get("ui.levelup.title", "LEVEL UP!");
            if (levelText != null && heroExperience != null)
            {
                int lv = heroExperience.CurrentLevel;
                if (LocalizationService.Current.ActiveEntries.ContainsKey("ui.levelup.subtitle"))
                {
                    levelText.text = Loc.Format("ui.levelup.subtitle", lv);
                }
                else
                {
                    int len = UiFormat.WriteString(levelBuffer, 0, Loc.Get("ui.levelup.level_prefix", "Level "));
                    len = UiFormat.WriteInt(levelBuffer, len, lv);
                    levelText.SetCharArray(levelBuffer, 0, len);
                }
            }
        }

        public void Hide()
        {
            isAnimatingCards = false;
            pendingInitialFocus = false;
            inputUnlockTime = 0f;
            ResetSlotScales();

            if (selectionPanel != null)
                selectionPanel.SetActive(false);

            UiFocusHelper.ClearSelection();
        }

        public void TriggerReroll()
        {
            OnRerollClicked();
        }

        public static string FormatLevelBadge(CardData card, HeroAbilities heroAbilities, out Color badgeColor)
        {
            switch (card)
            {
                case AbilityCardData abilityCard:
                    if (abilityCard.abilityData != null && abilityCard.abilityData.type == AbilityType.Passive)
                    {
                        badgeColor = new Color(0.14f, 0.64f, 0.76f, 1f);
                        return Loc.Get("ui.card.new_passive", "NEW PASSIVE");
                    }
                    if (abilityCard.abilityData != null && abilityCard.abilityData.type == AbilityType.Active)
                    {
                        badgeColor = new Color(0.86f, 0.44f, 0.14f, 1f);
                        return Loc.Get("ui.card.new_active", "NEW ACTIVE");
                    }
                    badgeColor = new Color(0.18f, 0.72f, 0.32f, 1f);
                    return Loc.Get("ui.card.new", "NEW!");

                case AbilityModifierCardData modCard:
                    badgeColor = new Color(0.16f, 0.56f, 0.88f, 1f);
                    int cur = heroAbilities != null ? heroAbilities.GetAbilityLevel(modCard.targetAbilityId) : 0;
                    if (cur > 0)
                    {
                        int next = cur + 1;
                        if (next % ActiveAbility.OVERDRIVE_LEVEL_INTERVAL == 0 &&
                            heroAbilities.GetActiveAbilityById(modCard.targetAbilityId) != null)
                        {
                            badgeColor = new Color(0.95f, 0.72f, 0.14f, 1f);
                            string tierLabel = ActiveAbility.GetOverdriveTierLabel(ActiveAbility.CalculateOverdriveTier(next));
                            return Loc.Format("ui.card.lv_progress_od", cur, next, tierLabel);
                        }
                        if (next >= heroAbilities.MaxAbilityLevel)
                        {
                            // Last upgrade before the Constellation level cap (Path of Mastery raises it).
                            badgeColor = new Color(0.86f, 0.30f, 0.30f, 1f);
                            return Loc.Format("ui.card.lv_progress_od", cur, next, Loc.Get("ui.card.max", "MAX"));
                        }
                        return Loc.Format("ui.card.lv_progress", cur, next);
                    }
                    return Loc.Get("ui.card.upgrade", "UPGRADE");

                case StatUpgradeCardData:
                    badgeColor = new Color(0.45f, 0.35f, 0.72f, 1f);
                    return Loc.Get("ui.card.stat_boost", "STAT BOOST");

                default:
                    badgeColor = Color.clear;
                    return string.Empty;
            }
        }

        /// <summary>
        /// Level progress a card grants: a new ability goes 0 → 1, an ability modifier cur → cur + 1, both against the
        /// current ability level cap. Stat / weapon cards have no ability level (cap 0).
        /// </summary>
        public static void GetLevelProgress(CardData card, HeroAbilities heroAbilities, out int current, out int next, out int cap)
        {
            current = 0;
            next = 0;
            cap = 0;
            int maxLevel = heroAbilities != null ? heroAbilities.MaxAbilityLevel : 0;
            switch (card)
            {
                case AbilityCardData:
                    current = 0;
                    next = 1;
                    cap = maxLevel;
                    break;
                case AbilityModifierCardData modCard:
                    current = heroAbilities != null ? heroAbilities.GetAbilityLevel(modCard.targetAbilityId) : 0;
                    if (current <= 0) return;
                    next = current + 1;
                    cap = maxLevel;
                    break;
            }
        }

        public static float EvaluateBounceScale(float normalizedTime, float overshoot = 1.08f)
        {
            float t = Mathf.Clamp01(normalizedTime);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            const float peakT = 0.72f;
            if (t < peakT)
            {
                float u = t / peakT;
                float inv = 1f - u;
                return (1f - inv * inv * inv) * overshoot;
            }

            float settleU = (t - peakT) / (1f - peakT);
            float smooth = settleU * settleU * (3f - 2f * settleU);
            return Mathf.Lerp(overshoot, 1f, smooth);
        }

        private void RefreshSlots()
        {
            if (cardSlots == null) return;

            bool adsSupported = adManager == null || adManager.IsAdsAvailable;
            bool canWatchAd = adManager != null && adManager.CanShowRewarded();
            int count = currentCards != null ? currentCards.Count : 0;

            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null) continue;

                bool requiresAd = i >= freeCardsCount;
                if (requiresAd && !adsSupported)
                {
                    cardSlots[i].gameObject.SetActive(false);
                    continue;
                }

                if (i < count)
                {
                    int slotIndex = i;
                    CardData cardData = currentCards[slotIndex];
                    string badgeLabel = FormatLevelBadge(cardData, heroAbilities, out Color badgeColor);

                    cachedIsInputLockedCheck ??= IsInputLockedCheck;
                    cardSlots[i].gameObject.SetActive(true);
                    cardSlots[i].Setup(
                        cardData,
                        clickedCard => OnCardClicked(slotIndex, clickedCard),
                        requiresAd,
                        canWatchAd,
                        badgeLabel,
                        badgeColor,
                        cachedIsInputLockedCheck);
                    GetLevelProgress(cardData, heroAbilities, out int current, out int next, out int cap);
                    cardSlots[i].SetLevelProgress(current, next, cap);
                }
                else
                {
                    cardSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void RefreshRerollButton()
        {
            bool adsSupported = adManager == null || adManager.IsAdsAvailable;
            bool showButton = onRerollCallback != null && (freeRerollsAvailable > 0 || adsSupported);
            if (rerollButton != null)
            {
                rerollButton.gameObject.SetActive(showButton);
                if (showButton)
                {
                    rerollButton.interactable = CanRerollCurrentOffer;
                }
            }

            bool hasFreeBadge = rerollFreeBadge != null;
            bool showFree = showButton && !hasRerolledThisLevelUp && freeRerollsAvailable > 0;
            if (hasFreeBadge) rerollFreeBadge.SetActive(showFree);
            if (rerollIcon != null)
            {
                rerollIcon.gameObject.SetActive(showButton && !hasRerolledThisLevelUp);
            }
            if (rerollFreeBadgeText != null && showFree)
            {
                rerollFreeBadgeText.text = freeRerollsAvailable == 1
                    ? Loc.Get("ui.levelup.free_badge", "FREE")
                    : Loc.Format("ui.levelup.free_badge_multi", freeRerollsAvailable);
            }

            if (rerollButtonText != null && showButton)
            {
                if (!hasFreeBadge)
                {
                    rerollButtonText.text = RerollButtonLabel;
                }
                else if (hasRerolledThisLevelUp)
                {
                    rerollButtonText.text = Loc.Get("ui.levelup.rerolled", "Rerolled");
                }
                else if (freeRerollsAvailable > 0)
                {
                    rerollButtonText.text = Loc.Get("ui.levelup.reroll_cards", "Reroll cards");
                }
                else if (adManager != null && adManager.CanShowRewarded())
                {
                    rerollButtonText.text = Loc.Get("ui.levelup.reroll_ad", "<sprite name=\"ad\"> Reroll");
                }
                else
                {
                    rerollButtonText.text = Loc.Get("ui.levelup.reroll_unavailable", "Reroll unavailable");
                }
            }
            if (rerollKeyHint != null)
            {
                rerollKeyHint.SetAllowed(showButton && CanRerollCurrentOffer);
            }
        }

        private void StartBounceAnimation()
        {
            if (cardSlots == null)
            {
                isAnimatingCards = false;
                return;
            }

            if (!Application.isPlaying)
            {
                isAnimatingCards = false;
                ResetSlotScales();
                return;
            }

            isAnimatingCards = true;
            animationElapsed = 0f;
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null && cardSlots[i].gameObject.activeSelf)
                {
                    cardSlots[i].transform.localScale = Vector3.zero;
                }
            }
        }

        private void ResetSlotScales()
        {
            if (cardSlots == null) return;
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null)
                {
                    cardSlots[i].transform.localScale = Vector3.one;
                }
            }
        }

        private void OnRerollClicked()
        {
            if (IsInputLocked || onRerollCallback == null || hasRerolledThisLevelUp) return;

            if (freeRerollsAvailable > 0)
            {
                audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
                ExecuteReroll(usedFreeReroll: true);
                return;
            }

            if (adManager != null && adManager.CanShowRewarded())
            {
                audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
                adManager.ShowRewarded(
                    AdPlacement.LevelUpReroll,
                    onRewardGranted: () => ExecuteReroll(usedFreeReroll: false),
                    onFailed: RefreshRerollButton);
                return;
            }

            RefreshRerollButton();
        }

        private void ExecuteReroll(bool usedFreeReroll)
        {
            if (onRerollCallback == null || hasRerolledThisLevelUp) return;

            List<CardData> rerolled = onRerollCallback.Invoke(currentCards, usedFreeReroll);
            if (usedFreeReroll && freeRerollsAvailable > 0)
            {
                freeRerollsAvailable--;
            }

            hasRerolledThisLevelUp = true;
            if (rerolled != null && rerolled.Count > 0)
            {
                currentCards = rerolled;
                RefreshSlots();
                StartBounceAnimation();
                ArmInputLock();
                PublishOfferShown(isReroll: true);
            }
            RefreshRerollButton();
        }

        private void OnCardClicked(int slotIndex, CardData card)
        {
            if (IsInputLocked || onCardSelectedCallback == null) return;

            bool requiresAd = slotIndex >= freeCardsCount;
            if (!requiresAd)
            {
                audioManager?.PlaySfx(SfxType.UiClick, randomizePitch: false);
                CompleteSelection(card);
                return;
            }

            if (adManager == null || !adManager.CanShowRewarded())
            {
                RefreshSlots();
                return;
            }

            adManager.ShowRewarded(
                AdPlacement.LevelUpThirdCard,
                onRewardGranted: () => CompleteSelection(card),
                onFailed: RefreshSlots);
        }

        private void CompleteSelection(CardData card)
        {
            if (onCardSelectedCallback == null) return;

            var callback = onCardSelectedCallback;
            PublishCardSelected(card);
            onCardSelectedCallback = null; // Защита от двойного клика (мультитач на смартфонах)
            onRerollCallback = null;
            currentCards = null;
            freeRerollsAvailable = 0;
            hasRerolledThisLevelUp = false;

            OnCardChosen?.Invoke();

            if (Application.isPlaying && isActiveAndEnabled && selectFeedbackDuration > 0f)
            {
                PlaySelectFeedback(card);
                StartCoroutine(FinishSelectionAfterFeedback(callback, card));
                return;
            }

            Hide();
            callback.Invoke(card);
        }

        /// <summary>Chosen card "punches", the others fade (Sprint 13.4).</summary>
        private void PlaySelectFeedback(CardData card)
        {
            isAnimatingCards = false;
            ResetSlotScales();
            if (panelGroup != null) panelGroup.interactable = false;
            if (cardSlots == null) return;
            for (int i = 0; i < cardSlots.Length; i++)
            {
                CardUI slot = cardSlots[i];
                if (slot == null || !slot.gameObject.activeSelf) continue;
                if (slot.CurrentData == card) UiTween.Punch(slot.transform, 0.1f, selectFeedbackDuration);
                else slot.SetDimmed(true);
            }
        }

        private System.Collections.IEnumerator FinishSelectionAfterFeedback(System.Action<CardData> callback, CardData card)
        {
            yield return new WaitForSecondsRealtime(selectFeedbackDuration);
            Hide();
            callback.Invoke(card);
        }
    }
}