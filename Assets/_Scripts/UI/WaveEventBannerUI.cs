using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data.Enemies;
using ProjectB.Enemies;

namespace ProjectB.UI
{
    /// <summary>
    /// Portrait HUD banner announcing active Wave Events with countdown timer or Elite Ambush progress.
    /// Zero GC allocations during Update.
    /// </summary>
    public class WaveEventBannerUI : MonoBehaviour
    {
        private readonly string[] cachedSecondsLabels = new string[61];
        private readonly string[] cachedAmbushLabels =
        {
            "CLEARED!",
            "1/3 LEFT",
            "2/3 LEFT",
            "3/3 LEFT"
        };


        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Outline borderOutline;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image progressFillImage;

        private WaveEventManager waveEventManager;
        private GameEventBus eventBus;
        private bool isSubscribed;
        private WaveEventType activeType = WaveEventType.None;
        private float punchScale = 1f;
        private int lastDisplayedSeconds = -1;
        private int lastDisplayedRemainingElites = -1;
        private Action cachedLanguageChanged;

        public string GetCachedSecondsLabel(int seconds)
        {
            if (string.IsNullOrEmpty(cachedSecondsLabels[0])) RefreshLocalizedLabels();
            if (seconds >= 0 && seconds < cachedSecondsLabels.Length)
            {
                return cachedSecondsLabels[seconds];
            }
            return cachedSecondsLabels[cachedSecondsLabels.Length - 1];
        }

        private void RefreshLocalizedLabels()
        {
            string secUnit = Loc.Get("ui.unit.seconds", "s");
            for (int i = 0; i < cachedSecondsLabels.Length; i++)
            {
                cachedSecondsLabels[i] = $"{i}{secUnit}";
            }
            cachedAmbushLabels[0] = Loc.Get("event.ambush.cleared", "CLEARED!");
            cachedAmbushLabels[1] = Loc.Format("event.ambush.left", 1);
            cachedAmbushLabels[2] = Loc.Format("event.ambush.left", 2);
            cachedAmbushLabels[3] = Loc.Format("event.ambush.left", 3);
        }

        private void HandleLanguageChanged()
        {
            RefreshLocalizedLabels();
            lastDisplayedSeconds = -1;
            lastDisplayedRemainingElites = -1;
        }

        [Inject]
        public void Construct(WaveEventManager waveEventManager, GameEventBus eventBus)
        {
            this.waveEventManager = waveEventManager;
            this.eventBus = eventBus;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            var graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = false;
                }
            }
            Hide();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            RefreshLocalizedLabels();
            cachedLanguageChanged ??= HandleLanguageChanged;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;

            if (eventBus != null && !isSubscribed)
            {
                eventBus.Subscribe<WaveEventStartedEvent>(OnWaveEventStarted);
                eventBus.Subscribe<WaveEventEndedEvent>(OnWaveEventEnded);
                eventBus.Subscribe<GameOverEvent>(OnGameOver);
                isSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
            if (eventBus != null && isSubscribed)
            {
                eventBus.Unsubscribe<WaveEventStartedEvent>(OnWaveEventStarted);
                eventBus.Unsubscribe<WaveEventEndedEvent>(OnWaveEventEnded);
                eventBus.Unsubscribe<GameOverEvent>(OnGameOver);
                isSubscribed = false;
            }
        }

        private void OnWaveEventStarted(WaveEventStartedEvent evt)
        {
            activeType = evt.EventType;
            lastDisplayedSeconds = -1;
            lastDisplayedRemainingElites = -1;
            punchScale = 1.12f;

            if (bannerRoot != null)
            {
                bannerRoot.SetActive(true);
                bannerRoot.transform.localScale = new Vector3(punchScale, punchScale, 1f);
            }

            Color accent = GetEventAccentColor(evt.EventType);
            if (borderOutline != null) borderOutline.effectColor = accent;
            if (backgroundImage != null)
            {
                UiTheme theme = UiTheme.Current;
                backgroundImage.color = evt.EventType == WaveEventType.BloodMoon
                    ? UiTheme.WithAlpha(UiTheme.Shade(theme.danger, 0.22f), 0.94f)
                    : UiTheme.WithAlpha(theme.surface, 0.94f);
            }
            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(evt.Title) ? Loc.Get("event.default_title", "WAVE EVENT") : evt.Title;
                titleText.color = accent;
            }
            if (subtitleText != null)
            {
                subtitleText.text = evt.Subtitle ?? string.Empty;
            }
            if (progressFillImage != null)
            {
                progressFillImage.color = accent;
                progressFillImage.fillAmount = 1f;
            }

            RefreshStatusText();
        }

        private void OnWaveEventEnded(WaveEventEndedEvent _)
        {
            Hide();
        }

        private void OnGameOver(GameOverEvent _)
        {
            Hide();
        }

        private void Update()
        {
            if (activeType == WaveEventType.None || bannerRoot == null || !bannerRoot.activeSelf) return;

            if (punchScale > 1.001f)
            {
                punchScale = Mathf.Lerp(punchScale, 1f, Mathf.Clamp01(Time.unscaledDeltaTime * 9f));
                bannerRoot.transform.localScale = new Vector3(punchScale, punchScale, 1f);
            }
            else if (punchScale != 1f)
            {
                punchScale = 1f;
                bannerRoot.transform.localScale = Vector3.one;
            }

            RefreshStatusText();
        }

        private void RefreshStatusText()
        {
            if (waveEventManager == null) return;

            if (activeType == WaveEventType.EliteAmbush)
            {
                int remaining = Mathf.Clamp(waveEventManager.AmbushRemainingElites, 0, cachedAmbushLabels.Length - 1);
                int total = Mathf.Max(1, waveEventManager.AmbushTotalElites);
                if (statusText != null && remaining != lastDisplayedRemainingElites)
                {
                    lastDisplayedRemainingElites = remaining;
                    statusText.text = cachedAmbushLabels[remaining];
                }
                if (progressFillImage != null)
                {
                    progressFillImage.fillAmount = Mathf.Clamp01((float)remaining / total);
                }
                return;
            }

            int secs = Mathf.Max(0, Mathf.CeilToInt(waveEventManager.RemainingTime));
            if (statusText != null && secs != lastDisplayedSeconds)
            {
                lastDisplayedSeconds = secs;
                statusText.text = GetCachedSecondsLabel(secs);
            }
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = waveEventManager.NormalizedRemainingTime;
            }
        }

        public void Hide()
        {
            activeType = WaveEventType.None;
            lastDisplayedSeconds = -1;
            lastDisplayedRemainingElites = -1;
            punchScale = 1f;
            if (bannerRoot != null)
            {
                bannerRoot.transform.localScale = Vector3.one;
                bannerRoot.SetActive(false);
            }
        }

        public static Color GetEventAccentColor(WaveEventType type)
        {
            UiTheme theme = UiTheme.Current;
            return type switch
            {
                WaveEventType.SwarmRush => theme.warning,
                WaveEventType.GoldGoblin => theme.gold,
                WaveEventType.BloodMoon => theme.danger,
                WaveEventType.EliteAmbush => theme.rarityEpic,
                _ => theme.textPrimary
            };
        }
    }
}
