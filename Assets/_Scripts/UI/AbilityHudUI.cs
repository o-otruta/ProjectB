using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Abilities;

namespace ProjectB.UI
{
    public class AbilityHudUI : MonoBehaviour
    {
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private AbilityHudSlotUI slotPrefab;

        private HeroAbilities heroAbilities;
        private readonly List<AbilityHudSlotUI> slots = new List<AbilityHudSlotUI>();
        private bool isSubscribed;

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
            if (heroAbilities == null || slotsContainer == null) return;

            var actives = heroAbilities.ActiveAbilities;
            var passives = heroAbilities.PassiveAbilities;
            int totalCount = actives.Count + passives.Count;

            EnsureSlotCount(totalCount);

            int slotIndex = 0;
            for (int i = 0; i < actives.Count; i++)
            {
                var ability = actives[i];
                if (ability != null && slotIndex < slots.Count)
                {
                    slots[slotIndex].SetAbility(ability.Data, ability.Level);
                    slotIndex++;
                }
            }

            for (int i = 0; i < passives.Count; i++)
            {
                var ability = passives[i];
                if (ability != null && slotIndex < slots.Count)
                {
                    slots[slotIndex].SetAbility(ability.Data, ability.Level);
                    slotIndex++;
                }
            }

            for (int i = slotIndex; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        private void EnsureSlotCount(int required)
        {
            if (slotPrefab == null) return;

            while (slots.Count < required)
            {
                var slot = Instantiate(slotPrefab, slotsContainer);
                slot.gameObject.SetActive(false);
                slots.Add(slot);
            }
        }
    }
}
