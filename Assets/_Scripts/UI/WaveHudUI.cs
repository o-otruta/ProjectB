using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core.Events;
using ProjectB.Enemies;

namespace ProjectB.UI
{
    public class WaveHudUI : MonoBehaviour
    {
        private static readonly string[] CachedWaveLabels = BuildWaveLabels(201);

        [SerializeField] private TextMeshProUGUI waveText;

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
        public void Construct(WaveManager waveManager, GameEventBus eventBus)
        {
            this.waveManager = waveManager;
            this.eventBus = eventBus;
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

        private void UpdateWaveText(int wave)
        {
            if (waveText != null && wave != lastDisplayedWave)
            {
                lastDisplayedWave = wave;
                waveText.text = GetCachedWaveText(wave);
            }
        }
    }
}
