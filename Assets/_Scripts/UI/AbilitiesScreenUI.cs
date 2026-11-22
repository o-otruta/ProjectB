using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;

namespace ProjectB.UI
{
    public class AbilitiesScreenUI : MonoBehaviour
    {
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private SynergyCatalog synergyCatalog;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private AbilityCollectionItemUI itemPrefab;
        [SerializeField] private TextMeshProUGUI headerText;

        private SaveManager saveManager;
        private AchievementManager achievementManager;
        private AbilitySlotService abilitySlotService;
        private ISynergyUnlockProvider synergyUnlockProvider;
        private readonly List<AbilityCollectionItemUI> spawnedItems = new List<AbilityCollectionItemUI>();

        [Inject]
        public void Construct(
            SaveManager saveManager,
            AchievementManager achievementManager = null,
            AbilitySlotService abilitySlotService = null,
            ISynergyUnlockProvider synergyUnlockProvider = null)
        {
            this.saveManager = saveManager;
            this.achievementManager = achievementManager;
            this.abilitySlotService = abilitySlotService;
            this.synergyUnlockProvider = synergyUnlockProvider;

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

            SynergyCatalog resolvedSynergies = synergyCatalog != null
                ? synergyCatalog
                : Resources.Load<SynergyCatalog>("SynergyCatalog");

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
                string synergyHint = BuildAbilitySynergyHint(data.id, resolvedSynergies);

                var item = Instantiate(itemPrefab, contentContainer);
                item.Setup(data, isUnlocked, requirementText, synergyHint);
                spawnedItems.Add(item);
            }

            int synergyCount = 0;
            if (resolvedSynergies != null && resolvedSynergies.synergies != null)
            {
                for (int i = 0; i < resolvedSynergies.synergies.Count; i++)
                {
                    SynergyData syn = resolvedSynergies.synergies[i];
                    if (syn == null) continue;

                    synergyCount++;
                    bool synUnlocked = synergyUnlockProvider == null || synergyUnlockProvider.IsSynergyUnlocked(syn.id);
                    string pairLabel = FormatSynergyPairLabel(syn);

                    var item = Instantiate(itemPrefab, contentContainer);
                    item.SetupSynergy(syn, pairLabel, synUnlocked);
                    spawnedItems.Add(item);
                }
            }

            if (headerText != null)
            {
                int act = abilitySlotService != null ? abilitySlotService.MaxActiveSlots : AbilitySlotService.BASE_ACTIVE_SLOTS;
                int pas = abilitySlotService != null ? abilitySlotService.MaxPassiveSlots : AbilitySlotService.BASE_PASSIVE_SLOTS;
                headerText.text = synergyCount > 0
                    ? $"Abilities ({unlockedCount}/{totalCount})  •  {synergyCount} Synergies  •  Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}A {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}P"
                    : $"Abilities ({unlockedCount}/{totalCount})  •  Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}A {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}P";
            }
        }

        private string BuildAbilitySynergyHint(string abilityId, SynergyCatalog catalog)
        {
            if (string.IsNullOrEmpty(abilityId) || catalog == null || catalog.synergies == null) return string.Empty;

            StringBuilder sb = null;
            for (int i = 0; i < catalog.synergies.Count; i++)
            {
                SynergyData syn = catalog.synergies[i];
                if (syn == null || syn.requiredAbilityIds == null || !syn.requiredAbilityIds.Contains(abilityId)) continue;

                string partnerNames = string.Empty;
                for (int r = 0; r < syn.requiredAbilityIds.Count; r++)
                {
                    string reqId = syn.requiredAbilityIds[r];
                    if (reqId == abilityId) continue;

                    string partnerName = RewardDisplayFormatter.ResolveAbilityName(reqId, abilityCatalog);
                    partnerNames = string.IsNullOrEmpty(partnerNames) ? partnerName : $"{partnerNames} + {partnerName}";
                }

                if (sb == null) sb = new StringBuilder("Synergy: ");
                else sb.Append("; ");

                sb.Append('+').Append(partnerNames).Append(" -> ").Append(syn.synergyName);
            }

            return sb != null ? sb.ToString() : string.Empty;
        }

        private string FormatSynergyPairLabel(SynergyData syn)
        {
            if (syn == null || syn.requiredAbilityIds == null || syn.requiredAbilityIds.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < syn.requiredAbilityIds.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(RewardDisplayFormatter.ResolveAbilityName(syn.requiredAbilityIds[i], abilityCatalog));
            }
            return sb.ToString();
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
