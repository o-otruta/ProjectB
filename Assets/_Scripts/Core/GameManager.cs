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
        private AudioManager audioManager;

        private bool hasRevivedThisRun;
        private bool hasDoubledCoinsThisRun;
        private bool hasCountedRunInTotals;
        private bool hasNotifiedAdRunCompleted;
        private int recordedCoinsThisRun;
        private int recordedShardsThisRun;
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
            RunComboTracker comboTracker = null,
            AudioManager audioManager = null)
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
            this.audioManager = audioManager;
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
            audioManager?.SetMusicDucked(false);

            achievementManager?.ClearRunUnlocks();
            UiFocusHelper.ClearSelection();

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

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                HandleApplicationBackgrounded();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                HandleApplicationBackgrounded();
            }
        }

        public void HandleApplicationBackgrounded()
        {
            if (currentState == GameState.GameOver)
            {
                saveManager?.SaveIfDirty();
                return;
            }

            if (currentState == GameState.Playing)
            {
                PauseGame();
            }

            CheckpointRunProgress();
        }

        public void CheckpointRunProgress()
        {
            if (currentState == GameState.GameOver)
            {
                saveManager?.SaveIfDirty();
                return;
            }

            int wave = waveManager != null ? waveManager.CurrentWave : 1;
            int coins = runStatistics != null ? runStatistics.CoinsEarned : 0;
            RecordRunProgressToSave(wave, coins, notifyAdRunCompleted: false, isCheckpoint: true);
        }

        private void HandleHeroDeath()
        {
            if (currentState == GameState.GameOver) return;

            currentState = GameState.GameOver;
            Time.timeScale = 0f;
            audioManager?.SetMusicDucked(true);
            virtualJoystick?.ResetJoystick();

            int wave = waveManager != null ? waveManager.CurrentWave : 1;
            int coins = runStatistics != null ? runStatistics.CoinsEarned : 0;
            lastShownRunCoins = coins;

            RecordRunProgressToSave(wave, coins, notifyAdRunCompleted: true);

            OnGameOver?.Invoke(wave, coins);
            eventBus?.Publish(new GameOverEvent(wave, coins));
        }

        private void RecordRunProgressToSave(int wave, int coins, bool notifyAdRunCompleted, bool isCheckpoint = false)
        {
            int deltaCoins = 0;
            int deltaShards = 0;
            int deltaKills = 0;
            float deltaTime = 0f;

            if (runStatistics != null)
            {
                deltaCoins = Mathf.Max(0, coins - recordedCoinsThisRun);
                deltaShards = Mathf.Max(0, runStatistics.BossShardsEarned - recordedShardsThisRun);
                deltaKills = Mathf.Max(0, runStatistics.EnemiesKilled - recordedKillsThisRun);
                deltaTime = Mathf.Max(0f, runStatistics.PlayTime - recordedPlayTimeThisRun);
            }

            if (isCheckpoint)
            {
                if (saveManager == null) return;
                if (runStatistics == null)
                {
                    saveManager.SaveIfDirty();
                    return;
                }

                bool? markInProgress = hasCountedRunInTotals ? (bool?)null : true;
                saveManager.ApplyRunProgressDelta(
                    wave,
                    deltaCoins,
                    deltaShards,
                    deltaKills,
                    deltaTime,
                    setRunInProgress: markInProgress,
                    saveImmediately: true);

                UpdateRecordedProgress(coins);
                return;
            }

            if (notifyAdRunCompleted && !hasNotifiedAdRunCompleted)
            {
                hasNotifiedAdRunCompleted = true;
                adManager?.NotifyRunCompleted();
            }

            if (!hasCountedRunInTotals)
            {
                hasCountedRunInTotals = true;

                if (runStatistics != null && saveManager != null)
                {
                    RunResult result = new RunResult
                    {
                        waveReached = wave,
                        enemiesKilled = deltaKills,
                        coinsEarned = deltaCoins,
                        playTime = deltaTime,
                        bossShardsEarned = deltaShards
                    };

                    saveManager.RecordRunResult(result);
                    UpdateRecordedProgress(coins);
                }
                else if (saveManager != null)
                {
                    if (saveManager.Data != null && saveManager.Data.runInProgress)
                    {
                        saveManager.Data.runInProgress = false;
                        saveManager.Save();
                    }
                    else
                    {
                        saveManager.SaveIfDirty();
                    }
                }
            }
            else if (runStatistics != null && saveManager != null)
            {
                saveManager.ApplyRunProgressDelta(
                    wave,
                    deltaCoins,
                    deltaShards,
                    deltaKills,
                    deltaTime,
                    setRunInProgress: false,
                    saveImmediately: true);

                UpdateRecordedProgress(coins);
            }
            else
            {
                saveManager?.SaveIfDirty();
            }
        }

        private void UpdateRecordedProgress(int coins)
        {
            if (runStatistics == null) return;
            recordedShardsThisRun = runStatistics.BossShardsEarned;
            recordedCoinsThisRun = coins;
            recordedKillsThisRun = runStatistics.EnemiesKilled;
            recordedPlayTimeThisRun = runStatistics.PlayTime;
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
            audioManager?.SetMusicDucked(false);
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
            audioManager?.SetMusicDucked(true);
            virtualJoystick?.ResetJoystick();
            OnPauseStateChanged?.Invoke(true);
            return true;
        }

        public bool ResumeGame()
        {
            if (currentState != GameState.Paused) return false;

            currentState = GameState.Playing;
            Time.timeScale = 1f;
            audioManager?.SetMusicDucked(false);
            UiFocusHelper.ClearSelection();
            OnPauseStateChanged?.Invoke(false);
            return true;
        }

        public bool PauseForLevelUp()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Paused) return false; // Prevent level up over death/pause
            
            currentState = GameState.LevelUp;
            Time.timeScale = 0f;
            audioManager?.SetMusicDucked(true);
            virtualJoystick?.ResetJoystick();
            return true;
        }

        public void ResumeFromLevelUp()
        {
            if (currentState == GameState.GameOver) return;

            currentState = GameState.Playing;
            Time.timeScale = 1f;
            audioManager?.SetMusicDucked(false);
        }

        public void RestartGame()
        {
            audioManager?.SetMusicDucked(false);
            saveManager?.SaveIfDirty();
            LoadSceneAfterInterstitial(SceneManager.GetActiveScene().name);
        }

        public void GoToMenu()
        {
            audioManager?.SetMusicDucked(false);
            saveManager?.SaveIfDirty();
            LoadSceneAfterInterstitial("MainMenu");
        }

        private void LoadSceneAfterInterstitial(string sceneName)
        {
            audioManager?.SetMusicDucked(false);
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
                audioManager?.SetMusicDucked(false);
                heroHealth?.Revive(adManager != null ? adManager.ReviveInvulnerabilitySeconds : 3f);
                waveManager?.ResumeAfterRevive();
                UiFocusHelper.ClearSelection();
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
