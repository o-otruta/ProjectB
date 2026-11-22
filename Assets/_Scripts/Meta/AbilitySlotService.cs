using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Data;

namespace ProjectB.Meta
{
    /// <summary>
    /// Extensible provider interface for future progression systems (e.g., Skill Tree)
    /// that grant additional active or passive ability slots.
    /// </summary>
    public interface IAbilitySlotBonusProvider
    {
        int GetBonusActiveSlots();
        int GetBonusPassiveSlots();
        event Action OnSlotBonusesChanged;
    }

    /// <summary>
    /// Resolves effective active and passive ability slot capacities (base 3+3, max 6+6)
    /// from achievements (SaveManager), meta upgrades, and future skill-tree providers.
    /// </summary>
    public class AbilitySlotService : IDisposable
    {
        public const int BASE_ACTIVE_SLOTS = 3;
        public const int BASE_PASSIVE_SLOTS = 3;
        public const int MAX_ACTIVE_SLOTS = 6;
        public const int MAX_PASSIVE_SLOTS = 6;

        private readonly SaveManager saveManager;
        private readonly MetaUpgradeManager metaUpgradeManager;
        private readonly List<IAbilitySlotBonusProvider> bonusProviders = new List<IAbilitySlotBonusProvider>();
        private bool disposed;

        public event Action OnSlotCapacityChanged;

        [Inject]
        public AbilitySlotService(SaveManager saveManager = null, MetaUpgradeManager metaUpgradeManager = null)
        {
            this.saveManager = saveManager;
            this.metaUpgradeManager = metaUpgradeManager;

            if (this.saveManager != null)
            {
                this.saveManager.OnDataChanged += HandleSaveDataChanged;
            }

            if (this.metaUpgradeManager != null)
            {
                this.metaUpgradeManager.OnUpgradePurchased += HandleMetaUpgradePurchased;
            }
        }

        public int MaxActiveSlots => Mathf.Clamp(
            BASE_ACTIVE_SLOTS + GetAchievementBonusActiveSlots() + GetMetaBonusActiveSlots() + GetProviderBonusActiveSlots(),
            BASE_ACTIVE_SLOTS,
            MAX_ACTIVE_SLOTS);

        public int MaxPassiveSlots => Mathf.Clamp(
            BASE_PASSIVE_SLOTS + GetAchievementBonusPassiveSlots() + GetMetaBonusPassiveSlots() + GetProviderBonusPassiveSlots(),
            BASE_PASSIVE_SLOTS,
            MAX_PASSIVE_SLOTS);

        public int GetAchievementBonusActiveSlots()
        {
            return saveManager != null ? saveManager.GetBonusActiveSlots() : 0;
        }

        public int GetAchievementBonusPassiveSlots()
        {
            return saveManager != null ? saveManager.GetBonusPassiveSlots() : 0;
        }

        public int GetMetaBonusActiveSlots()
        {
            return metaUpgradeManager != null
                ? Mathf.Max(0, Mathf.RoundToInt(metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.BonusActiveSlots)))
                : 0;
        }

        public int GetMetaBonusPassiveSlots()
        {
            return metaUpgradeManager != null
                ? Mathf.Max(0, Mathf.RoundToInt(metaUpgradeManager.GetTotalBonus(MetaUpgradeEffectType.BonusPassiveSlots)))
                : 0;
        }

        public int GetProviderBonusActiveSlots()
        {
            int sum = 0;
            for (int i = 0; i < bonusProviders.Count; i++)
            {
                if (bonusProviders[i] != null)
                {
                    sum += Mathf.Max(0, bonusProviders[i].GetBonusActiveSlots());
                }
            }
            return sum;
        }

        public int GetProviderBonusPassiveSlots()
        {
            int sum = 0;
            for (int i = 0; i < bonusProviders.Count; i++)
            {
                if (bonusProviders[i] != null)
                {
                    sum += Mathf.Max(0, bonusProviders[i].GetBonusPassiveSlots());
                }
            }
            return sum;
        }

        public void RegisterBonusProvider(IAbilitySlotBonusProvider provider)
        {
            if (provider == null || bonusProviders.Contains(provider)) return;
            bonusProviders.Add(provider);
            provider.OnSlotBonusesChanged += HandleExternalBonusChanged;
            OnSlotCapacityChanged?.Invoke();
        }

        public void UnregisterBonusProvider(IAbilitySlotBonusProvider provider)
        {
            if (provider == null) return;
            if (bonusProviders.Remove(provider))
            {
                provider.OnSlotBonusesChanged -= HandleExternalBonusChanged;
                OnSlotCapacityChanged?.Invoke();
            }
        }

        private void HandleSaveDataChanged()
        {
            OnSlotCapacityChanged?.Invoke();
        }

        private void HandleMetaUpgradePurchased(string _)
        {
            OnSlotCapacityChanged?.Invoke();
        }

        private void HandleExternalBonusChanged()
        {
            OnSlotCapacityChanged?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (saveManager != null)
            {
                saveManager.OnDataChanged -= HandleSaveDataChanged;
            }

            if (metaUpgradeManager != null)
            {
                metaUpgradeManager.OnUpgradePurchased -= HandleMetaUpgradePurchased;
            }

            for (int i = 0; i < bonusProviders.Count; i++)
            {
                if (bonusProviders[i] != null)
                {
                    bonusProviders[i].OnSlotBonusesChanged -= HandleExternalBonusChanged;
                }
            }
            bonusProviders.Clear();
        }
    }
}
