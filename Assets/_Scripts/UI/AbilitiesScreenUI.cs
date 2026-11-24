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
    public enum AbilityCollectionFilter
    {
        Active = 0,
        Passive = 1,
        Synergies = 2
    }

    /// <summary>
    /// Abilities collection (Sprint 13.2): chip header (<c>16/27</c>, <c>Synergies 12</c>, <c>Slots 3/6 · 3/6</c>),
    /// segmented filter Active / Passive / Synergies, rarity-framed cards, synergy chips "icon + icon → name"
    /// and locked entries as a silhouette with a lock and the unlocking condition.
    /// </summary>
    public class AbilitiesScreenUI : MonoBehaviour
    {
        [SerializeField] private AbilityCatalog abilityCatalog;
        [SerializeField] private SynergyCatalog synergyCatalog;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private AbilityCollectionItemUI itemPrefab;
        [SerializeField] private TextMeshProUGUI headerText;

        [Header("Sprint 13: chips & filter")]
        [SerializeField] private TMP_Text collectedChipText;
        [SerializeField] private TMP_Text synergiesChipText;
        [SerializeField] private TMP_Text slotsChipText;
        [SerializeField] private Button[] filterButtons = new Button[3];
        [SerializeField] private Image[] filterBackgrounds = new Image[3];
        [SerializeField] private TMP_Text[] filterLabels = new TMP_Text[3];
        [SerializeField] private ScrollRect scrollRect;

        private SaveManager saveManager;
        private AchievementManager achievementManager;
        private AbilitySlotService abilitySlotService;
        private ISynergyUnlockProvider synergyUnlockProvider;
        private readonly List<AbilityCollectionItemUI> spawnedItems = new List<AbilityCollectionItemUI>();
        private readonly List<AbilityCollectionFilter> spawnedCategories = new List<AbilityCollectionFilter>();
        private AbilityCollectionFilter filter = AbilityCollectionFilter.Active;
        private bool listenersAdded;

        public AbilityCollectionFilter Filter => filter;

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

        private void Awake()
        {
            AddFilterListeners();
        }

        private void AddFilterListeners()
        {
            if (listenersAdded || filterButtons == null) return;
            listenersAdded = true;
            for (int i = 0; i < filterButtons.Length; i++)
            {
                if (filterButtons[i] == null) continue;
                var f = (AbilityCollectionFilter)i;
                filterButtons[i].onClick.AddListener(() => SetFilter(f));
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

        public void SetFilter(AbilityCollectionFilter newFilter)
        {
            filter = newFilter;
            ApplyFilter();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void ApplyFilter()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null) spawnedItems[i].gameObject.SetActive(spawnedCategories[i] == filter);
            }

            UiTheme theme = UiTheme.Current;
            for (int i = 0; filterBackgrounds != null && i < filterBackgrounds.Length; i++)
            {
                bool selected = i == (int)filter;
                if (filterBackgrounds[i] != null) filterBackgrounds[i].color = selected ? theme.accent : UiTheme.WithAlpha(theme.surfaceRaised, 0f);
                if (filterLabels != null && i < filterLabels.Length && filterLabels[i] != null)
                {
                    filterLabels[i].color = selected ? theme.white : theme.textSecondary;
                }
            }

            if (contentContainer != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(contentContainer as RectTransform);
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
            spawnedCategories.Clear();

            SynergyCatalog resolvedSynergies = synergyCatalog != null
                ? synergyCatalog
                : Resources.Load<SynergyCatalog>("SynergyCatalog");

            int unlockedCount = 0;
            int totalCount = 0;
            var chips = new List<AbilityCollectionItemUI.SynergyChip>(4);

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
                item.Setup(data, isUnlocked, requirementText, item.SupportsSynergyChips ? null : synergyHint);

                if (item.SupportsSynergyChips)
                {
                    chips.Clear();
                    CollectSynergyChips(data.id, resolvedSynergies, chips);
                    item.SetSynergyChips(chips);
                }

                spawnedItems.Add(item);
                spawnedCategories.Add(data.type == AbilityType.Passive ? AbilityCollectionFilter.Passive : AbilityCollectionFilter.Active);
            }

            int synergyCount = 0;
            int unlockedSynergies = 0;
            if (resolvedSynergies != null && resolvedSynergies.synergies != null)
            {
                for (int i = 0; i < resolvedSynergies.synergies.Count; i++)
                {
                    SynergyData syn = resolvedSynergies.synergies[i];
                    if (syn == null) continue;

                    synergyCount++;
                    bool synUnlocked = synergyUnlockProvider == null || synergyUnlockProvider.IsSynergyUnlocked(syn.id);
                    if (synUnlocked) unlockedSynergies++;
                    string pairLabel = FormatSynergyPairLabel(syn);

                    var item = Instantiate(itemPrefab, contentContainer);
                    item.SetupSynergy(syn, pairLabel, synUnlocked);
                    if (item.SupportsSynergyChips)
                    {
                        chips.Clear();
                        chips.Add(BuildPairChip(syn, synUnlocked));
                        item.SetSynergyChips(chips);
                    }
                    spawnedItems.Add(item);
                    spawnedCategories.Add(AbilityCollectionFilter.Synergies);
                }
            }

            int act = abilitySlotService != null ? abilitySlotService.MaxActiveSlots : AbilitySlotService.BASE_ACTIVE_SLOTS;
            int pas = abilitySlotService != null ? abilitySlotService.MaxPassiveSlots : AbilitySlotService.BASE_PASSIVE_SLOTS;

            if (collectedChipText != null) collectedChipText.text = $"{unlockedCount}/{totalCount}";
            if (synergiesChipText != null) synergiesChipText.text = $"Synergies {unlockedSynergies}/{synergyCount}";
            if (slotsChipText != null)
            {
                slotsChipText.text = $"Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS} · {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}";
            }

            if (headerText != null)
            {
                headerText.text = collectedChipText != null
                    ? "ABILITIES"
                    : (synergyCount > 0
                        ? $"Abilities ({unlockedCount}/{totalCount})  •  {synergyCount} Synergies  •  Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}A {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}P"
                        : $"Abilities ({unlockedCount}/{totalCount})  •  Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}A {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}P");
            }

            AddFilterListeners();
            if (filterButtons != null && filterButtons.Length > 0 && filterButtons[0] != null)
            {
                ApplyFilter();
            }
        }

        private void CollectSynergyChips(string abilityId, SynergyCatalog catalog, List<AbilityCollectionItemUI.SynergyChip> chips)
        {
            if (string.IsNullOrEmpty(abilityId) || catalog == null || catalog.synergies == null) return;
            for (int i = 0; i < catalog.synergies.Count; i++)
            {
                SynergyData syn = catalog.synergies[i];
                if (syn == null || syn.requiredAbilityIds == null || !syn.requiredAbilityIds.Contains(abilityId)) continue;

                string partnerId = null;
                for (int r = 0; r < syn.requiredAbilityIds.Count; r++)
                {
                    if (syn.requiredAbilityIds[r] != abilityId)
                    {
                        partnerId = syn.requiredAbilityIds[r];
                        break;
                    }
                }

                bool unlocked = synergyUnlockProvider == null || synergyUnlockProvider.IsSynergyUnlocked(syn.id);
                chips.Add(new AbilityCollectionItemUI.SynergyChip
                {
                    leftIcon = FindAbilityIcon(abilityId),
                    rightIcon = FindAbilityIcon(partnerId),
                    name = syn.synergyName,
                    unlocked = unlocked
                });
            }
        }

        private AbilityCollectionItemUI.SynergyChip BuildPairChip(SynergyData syn, bool unlocked)
        {
            string a = syn.requiredAbilityIds != null && syn.requiredAbilityIds.Count > 0 ? syn.requiredAbilityIds[0] : null;
            string b = syn.requiredAbilityIds != null && syn.requiredAbilityIds.Count > 1 ? syn.requiredAbilityIds[1] : null;
            return new AbilityCollectionItemUI.SynergyChip
            {
                leftIcon = FindAbilityIcon(a),
                rightIcon = FindAbilityIcon(b),
                name = syn.synergyName,
                unlocked = unlocked
            };
        }

        private Sprite FindAbilityIcon(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || abilityCatalog == null) return null;
            for (int i = 0; i < abilityCatalog.abilities.Count; i++)
            {
                AbilityData ab = abilityCatalog.abilities[i];
                if (ab != null && ab.id == abilityId) return ab.icon;
            }
            return null;
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
