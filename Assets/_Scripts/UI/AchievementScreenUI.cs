using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.Core;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;

namespace ProjectB.UI
{
    /// <summary>
    /// Achievements tab. Sprint 13.2: closest-to-completion achievements first, completed ones (checked, dimmed)
    /// at the end; a summary line shows "12 / 27 completed".
    /// </summary>
    public class AchievementScreenUI : MonoBehaviour
    {
        [SerializeField] private Transform contentContainer;
        [SerializeField] private AchievementItemUI itemPrefab;
        [SerializeField] private AchievementRewardPopupUI rewardPopup;

        [Header("Sprint 13")]
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private TMPro.TMP_Text summaryText;

        private AchievementManager achievementManager;
        private SaveManager saveManager;

        private readonly List<AchievementItemUI> spawnedItems = new List<AchievementItemUI>();
        private readonly List<AchievementData> sortBuffer = new List<AchievementData>(32);
        private Action cachedLanguageChanged;

        [Inject]
        public void Construct(AchievementManager achievementManager, SaveManager saveManager)
        {
            this.achievementManager = achievementManager;
            this.saveManager = saveManager;
            if (isActiveAndEnabled)
            {
                RefreshUI();
            }
        }

        private void OnEnable()
        {
            cachedLanguageChanged ??= RefreshUI;
            LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            LocalizationService.Current.LanguageChanged += cachedLanguageChanged;
            if (achievementManager != null && saveManager != null)
            {
                RefreshUI();
            }
            if (contentContainer != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            }
        }

        private void OnDisable()
        {
            if (cachedLanguageChanged != null)
            {
                LocalizationService.Current.LanguageChanged -= cachedLanguageChanged;
            }
        }

        public void RefreshUI()
        {
            if (achievementManager == null || saveManager == null || itemPrefab == null || contentContainer == null)
            {
                Debug.LogWarning($"[AchievementScreenUI] Cannot refresh. achievementManager: {achievementManager != null}, saveManager: {saveManager != null}, itemPrefab: {itemPrefab != null}, contentContainer: {contentContainer != null}");
                return;
            }

            foreach (var item in spawnedItems)
            {
                if (item != null)
                {
                    item.OnItemClicked -= HandleItemClicked;
                    Destroy(item.gameObject);
                }
            }
            spawnedItems.Clear();

            sortBuffer.Clear();
            var allAchievements = achievementManager.GetAllAchievements();
            for (int i = 0; i < allAchievements.Count; i++)
            {
                if (allAchievements[i] != null) sortBuffer.Add(allAchievements[i]);
            }
            SortByCompletion(sortBuffer, saveManager.Data);

            int completed = 0;
            foreach (var data in sortBuffer)
            {
                var item = Instantiate(itemPrefab, contentContainer);

                bool isCompleted = saveManager.Data.completedAchievements.Contains(data.id);
                if (isCompleted) completed++;
                saveManager.Data.achievementProgress.TryGetValue(data.id, out int progress);

                item.Setup(data, progress, isCompleted, abilityCatalog);
                item.OnItemClicked += HandleItemClicked;
                spawnedItems.Add(item);
            }

            if (summaryText != null) summaryText.text = Loc.Format("ui.achievements.summary", completed, sortBuffer.Count);
        }

        /// <summary>In progress (highest ratio first) → not started → completed.</summary>
        public static void SortByCompletion(List<AchievementData> list, SaveData data)
        {
            if (list == null || data == null) return;
            list.Sort((a, b) =>
            {
                float ka = SortKey(a, data);
                float kb = SortKey(b, data);
                int cmp = kb.CompareTo(ka);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.id, b.id);
            });
        }

        private static float SortKey(AchievementData ach, SaveData data)
        {
            if (data.completedAchievements.Contains(ach.id)) return -1f;
            data.achievementProgress.TryGetValue(ach.id, out int progress);
            return ach.targetValue > 0 ? Mathf.Clamp01((float)progress / ach.targetValue) : 0f;
        }

        private void HandleItemClicked(AchievementData data)
        {
            if (data == null || rewardPopup == null || saveManager?.Data == null) return;

            bool isCompleted = saveManager.Data.completedAchievements.Contains(data.id);
            if (!isCompleted) return;

            rewardPopup.ShowReward(data, abilityCatalog);
        }
    }
}
