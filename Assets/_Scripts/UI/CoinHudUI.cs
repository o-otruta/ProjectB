using UnityEngine;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;

namespace ProjectB.UI
{
    public class CoinHudUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI coinsText;

        private RunStatistics runStatistics;
        private GameEventBus eventBus;
        private bool isSubscribed;

        [Inject]
        public void Construct(RunStatistics runStatistics, GameEventBus eventBus)
        {
            this.runStatistics = runStatistics;
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
                eventBus.Subscribe<CoinCollectedEvent>(OnCoinCollected);
                isSubscribed = true;
            }

            RefreshDisplay();
        }

        private void Unsubscribe()
        {
            if (eventBus != null && isSubscribed)
            {
                eventBus.Unsubscribe<CoinCollectedEvent>(OnCoinCollected);
                isSubscribed = false;
            }
        }

        private void OnCoinCollected(CoinCollectedEvent _)
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (coinsText != null)
            {
                int coins = runStatistics != null ? runStatistics.CoinsEarned : 0;
                coinsText.text = coins.ToString();
            }
        }
    }
}
