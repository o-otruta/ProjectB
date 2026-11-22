using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Abilities;

namespace ProjectB.UI
{
    public class AbilityHudUI : MonoBehaviour
    {
        [SerializeField] private Transform activeSlotsContainer;
        [SerializeField] private Transform passiveSlotsContainer;
        [SerializeField] private Transform slotsContainer; // Legacy fallback
        [SerializeField] private AbilityHudSlotUI slotPrefab;
        [SerializeField] private float slotSize = 68f;

        private HeroAbilities heroAbilities;
        private readonly List<AbilityHudSlotUI> activeSlots = new List<AbilityHudSlotUI>(HeroAbilities.MAX_ACTIVE_SLOTS);
        private readonly List<AbilityHudSlotUI> passiveSlots = new List<AbilityHudSlotUI>(HeroAbilities.MAX_PASSIVE_SLOTS);
        private readonly List<AbilityHudSlotUI> legacySlots = new List<AbilityHudSlotUI>();
        private bool isSubscribed;

        public IReadOnlyList<AbilityHudSlotUI> ActiveSlots => activeSlots;
        public IReadOnlyList<AbilityHudSlotUI> PassiveSlots => passiveSlots;

        [Inject]
        public void Construct(HeroAbilities heroAbilities)
        {
            this.heroAbilities = heroAbilities;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (heroAbilities != null && !isSubscribed)
            {
                heroAbilities.OnAbilitiesUpdated += Refresh;
                isSubscribed = true;
            }
            Refresh();
        }

        private void Unsubscribe()
        {
            if (heroAbilities != null && isSubscribed)
            {
                heroAbilities.OnAbilitiesUpdated -= Refresh;
                isSubscribed = false;
            }
        }

        public void Refresh()
        {
            if (heroAbilities == null) return;

            var actives = heroAbilities.ActiveAbilities;
            var passives = heroAbilities.PassiveAbilities;

            if (activeSlotsContainer != null || passiveSlotsContainer != null)
            {
                if (activeSlotsContainer != null)
                {
                    EnsureSlotCount(activeSlotsContainer, activeSlots, HeroAbilities.MAX_ACTIVE_SLOTS);
                    PopulateFixedRow(actives, activeSlots, AbilityType.Active, heroAbilities.UnlockedActiveSlots);
                }

                if (passiveSlotsContainer != null)
                {
                    EnsureSlotCount(passiveSlotsContainer, passiveSlots, HeroAbilities.MAX_PASSIVE_SLOTS);
                    PopulateFixedRow(passives, passiveSlots, AbilityType.Passive, heroAbilities.UnlockedPassiveSlots);
                }
                return;
            }

            if (slotsContainer == null) return;

            int totalCount = actives.Count + passives.Count;
            EnsureSlotCount(slotsContainer, legacySlots, totalCount);

            int slotIndex = 0;
            for (int i = 0; i < actives.Count; i++)
            {
                var ability = actives[i];
                if (ability != null && slotIndex < legacySlots.Count)
                {
                    legacySlots[slotIndex].SetAbility(ability.Data, ability.Level);
                    slotIndex++;
                }
            }

            for (int i = 0; i < passives.Count; i++)
            {
                var ability = passives[i];
                if (ability != null && slotIndex < legacySlots.Count)
                {
                    legacySlots[slotIndex].SetAbility(ability.Data, ability.Level);
                    slotIndex++;
                }
            }

            for (int i = slotIndex; i < legacySlots.Count; i++)
            {
                legacySlots[i].gameObject.SetActive(false);
            }
        }

        private static void PopulateFixedRow<T>(IReadOnlyList<T> abilities, List<AbilityHudSlotUI> slots, AbilityType slotType, int unlockedCount)
            where T : AbilityBase
        {
            int filledCount = 0;
            for (int i = 0; i < abilities.Count && filledCount < slots.Count; i++)
            {
                var ability = abilities[i];
                if (ability == null) continue;
                slots[filledCount].SetAbility(ability.Data, ability.Level);
                filledCount++;
            }

            int unlockedLimit = Mathf.Clamp(unlockedCount, 0, slots.Count);
            for (int i = filledCount; i < slots.Count; i++)
            {
                if (i < unlockedLimit)
                {
                    slots[i].SetEmpty(slotType);
                }
                else
                {
                    slots[i].SetLocked(slotType);
                }
            }
        }

        private void EnsureSlotCount(Transform container, List<AbilityHudSlotUI> pool, int required)
        {
            if (slotPrefab == null || container == null) return;

            while (pool.Count < required)
            {
                var slot = Instantiate(slotPrefab, container);
                if (slot.transform is RectTransform rt && slotSize > 0f)
                {
                    rt.sizeDelta = new Vector2(slotSize, slotSize);
                }
                slot.gameObject.SetActive(false);
                pool.Add(slot);
            }
        }
    }
}