using System.Collections.Generic;
using UnityEngine;
using TMPro;
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

        [Header("Sprint 13: locked slots")]
        [Tooltip("Hide locked sockets and collapse them into one small \"lock +N\" chip per row.")]
        [SerializeField] private bool hideLockedSlots;
        [SerializeField] private GameObject activeLockedChip;
        [SerializeField] private TMP_Text activeLockedChipText;
        [SerializeField] private GameObject passiveLockedChip;
        [SerializeField] private TMP_Text passiveLockedChipText;
        private readonly char[] chipBuffer = new char[4];

        private HeroAbilities heroAbilities;
        private readonly List<AbilityHudSlotUI> activeSlots = new List<AbilityHudSlotUI>(HeroAbilities.MAX_ACTIVE_SLOTS);
        private readonly List<AbilityHudSlotUI> passiveSlots = new List<AbilityHudSlotUI>(HeroAbilities.MAX_PASSIVE_SLOTS);
        private readonly List<AbilityHudSlotUI> legacySlots = new List<AbilityHudSlotUI>();
        private bool isSubscribed;

        public IReadOnlyList<AbilityHudSlotUI> ActiveSlots => activeSlots;
        public IReadOnlyList<AbilityHudSlotUI> PassiveSlots => passiveSlots;
        public bool HidesLockedSlots => hideLockedSlots;

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
                    int locked = PopulateFixedRow(actives, activeSlots, AbilityType.Active, heroAbilities.UnlockedActiveSlots, hideLockedSlots);
                    UpdateLockedChip(activeLockedChip, activeLockedChipText, locked);
                }

                if (passiveSlotsContainer != null)
                {
                    EnsureSlotCount(passiveSlotsContainer, passiveSlots, HeroAbilities.MAX_PASSIVE_SLOTS);
                    int locked = PopulateFixedRow(passives, passiveSlots, AbilityType.Passive, heroAbilities.UnlockedPassiveSlots, hideLockedSlots);
                    UpdateLockedChip(passiveLockedChip, passiveLockedChipText, locked);
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

        private void UpdateLockedChip(GameObject chip, TMP_Text text, int lockedCount)
        {
            if (chip == null) return;
            bool show = hideLockedSlots && lockedCount > 0;
            if (chip.activeSelf != show) chip.SetActive(show);
            if (show) chip.transform.SetAsLastSibling();
            if (!show || text == null) return;
            chipBuffer[0] = '+';
            int len = UiFormat.WriteInt(chipBuffer, 1, lockedCount);
            text.SetCharArray(chipBuffer, 0, len);
        }

        /// <summary>Fills one row; returns how many sockets are still locked.</summary>
        private static int PopulateFixedRow<T>(IReadOnlyList<T> abilities, List<AbilityHudSlotUI> slots, AbilityType slotType, int unlockedCount, bool hideLocked)
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
            int lockedCount = 0;
            for (int i = filledCount; i < slots.Count; i++)
            {
                if (i < unlockedLimit)
                {
                    slots[i].SetEmpty(slotType);
                }
                else
                {
                    slots[i].SetLocked(slotType);
                    lockedCount++;
                    if (hideLocked && slots[i].gameObject.activeSelf) slots[i].gameObject.SetActive(false);
                }
            }
            return lockedCount;
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