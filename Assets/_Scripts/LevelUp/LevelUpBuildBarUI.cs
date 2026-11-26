using System.Collections.Generic;
using UnityEngine;
using ProjectB.Abilities;
using ProjectB.UI;

namespace ProjectB.LevelUp
{
    /// <summary>
    /// "Your build" strip on the LevelUp screen (UI redesign, variant A): the equipped active and passive abilities with
    /// their levels plus the free unlocked slots, so the player picks a card knowing what the build already has.
    /// Slots are pooled (instantiated once, reused on every LevelUp).
    /// </summary>
    public class LevelUpBuildBarUI : MonoBehaviour
    {
        [SerializeField] private AbilityHudSlotUI slotPrefab;
        [SerializeField] private RectTransform activeContainer;
        [SerializeField] private RectTransform passiveContainer;
        [SerializeField, Min(24f)] private float slotSize = 76f;

        private readonly List<AbilityHudSlotUI> activeSlots = new List<AbilityHudSlotUI>(HeroAbilities.MAX_ACTIVE_SLOTS);
        private readonly List<AbilityHudSlotUI> passiveSlots = new List<AbilityHudSlotUI>(HeroAbilities.MAX_PASSIVE_SLOTS);

        public int VisibleActiveSlots { get; private set; }
        public int VisiblePassiveSlots { get; private set; }

        public void Refresh(HeroAbilities hero)
        {
            if (hero == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            VisibleActiveSlots = FillActives(hero);
            VisiblePassiveSlots = FillPassives(hero);
        }

        private int FillActives(HeroAbilities hero)
        {
            IReadOnlyList<ActiveAbility> owned = hero.ActiveAbilities;
            int total = Mathf.Max(hero.UnlockedActiveSlots, owned.Count);
            for (int i = 0; i < total; i++)
            {
                AbilityHudSlotUI slot = GetSlot(activeSlots, activeContainer, i);
                if (slot == null) return i;
                if (i < owned.Count && owned[i] != null) slot.SetAbility(owned[i].Data, owned[i].Level);
                else slot.SetEmpty(AbilityType.Active);
            }
            HideFrom(activeSlots, total);
            return total;
        }

        private int FillPassives(HeroAbilities hero)
        {
            IReadOnlyList<PassiveAbility> owned = hero.PassiveAbilities;
            int total = Mathf.Max(hero.UnlockedPassiveSlots, owned.Count);
            for (int i = 0; i < total; i++)
            {
                AbilityHudSlotUI slot = GetSlot(passiveSlots, passiveContainer, i);
                if (slot == null) return i;
                if (i < owned.Count && owned[i] != null) slot.SetAbility(owned[i].Data, owned[i].Level);
                else slot.SetEmpty(AbilityType.Passive);
            }
            HideFrom(passiveSlots, total);
            return total;
        }

        private AbilityHudSlotUI GetSlot(List<AbilityHudSlotUI> pool, RectTransform container, int index)
        {
            if (slotPrefab == null || container == null) return null;
            while (pool.Count <= index)
            {
                AbilityHudSlotUI slot = Instantiate(slotPrefab, container);
                if (slot.transform is RectTransform rt) rt.sizeDelta = new Vector2(slotSize, slotSize);
                pool.Add(slot);
            }
            return pool[index];
        }

        private static void HideFrom(List<AbilityHudSlotUI> pool, int start)
        {
            for (int i = start; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(AbilityHudSlotUI prefab, RectTransform actives, RectTransform passives, float size)
        {
            slotPrefab = prefab;
            activeContainer = actives;
            passiveContainer = passives;
            slotSize = size;
        }
#endif
    }
}
