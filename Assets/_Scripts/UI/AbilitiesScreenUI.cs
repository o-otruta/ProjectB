using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.Meta;

namespace ProjectB.UI
{
    public class AbilitiesScreenUI : MonoBehaviour
    {
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private AbilityCollectionItemUI itemPrefab;
        [SerializeField] private TextMeshProUGUI headerText;

        private SaveManager saveManager;
        private AchievementManager achievementManager;
        private readonly List<AbilityCollectionItemUI> spawnedItems = new List<AbilityCollectionItemUI>();

        [Inject]
        public void Construct(SaveManager saveManager, AchievementManager achievementManager = null)
        {
            this.saveManager = saveManager;
            this.achievementManager = achievementManager;

            if (isActiveAndEnabled)
            {
                RefreshUI();
            }
        }

        private void OnEnable()
        {
            if (saveManager != null)
            {
                RefreshUI();
            }
            if (contentContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            }
        }

        public void RefreshUI()
        {
            if (abilityCatalog == null || itemPrefab == null || contentContainer == null)
            {
                Debug.LogWarning("[AbilitiesScreenUI] Missing catalog, prefab, or contentContainer.");
                return;
            }

            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null) Destroy(spawnedItems[i].gameObject);
            }
            spawnedItems.Clear();

            int unlockedCount = 0;
            int totalCount = 0;

            var abilities = abilityCatalog.abilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                var data = abilities[i];
                if (data == null) continue;

                totalCount++;
                bool isUnlocked = IsAbilityUnlocked(data);
                if (isUnlocked) unlockedCount++;

                string requirementText = isUnlocked ? string.Empty : FindUnlockRequirement(data.id);

                var item = Instantiate(itemPrefab, contentContainer);
                item.Setup(data, isUnlocked, requirementText);
                spawnedItems.Add(item);
            }

            if (headerText != null)
            {
                headerText.text = $"Abilities ({unlockedCount} / {totalCount})";
            }
        }

        private bool IsAbilityUnlocked(AbilityData data)
        {
            if (data == null) return false;
            if (data.unlockedByDefault) return true;
            return saveManager != null && saveManager.IsAbilityUnlocked(data.id);
        }

        private string FindUnlockRequirement(string abilityId)
        {
            if (achievementManager == null || string.IsNullOrEmpty(abilityId)) return string.Empty;

            var allAchievements = achievementManager.GetAllAchievements();
            for (int i = 0; i < allAchievements.Count; i++)
            {
                var ach = allAchievements[i];
                if (ach == null) continue;
                if (ach.rewardType == AchievementRewardType.UnlockAbility && ach.rewardId == abilityId)
                {
                    return !string.IsNullOrEmpty(ach.description) ? ach.description : ach.title;
                }
            }

            return string.Empty;
        }
    }
}
