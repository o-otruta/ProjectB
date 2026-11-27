using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Core;
using ProjectB.Core.Events;
using ProjectB.Data;
using ProjectB.LevelUp;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class HeroAbilities : MonoBehaviour
    {
        public const int DEFAULT_ACTIVE_SLOTS = AbilitySlotService.BASE_ACTIVE_SLOTS;
        public const int DEFAULT_PASSIVE_SLOTS = AbilitySlotService.BASE_PASSIVE_SLOTS;
        public const int MAX_ACTIVE_SLOTS = AbilitySlotService.MAX_ACTIVE_SLOTS;
        public const int MAX_PASSIVE_SLOTS = AbilitySlotService.MAX_PASSIVE_SLOTS;

        [Header("Reactive Synergies")]
        [SerializeField] private List<SynergyData> availableSynergies = new List<SynergyData>();

        private readonly List<ActiveAbility> activeAbilities = new List<ActiveAbility>();
        private readonly List<PassiveAbility> passiveAbilities = new List<PassiveAbility>();
        private readonly HashSet<string> activeSynergyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<SynergyData> activeSynergies = new List<SynergyData>(8);

        private HeroRuntimeStats runtimeStats;
        private HeroHealth heroHealth;
        private GameEventBus eventBus;
        private AchievementManager achievementManager;
        private AbilitySlotService abilitySlotService;
        private RunStatistics runStatistics;
        private ISynergyUnlockProvider synergyUnlockProvider;
        private bool hasExplicitSynergyList;
        private SynergyCatalog cachedCatalogFallback;
        private RunRulesConfig runRulesConfig;
        private int levelCapOverride;

        public IReadOnlyList<ActiveAbility> ActiveAbilities => activeAbilities;
        public IReadOnlyList<PassiveAbility> PassiveAbilities => passiveAbilities;
        public IReadOnlyList<SynergyData> ActiveSynergies => activeSynergies;
        public int ActiveSynergyCount => activeSynergyIds.Count;
        public int TotalAbilitiesCount => activeAbilities.Count + passiveAbilities.Count;
        public int UnlockedActiveSlots => abilitySlotService != null ? abilitySlotService.MaxActiveSlots : DEFAULT_ACTIVE_SLOTS;
        public int UnlockedPassiveSlots => abilitySlotService != null ? abilitySlotService.MaxPassiveSlots : DEFAULT_PASSIVE_SLOTS;

        /// <summary>
        /// Per-run ability level cap: <see cref="RunRulesConfig.baseAbilityMaxLevel"/> (3) raised by the Constellation
        /// Path of Mastery (5 / 20 / 50). Applies to active and passive abilities alike.
        /// </summary>
        public int MaxAbilityLevel
        {
            get
            {
                if (levelCapOverride > 0) return levelCapOverride;
                int baseCap = runRulesConfig != null ? runRulesConfig.baseAbilityMaxLevel : RunRulesConfig.DEFAULT_BASE_ABILITY_MAX_LEVEL;
                int talentCap = runtimeStats != null ? runtimeStats.TalentAbilityLevelCap : 0;
                return Mathf.Max(1, Mathf.Max(baseCap, talentCap));
            }
        }

        public event Action OnAbilitiesUpdated;
        public event Action<SynergyData> OnSynergyActivated;

        [Inject]
        public void Construct(
            HeroRuntimeStats runtimeStats,
            GameEventBus eventBus,
            AchievementManager achievementManager = null,
            HeroHealth heroHealth = null,
            AbilitySlotService abilitySlotService = null,
            RunStatistics runStatistics = null,
            ISynergyUnlockProvider synergyUnlockProvider = null,
            RunRulesConfig runRulesConfig = null)
        {
            if (this.abilitySlotService != null)
            {
                this.abilitySlotService.OnSlotCapacityChanged -= OnAbilitiesChanged;
            }

            this.runtimeStats = runtimeStats;
            this.eventBus = eventBus;
            this.achievementManager = achievementManager;
            this.heroHealth = heroHealth != null ? heroHealth : GetComponent<HeroHealth>();
            this.abilitySlotService = abilitySlotService;
            this.runStatistics = runStatistics;
            this.synergyUnlockProvider = synergyUnlockProvider;
            this.runRulesConfig = runRulesConfig;

            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (activeAbilities[i] != null)
                {
                    activeAbilities[i].SetRunStatistics(runStatistics);
                }
            }

            if (this.abilitySlotService != null)
            {
                this.abilitySlotService.OnSlotCapacityChanged += OnAbilitiesChanged;
            }
        }

        private void OnDestroy()
        {
            if (abilitySlotService != null)
            {
                abilitySlotService.OnSlotCapacityChanged -= OnAbilitiesChanged;
            }
        }

        public void SetRunRulesConfig(RunRulesConfig rules)
        {
            runRulesConfig = rules;
        }

        /// <summary>Forces a level cap (tests / debug). 0 restores the config + Constellation cap.</summary>
        public void SetAbilityLevelCapOverride(int cap)
        {
            levelCapOverride = Mathf.Max(0, cap);
        }

        public bool IsAbilityAtMaxLevel(string id)
        {
            int level = GetAbilityLevel(id);
            return level > 0 && level >= MaxAbilityLevel;
        }

        public void SetSynergyUnlockProvider(ISynergyUnlockProvider provider)
        {
            synergyUnlockProvider = provider;
            EvaluateAutoSynergies();
        }

        public void SetAvailableSynergies(IReadOnlyList<SynergyData> synergies)
        {
            hasExplicitSynergyList = true;
            if (availableSynergies == null)
            {
                availableSynergies = new List<SynergyData>();
            }
            else
            {
                availableSynergies.Clear();
            }

            if (synergies != null)
            {
                for (int i = 0; i < synergies.Count; i++)
                {
                    if (synergies[i] != null && !availableSynergies.Contains(synergies[i]))
                    {
                        availableSynergies.Add(synergies[i]);
                    }
                }
            }

            EvaluateAutoSynergies();
        }

        public bool IsSynergyUnlocked(string synergyId)
        {
            if (string.IsNullOrEmpty(synergyId)) return false;
            return synergyUnlockProvider == null || synergyUnlockProvider.IsSynergyUnlocked(synergyId);
        }

        public bool HasSynergy(string synergyId)
        {
            return !string.IsNullOrEmpty(synergyId) && activeSynergyIds.Contains(synergyId);
        }

        public T GetActiveAbility<T>() where T : ActiveAbility
        {
            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (activeAbilities[i] is T typed)
                {
                    return typed;
                }
            }
            return null;
        }

        public ActiveAbility GetActiveAbilityById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < activeAbilities.Count; i++)
            {
                ActiveAbility ability = activeAbilities[i];
                if (ability != null && ability.Data != null && ability.Data.id == id)
                {
                    return ability;
                }
            }
            return null;
        }

        public bool AddAbility(AbilityData data)
        {
            if (data == null) return false;

            bool added = false;

            if (data.type == AbilityType.Active)
            {
                if (!HasActive(data.id, out _) && activeAbilities.Count < UnlockedActiveSlots)
                {
                    var ability = data.CreateAbility(transform) as ActiveAbility;
                    if (ability != null)
                    {
                        ability.SetRuntimeStats(runtimeStats);
                        ability.SetEventBus(eventBus);
                        ability.SetHeroAbilities(this);
                        ability.SetRunStatistics(runStatistics);
                        ability.Initialize(data);
                        activeAbilities.Add(ability);
                        added = true;
                    }
                }
            }
            else if (data.type == AbilityType.Passive)
            {
                if (!HasPassive(data.id, out _) && passiveAbilities.Count < UnlockedPassiveSlots)
                {
                    var ability = data.CreateAbility(transform) as PassiveAbility;
                    if (ability != null)
                    {
                        var health = heroHealth != null ? heroHealth : GetComponent<HeroHealth>();
                        ability.SetContext(runtimeStats, health);
                        ability.SetEventBus(eventBus);
                        ability.Initialize(data);
                        passiveAbilities.Add(ability);
                        added = true;
                    }
                }
            }

            if (added)
            {
                OnAbilitiesChanged();
                achievementManager?.OnAbilitiesCollectedInRun(TotalAbilitiesCount);
                eventBus?.Publish(new AbilityUnlockedEvent(data.id, data.type));
                EvaluateAutoSynergies();
            }

            return added;
        }

        public void EvaluateAutoSynergies()
        {
            IReadOnlyList<SynergyData> candidates = ResolveSynergyPool();
            if (candidates == null || candidates.Count == 0) return;

            for (int i = 0; i < candidates.Count; i++)
            {
                SynergyData syn = candidates[i];
                if (syn == null || string.IsNullOrEmpty(syn.id)) continue;
                if (activeSynergyIds.Contains(syn.id)) continue;
                if (!IsSynergyUnlocked(syn.id)) continue;
                if (!AreRequiredAbilitiesEquipped(syn)) continue;

                ActivateSynergyInternal(syn, syn.id, syn.synergyName);
            }
        }

        public bool TryActivateSynergyById(string synergyId, string fallbackDisplayName = null)
        {
            if (string.IsNullOrEmpty(synergyId)) return false;
            if (activeSynergyIds.Contains(synergyId)) return false;
            if (!IsSynergyUnlocked(synergyId)) return false;

            SynergyData matched = FindSynergyInPool(synergyId);
            string displayName = matched != null && !string.IsNullOrEmpty(matched.synergyName)
                ? matched.synergyName
                : (!string.IsNullOrEmpty(fallbackDisplayName) ? fallbackDisplayName : synergyId);

            ActivateSynergyInternal(matched, synergyId, displayName);
            return true;
        }

        private void ActivateSynergyInternal(SynergyData syn, string synergyId, string displayName)
        {
            activeSynergyIds.Add(synergyId);
            if (syn != null && !activeSynergies.Contains(syn))
            {
                activeSynergies.Add(syn);
            }

            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (activeAbilities[i] != null)
                {
                    activeAbilities[i].OnSynergyActivated(synergyId);
                }
            }

            if (syn != null)
            {
                OnSynergyActivated?.Invoke(syn);
            }

            string bannerName = !string.IsNullOrEmpty(displayName) ? displayName : synergyId;
            string formattedBanner = bannerName.StartsWith("SYNERGY:", StringComparison.OrdinalIgnoreCase)
                ? bannerName.ToUpperInvariant()
                : $"SYNERGY: {bannerName.ToUpperInvariant()}!";
            eventBus?.Publish(new SynergyActivatedEvent(synergyId, formattedBanner));
        }

        private bool AreRequiredAbilitiesEquipped(SynergyData syn)
        {
            if (syn.requiredAbilityIds == null || syn.requiredAbilityIds.Count == 0) return false;

            for (int i = 0; i < syn.requiredAbilityIds.Count; i++)
            {
                string reqId = syn.requiredAbilityIds[i];
                if (string.IsNullOrEmpty(reqId) || !HasAbility(reqId))
                {
                    return false;
                }
            }
            return true;
        }

        private IReadOnlyList<SynergyData> ResolveSynergyPool()
        {
            if (hasExplicitSynergyList)
            {
                return availableSynergies;
            }

            if (availableSynergies != null && availableSynergies.Count > 0)
            {
                return availableSynergies;
            }

            if (cachedCatalogFallback == null)
            {
                cachedCatalogFallback = Resources.Load<SynergyCatalog>("SynergyCatalog");
            }

            return cachedCatalogFallback != null ? cachedCatalogFallback.synergies : availableSynergies;
        }

        private SynergyData FindSynergyInPool(string synergyId)
        {
            IReadOnlyList<SynergyData> pool = ResolveSynergyPool();
            if (pool == null) return null;

            for (int i = 0; i < pool.Count; i++)
            {
                SynergyData syn = pool[i];
                if (syn != null && syn.id == synergyId)
                {
                    return syn;
                }
            }

            return null;
        }

        public bool UpgradeAbility(string id, ModifierType type, float value)
        {
            return UpgradeAbility(id, type, value, hasSecondary: false, default, 0f);
        }

        /// <summary>Applies a modifier and raises the ability level. Returns false when the ability is missing or capped.</summary>
        public bool UpgradeAbility(
            string id,
            ModifierType type,
            float value,
            bool hasSecondary,
            ModifierType secondaryType,
            float secondaryValue)
        {
            if (IsAbilityAtMaxLevel(id))
            {
                DevLog.Log($"[HeroAbilities] {id} is at the level cap (Lv {MaxAbilityLevel}); upgrade ignored.");
                return false;
            }

            if (HasActive(id, out ActiveAbility active))
            {
                active.ApplyModifier(type, value);
                if (hasSecondary)
                {
                    active.ApplyModifier(secondaryType, secondaryValue);
                }
                active.IncrementLevel();
                achievementManager?.OnAbilityUpgraded(id, active.Level);
                OnAbilitiesChanged();
            }
            else if (HasPassive(id, out PassiveAbility passive))
            {
                passive.ApplyModifier(type, value);
                if (hasSecondary)
                {
                    passive.ApplyModifier(secondaryType, secondaryValue);
                }
                passive.IncrementLevel();
                achievementManager?.OnAbilityUpgraded(id, passive.Level);
                OnAbilitiesChanged();
            }
            else
            {
                Debug.LogWarning($"[HeroAbilities] Trying to upgrade ability {id} but it's not equipped.");
                return false;
            }
            return true;
        }

        public bool CanAddActive() => activeAbilities.Count < UnlockedActiveSlots;
        public bool CanAddPassive() => passiveAbilities.Count < UnlockedPassiveSlots;

        public int GetAbilityLevel(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            if (HasActive(id, out ActiveAbility active)) return active.Level;
            if (HasPassive(id, out PassiveAbility passive)) return passive.Level;
            return 0;
        }

        public bool HasAbility(string id)
        {
            return HasActive(id, out _) || HasPassive(id, out _);
        }

        private bool HasActive(string id, out ActiveAbility existing)
        {
            foreach (var a in activeAbilities)
            {
                if (a.Data.id == id)
                {
                    existing = a;
                    return true;
                }
            }
            existing = null;
            return false;
        }

        private bool HasPassive(string id, out PassiveAbility existing)
        {
            foreach (var p in passiveAbilities)
            {
                if (p.Data.id == id)
                {
                    existing = p;
                    return true;
                }
            }
            existing = null;
            return false;
        }

        private void OnAbilitiesChanged()
        {
            OnAbilitiesUpdated?.Invoke();
        }
    }
}
