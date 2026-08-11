using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Abilities
{
    public class HeroAbilities : MonoBehaviour
    {
        public const int MAX_ACTIVE_SLOTS = 6;
        public const int MAX_PASSIVE_SLOTS = 6;

        private List<ActiveAbility> activeAbilities = new List<ActiveAbility>();
        private List<PassiveAbility> passiveAbilities = new List<PassiveAbility>();

        public IReadOnlyList<ActiveAbility> ActiveAbilities => activeAbilities;
        public IReadOnlyList<PassiveAbility> PassiveAbilities => passiveAbilities;

        public void AddAbility(AbilityData data)
        {
            if (data.type == AbilityType.Active)
            {
                if (!HasActive(data.id, out _) && activeAbilities.Count < MAX_ACTIVE_SLOTS)
                {
                    var ability = data.CreateAbility(transform) as ActiveAbility;
                    if (ability != null)
                    {
                        ability.Initialize(data);
                        activeAbilities.Add(ability);
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
                    }
                }
            }
            
            // Notify if necessary
            OnAbilitiesChanged();
        }

        public void UpgradeAbility(string id, ModifierType type, float value)
        {
            if (HasActive(id, out ActiveAbility active))
            {
                active.ApplyModifier(type, value);
            }
            else if (HasPassive(id, out PassiveAbility passive))
            {
                passive.ApplyModifier(type, value);
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
            // E.g., recalculate stats from passives
        }
    }
}
