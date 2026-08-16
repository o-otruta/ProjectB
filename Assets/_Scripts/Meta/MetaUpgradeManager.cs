using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectB.Data;

namespace ProjectB.Meta
{
    public class MetaUpgradeManager
    {
        private readonly SaveManager saveManager;
        private Dictionary<string, MetaUpgradeData> upgradeDataMap = new Dictionary<string, MetaUpgradeData>();

        // Опционально: можно загружать из ресурсов или прокидывать через другой конфиг
        // Пока мы будем загружать все из папки Resources (надо будет положить их в Resources/MetaUpgrades)
        // Или можно просто предоставить метод для инициализации
        
        public event Action<string> OnUpgradePurchased;

        public MetaUpgradeManager(SaveManager saveManager)
        {
            this.saveManager = saveManager;
            LoadAllUpgradeData();
        }

        private void LoadAllUpgradeData()
        {
            // Для MVP загружаем все ScriptableObject из Resources/MetaUpgrades
            // Создайте папку Assets/Resources/MetaUpgrades и положите туда все созданные SO
            MetaUpgradeData[] loadedData = Resources.LoadAll<MetaUpgradeData>("MetaUpgrades");
            foreach (var data in loadedData)
            {
                if (!string.IsNullOrEmpty(data.id) && !upgradeDataMap.ContainsKey(data.id))
                {
                    upgradeDataMap.Add(data.id, data);
                }
            }
            
            Debug.Log($"[MetaUpgradeManager] Loaded {upgradeDataMap.Count} meta upgrades.");
        }

        public IReadOnlyCollection<MetaUpgradeData> GetAllUpgrades()
        {
            return upgradeDataMap.Values;
        }

        public MetaUpgradeData GetUpgradeData(string id)
        {
            if (upgradeDataMap.TryGetValue(id, out var data))
            {
                return data;
            }
            return null;
        }

        public int GetCurrentLevel(string id)
        {
            return saveManager.GetMetaUpgradeLevel(id);
        }

        public bool CanAfford(string id)
        {
            return saveManager.Data.coins >= GetUpgradeCost(id);
        }

        public int GetUpgradeCost(string id)
        {
            var data = GetUpgradeData(id);
            if (data == null) return 0;

            int currentLevel = GetCurrentLevel(id);
            if (currentLevel >= data.maxLevel) return 0; // Макс уровень

            // cost = baseCost * (multiplier ^ currentLevel)
            float rawCost = data.baseCost * Mathf.Pow(data.costMultiplier, currentLevel);
            return Mathf.RoundToInt(rawCost);
        }

        public bool TryBuyUpgrade(string id)
        {
            var data = GetUpgradeData(id);
            if (data == null) return false;

            int currentLevel = GetCurrentLevel(id);
            if (currentLevel >= data.maxLevel) return false;

            int cost = GetUpgradeCost(id);
            if (saveManager.Data.coins >= cost)
            {
                if (saveManager.SpendCoins(cost))
                {
                    saveManager.SetMetaUpgradeLevel(id, currentLevel + 1);
                    OnUpgradePurchased?.Invoke(id);
                    return true;
                }
            }

            return false;
        }

        public float GetTotalBonus(MetaUpgradeEffectType effectType)
        {
            float totalBonus = 0f;
            foreach (var kvp in upgradeDataMap)
            {
                var data = kvp.Value;
                if (data.effectType == effectType)
                {
                    int level = GetCurrentLevel(data.id);
                    totalBonus += data.effectPerLevel * level;
                }
            }
            return totalBonus;
        }
    }
}
