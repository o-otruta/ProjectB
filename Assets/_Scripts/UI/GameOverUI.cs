using UnityEngine;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TextMeshProUGUI waveText;
        [SerializeField] private TMPro.TextMeshProUGUI coinsText;
        private IObjectResolver resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void Show(int currentWave, int coinsEarned)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }

            if (waveText != null)
            {
                waveText.text = $"Wave Reached: {currentWave}";
            }
            
            if (coinsText != null)
            {
                coinsText.text = $"Coins Earned: {coinsEarned}";
            }
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void OnRestartClicked()
        {
            if (resolver != null)
            {
                resolver.Resolve<GameManager>().RestartGame();
            }
        }

        public void OnMenuClicked()
        {
            if (resolver != null)
            {
                resolver.Resolve<GameManager>().GoToMenu();
            }
        }

        public void OnReviveClicked()
        {
            if (resolver != null)
            {
                resolver.Resolve<GameManager>().ReviveHero();
            }
        }
    }
}
