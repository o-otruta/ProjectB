using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [Header("Core Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Run Achievements Section")]
        [SerializeField] private GameObject achievementsSection;
        [SerializeField] private Transform achievementsContainer;

        private GameManager gameManager;
        private AchievementManager achievementManager;
        private RunStatistics runStatistics;
        private readonly List<GameObject> spawnedAchievementRows = new List<GameObject>();

        [Inject]
        public void Construct(
            GameManager gameManager,
            AchievementManager achievementManager,
            RunStatistics runStatistics = null)
        {
            this.gameManager = gameManager;
            this.achievementManager = achievementManager;
            this.runStatistics = runStatistics;

            if (this.gameManager != null)
            {
                this.gameManager.OnGameOver += Show;
            }
        }

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
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

            if (statsText != null)
            {
                int kills = runStatistics != null ? runStatistics.EnemiesKilled : 0;
                float seconds = runStatistics != null ? runStatistics.PlayTime : 0f;
                int mins = Mathf.FloorToInt(seconds / 60f);
                int secs = Mathf.FloorToInt(seconds % 60f);
                statsText.text = $"Kills: {kills}   •   Time: {mins:00}:{secs:00}";
            }

            PopulateRunAchievements();
        }

        private void PopulateRunAchievements()
        {
            for (int i = 0; i < spawnedAchievementRows.Count; i++)
            {
                if (spawnedAchievementRows[i] != null) Destroy(spawnedAchievementRows[i]);
            }
            spawnedAchievementRows.Clear();

            bool hasUnlocks = achievementManager != null
                && achievementManager.unlockedThisRun != null
                && achievementManager.unlockedThisRun.Count > 0;

            if (achievementsSection != null)
            {
                achievementsSection.SetActive(hasUnlocks);
            }

            if (!hasUnlocks || achievementsContainer == null) return;

            var unlocks = achievementManager.unlockedThisRun;
            for (int i = 0; i < unlocks.Count; i++)
            {
                var ach = unlocks[i];
                if (ach == null) continue;

                GameObject row = CreateAchievementRow(ach);
                row.transform.SetParent(achievementsContainer, false);
                spawnedAchievementRows.Add(row);
            }
        }

        private static GameObject CreateAchievementRow(AchievementData ach)
        {
            var row = new GameObject($"Unlocked_{ach.id}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = 64f;
            le.preferredHeight = 64f;

            var bg = row.GetComponent<Image>();
            bg.color = new Color(0.22f, 0.19f, 0.08f, 0.92f);
            bg.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(row.transform, false);
            var rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16f, 4f);
            rt.offsetMax = new Vector2(-16f, -4f);

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            string rewardStr = FormatReward(ach);
            tmp.text = string.IsNullOrEmpty(rewardStr)
                ? $"<b>{ach.title}</b>"
                : $"<b>{ach.title}</b>  <color=#FFD54F>({rewardStr})</color>";
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 16f;
            tmp.fontSizeMax = 24f;
            tmp.raycastTarget = false;

            return row;
        }

        private static string FormatReward(AchievementData ach)
        {
            return ach.rewardType switch
            {
                AchievementRewardType.GiveCoins => $"+{ach.rewardValue} Coins",
                AchievementRewardType.UnlockAbility => $"Unlocked: {ach.rewardId}",
                AchievementRewardType.UnlockHero => $"Hero: {ach.rewardId}",
                _ => string.Empty
            };
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
