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
        private GameManager gameManager;
        private ProjectB.Meta.AchievementManager achievementManager;

        [Inject]
        public void Construct(GameManager gameManager, ProjectB.Meta.AchievementManager achievementManager)
        {
            this.gameManager = gameManager;
            this.achievementManager = achievementManager;
        }

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Start()
        {
            if (gameManager != null)
            {
                gameManager.OnGameOver += Show;
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameOver -= Show;
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
            
            if (achievementManager != null && achievementManager.unlockedThisRun.Count > 0)
            {
                Debug.Log($"[GameOverUI] Unlocked {achievementManager.unlockedThisRun.Count} achievements this run!");
                // TODO: Instantiate AchievementItemUI for each unlocked achievement to display them
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
