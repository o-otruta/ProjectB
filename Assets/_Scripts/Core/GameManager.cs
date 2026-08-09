using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Player;
using ProjectB.Enemies;
using ProjectB.UI;
using ProjectB.LevelUp;
using VContainer;

namespace ProjectB.Core
{
    public enum GameState
    {
        Playing,
        Paused,
        LevelUp,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        private HeroHealth heroHealth;
        private WaveManager waveManager;
        private GameOverUI gameOverUI;
        
        [Inject]
        public void Construct(HeroHealth heroHealth, WaveManager waveManager, GameOverUI gameOverUI)
        {
            this.heroHealth = heroHealth;
            this.waveManager = waveManager;
            this.gameOverUI = gameOverUI;
        }

        private GameState currentState;

        public GameState CurrentState => currentState;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // На мобилках vSync часто мешает targetFrameRate, поэтому отключаем его
            QualitySettings.vSyncCount = 0; 
        }

        private void Start()
        {
            currentState = GameState.Playing;
            Time.timeScale = 1f;

            if (heroHealth != null)
            {
                heroHealth.OnDied += HandleHeroDeath;
            }
        }

        private void OnDestroy()
        {
            if (heroHealth != null)
            {
                // Prevent warnings if hero is already destroyed during scene teardown
                heroHealth.OnDied -= HandleHeroDeath;
            }
        }

        private void HandleHeroDeath()
        {
            if (currentState == GameState.GameOver) return;

            currentState = GameState.GameOver;
            Time.timeScale = 0f;

            if (gameOverUI != null)
            {
                int wave = waveManager != null ? waveManager.CurrentWave : 1;
                gameOverUI.Show(wave);
            }
            else
            {
                Debug.LogWarning("[GameManager] GameOverUI not found! Cannot show Game Over screen.");
            }
        }

        public void PauseForLevelUp()
        {
            if (currentState == GameState.GameOver) return; // Prevent level up over death
            
            currentState = GameState.LevelUp;
            Time.timeScale = 0f;
        }

        public void ResumeFromLevelUp()
        {
            if (currentState == GameState.GameOver) return;

            currentState = GameState.Playing;
            Time.timeScale = 1f;
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToMenu()
        {
            Debug.Log("[GameManager] TODO: Load Main Menu scene");
            // Time.timeScale = 1f;
            // SceneManager.LoadScene("MainMenu");
        }

        public void ReviveHero()
        {
            Debug.Log("[GameManager] TODO: Revive via Ad logic");
            // E.g., heroHealth.Heal(maxHp), hide game over, Time.timeScale = 1f
        }
    }
}
