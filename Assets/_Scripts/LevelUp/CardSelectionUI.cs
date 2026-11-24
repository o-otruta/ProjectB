using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Ads;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.UI;

namespace ProjectB.LevelUp
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject selectionPanel;
        [SerializeField] private CardUI[] cardSlots;
        [SerializeField] private int freeCardsCount = 2;

        [Header("Reroll")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollButtonText;

        [Header("Sprint 13: header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private CanvasGroup panelGroup;

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
        private readonly char[] levelBuffer = new char[16];

        private int freeRerollsAvailable;
        private bool hasRerolledThisLevelUp;
        private bool isAnimatingCards;
        private float animationElapsed;

        public bool IsActive => selectionPanel != null && selectionPanel.activeSelf;

        public bool CanRerollCurrentOffer =>
            onRerollCallback != null
            && !hasRerolledThisLevelUp
            && (freeRerollsAvailable > 0 || (adManager != null && adManager.CanShowRewarded()));

        public string RerollButtonLabel
        {
            get
            {
                if (onRerollCallback == null) return string.Empty;
                if (hasRerolledThisLevelUp) return "Rerolled";
                if (freeRerollsAvailable > 0) return $"Reroll · Free ×{freeRerollsAvailable}";
                if (adManager != null && adManager.CanShowRewarded()) return "<sprite name=\"ad\"> Reroll";
                return "Reroll unavailable";
            }
        }

        [Inject]
        public void Construct(AdManager adManager = null, HeroAbilities heroAbilities = null, AudioManager audioManager = null, HeroExperience heroExperience = null)
        {
            this.adManager = adManager;
            this.heroAbilities = heroAbilities;
            this.audioManager = audioManager;
            this.heroExperience = heroExperience;
        }

        private void Awake()
        {
            if (rerollButton != null)
            {
                rerollButton.onClick.AddListener(OnRerollClicked);
            }
            Hide();
        }

        private void OnDestroy()
        {
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveListener(OnRerollClicked);
            }
        }

        private void Update()
        {
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
        }

        private void RefreshHeader()
        {
            if (titleText != null) titleText.text = "LEVEL UP!";
            if (levelText != null && heroExperience != null)
            {
                int len = UiFormat.WriteString(levelBuffer, 0, "Level ");
                len = UiFormat.WriteInt(levelBuffer, len, heroExperience.CurrentLevel);
                levelText.SetCharArray(levelBuffer, 0, len);
            }
        }

        public void Hide()
        {
            isAnimatingCards = false;
            ResetSlotScales();

            if (selectionPanel != null)
                selectionPanel.SetActive(false);
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
                        return "NEW PASSIVE";
                    }
                    if (abilityCard.abilityData != null && abilityCard.abilityData.type == AbilityType.Active)
                    {
                        badgeColor = new Color(0.86f, 0.44f, 0.14f, 1f);
                        return "NEW ACTIVE";
                    }
                    badgeColor = new Color(0.18f, 0.72f, 0.32f, 1f);
                    return "NEW!";

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
                            return $"Lv {cur} -> Lv {next} {tierLabel}";
                        }
                        return $"Lv {cur} -> Lv {next}";
                    }
                    return "UPGRADE";

                case SynergyCardData:
                    badgeColor = new Color(0.92f, 0.68f, 0.12f, 1f);
                    return "SYNERGY!";

                case StatUpgradeCardData:
                    badgeColor = new Color(0.45f, 0.35f, 0.72f, 1f);
                    return "STAT BOOST";

                default:
                    badgeColor = Color.clear;
                    return string.Empty;
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

            bool canWatchAd = adManager != null && adManager.CanShowRewarded();
            int count = currentCards != null ? currentCards.Count : 0;

            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null) continue;

                if (i < count)
                {
                    int slotIndex = i;
                    bool requiresAd = slotIndex >= freeCardsCount;
                    CardData cardData = currentCards[slotIndex];
                    string badgeLabel = FormatLevelBadge(cardData, heroAbilities, out Color badgeColor);

                    cardSlots[i].gameObject.SetActive(true);
                    cardSlots[i].Setup(
                        cardData,
                        clickedCard => OnCardClicked(slotIndex, clickedCard),
                        requiresAd,
                        canWatchAd,
                        badgeLabel,
                        badgeColor);
                }
                else
                {
                    cardSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void RefreshRerollButton()
        {
            bool showButton = onRerollCallback != null;
            if (rerollButton != null)
            {
                rerollButton.gameObject.SetActive(showButton);
                if (showButton)
                {
                    rerollButton.interactable = CanRerollCurrentOffer;
                }
            }

            if (rerollButtonText != null && showButton)
            {
                rerollButtonText.text = RerollButtonLabel;
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
            if (onRerollCallback == null || hasRerolledThisLevelUp) return;

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
            }
            RefreshRerollButton();
        }

        private void OnCardClicked(int slotIndex, CardData card)
        {
            if (onCardSelectedCallback == null) return;

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
            onCardSelectedCallback = null; // Защита от двойного клика (мультитач на смартфонах)
            onRerollCallback = null;
            currentCards = null;
            freeRerollsAvailable = 0;
            hasRerolledThisLevelUp = false;

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