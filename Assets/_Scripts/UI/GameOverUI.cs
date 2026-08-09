using UnityEngine;
using VContainer;
using ProjectB.Core;

namespace ProjectB.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TextMeshProUGUI waveText;
        private GameManager gameManager;

        [Inject]
        public void Construct(GameManager gameManager)
        {
            this.gameManager = gameManager;
        }

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        public void Show(int currentWave)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }

            if (waveText != null)
            {
                waveText.text = $"Wave Reached: {currentWave}";
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
            if (gameManager != null)
            {
                gameManager.RestartGame();
            }
        }

        public void OnMenuClicked()
        {
            if (gameManager != null)
            {
                gameManager.GoToMenu();
            }
        }

        public void OnReviveClicked()
        {
            if (gameManager != null)
            {
                gameManager.ReviveHero();
            }
        }
    }
}
