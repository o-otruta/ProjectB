using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Enemies;
using ProjectB.Meta;
using ProjectB.Player;
using ProjectB.UI;

namespace ProjectB.Tests.EditMode
{
    public class LifecycleTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private bool hadOriginalSave;
        private string originalSaveJson;

        [SetUp]
        public void SetUp()
        {
            hadOriginalSave = PlayerPrefs.HasKey(SaveManager.SAVE_KEY);
            originalSaveJson = hadOriginalSave ? PlayerPrefs.GetString(SaveManager.SAVE_KEY) : null;
            PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }
            spawned.Clear();

            if (hadOriginalSave)
            {
                PlayerPrefs.SetString(SaveManager.SAVE_KEY, originalSaveJson);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveManager.SAVE_KEY);
            }
            PlayerPrefs.Save();
        }

        private T Track<T>(T obj) where T : Object
        {
            if (obj != null) spawned.Add(obj);
            return obj;
        }

        private static void InvokeLifecycle(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"Method {methodName} not found on {target.GetType().Name}");
            method.Invoke(target, args);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private (GameManager gm, HeroHealth heroHealth, WaveManager waveMgr, RunStatistics runStats, SaveManager saveManager, PauseMenuUI pauseUi) CreateRunRig(int startWave = 5)
        {
            var saveManager = new SaveManager();
            var runStats = new RunStatistics();
            var bus = new GameEventBus();
            var heroStats = new HeroRuntimeStats();

            var heroGo = Track(new GameObject("LifecycleHero"));
            var heroHealth = heroGo.AddComponent<HeroHealth>();
            heroHealth.Construct(heroStats, bus, null);
            SetPrivateField(heroHealth, "currentMaxHp", 100);
            SetPrivateField(heroHealth, "currentHp", 100);

            var waveGo = Track(new GameObject("LifecycleWaveMgr"));
            var waveMgr = waveGo.AddComponent<WaveManager>();
            SetPrivateField(waveMgr, "currentWave", startWave);

            var gmGo = Track(new GameObject("LifecycleGameManager"));
            var gm = gmGo.AddComponent<GameManager>();
            gm.Construct(heroHealth, waveMgr, saveManager, runStats, null, bus);
            InvokeLifecycle(gm, "Start");

            var pauseRoot = Track(new GameObject("LifecyclePauseRoot", typeof(RectTransform)));
            var pausePanel = Track(new GameObject("PausePanel", typeof(RectTransform)));
            pausePanel.transform.SetParent(pauseRoot.transform, false);
            var pauseUi = pauseRoot.AddComponent<PauseMenuUI>();
            SetPrivateField(pauseUi, "pausePanel", pausePanel);
            pauseUi.Construct(gm, null, runStats, waveMgr, null);
            InvokeLifecycle(pauseUi, "Awake");

            return (gm, heroHealth, waveMgr, runStats, saveManager, pauseUi);
        }

        [Test]
        public void ApplicationBackgrounded_InPlaying_AutoPausesAndOpensPauseMenu_AndStaysPausedOnFocusReturn()
        {
            var (gm, _, _, _, saveManager, pauseUi) = CreateRunRig(4);
            try
            {
                Assert.AreEqual(GameState.Playing, gm.CurrentState);
                Assert.IsFalse(pauseUi.IsPanelOpen);

                InvokeLifecycle(gm, "OnApplicationFocus", false);

                Assert.AreEqual(GameState.Paused, gm.CurrentState);
                Assert.IsTrue(gm.IsPaused);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
                Assert.IsTrue(pauseUi.IsPanelOpen, "Auto-pause must open PauseMenuUI so the player can resume");

                // Returning focus / unpausing OS app must NOT auto-resume gameplay
                InvokeLifecycle(gm, "OnApplicationFocus", true);
                InvokeLifecycle(gm, "OnApplicationPause", false);

                Assert.AreEqual(GameState.Paused, gm.CurrentState);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
                Assert.IsTrue(pauseUi.IsPanelOpen);

                // Only explicit Resume resumes
                pauseUi.OnResumeClicked();
                Assert.AreEqual(GameState.Playing, gm.CurrentState);
                Assert.AreEqual(1f, Time.timeScale, 0.001f);
                Assert.IsFalse(pauseUi.IsPanelOpen);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void ApplicationPause_InPlaying_AutoPausesGame()
        {
            var (gm, _, _, _, saveManager, pauseUi) = CreateRunRig(3);
            try
            {
                InvokeLifecycle(gm, "OnApplicationPause", true);

                Assert.AreEqual(GameState.Paused, gm.CurrentState);
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
                Assert.IsTrue(pauseUi.IsPanelOpen);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void ApplicationBackgrounded_InLevelUpAndGameOver_DoesNotChangeState()
        {
            var (gm, heroHealth, _, runStats, saveManager, pauseUi) = CreateRunRig(6);
            try
            {
                runStats.AddCoin(45);
                runStats.AddBossShards(1);
                runStats.AddKill();

                Assert.IsTrue(gm.PauseForLevelUp());
                Assert.AreEqual(GameState.LevelUp, gm.CurrentState);

                InvokeLifecycle(gm, "OnApplicationFocus", false);

                Assert.AreEqual(GameState.LevelUp, gm.CurrentState, "LevelUp state must not be overwritten by Paused");
                Assert.IsFalse(pauseUi.IsPanelOpen, "PauseMenuUI must not open over LevelUp");
                Assert.AreEqual(45, saveManager.Data.coins, "Checkpoint must still flush progress in LevelUp");
                Assert.AreEqual(1, saveManager.Data.bossShards);
                Assert.IsTrue(saveManager.Data.runInProgress);

                gm.ResumeFromLevelUp();
                runStats.AddCoin(15);
                heroHealth.TakeDamage(999);
                Assert.AreEqual(GameState.GameOver, gm.CurrentState);
                Assert.AreEqual(60, saveManager.Data.coins);
                Assert.AreEqual(1, saveManager.Data.totalRuns);
                Assert.IsFalse(saveManager.Data.runInProgress);

                InvokeLifecycle(gm, "OnApplicationPause", true);
                Assert.AreEqual(GameState.GameOver, gm.CurrentState);
                Assert.IsFalse(pauseUi.IsPanelOpen);
                Assert.AreEqual(60, saveManager.Data.coins);
                Assert.AreEqual(1, saveManager.Data.totalRuns);
                Assert.IsFalse(saveManager.Data.runInProgress);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void Checkpoints_RepeatedAndFollowedByDeath_NeverDoubleCountProgressOrRuns()
        {
            var (gm, heroHealth, waveMgr, runStats, saveManager, _) = CreateRunRig(4);
            try
            {
                runStats.AddCoin(50);
                runStats.AddBossShards(2);
                for (int i = 0; i < 10; i++) runStats.AddKill();
                runStats.UpdatePlayTime(30f);

                // Android often fires OnApplicationFocus(false) followed immediately by OnApplicationPause(true)
                InvokeLifecycle(gm, "OnApplicationFocus", false);
                InvokeLifecycle(gm, "OnApplicationPause", true);

                Assert.AreEqual(50, saveManager.Data.coins);
                Assert.AreEqual(2, saveManager.Data.bossShards);
                Assert.AreEqual(10, saveManager.Data.totalKills);
                Assert.AreEqual(30f, saveManager.Data.totalPlayTime, 0.001f);
                Assert.AreEqual(4, saveManager.Data.bestWave);
                Assert.AreEqual(0, saveManager.Data.totalRuns, "Checkpoint must not increment totalRuns yet");
                Assert.IsTrue(saveManager.Data.runInProgress);

                // Resume, gain more progress, checkpoint again at wave 8
                gm.ResumeGame();
                SetPrivateField(waveMgr, "currentWave", 8);
                runStats.AddCoin(35);
                runStats.AddBossShards(1);
                for (int i = 0; i < 5; i++) runStats.AddKill();
                runStats.UpdatePlayTime(25f);

                gm.CheckpointRunProgress();

                Assert.AreEqual(85, saveManager.Data.coins);
                Assert.AreEqual(3, saveManager.Data.bossShards);
                Assert.AreEqual(15, saveManager.Data.totalKills);
                Assert.AreEqual(55f, saveManager.Data.totalPlayTime, 0.001f);
                Assert.AreEqual(8, saveManager.Data.bestWave);
                Assert.IsTrue(saveManager.Data.runInProgress);

                // Resume, gain final progress, then die
                gm.ResumeGame();
                SetPrivateField(waveMgr, "currentWave", 9);
                runStats.AddCoin(15);
                for (int i = 0; i < 3; i++) runStats.AddKill();
                runStats.UpdatePlayTime(10f);

                heroHealth.TakeDamage(999);

                Assert.AreEqual(100, saveManager.Data.coins);
                Assert.AreEqual(3, saveManager.Data.bossShards);
                Assert.AreEqual(18, saveManager.Data.totalKills);
                Assert.AreEqual(65f, saveManager.Data.totalPlayTime, 0.001f);
                Assert.AreEqual(9, saveManager.Data.bestWave);
                Assert.AreEqual(1, saveManager.Data.totalRuns);
                Assert.IsFalse(saveManager.Data.runInProgress);

                using var coldStart = new SaveManager();
                Assert.IsFalse(coldStart.WasInterruptedRunRecovered);
                Assert.AreEqual(100, coldStart.Data.coins);
                Assert.AreEqual(3, coldStart.Data.bossShards);
                Assert.AreEqual(18, coldStart.Data.totalKills);
                Assert.AreEqual(1, coldStart.Data.totalRuns);
            }
            finally
            {
                saveManager.Dispose();
            }
        }

        [Test]
        public void ColdStartAfterOsKill_RecoversCheckpointedProgress_CountsRunOnceAndClearsFlag()
        {
            var (gm, _, _, runStats, saveManager, _) = CreateRunRig(7);
            try
            {
                runStats.AddCoin(120);
                runStats.AddBossShards(4);
                for (int i = 0; i < 22; i++) runStats.AddKill();
                runStats.UpdatePlayTime(90f);

                InvokeLifecycle(gm, "OnApplicationFocus", false);
                Assert.IsTrue(saveManager.Data.runInProgress);
                Assert.AreEqual(0, saveManager.Data.totalRuns);
            }
            finally
            {
                saveManager.Dispose();
            }

            // Simulate process killed by OS and cold start with a brand new SaveManager
            using (var recovered = new SaveManager())
            {
                Assert.IsTrue(recovered.WasInterruptedRunRecovered);
                Assert.IsFalse(recovered.Data.runInProgress);
                Assert.AreEqual(1, recovered.Data.totalRuns);
                Assert.AreEqual(120, recovered.Data.coins);
                Assert.AreEqual(4, recovered.Data.bossShards);
                Assert.AreEqual(4, recovered.Data.totalBossShardsEarned);
                Assert.AreEqual(22, recovered.Data.totalKills);
                Assert.AreEqual(7, recovered.Data.bestWave);
                Assert.AreEqual(90f, recovered.Data.totalPlayTime, 0.001f);
            }

            // Subsequent cold start must not recover or increment totalRuns again
            using (var secondLaunch = new SaveManager())
            {
                Assert.IsFalse(secondLaunch.WasInterruptedRunRecovered);
                Assert.IsFalse(secondLaunch.Data.runInProgress);
                Assert.AreEqual(1, secondLaunch.Data.totalRuns);
                Assert.AreEqual(120, secondLaunch.Data.coins);
            }
        }

        [Test]
        public void ReviveThenBackgroundAndOsKill_SavesPostReviveDeltasWithoutDoubleCountingRuns()
        {
            var (gm, heroHealth, waveMgr, runStats, saveManager, _) = CreateRunRig(10);
            try
            {
                runStats.AddCoin(80);
                heroHealth.TakeDamage(999);
                Assert.AreEqual(GameState.GameOver, gm.CurrentState);
                Assert.AreEqual(1, saveManager.Data.totalRuns);
                Assert.IsFalse(saveManager.Data.runInProgress);

                gm.ReviveHero();
                Assert.AreEqual(GameState.Playing, gm.CurrentState);

                SetPrivateField(waveMgr, "currentWave", 12);
                runStats.AddCoin(40);
                runStats.AddBossShards(1);
                InvokeLifecycle(gm, "OnApplicationFocus", false);

                Assert.AreEqual(120, saveManager.Data.coins);
                Assert.AreEqual(1, saveManager.Data.bossShards);
                Assert.AreEqual(12, saveManager.Data.bestWave);
                Assert.AreEqual(1, saveManager.Data.totalRuns);
                Assert.IsFalse(saveManager.Data.runInProgress, "Already-counted run must not re-set runInProgress on revive checkpoint");
            }
            finally
            {
                saveManager.Dispose();
            }

            using var coldStart = new SaveManager();
            Assert.IsFalse(coldStart.WasInterruptedRunRecovered);
            Assert.AreEqual(1, coldStart.Data.totalRuns);
            Assert.AreEqual(120, coldStart.Data.coins);
            Assert.AreEqual(1, coldStart.Data.bossShards);
        }
    }
}
