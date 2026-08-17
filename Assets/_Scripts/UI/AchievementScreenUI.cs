using System.Collections.Generic;
using UnityEngine;
using ProjectB.Data;
using ProjectB.Meta;
using VContainer;

namespace ProjectB.UI
{
    public class AchievementScreenUI : MonoBehaviour
    {
        [SerializeField] private Transform contentContainer;
        [SerializeField] private AchievementItemUI itemPrefab;
        [SerializeField] private AchievementRewardPopupUI rewardPopup;
        
        private AchievementManager achievementManager;
        private SaveManager saveManager;
        
        private List<AchievementItemUI> spawnedItems = new List<AchievementItemUI>();

        [Inject]
        public void Construct(AchievementManager achievementManager, SaveManager saveManager)
        {
            this.achievementManager = achievementManager;
            this.saveManager = saveManager;
            RefreshUI();
        }

        private void OnEnable()
        {
            if (contentContainer != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
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

            var allAchievements = achievementManager.GetAllAchievements();
            
            foreach (var data in allAchievements)
            {
                var item = Instantiate(itemPrefab, contentContainer);
                
                bool isCompleted = saveManager.Data.completedAchievements.Contains(data.id);
                int progress = 0;
                saveManager.Data.achievementProgress.TryGetValue(data.id, out progress);
                
                item.Setup(data, progress, isCompleted);
                item.OnItemClicked += HandleItemClicked;
                spawnedItems.Add(item);
            }
        }

        private void HandleItemClicked(AchievementData data)
        {
            if (rewardPopup != null && saveManager != null)
            {
                rewardPopup.ShowReward(data);
            }
        }
    }
}
