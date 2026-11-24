using System;
using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.UI
{
    public class WaveHudUI : MonoBehaviour
    {
        private static readonly string[] CachedWaveLabels = BuildWaveLabels("Wave ");
        private static SystemLanguage cachedWaveLabelsLang = SystemLanguage.English;

        [SerializeField] private TextMeshProUGUI waveText;
        [Tooltip("Sprint 13: append the run timer — \"Wave 12 · 03:41\".")]
        [SerializeField] private bool showRunTimer = true;
        [Tooltip("Sprint 13 HUD revision: big centred run timer; waveText then shows only \"WAVE 12\".")]
        [SerializeField] private TextMeshProUGUI timerText;
        private RunStatistics runStatistics;
        private readonly char[] labelBuffer = new char[48];
        private int lastDisplayedSeconds = int.MinValue;

        private WaveManager waveManager;
        private GameEventBus eventBus;
        private bool isSubscribed;
        private int lastDisplayedWave = int.MinValue;
        private string cachedWavePrefix = "Wave ";
        private string cachedWaveUpperPrefix = "WAVE ";
        private Action cachedLanguageChanged;

        private static string[] BuildWaveLabels(string prefix)
        {
            var arr = new string[201];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = $"{prefix}{i}";
            }
            return arr;
        }

        public static string GetCachedWaveText(int wave)
        {
            SystemLanguage lang = LocalizationService.Current.CurrentLanguage;
            if (cachedWaveLabelsLang != lang)
            {
                cachedWaveLabelsLang = lang;
                string prefix = Loc.Get("ui.hud.wave_prefix", "Wave ");
                for (int i = 0; i < CachedWaveLabels.Length; i++)
                {
                    CachedWaveLabels[i] = $"{prefix}{i}";
                }
            }

            if (wave >= 0 && wave < CachedWaveLabels.Length)
            {
                return CachedWaveLabels[wave];
            }
            return Loc.Format("ui.hud.wave_title", wave);
        }

        [Inject]
        public void Construct(WaveManager waveManager, GameEventBus eventBus, RunStatistics runStatistics = null)
        {
            this.waveManager = waveManager;
            this.eventBus = eventBus;
            this.runStatistics = runStatistics;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
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
            RefreshPrefixes();
            cachedLanguageChanged ??= HandleLanguageChanged;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;

            if (eventBus != null && !isSubscribed)
            {
                eventBus.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
                isSubscribed = true;
            }

            int currentWave = waveManager != null ? waveManager.CurrentWave : 1;
            UpdateWaveText(currentWave);
        }

        private void Unsubscribe()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
            if (eventBus != null && isSubscribed)
            {
                eventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
                isSubscribed = false;
            }
        }

        private void HandleLanguageChanged()
        {
            RefreshPrefixes();
            lastDisplayedWave = int.MinValue;
            int currentWave = waveManager != null ? waveManager.CurrentWave : 1;
            UpdateWaveText(currentWave);
        }

        private void RefreshPrefixes()
        {
            cachedWavePrefix = Loc.Get("ui.hud.wave_prefix", "Wave ");
            cachedWaveUpperPrefix = Loc.Get("ui.hud.wave_upper_prefix", "WAVE ");
        }

        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            UpdateWaveText(evt.NextWave);
        }

        private void Update()
        {
            if (!showRunTimer || runStatistics == null || waveText == null) return;
            int seconds = Mathf.FloorToInt(runStatistics.PlayTime);
            if (seconds == lastDisplayedSeconds) return;
            if (timerText != null) WriteTimer(seconds);
            else WriteLabel(lastDisplayedWave, seconds);
        }

        /// <summary>Zero-GC "03:41" into the big timer.</summary>
        private void WriteTimer(int seconds)
        {
            lastDisplayedSeconds = seconds;
            int len = UiFormat.WriteClock(labelBuffer, 0, seconds);
            timerText.SetCharArray(labelBuffer, 0, len);
        }

        private void UpdateWaveText(int wave)
        {
            if (waveText != null && wave != lastDisplayedWave)
            {
                lastDisplayedWave = wave;
                if (timerText != null)
                {
                    int len = UiFormat.WriteString(labelBuffer, 0, cachedWaveUpperPrefix);
                    len = UiFormat.WriteInt(labelBuffer, len, Mathf.Max(0, wave));
                    waveText.SetCharArray(labelBuffer, 0, len);
                    if (Application.isPlaying && wave > 1) UiTween.Punch(waveText.transform, 0.2f, 0.3f);
                    if (runStatistics != null) WriteTimer(Mathf.FloorToInt(runStatistics.PlayTime));
                }
                else if (showRunTimer && runStatistics != null)
                {
                    WriteLabel(wave, Mathf.FloorToInt(runStatistics.PlayTime));
                }
                else
                {
                    waveText.text = GetCachedWaveText(wave);
                }
            }
        }

        /// <summary>Zero-GC "Wave 12 · 03:41".</summary>
        private void WriteLabel(int wave, int seconds)
        {
            lastDisplayedSeconds = seconds;
            int len = UiFormat.WriteString(labelBuffer, 0, cachedWavePrefix);
            len = UiFormat.WriteInt(labelBuffer, len, Mathf.Max(0, wave));
            labelBuffer[len++] = ' ';
            labelBuffer[len++] = '•';
            labelBuffer[len++] = ' ';
            len = UiFormat.WriteClock(labelBuffer, len, seconds);
            waveText.SetCharArray(labelBuffer, 0, len);
        }
    }
}
