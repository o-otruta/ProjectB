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
        private static readonly string[] CachedWaveLabels = BuildWaveLabels(201);

        [SerializeField] private TextMeshProUGUI waveText;
        [Tooltip("Sprint 13: append the run timer — \"Wave 12 · 03:41\".")]
        [SerializeField] private bool showRunTimer = true;
        [Tooltip("Sprint 13 HUD revision: big centred run timer; waveText then shows only \"WAVE 12\".")]
        [SerializeField] private TextMeshProUGUI timerText;
        private RunStatistics runStatistics;
        private readonly char[] labelBuffer = new char[32];
        private int lastDisplayedSeconds = int.MinValue;

        private WaveManager waveManager;
        private GameEventBus eventBus;
        private bool isSubscribed;
        private int lastDisplayedWave = int.MinValue;

        private static string[] BuildWaveLabels(int count)
        {
            var arr = new string[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = $"Wave {i}";
            }
            return arr;
        }

        public static string GetCachedWaveText(int wave)
        {
            if (wave >= 0 && wave < CachedWaveLabels.Length)
            {
                return CachedWaveLabels[wave];
            }
            return $"Wave {wave}";
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
            if (eventBus != null && isSubscribed)
            {
                eventBus.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
                isSubscribed = false;
            }
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
                    int len = UiFormat.WriteString(labelBuffer, 0, "WAVE ");
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
            int len = UiFormat.WriteString(labelBuffer, 0, "Wave ");
            len = UiFormat.WriteInt(labelBuffer, len, Mathf.Max(0, wave));
            labelBuffer[len++] = ' ';
            labelBuffer[len++] = '•';
            labelBuffer[len++] = ' ';
            len = UiFormat.WriteClock(labelBuffer, len, seconds);
            waveText.SetCharArray(labelBuffer, 0, len);
        }
    }
}
