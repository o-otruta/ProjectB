using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Player;

namespace ProjectB.UI
{
    public class UltimateButtonUI : MonoBehaviour
    {
        private static readonly string[] CachedTenthsStrings = BuildTenthsCache(151);
        private static readonly string[] CachedSecondsStrings = BuildSecondsCache(121);

        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownFillImage;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Sprint 13")]
        [Tooltip("Pulsing glow ring shown while the ultimate is ready.")]
        [SerializeField] private GameObject readyGlow;
        [SerializeField] private bool playReadySfx = true;
        [SerializeField] private KeyHintBadgeUI keyHintBadge;

        private HeroUltimate heroUltimate;
        private int lastDisplayedState = -1; // 0 = ready, 1 = active, 2 = cooldown
        private int lastDisplayedValue = int.MinValue;
        private bool wasReady;
        private bool readyInitialized;
        private string cachedReadyLabel = "READY";
        private Action cachedLanguageChanged;

        private static string[] BuildTenthsCache(int count)
        {
            var arr = new string[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = $"{(i / 10)}.{(i % 10)}s";
            }
            return arr;
        }

        private static string[] BuildSecondsCache(int count)
        {
            var arr = new string[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = $"{i}s";
            }
            return arr;
        }

        public static string GetCachedTenthsText(int tenths)
        {
            if (tenths >= 0 && tenths < CachedTenthsStrings.Length)
            {
                return CachedTenthsStrings[tenths];
            }
            return $"{(tenths / 10f):0.0}s";
        }

        public static string GetCachedSecondsText(int seconds)
        {
            if (seconds >= 0 && seconds < CachedSecondsStrings.Length)
            {
                return CachedSecondsStrings[seconds];
            }
            return $"{seconds}s";
        }

        [Inject]
        public void Construct(HeroUltimate heroUltimate)
        {
            this.heroUltimate = heroUltimate;
        }

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnEnable()
        {
            cachedReadyLabel = Loc.Get("ui.hud.ready", "READY");
            cachedLanguageChanged ??= HandleLanguageChanged;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
        }

        private void HandleLanguageChanged()
        {
            cachedReadyLabel = Loc.Get("ui.hud.ready", "READY");
            InitializeVisuals();
            lastDisplayedState = -1;
            RefreshVisuals(force: true);
        }

        private void Start()
        {
            cachedReadyLabel = Loc.Get("ui.hud.ready", "READY");
            InitializeVisuals();
            RefreshVisuals(force: true);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        private void InitializeVisuals()
        {
            if (heroUltimate == null) return;

            var data = heroUltimate.ActiveHeroData;
            if (data != null)
            {
                string ultName = Loc.UltimateName(data);
                if (labelText != null && !string.IsNullOrEmpty(ultName))
                {
                    labelText.text = ultName.ToUpperInvariant();
                }

                if (iconImage != null)
                {
                    bool hasIcon = data.ultimateIcon != null;
                    iconImage.gameObject.SetActive(hasIcon);
                    if (hasIcon) iconImage.sprite = data.ultimateIcon;
                }
            }
        }

        private void Update()
        {
            if (heroUltimate == null) return;
            RefreshVisuals(force: false);
        }

        private void HandleClick()
        {
            heroUltimate?.TryActivate();
            RefreshVisuals(force: true);
        }

        private void SetReadyGlow(bool ready)
        {
            if (readyGlow != null && readyGlow.activeSelf != ready) readyGlow.SetActive(ready);

            if (readyInitialized && ready && !wasReady && Application.isPlaying)
            {
                if (playReadySfx) UiAudio.Play(ProjectB.Data.SfxType.UltReady);
                UiTween.Punch(transform, 0.18f, 0.3f);
            }
            wasReady = ready;
            readyInitialized = true;
        }

        private void RefreshVisuals(bool force)
        {
            if (heroUltimate == null) return;

            if (heroUltimate.IsActive)
            {
                float ratio = Mathf.Clamp01(heroUltimate.ActiveRemaining / heroUltimate.ActiveDuration);
                if (cooldownFillImage != null)
                {
                    if (!cooldownFillImage.gameObject.activeSelf) cooldownFillImage.gameObject.SetActive(true);
                    cooldownFillImage.fillAmount = ratio;
                    if (lastDisplayedState != 1) cooldownFillImage.color = UiTheme.WithAlpha(UiTheme.Current.gold, 0.45f);
                }

                int tenths = Mathf.CeilToInt(heroUltimate.ActiveRemaining * 10f);
                if (force || lastDisplayedState != 1 || lastDisplayedValue != tenths)
                {
                    lastDisplayedState = 1;
                    lastDisplayedValue = tenths;
                    if (button != null) button.interactable = false;
                    if (backgroundImage != null) backgroundImage.color = UiTheme.Current.gold;
                    SetReadyGlow(false);
                    if (statusText != null) statusText.text = GetCachedTenthsText(tenths);
                }
            }
            else if (heroUltimate.CooldownRemaining > 0f)
            {
                float ratio = Mathf.Clamp01(heroUltimate.CooldownRemaining / heroUltimate.CooldownDuration);
                if (cooldownFillImage != null)
                {
                    if (!cooldownFillImage.gameObject.activeSelf) cooldownFillImage.gameObject.SetActive(true);
                    cooldownFillImage.fillAmount = ratio;
                    if (lastDisplayedState != 2) cooldownFillImage.color = UiTheme.WithAlpha(UiTheme.Current.surfaceSunken, 0.72f);
                }

                int seconds = Mathf.CeilToInt(heroUltimate.CooldownRemaining);
                if (force || lastDisplayedState != 2 || lastDisplayedValue != seconds)
                {
                    lastDisplayedState = 2;
                    lastDisplayedValue = seconds;
                    if (button != null) button.interactable = false;
                    if (backgroundImage != null) backgroundImage.color = UiTheme.Current.surfaceRaised;
                    SetReadyGlow(false);
                    if (statusText != null) statusText.text = GetCachedSecondsText(seconds);
                }
            }
            else
            {
                if (cooldownFillImage != null)
                {
                    cooldownFillImage.fillAmount = 0f;
                    cooldownFillImage.gameObject.SetActive(false);
                }

                bool ready = heroUltimate.IsReady;
                int readyFlag = ready ? 1 : 0;
                if (force || lastDisplayedState != 0 || lastDisplayedValue != readyFlag)
                {
                    lastDisplayedState = 0;
                    lastDisplayedValue = readyFlag;
                    if (button != null) button.interactable = ready;
                    if (backgroundImage != null) backgroundImage.color = ready ? UiTheme.Current.accent : UiTheme.Current.surfaceRaised;
                    if (statusText != null) statusText.text = ready ? string.Empty : cachedReadyLabel;
                    SetReadyGlow(ready);
                }
            }
        }
    }
}
