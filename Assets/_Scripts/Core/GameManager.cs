using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectB.Ads;
using ProjectB.Data;
using ProjectB.Player;
using ProjectB.Enemies;
using ProjectB.UI;
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
        public event System.Action OnHeroRevived;
        public event System.Action<int> OnCoinsDoubled;
        public event System.Action<bool> OnPauseStateChanged;

        private HeroHealth heroHealth;
        private WaveManager waveManager;
        private SaveManager saveManager;
        private RunStatistics runStatistics;
        private AchievementManager achievementManager;
        private GameEventBus eventBus;
        private VirtualJoystick virtualJoystick;
        private AdManager adManager;
        private RunComboTracker comboTracker;

        private bool hasRevivedThisRun;
        private bool hasDoubledCoinsThisRun;
        private bool hasCountedRunInTotals;
        private int recordedCoinsThisRun;
        private int recordedKillsThisRun;
        private float recordedPlayTimeThisRun;
        private int lastShownRunCoins;

        [Inject]
        public void Construct(
            HeroHealth heroHealth, 
            WaveManager waveManager, 
            SaveManager saveManager, 
            RunStatistics runStatistics, 
            AchievementManager achievementManager, 
            GameEventBus eventBus,
            VirtualJoystick virtualJoystick = null,
            AdManager adManager = null,
            RunComboTracker comboTracker = null)
        {
            this.heroHealth = heroHealth;
            this.waveManager = waveManager;
            this.saveManager = saveManager;
            this.runStatistics = runStatistics;
            this.achievementManager = achievementManager;
            this.eventBus = eventBus;
            this.virtualJoystick = virtualJoystick;
            this.adManager = adManager;
            this.comboTracker = comboTracker;
        }

        private GameState currentState;

        public GameState CurrentState => currentState;
        public bool IsGameOver => currentState == GameState.GameOver;
        public bool IsPaused => currentState == GameState.Paused;
        public bool CanPause => currentState == GameState.Playing;
        public bool HasRevivedThisRun => hasRevivedThisRun;
        public bool HasDoubledCoinsThisRun => hasDoubledCoinsThisRun;
        public bool CanRevive => currentState == GameState.GameOver
            && !hasRevivedThisRun
            && (adManager == null || adManager.CanShowRewarded());
        public bool CanDoubleCoins => currentState == GameState.GameOver
            && !hasDoubledCoinsThisRun
            && lastShownRunCoins > 0
            && (adManager == null || adManager.CanShowRewarded());

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
            if (currentState == GameState.Playing)
            {
                runStatistics?.UpdatePlayTime(Time.deltaTime);
                comboTracker?.Tick(Time.deltaTime);
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
            virtualJoystick?.ResetJoystick();

            int wave = waveManager != null ? waveManager.CurrentWave : 1;
            int coins = runStatistics != null ? runStatistics.CoinsEarned : 0;
            lastShownRunCoins = coins;

            RecordRunProgressToSave(wave, coins, notifyAdRunCompleted: true);

            OnGameOver?.Invoke(wave, coins);
            eventBus?.Publish(new GameOverEvent(wave, coins));
        }

        private void RecordRunProgressToSave(int wave, int coins, bool notifyAdRunCompleted)
        {
            if (!hasCountedRunInTotals)
            {
                hasCountedRunInTotals = true;
                if (notifyAdRunCompleted)
                {
                    adManager?.NotifyRunCompleted();
                }

                if (runStatistics != null && saveManager != null)
                {
                    RunResult result = new RunResult
                    {
                        waveReached = wave,
                        enemiesKilled = runStatistics.EnemiesKilled,
                        coinsEarned = coins,
                        playTime = runStatistics.PlayTime
                    };

                    saveManager.RecordRunResult(result);
                    recordedCoinsThisRun = coins;
                    recordedKillsThisRun = runStatistics.EnemiesKilled;
                    recordedPlayTimeThisRun = runStatistics.PlayTime;
                }
                else
                {
                    saveManager?.SaveIfDirty();
                }
            }
            else if (runStatistics != null && saveManager != null)
            {
                int deltaCoins = Mathf.Max(0, coins - recordedCoinsThisRun);
                int deltaKills = Mathf.Max(0, runStatistics.EnemiesKilled - recordedKillsThisRun);
                float deltaTime = Mathf.Max(0f, runStatistics.PlayTime - recordedPlayTimeThisRun);

                if (deltaCoins > 0) saveManager.AddCoins(deltaCoins, saveImmediately: false);
                saveManager.Data.totalKills += deltaKills;
                saveManager.Data.totalPlayTime += deltaTime;
                if (wave > saveManager.Data.bestWave) saveManager.Data.bestWave = wave;
                saveManager.Save();

                recordedCoinsThisRun = coins;
                recordedKillsThisRun = runStatistics.EnemiesKilled;
                recordedPlayTimeThisRun = runStatistics.PlayTime;
            }
        }

        public void SaveAbandonedRunProgress()
        {
            if (currentState == GameState.GameOver)
            {
                saveManager?.SaveIfDirty();
                return;
            }

            int wave = waveManager != null ? waveManager.CurrentWave : 1;
            int coins = runStatistics != null ? runStatistics.CoinsEarned : 0;
            RecordRunProgressToSave(wave, coins, notifyAdRunCompleted: false);
        }

        public void AbandonRunAndGoToMenu()
        {
            SaveAbandonedRunProgress();
            if (Application.isPlaying)
            {
                GoToMenu();
            }
            else
            {
                Time.timeScale = 1f;
                saveManager?.SaveIfDirty();
            }
        }

        public bool PauseGame()
        {
            if (currentState != GameState.Playing) return false;

            currentState = GameState.Paused;
            Time.timeScale = 0f;
            virtualJoystick?.ResetJoystick();
            OnPauseStateChanged?.Invoke(true);
            return true;
        }

        public bool ResumeGame()
        {
            if (currentState != GameState.Paused) return false;

            currentState = GameState.Playing;
            Time.timeScale = 1f;
            OnPauseStateChanged?.Invoke(false);
            return true;
        }

        public bool PauseForLevelUp()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Paused) return false; // Prevent level up over death/pause
            
            currentState = GameState.LevelUp;
            Time.timeScale = 0f;
            virtualJoystick?.ResetJoystick();
            return true;
        }

        public void ResumeFromLevelUp()
        {
            if (currentState == GameState.GameOver) return;

            currentState = GameState.Playing;
            Time.timeScale = 1f;
        }

        public void RestartGame()
        {
            saveManager?.SaveIfDirty();
            LoadSceneAfterInterstitial(SceneManager.GetActiveScene().name);
        }

        public void GoToMenu()
        {
            saveManager?.SaveIfDirty();
            LoadSceneAfterInterstitial("MainMenu");
        }

        private void LoadSceneAfterInterstitial(string sceneName)
        {
            if (adManager != null)
            {
                adManager.TryShowInterstitialBetweenRuns(() =>
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(sceneName);
                });
            }
            else
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(sceneName);
            }
        }

        public void ReviveHero()
        {
            if (!CanRevive) return;

            void ExecuteRevive()
            {
                hasRevivedThisRun = true;
                currentState = GameState.Playing;
                Time.timeScale = 1f;
                heroHealth?.Revive(adManager != null ? adManager.ReviveInvulnerabilitySeconds : 3f);
                waveManager?.ResumeAfterRevive();
                OnHeroRevived?.Invoke();
            }

            if (adManager != null)
            {
                adManager.ShowRewarded(AdPlacement.GameOverRevive, ExecuteRevive);
            }
            else
            {
                ExecuteRevive();
            }
        }

        public void TryDoubleCoins()
        {
            if (!CanDoubleCoins) return;

            void ExecuteDoubleCoins()
            {
                hasDoubledCoinsThisRun = true;
                int bonus = lastShownRunCoins;
                saveManager?.AddCoins(bonus, saveImmediately: true);
                OnCoinsDoubled?.Invoke(lastShownRunCoins + bonus);
            }

            if (adManager != null)
            {
                adManager.ShowRewarded(AdPlacement.GameOverDoubleCoins, ExecuteDoubleCoins);
            }
            else
            {
                ExecuteDoubleCoins();
            }
        }
    }
}
