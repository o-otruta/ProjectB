using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Core.Events;
using ProjectB.Meta;
using ProjectB.Player;

namespace ProjectB.Abilities
{
    public class HeroAbilities : MonoBehaviour
    {
        public const int MAX_ACTIVE_SLOTS = 6;
        public const int MAX_PASSIVE_SLOTS = 6;

        private List<ActiveAbility> activeAbilities = new List<ActiveAbility>();
        private List<PassiveAbility> passiveAbilities = new List<PassiveAbility>();

        private HeroRuntimeStats runtimeStats;
        private GameEventBus eventBus;
        private AchievementManager achievementManager;

        public IReadOnlyList<ActiveAbility> ActiveAbilities => activeAbilities;
        public IReadOnlyList<PassiveAbility> PassiveAbilities => passiveAbilities;
        public int TotalAbilitiesCount => activeAbilities.Count + passiveAbilities.Count;

        public event System.Action OnAbilitiesUpdated;

        [Inject]
        public void Construct(HeroRuntimeStats runtimeStats, GameEventBus eventBus, AchievementManager achievementManager = null)
        {
            this.runtimeStats = runtimeStats;
            this.eventBus = eventBus;
            this.achievementManager = achievementManager;
        }

        public bool AddAbility(AbilityData data)
        {
            if (data == null) return false;

            bool added = false;

            if (data.type == AbilityType.Active)
            {
                if (!HasActive(data.id, out _) && activeAbilities.Count < MAX_ACTIVE_SLOTS)
                {
                    var ability = data.CreateAbility(transform) as ActiveAbility;
                    if (ability != null)
                    {
                        ability.SetRuntimeStats(runtimeStats);
                        ability.Initialize(data);
                        activeAbilities.Add(ability);
                        added = true;
                    }
                }
            }
            else if (data.type == AbilityType.Passive)
            {
                if (!HasPassive(data.id, out _) && passiveAbilities.Count < MAX_PASSIVE_SLOTS)
                {
                    var ability = data.CreateAbility(transform) as PassiveAbility;
                    if (ability != null)
                    {
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
            }

            return added;
        }

        public void UpgradeAbility(string id, ModifierType type, float value)
        {
            if (HasActive(id, out ActiveAbility active))
            {
                active.ApplyModifier(type, value);
                active.IncrementLevel();
                achievementManager?.OnAbilityUpgraded(id, active.Level);
                OnAbilitiesChanged();
            }
            else if (HasPassive(id, out PassiveAbility passive))
            {
                passive.ApplyModifier(type, value);
                passive.IncrementLevel();
                achievementManager?.OnAbilityUpgraded(id, passive.Level);
                OnAbilitiesChanged();
            }
            else
            {
                Debug.LogWarning($"[HeroAbilities] Trying to upgrade ability {id} but it's not equipped.");
            }
        }

        public bool CanAddActive() => activeAbilities.Count < MAX_ACTIVE_SLOTS;
        public bool CanAddPassive() => passiveAbilities.Count < MAX_PASSIVE_SLOTS;

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
