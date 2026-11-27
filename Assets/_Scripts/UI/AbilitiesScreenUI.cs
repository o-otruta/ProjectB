using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using ProjectB.Abilities;
using ProjectB.Core;
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
        private const int CATEGORY_COUNT = 3;
        private const int INITIAL_BATCH_SIZE = 8;
        private const int BATCH_PER_FRAME = 6;

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
        private MainMenuUI mainMenu;
        private Action<string> cachedSynergyClickHandler;

        private readonly PanelCanvasVisibility panelVisibility = new PanelCanvasVisibility();
        private readonly List<AbilityCollectionItemUI>[] categoryItems =
        {
            new List<AbilityCollectionItemUI>(20),
            new List<AbilityCollectionItemUI>(12),
            new List<AbilityCollectionItemUI>(16)
        };
        private readonly bool[] categoryDirty = { true, true, true };
        private readonly List<AbilityCollectionItemUI.SynergyChip> chipsBuffer = new List<AbilityCollectionItemUI.SynergyChip>(4);

        private readonly List<AbilityData> activeAbilities = new List<AbilityData>(20);
        private readonly List<AbilityData> passiveAbilities = new List<AbilityData>(12);
        private readonly List<SynergyData> allSynergies = new List<SynergyData>(16);
        private readonly Dictionary<string, AbilityData> abilitiesById = new Dictionary<string, AbilityData>(32, StringComparer.Ordinal);
        private readonly Dictionary<string, List<SynergyData>> synergiesByAbilityId = new Dictionary<string, List<SynergyData>>(32, StringComparer.Ordinal);
        private readonly Dictionary<string, AchievementData> unlockAchievementByAbilityId = new Dictionary<string, AchievementData>(16, StringComparer.Ordinal);

        private AbilityCatalog indexedAbilityCatalog;
        private SynergyCatalog indexedSynergyCatalog;
        private SynergyCatalog resolvedSynergies;
        private AchievementManager indexedAchievementManager;
        private bool catalogIndexesBuilt;
        private bool unlockIndexBuilt;

        private AbilityCollectionFilter filter = AbilityCollectionFilter.Active;
        private bool listenersAdded;
        private bool headerDirty = true;
        private bool eventsSubscribed;
        private Coroutine populateCoroutine;
        private int lastAppliedFilter = -1;
        private int unlockedActiveCount;
        private int unlockedPassiveCount;
        private int unlockedSynergyCount;

        private SaveManager subscribedSaveManager;
        private AbilitySlotService subscribedSlotService;
        private AchievementManager subscribedAchievementManager;
        private TalentTreeManager subscribedTalentTree;
        private LocalizationService subscribedLocalization;
        private Action cachedDataInvalidated;

        public AbilityCollectionFilter Filter => filter;
        public bool IsVisible => panelVisibility.IsVisible;

        public Selectable GetFirstSelectable()
        {
            if (filterButtons != null && filterButtons.Length > 0 && filterButtons[0] != null &&
                filterButtons[0].gameObject.activeInHierarchy && filterButtons[0].interactable)
            {
                return filterButtons[0];
            }
            return UiFocusHelper.FindFirstInteractable(transform);
        }

        [Inject]
        public void Construct(
            SaveManager saveManager,
            AchievementManager achievementManager = null,
            AbilitySlotService abilitySlotService = null,
            ISynergyUnlockProvider synergyUnlockProvider = null,
            MainMenuUI mainMenu = null)
        {
            bool achChanged = !ReferenceEquals(this.achievementManager, achievementManager);
            this.saveManager = saveManager;
            this.achievementManager = achievementManager;
            this.abilitySlotService = abilitySlotService;
            this.synergyUnlockProvider = synergyUnlockProvider;
            this.mainMenu = mainMenu;

            if (achChanged)
            {
                unlockIndexBuilt = false;
            }

            MarkAllDirty();

            if (isActiveAndEnabled && panelVisibility.IsVisible)
            {
                EnsureEventSubscriptions();
                RefreshVisible(immediate: !Application.isPlaying);
            }
        }

        private void Awake()
        {
            AddFilterListeners();
        }

        private void OnEnable()
        {
            panelVisibility.SyncOnEnable(gameObject);
            EnsureEventSubscriptions();
            RefreshIfNeeded();
        }

        private void OnDisable()
        {
            StopPopulateCoroutine();
            panelVisibility.SyncOnDisable();
        }

        private void OnDestroy()
        {
            StopPopulateCoroutine();
            UnsubscribeEventSubscriptions();
        }

        public void Show()
        {
            bool wasAlreadyActive = panelVisibility.BeginShow(gameObject);
            if (wasAlreadyActive)
            {
                RefreshIfNeeded();
                panelVisibility.FinishShow(true);
            }
            else if (!Application.isPlaying)
            {
                EnsureEventSubscriptions();
                RefreshIfNeeded();
            }
        }

        public void Hide()
        {
            StopPopulateCoroutine();
            if (scrollRect != null) scrollRect.StopMovement();
            panelVisibility.Hide(gameObject);
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

        private void EnsureEventSubscriptions()
        {
            cachedDataInvalidated ??= HandleDataInvalidated;
            if (eventsSubscribed
                && ReferenceEquals(subscribedSaveManager, saveManager)
                && ReferenceEquals(subscribedSlotService, abilitySlotService)
                && ReferenceEquals(subscribedAchievementManager, achievementManager)
                && ReferenceEquals(subscribedTalentTree, synergyUnlockProvider as TalentTreeManager)
                && ReferenceEquals(subscribedLocalization, LocalizationService.Current))
            {
                return;
            }

            UnsubscribeEventSubscriptions();

            subscribedSaveManager = saveManager;
            if (subscribedSaveManager != null) subscribedSaveManager.OnDataChanged += cachedDataInvalidated;

            subscribedSlotService = abilitySlotService;
            if (subscribedSlotService != null) subscribedSlotService.OnSlotCapacityChanged += cachedDataInvalidated;

            subscribedAchievementManager = achievementManager;
            if (subscribedAchievementManager != null) subscribedAchievementManager.OnAchievementUnlocked += HandleAchievementUnlocked;

            subscribedTalentTree = synergyUnlockProvider as TalentTreeManager;
            if (subscribedTalentTree != null) subscribedTalentTree.OnTreeChanged += cachedDataInvalidated;

            subscribedLocalization = LocalizationService.Current;
            if (subscribedLocalization != null) subscribedLocalization.LanguageChanged += cachedDataInvalidated;

            eventsSubscribed = true;
        }

        private void UnsubscribeEventSubscriptions()
        {
            if (!eventsSubscribed) return;

            if (subscribedSaveManager != null) subscribedSaveManager.OnDataChanged -= cachedDataInvalidated;
            if (subscribedSlotService != null) subscribedSlotService.OnSlotCapacityChanged -= cachedDataInvalidated;
            if (subscribedAchievementManager != null) subscribedAchievementManager.OnAchievementUnlocked -= HandleAchievementUnlocked;
            if (subscribedTalentTree != null) subscribedTalentTree.OnTreeChanged -= cachedDataInvalidated;
            if (subscribedLocalization != null) subscribedLocalization.LanguageChanged -= cachedDataInvalidated;

            subscribedSaveManager = null;
            subscribedSlotService = null;
            subscribedAchievementManager = null;
            subscribedTalentTree = null;
            subscribedLocalization = null;
            eventsSubscribed = false;
        }

        private void HandleAchievementUnlocked(AchievementData _)
        {
            HandleDataInvalidated();
        }

        private void HandleDataInvalidated()
        {
            MarkAllDirty();
            if (panelVisibility.IsVisible && isActiveAndEnabled)
            {
                RefreshVisible(immediate: !Application.isPlaying);
            }
        }

        private void MarkAllDirty()
        {
            headerDirty = true;
            for (int i = 0; i < CATEGORY_COUNT; i++)
            {
                categoryDirty[i] = true;
            }
        }

        public void SetFilter(AbilityCollectionFilter newFilter)
        {
            if (filter == newFilter && !categoryDirty[(int)newFilter] && lastAppliedFilter == (int)newFilter)
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            StopPopulateCoroutine();
            filter = newFilter;
            EnsureCategoryPopulatedAndUpdated(filter, immediate: !Application.isPlaying);
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        public void RefreshUI()
        {
            MarkAllDirty();
            RefreshVisible(immediate: !Application.isPlaying);
        }

        private void RefreshIfNeeded()
        {
            if (abilityCatalog == null || itemPrefab == null || contentContainer == null) return;

            if (!catalogIndexesBuilt || !ReferenceEquals(indexedAbilityCatalog, abilityCatalog) || !ReferenceEquals(indexedSynergyCatalog, synergyCatalog))
            {
                MarkAllDirty();
            }

            int idx = (int)filter;
            int targetCount = GetCategoryCount(filter);
            bool needsHeader = headerDirty;
            bool needsCategory = categoryDirty[idx] || lastAppliedFilter != idx || categoryItems[idx].Count < targetCount;

            if (!needsHeader && !needsCategory) return;

            RefreshVisible(immediate: !Application.isPlaying);
        }

        private void RefreshVisible(bool immediate)
        {
            if (abilityCatalog == null || itemPrefab == null || contentContainer == null)
            {
                Debug.LogWarning("[AbilitiesScreenUI] Missing catalog, prefab, or contentContainer.");
                return;
            }

            StopPopulateCoroutine();
            EnsureIndexes();

            if (headerDirty)
            {
                RefreshHeader();
            }

            AddFilterListeners();
            EnsureCategoryPopulatedAndUpdated(filter, immediate);
        }

        private void EnsureIndexes()
        {
            if (!catalogIndexesBuilt || !ReferenceEquals(indexedAbilityCatalog, abilityCatalog) || !ReferenceEquals(indexedSynergyCatalog, synergyCatalog))
            {
                indexedAbilityCatalog = abilityCatalog;
                indexedSynergyCatalog = synergyCatalog;
                resolvedSynergies = synergyCatalog != null
                    ? synergyCatalog
                    : Resources.Load<SynergyCatalog>("SynergyCatalog");

                activeAbilities.Clear();
                passiveAbilities.Clear();
                allSynergies.Clear();
                abilitiesById.Clear();
                synergiesByAbilityId.Clear();

                if (abilityCatalog != null && abilityCatalog.abilities != null)
                {
                    var abilities = abilityCatalog.abilities;
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        var data = abilities[i];
                        if (data == null) continue;

                        if (data.type == AbilityType.Passive) passiveAbilities.Add(data);
                        else activeAbilities.Add(data);

                        if (!string.IsNullOrEmpty(data.id) && !abilitiesById.ContainsKey(data.id))
                        {
                            abilitiesById.Add(data.id, data);
                        }
                    }
                }

                if (resolvedSynergies != null && resolvedSynergies.synergies != null)
                {
                    for (int i = 0; i < resolvedSynergies.synergies.Count; i++)
                    {
                        SynergyData syn = resolvedSynergies.synergies[i];
                        if (syn == null) continue;

                        allSynergies.Add(syn);
                        if (syn.requiredAbilityIds == null) continue;

                        for (int r = 0; r < syn.requiredAbilityIds.Count; r++)
                        {
                            string reqId = syn.requiredAbilityIds[r];
                            if (string.IsNullOrEmpty(reqId)) continue;
                            if (!synergiesByAbilityId.TryGetValue(reqId, out List<SynergyData> list))
                            {
                                list = new List<SynergyData>(2);
                                synergiesByAbilityId.Add(reqId, list);
                            }
                            if (!list.Contains(syn))
                            {
                                list.Add(syn);
                            }
                        }
                    }
                }

                catalogIndexesBuilt = true;
            }

            if (!unlockIndexBuilt || !ReferenceEquals(indexedAchievementManager, achievementManager))
            {
                indexedAchievementManager = achievementManager;
                unlockAchievementByAbilityId.Clear();
                if (achievementManager != null)
                {
                    var allAchievements = achievementManager.GetAllAchievements();
                    for (int i = 0; i < allAchievements.Count; i++)
                    {
                        var ach = allAchievements[i];
                        if (ach == null || ach.rewardType != AchievementRewardType.UnlockAbility || string.IsNullOrEmpty(ach.rewardId)) continue;
                        if (!unlockAchievementByAbilityId.ContainsKey(ach.rewardId))
                        {
                            unlockAchievementByAbilityId.Add(ach.rewardId, ach);
                        }
                    }
                }
                unlockIndexBuilt = true;
            }
        }

        private void RefreshHeader()
        {
            int unlockedActive = 0;
            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (IsAbilityUnlocked(activeAbilities[i])) unlockedActive++;
            }
            int unlockedPassive = 0;
            for (int i = 0; i < passiveAbilities.Count; i++)
            {
                if (IsAbilityUnlocked(passiveAbilities[i])) unlockedPassive++;
            }

            int unlockedCount = unlockedActive + unlockedPassive;
            int totalCount = activeAbilities.Count + passiveAbilities.Count;

            int synergyCount = allSynergies.Count;
            int unlockedSynergies = 0;
            for (int i = 0; i < synergyCount; i++)
            {
                SynergyData syn = allSynergies[i];
                if (synergyUnlockProvider == null || synergyUnlockProvider.IsSynergyUnlocked(syn.id))
                {
                    unlockedSynergies++;
                }
            }

            unlockedActiveCount = unlockedActive;
            unlockedPassiveCount = unlockedPassive;
            unlockedSynergyCount = unlockedSynergies;

            int act = abilitySlotService != null ? abilitySlotService.MaxActiveSlots : AbilitySlotService.BASE_ACTIVE_SLOTS;
            int pas = abilitySlotService != null ? abilitySlotService.MaxPassiveSlots : AbilitySlotService.BASE_PASSIVE_SLOTS;

            if (collectedChipText != null) SetTextIfChanged(collectedChipText, $"{unlockedCount}/{totalCount}");
            if (synergiesChipText != null) SetTextIfChanged(synergiesChipText, Loc.Format("ui.abilities.synergies_chip", unlockedSynergies, synergyCount));
            if (slotsChipText != null)
            {
                SetTextIfChanged(slotsChipText, Loc.Format("ui.abilities.slots_chip", $"{act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}", $"{pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}"));
            }

            if (headerText != null)
            {
                string title = collectedChipText != null
                    ? Loc.Get("ui.abilities.title", "ABILITIES")
                    : (synergyCount > 0
                        ? $"Abilities ({unlockedCount}/{totalCount})  •  {synergyCount} Synergies  •  Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}A {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}P"
                        : $"Abilities ({unlockedCount}/{totalCount})  •  Slots {act}/{AbilitySlotService.MAX_ACTIVE_SLOTS}A {pas}/{AbilitySlotService.MAX_PASSIVE_SLOTS}P");
                SetTextIfChanged(headerText, title);
            }

            headerDirty = false;
            UpdateFilterVisuals();
        }

        private void EnsureCategoryPopulatedAndUpdated(AbilityCollectionFilter targetFilter, bool immediate)
        {
            if (abilityCatalog == null || itemPrefab == null || contentContainer == null) return;
            EnsureIndexes();

            int targetIdx = (int)targetFilter;
            bool layoutChanged = lastAppliedFilter != targetIdx;

            for (int c = 0; c < CATEGORY_COUNT; c++)
            {
                if (c == targetIdx) continue;
                var otherPool = categoryItems[c];
                for (int i = 0; i < otherPool.Count; i++)
                {
                    if (otherPool[i] != null && otherPool[i].gameObject.activeSelf)
                    {
                        otherPool[i].gameObject.SetActive(false);
                        layoutChanged = true;
                    }
                }
            }

            UpdateFilterVisuals();

            var pool = categoryItems[targetIdx];
            int total = GetCategoryCount(targetFilter);
            bool dirty = categoryDirty[targetIdx];

            int existingToProcess = Mathf.Min(pool.Count, total);
            for (int i = 0; i < existingToProcess; i++)
            {
                var item = pool[i];
                if (item == null) continue;
                if (!item.gameObject.activeSelf)
                {
                    item.gameObject.SetActive(true);
                    layoutChanged = true;
                }
                if (dirty)
                {
                    BindItem(targetFilter, i, item);
                }
            }

            for (int i = total; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].gameObject.activeSelf)
                {
                    pool[i].gameObject.SetActive(false);
                    layoutChanged = true;
                }
            }

            if (pool.Count < total)
            {
                int syncTarget = immediate ? total : Mathf.Min(total, Mathf.Max(pool.Count, INITIAL_BATCH_SIZE));
                while (pool.Count < syncTarget)
                {
                    SpawnAndBindNext(targetFilter, pool);
                    layoutChanged = true;
                }

                if (pool.Count < total && Application.isPlaying && isActiveAndEnabled)
                {
                    populateCoroutine = StartCoroutine(PopulateRemainingCoroutine(targetFilter, total));
                }
            }

            if (pool.Count >= total)
            {
                categoryDirty[targetIdx] = false;
            }
            lastAppliedFilter = targetIdx;

            if (layoutChanged && contentContainer != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(contentContainer as RectTransform);
            }
        }

        private IEnumerator PopulateRemainingCoroutine(AbilityCollectionFilter targetFilter, int total)
        {
            int targetIdx = (int)targetFilter;
            var pool = categoryItems[targetIdx];
            bool dirty = categoryDirty[targetIdx];

            while (pool.Count < total)
            {
                yield return null;
                if (!panelVisibility.IsVisible || filter != targetFilter || !isActiveAndEnabled)
                {
                    populateCoroutine = null;
                    yield break;
                }

                int spawnedThisFrame = 0;
                while (pool.Count < total && spawnedThisFrame < BATCH_PER_FRAME)
                {
                    SpawnAndBindNext(targetFilter, pool);
                    spawnedThisFrame++;
                }

                if (dirty)
                {
                    int existing = Mathf.Min(pool.Count, total);
                    for (int i = 0; i < existing; i++)
                    {
                        if (pool[i] != null) BindItem(targetFilter, i, pool[i]);
                    }
                    dirty = false;
                }

                if (contentContainer != null)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(contentContainer as RectTransform);
                }
            }

            categoryDirty[targetIdx] = false;
            populateCoroutine = null;
        }

        private void StopPopulateCoroutine()
        {
            if (populateCoroutine != null)
            {
                StopCoroutine(populateCoroutine);
                populateCoroutine = null;
            }
        }

        private int GetCategoryCount(AbilityCollectionFilter targetFilter)
        {
            return targetFilter switch
            {
                AbilityCollectionFilter.Passive => passiveAbilities.Count,
                AbilityCollectionFilter.Synergies => allSynergies.Count,
                _ => activeAbilities.Count
            };
        }

        private void SpawnAndBindNext(AbilityCollectionFilter targetFilter, List<AbilityCollectionItemUI> pool)
        {
            int index = pool.Count;
            var item = Instantiate(itemPrefab, contentContainer);
            cachedSynergyClickHandler ??= HandleSynergyClicked;
            item.SetSynergyClickHandler(cachedSynergyClickHandler);
            BindItem(targetFilter, index, item);
            pool.Add(item);
        }

        private void BindItem(AbilityCollectionFilter targetFilter, int index, AbilityCollectionItemUI item)
        {
            if (targetFilter == AbilityCollectionFilter.Synergies)
            {
                SynergyData syn = allSynergies[index];
                bool synUnlocked = synergyUnlockProvider == null || synergyUnlockProvider.IsSynergyUnlocked(syn.id);
                string pairLabel = item.SupportsSynergyChips ? null : FormatSynergyPairLabel(syn);
                item.SetupSynergy(syn, pairLabel, synUnlocked);
                if (item.SupportsSynergyChips)
                {
                    chipsBuffer.Clear();
                    chipsBuffer.Add(BuildPairChip(syn, synUnlocked));
                    item.SetSynergyChips(chipsBuffer);
                }
                return;
            }

            AbilityData data = targetFilter == AbilityCollectionFilter.Passive
                ? passiveAbilities[index]
                : activeAbilities[index];

            bool isUnlocked = IsAbilityUnlocked(data);
            string requirementText = isUnlocked ? string.Empty : FindUnlockRequirement(data.id);
            string synergyHint = item.SupportsSynergyChips ? null : BuildAbilitySynergyHint(data.id);

            item.Setup(data, isUnlocked, requirementText, synergyHint);

            if (item.SupportsSynergyChips)
            {
                chipsBuffer.Clear();
                CollectSynergyChips(data.id, chipsBuffer);
                item.SetSynergyChips(chipsBuffer);
            }
        }

        private void UpdateFilterVisuals()
        {
            if (filterButtons == null || filterButtons.Length == 0 || filterButtons[0] == null) return;

            UiTheme theme = UiTheme.Current;
            for (int i = 0; filterBackgrounds != null && i < filterBackgrounds.Length; i++)
            {
                bool selected = i == (int)filter;
                if (filterBackgrounds[i] != null)
                {
                    filterBackgrounds[i].color = selected ? theme.accent : theme.surfaceRaised;
                    if (theme.surfaceMaterial != null && filterBackgrounds[i].material == theme.surfaceMaterial)
                    {
                        filterBackgrounds[i].material = null;
                    }
                }
                if (filterLabels != null && i < filterLabels.Length && filterLabels[i] != null)
                {
                    filterLabels[i].color = selected ? theme.white : theme.textSecondary;
                    if (catalogIndexesBuilt)
                    {
                        string chipText = i switch
                        {
                            (int)AbilityCollectionFilter.Passive => $"{Loc.Get("ui.abilities.passive", "Passive")}  {unlockedPassiveCount}/{passiveAbilities.Count}",
                            (int)AbilityCollectionFilter.Synergies => $"{Loc.Get("ui.abilities.synergies", "Synergies")}  {unlockedSynergyCount}/{allSynergies.Count}",
                            _ => $"{Loc.Get("ui.abilities.active", "Active")}  {unlockedActiveCount}/{activeAbilities.Count}"
                        };
                        SetTextIfChanged(filterLabels[i], chipText);
                    }
                }
            }

            if (filterButtons[0].transform.parent is RectTransform filterParent)
            {
                LayoutRebuilder.MarkLayoutForRebuild(filterParent);
            }
        }

        private void CollectSynergyChips(string abilityId, List<AbilityCollectionItemUI.SynergyChip> chips)
        {
            if (string.IsNullOrEmpty(abilityId) || !synergiesByAbilityId.TryGetValue(abilityId, out List<SynergyData> list)) return;

            Sprite selfIcon = FindAbilityIcon(abilityId);
            for (int i = 0; i < list.Count; i++)
            {
                SynergyData syn = list[i];
                if (syn == null || syn.requiredAbilityIds == null) continue;

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
                    leftIcon = selfIcon,
                    rightIcon = FindAbilityIcon(partnerId),
                    name = Loc.Name(syn),
                    unlocked = unlocked,
                    synergyId = syn.id
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
                name = Loc.Name(syn),
                unlocked = unlocked,
                synergyId = syn.id
            };
        }

        private void HandleSynergyClicked(string synergyId)
        {
            if (mainMenu == null || string.IsNullOrEmpty(synergyId)) return;
            UiAudio.Click();
            TalentNodeData node = (synergyUnlockProvider as TalentTreeManager)?.FindSynergyNode(synergyId);
            mainMenu.OpenConstellationNode(node != null ? node.id : null);
        }

        private Sprite FindAbilityIcon(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return null;
            return abilitiesById.TryGetValue(abilityId, out AbilityData ab) && ab != null ? ab.icon : null;
        }

        private string BuildAbilitySynergyHint(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || !synergiesByAbilityId.TryGetValue(abilityId, out List<SynergyData> list)) return string.Empty;

            StringBuilder sb = null;
            for (int i = 0; i < list.Count; i++)
            {
                SynergyData syn = list[i];
                if (syn == null || syn.requiredAbilityIds == null) continue;

                string partnerNames = string.Empty;
                for (int r = 0; r < syn.requiredAbilityIds.Count; r++)
                {
                    string reqId = syn.requiredAbilityIds[r];
                    if (reqId == abilityId) continue;

                    string partnerName = RewardDisplayFormatter.ResolveAbilityName(reqId, abilityCatalog);
                    partnerNames = string.IsNullOrEmpty(partnerNames) ? partnerName : $"{partnerNames} + {partnerName}";
                }

                if (sb == null) sb = new StringBuilder(Loc.Get("ui.abilities.synergy_prefix", "Synergy: "));
                else sb.Append("; ");

                sb.Append('+').Append(partnerNames).Append(" -> ").Append(Loc.Name(syn));
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
            if (string.IsNullOrEmpty(abilityId) || !unlockAchievementByAbilityId.TryGetValue(abilityId, out AchievementData ach) || ach == null)
            {
                return string.Empty;
            }

            string desc = Loc.Desc(ach);
            return !string.IsNullOrEmpty(desc) ? desc : Loc.Title(ach);
        }

        private static void SetTextIfChanged(TMP_Text textComponent, string value)
        {
            if (!string.Equals(textComponent.text, value, StringComparison.Ordinal))
            {
                textComponent.text = value;
            }
        }
    }
}
