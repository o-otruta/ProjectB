using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Player;
using ProjectB.Enemies;
using ProjectB.UI;
using ProjectB.LevelUp;
using ProjectB.Meta;
using VContainer;
using ProjectB.Core.Events;

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
        public event System.Action<int, int> OnGameOver;

        private HeroHealth heroHealth;
        private WaveManager waveManager;
        private SaveManager saveManager;
        private RunStatistics runStatistics;
        private AchievementManager achievementManager;
        private GameEventBus eventBus;
        
        [Inject]
        public void Construct(
            HeroHealth heroHealth, 
            WaveManager waveManager, 
            SaveManager saveManager, 
            RunStatistics runStatistics, 
            AchievementManager achievementManager, 
            GameEventBus eventBus)
        {
            this.heroHealth = heroHealth;
            this.waveManager = waveManager;
            this.saveManager = saveManager;
            this.runStatistics = runStatistics;
            this.achievementManager = achievementManager;
            this.eventBus = eventBus;
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

            achievementManager?.ClearRunUnlocks();

            if (heroHealth != null)
            {
                heroHealth.OnDied += HandleHeroDeath;
            }
        }

        private void Update()
        {
            if (currentState == GameState.Playing && runStatistics != null)
            {
                runStatistics.UpdatePlayTime(Time.deltaTime);
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

            int wave = waveManager != null ? waveManager.CurrentWave : 1;
            int coins = 0;

            if (runStatistics != null && saveManager != null)
            {
                coins = runStatistics.CoinsEarned;
                RunResult result = new RunResult
                {
                    waveReached = wave,
                    enemiesKilled = runStatistics.EnemiesKilled,
                    coinsEarned = coins,
                    playTime = runStatistics.PlayTime,
                    // Additional stats can be filled here later
                };
                
                saveManager.RecordRunResult(result);
            }

            OnGameOver?.Invoke(wave, coins);
            eventBus?.Publish(new GameOverEvent(wave, coins));
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
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public void ReviveHero()
        {
            Debug.Log("[GameManager] TODO: Revive via Ad logic");
            // E.g., heroHealth.Heal(maxHp), hide game over, Time.timeScale = 1f
        }
    }
}
